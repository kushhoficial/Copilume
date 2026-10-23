using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace Copilume;

public partial class App : Application
{
    public App() => EventManager.RegisterClassHandler(typeof(TextBox), FrameworkElement.ContextMenuOpeningEvent, new ContextMenuEventHandler(GlassTextBoxMenus.Open));
    private Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        bool demo = e.Args.Contains("--demo");
        instance = new Mutex(true, "Local\\io.github.kushhoficial.Copilume" + (demo ? ".Demo" : ""), out bool created);
        if (!created) { MessageBox.Show("O Copilume já está aberto. Use o ícone da bandeja para acessar o histórico.", "Copilume"); Shutdown(); return; }
        var window = new MainWindow(demo, e.Args.Contains("--dark") ? "Escuro" : e.Args.Contains("--light") ? "Claro" : null);
        MainWindow = window;
        window.Initialize();
        if (!e.Args.Contains("--background")) window.OpenHistory();
        if (demo && e.Args.Contains("--smoke-test"))
        {
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (_, _) => { timer.Stop(); window.Quit(); };
            timer.Start();
        }
    }
    protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }
}
