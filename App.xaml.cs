using System.Threading;
using System.Windows;

namespace MtdVpn;

public partial class App : Application
{
    private Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "MtdVpn.SingleInstance", out bool isNew);
        if (!isNew) { Shutdown(); return; }
        SingBox.KillAll(); // убрать "осиротевшее" ядро после прошлого сбоя
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SingBox.KillAll();
        base.OnExit(e);
    }
}
