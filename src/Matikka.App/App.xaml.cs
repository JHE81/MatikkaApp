using Matikka.Client.Sync;

namespace Matikka.App;

public partial class App : Application
{
    private readonly SyncService _sync;

    public App(SyncService sync)
    {
        InitializeComponent();
        _sync = sync;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage()) { Title = "Matikkapeli" };

        // Synkronointi käynnistyy taustalla eikä koskaan estä pelaamista.
        window.Created += (_, _) => _sync.Start();
        window.Resumed += (_, _) => _ = _sync.SyncAsync();

        return window;
    }
}
