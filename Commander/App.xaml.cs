using Commander.Services;
using System.Windows;

namespace Commander
{
  /// <summary>
  /// Interaction logic for App.xaml
  /// </summary>
  public partial class App : Application
  {
    protected override void OnStartup(StartupEventArgs e)
    {
      base.OnStartup(e);
      PluginLoggingService.Initialize();
    }

    protected override void OnExit(ExitEventArgs e)
    {
      PluginLoggingService.Shutdown();
      base.OnExit(e);
    }
  }

}
