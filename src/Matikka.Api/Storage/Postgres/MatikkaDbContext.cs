using Microsoft.EntityFrameworkCore;

namespace Matikka.Api.Storage.Postgres;

public sealed class AccountRow
{
    public Guid Id { get; set; }

    /// <summary>SHA-256-tiiviste tokenista. Itse tokenia ei tallenneta.</summary>
    public string TokenHash { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }
}

public sealed class EventRow
{
    /// <summary>Palvelimen järjestysnumero. Laite pyytää "kaikki Seq:n X jälkeen".</summary>
    public long Seq { get; set; }

    public Guid AccountId { get; set; }

    public Guid EventId { get; set; }

    // Muutama kenttä omina sarakkeinaan analytiikkaa varten (esim. tasojen vaikeuden kalibrointi).
    public string GameId { get; set; } = "";

    public int Level { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }

    /// <summary>Koko tapahtuma JSON:na (jsonb). Palautetaan laitteille sellaisenaan.</summary>
    public string Payload { get; set; } = "";
}

public sealed class MatikkaDbContext(DbContextOptions<MatikkaDbContext> options) : DbContext(options)
{
    public DbSet<AccountRow> Accounts => Set<AccountRow>();

    public DbSet<EventRow> Events => Set<EventRow>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<AccountRow>(a =>
        {
            a.ToTable("accounts");
            a.HasKey(x => x.Id);
            a.Property(x => x.Id).HasColumnName("id");
            a.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(64);
            a.Property(x => x.CreatedAt).HasColumnName("created_at");
            a.Property(x => x.LastSeenAt).HasColumnName("last_seen_at");
            a.HasIndex(x => x.TokenHash).IsUnique();
        });

        model.Entity<EventRow>(e =>
        {
            e.ToTable("events");
            e.HasKey(x => x.Seq);
            e.Property(x => x.Seq).HasColumnName("seq").UseIdentityAlwaysColumn();
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.EventId).HasColumnName("event_id");
            e.Property(x => x.GameId).HasColumnName("game_id").HasMaxLength(64);
            e.Property(x => x.Level).HasColumnName("level");
            e.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            e.Property(x => x.ReceivedAt).HasColumnName("received_at");
            e.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb");
            e.HasIndex(x => new { x.AccountId, x.EventId }).IsUnique();
            e.HasIndex(x => new { x.AccountId, x.Seq });
            e.HasOne<AccountRow>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
