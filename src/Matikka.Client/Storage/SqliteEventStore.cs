using System.Text.Json;
using Matikka.Core.Progress;
using Microsoft.Data.Sqlite;

namespace Matikka.Client.Storage;

/// <summary>
/// SQLite-tallennus laitteella. Tapahtuma tallennetaan JSON-muodossa yhteen sarakkeeseen,
/// jolloin uusien kenttien lisääminen ei vaadi tietokantamuutoksia.
/// </summary>
public sealed class SqliteEventStore : IEventStore
{
    private const int SchemaVersion = 1;

    private readonly string _connectionString;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private bool _schemaReady;

    public SqliteEventStore(string databasePath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
        }.ToString();
    }

    /// <summary>Luo taulut tarvittaessa. Kutsutaan automaattisesti ensimmäisellä käytöllä.</summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
    }

    private async Task EnsureSchemaAsync(SqliteConnection connection, CancellationToken ct)
    {
        if (_schemaReady)
            return;

        await _schemaGate.WaitAsync(ct);
        try
        {
            if (!_schemaReady)
            {
                await MigrateAsync(connection, ct);
                _schemaReady = true;
            }
        }
        finally
        {
            _schemaGate.Release();
        }
    }

    private static async Task MigrateAsync(SqliteConnection connection, CancellationToken ct)
    {
        long version = (long)(await ScalarAsync(connection, "PRAGMA user_version;", ct) ?? 0L);
        if (version >= SchemaVersion)
            return;

        // Uudet skeemaversiot lisätään tänne omina askelinaan (if version < 2 ...).
        await ExecuteAsync(connection, """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS events (
                id TEXT PRIMARY KEY,
                occurred_utc_ticks INTEGER NOT NULL,
                payload TEXT NOT NULL,
                synced INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS ix_events_synced ON events (synced, occurred_utc_ticks);
            CREATE TABLE IF NOT EXISTS kv (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            PRAGMA user_version = 1;
            """, ct);
    }

    public async Task AppendAsync(GameEvent gameEvent, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await InsertAsync(connection, null, gameEvent, synced: false, ct);
    }

    public async Task<IReadOnlyList<GameEvent>> GetAllAsync(CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload FROM events ORDER BY occurred_utc_ticks, id;";
        return await ReadEventsAsync(command, ct);
    }

    public async Task<IReadOnlyList<GameEvent>> GetUnsyncedAsync(int max, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload FROM events WHERE synced = 0 ORDER BY occurred_utc_ticks, id LIMIT $max;";
        command.Parameters.AddWithValue("$max", max);
        return await ReadEventsAsync(command, ct);
    }

    public async Task MarkSyncedAsync(IEnumerable<Guid> eventIds, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE events SET synced = 1 WHERE id = $id;";
        var id = command.Parameters.Add("$id", SqliteType.Text);

        foreach (Guid eventId in eventIds)
        {
            id.Value = eventId.ToString();
            await command.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }

    public async Task MarkAllUnsyncedAsync(CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await ExecuteAsync(connection, "UPDATE events SET synced = 0;", ct);
    }

    public async Task<int> AddFromServerAsync(IEnumerable<GameEvent> events, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

        int added = 0;
        foreach (GameEvent gameEvent in events)
            added += await InsertAsync(connection, transaction, gameEvent, synced: true, ct);

        await transaction.CommitAsync(ct);
        return added;
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM kv WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);
        return (string?)await command.ExecuteScalarAsync(ct);
    }

    public async Task SetValueAsync(string key, string value, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO kv (key, value) VALUES ($key, $value) ON CONFLICT(key) DO UPDATE SET value = excluded.value;";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task RemoveValueAsync(string key, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM kv WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAllAsync(CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await ExecuteAsync(connection, "DELETE FROM events; DELETE FROM kv;", ct);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await EnsureSchemaAsync(connection, ct);
        return connection;
    }

    private static async Task<int> InsertAsync(
        SqliteConnection connection, SqliteTransaction? transaction, GameEvent gameEvent, bool synced, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO events (id, occurred_utc_ticks, payload, synced)
            VALUES ($id, $ticks, $payload, $synced)
            ON CONFLICT(id) DO NOTHING;
            """;
        command.Parameters.AddWithValue("$id", gameEvent.Id.ToString());
        command.Parameters.AddWithValue("$ticks", gameEvent.OccurredAt.UtcTicks);
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(gameEvent, ClientJsonContext.Default.GameEvent));
        command.Parameters.AddWithValue("$synced", synced ? 1 : 0);
        return await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<IReadOnlyList<GameEvent>> ReadEventsAsync(SqliteCommand command, CancellationToken ct)
    {
        var result = new List<GameEvent>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            GameEvent? gameEvent = JsonSerializer.Deserialize(reader.GetString(0), ClientJsonContext.Default.GameEvent);
            if (gameEvent != null)
                result.Add(gameEvent);
        }

        return result;
    }

    private static async Task<object?> ScalarAsync(SqliteConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(ct);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct);
    }
}
