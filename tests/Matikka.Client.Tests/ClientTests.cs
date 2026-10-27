using Matikka.Client.Profile;
using Matikka.Client.Progress;
using Matikka.Client.Storage;
using Matikka.Client.Sync;
using Matikka.Core.Games.EquationBalance;
using Matikka.Core.Progress;

namespace Matikka.Client.Tests;

public class ClientTests
{
    /// <summary>Yksi laite: oma tallennus ja palvelut.</summary>
    private sealed class Device
    {
        public Device(FakeSyncServer server)
        {
            Progress = new ProgressService(Store, TimeProvider.System);
            Profile = new ProfileService(Store, TimeProvider.System);
            Secrets = new DevelopmentSecretStore(Store);
            Sync = new SyncService(server.CreateClient(), Store, Secrets, Progress, Profile);
        }

        public InMemoryEventStore Store { get; } = new();
        public ProgressService Progress { get; }
        public ProfileService Profile { get; }
        public DevelopmentSecretStore Secrets { get; }
        public SyncService Sync { get; }

        public async Task<GameEvent> SolveAsync(int level, long seed)
        {
            var session = new BalanceSession(BalancePuzzleGenerator.Generate(level, seed));
            foreach (var move in BalanceSolver.Solve(session.Puzzle.Start))
                session.TryApply(move);

            var e = session.ToEvent(GameOutcome.Solved, Guid.NewGuid(), 15_000, DateTimeOffset.Now.AddSeconds(seed));
            await Progress.RecordAsync(e);
            return e;
        }
    }

    [Fact]
    public async Task Recording_updates_progress_without_network()
    {
        var server = new FakeSyncServer { Offline = true };
        var device = new Device(server);

        await device.SolveAsync(1, 1);
        var model = await device.Progress.GetModelAsync();

        Assert.Equal(1, model.TotalSolved);
        Assert.Equal(3, model.GetLevel(EquationBalanceGame.GameId, 1).BestStars);
    }

    [Fact]
    public async Task Progress_survives_restart()
    {
        var server = new FakeSyncServer();
        var device = new Device(server);
        await device.SolveAsync(1, 1);
        await device.SolveAsync(2, 2);

        var restarted = new ProgressService(device.Store, TimeProvider.System);
        var model = await restarted.GetModelAsync();

        Assert.Equal(2, model.TotalSolved);
    }

    [Fact]
    public async Task Offline_events_wait_and_are_sent_when_network_returns()
    {
        var server = new FakeSyncServer { Offline = true };
        var device = new Device(server);
        await device.SolveAsync(1, 1);

        await device.Sync.SyncAsync();
        Assert.Equal(SyncState.Offline, device.Sync.State);
        Assert.Single(await device.Store.GetUnsyncedAsync(10));

        server.Offline = false;
        await device.Sync.SyncAsync();

        Assert.Equal(SyncState.Synced, device.Sync.State);
        Assert.Empty(await device.Store.GetUnsyncedAsync(10));
        Assert.Equal(1, server.EventCount);
    }

    [Fact]
    public async Task Second_device_receives_progress_and_computes_same_rating()
    {
        var server = new FakeSyncServer();
        var phone = new Device(server);
        for (int i = 0; i < 5; i++)
            await phone.SolveAsync(1 + i % 2, i);
        await phone.Sync.SyncAsync();

        // Toinen laite käyttää samaa tiliä (esim. kirjautuminen myöhemmin): kopioidaan token.
        var tablet = new Device(server);
        await tablet.Secrets.SetAsync(SyncService.TokenKey, (await phone.Secrets.GetAsync(SyncService.TokenKey))!);
        await tablet.SolveAsync(3, 99);
        await tablet.Sync.SyncAsync();
        await phone.Sync.SyncAsync();

        var phoneModel = await phone.Progress.GetModelAsync();
        var tabletModel = await tablet.Progress.GetModelAsync();

        Assert.Equal(6, phoneModel.TotalSolved);
        Assert.Equal(6, tabletModel.TotalSolved);
        Assert.Equal(
            phoneModel.GetSkill(EquationBalanceGame.Info).Rating,
            tabletModel.GetSkill(EquationBalanceGame.Info).Rating);
    }

    [Fact]
    public async Task Repeated_sync_does_not_duplicate_events()
    {
        var server = new FakeSyncServer();
        var device = new Device(server);
        await device.SolveAsync(1, 1);

        await device.Sync.SyncAsync();
        await device.Store.MarkAllUnsyncedAsync(); // simuloi katkennutta vastausta
        await device.Sync.SyncAsync();

        Assert.Equal(1, server.EventCount);
        Assert.Equal(1, (await device.Progress.GetModelAsync()).TotalSolved);
    }

    [Fact]
    public async Task Revoked_account_is_recreated_and_events_reuploaded()
    {
        var server = new FakeSyncServer();
        var device = new Device(server);
        await device.SolveAsync(1, 1);
        await device.Sync.SyncAsync();

        server.RevokeAllTokens();
        await device.Sync.SyncAsync();

        Assert.Equal(SyncState.Synced, device.Sync.State);
        Assert.Equal(1, server.AccountCount);
        Assert.Equal(2, server.EventCount); // vanha tili + uusi tili; vanhan tilin data jää palvelimen siivottavaksi
    }

    [Fact]
    public async Task Delete_all_data_clears_server_and_device()
    {
        var server = new FakeSyncServer();
        var device = new Device(server);
        await device.Profile.SaveAsync("Testaaja", 8);
        await device.SolveAsync(1, 1);
        await device.Sync.SyncAsync();

        await device.Sync.DeleteAllDataAsync();

        Assert.Equal(0, server.EventCount);
        Assert.Null(await device.Profile.GetAsync());
        Assert.Equal(0, (await device.Progress.GetModelAsync()).TotalSolved);
        Assert.Empty(await device.Store.GetAllAsync());
    }

    [Fact]
    public async Task Profile_is_trimmed_and_validated()
    {
        var device = new Device(new FakeSyncServer());

        var profile = await device.Profile.SaveAsync("  Pitkänimimerkkiniminen pelaaja  ", 9);

        Assert.Equal(ProfileService.MaxNicknameLength, profile.Nickname.Length);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => device.Profile.SaveAsync("A", 6));
    }
}

public class CharacterServiceTests
{
    [Fact]
    public async Task Character_choice_is_remembered_and_validated()
    {
        var store = new InMemoryEventStore();
        var service = new CharacterService(store);

        Assert.Equal(CharacterService.DefaultCharacter, (await service.GetAsync()).Id);

        await service.SetAsync("bitti");
        Assert.Equal("bitti", (await new CharacterService(store).GetAsync()).Id);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SetAsync("lohikaarme"));
    }
}
