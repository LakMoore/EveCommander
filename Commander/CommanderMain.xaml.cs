using BotLib;
using NHotkey;
using NHotkey.Wpf;
using read_memory_64_bit;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Reflection;
using System.Windows.Threading;

namespace Commander
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class CommanderMain : Window
  {
    private volatile bool _isRunning = false;
    private readonly int ONE_ROUND_TIME = 3000;
    private static readonly IEnumerable<IAutoRunPlugin> _AutoRunPlugins = _GetAutoRunPlugins();
    private static readonly IEnumerable<IGlobalPlugin> _GlobalPlugins = _GetGlobalPlugins();

    public CommanderMain()
    {
      InitializeComponent();
      Loaded += CommanderMain_Loaded;
    }

    private void StopKeyHandler(object? sender, HotkeyEventArgs e)
    {
      _isRunning = false;
    }

    private int currentClientIndex = -1;

    private void NextClientHandler(object? sender, HotkeyEventArgs e)
    {
      var clients = ClientPanel.Children.Cast<EveClient>().ToList();
      currentClientIndex++;
      if (currentClientIndex >= clients.Count)
      {
        currentClientIndex = 0;
      }
      long windowId = clients[currentClientIndex].CommanderClient?.GameClient.mainWindowId ?? 0;
      EveProcess.SetForegroundWindowInWindows.TrySetForegroundWindow((nint)windowId);
    }

    [SupportedOSPlatform("windows5.0")]
    private async void CommanderMain_Loaded(object sender, RoutedEventArgs e)
    {
      HotkeyManager.Current.AddOrReplace("Stop", Key.F8, ModifierKeys.Control, StopKeyHandler);
      HotkeyManager.Current.AddOrReplace("NextClient", Key.OemBackslash, ModifierKeys.None, NextClientHandler);
      await StartAsync();
    }

    private static IReadOnlyList<IGlobalPlugin> _GetGlobalPlugins()
    {
      LoadPluginDlls();

      // use reflection to find all the classes that inherit from IAutoRunPlugin
      var plugins = AppDomain.CurrentDomain.GetAssemblies()
          .SelectMany(s => s.GetTypes())
          .Where(p => typeof(IGlobalPlugin).IsAssignableFrom(p) && !p.IsAbstract);

      var pluginInstances = plugins.Select(t => Activator.CreateInstance(t)).Cast<IGlobalPlugin>() ?? [];

      return [.. pluginInstances];
    }

    internal static IEnumerable<IGlobalPlugin> GetGlobalPlugins()
    {
      return _GlobalPlugins;
    }

    private static IReadOnlyList<IAutoRunPlugin> _GetAutoRunPlugins()
    {
      LoadPluginDlls();

      // use reflection to find all the classes that inherit from IAutoRunPlugin
      var plugins = AppDomain.CurrentDomain.GetAssemblies()
          .SelectMany(s => s.GetTypes())
          .Where(p => typeof(IAutoRunPlugin).IsAssignableFrom(p) && !p.IsAbstract);

      var pluginInstances = plugins.Select(t => Activator.CreateInstance(t)).Cast<IAutoRunPlugin>() ?? [];

      return [.. pluginInstances];
    }

    internal static IEnumerable<IAutoRunPlugin> GetAutoRunPlugins()
    {
      return [.._AutoRunPlugins];
    }

    internal static IReadOnlyList<IBotLibPlugin> GetNewPluginsForCharacter(string characterName, long windowID)
    {
      LoadPluginDlls();

      // use reflection to find all the classes that inherit from IBotLibPlugin
      var plugins = AppDomain.CurrentDomain.GetAssemblies()
          .SelectMany(s => s.GetTypes())
          .Where(p => typeof(IBotLibPlugin).IsAssignableFrom(p) && !p.IsAbstract);

      var pluginInstances = plugins.Select(t => Activator.CreateInstance(t, characterName, windowID)).Cast<IBotLibPlugin>() ?? [];
      pluginInstances.ToList().ForEach(pi =>
      {
        pi.SetSettings(PluginSettingsCache.GetSettings(pi));
      });

      return [.. pluginInstances];
    }

    internal static IEnumerable<IBotLibPlugin> GetAllPlugins()
    {
      if (Application.Current.MainWindow is not CommanderMain main)
        return [];

      return GameClientCache.GetAllCharacters()
          .SelectMany(c => c.Plugins);
    }

    internal static bool IsRunning()
    {
      var app = Application.Current;
      if (app == null)
        return false;

      if (app.Dispatcher.CheckAccess())
      {
        return app.MainWindow is CommanderMain main && main._isRunning;
      }

      return app.Dispatcher.Invoke(() => 
        Application.Current.MainWindow is CommanderMain main && main._isRunning
      );
    }

    private async Task StartAsync()
    {

      //var stockText = "Navy Cap Booster 150\t46\r\nCaldari Navy Mjolnir Heavy Missile\t7686\r\nCaldari Navy Nova Heavy Missile\t24000\r\nNanite Repair Paste\t7000\r\nSisters Core Scanner Probe\t100\r\nMegacyte\t75000\r\nMorphite\t10000\r\nNocxium\t220000\r\nZydrine\t160000\r\nCadmium\t17000\r\nCaesium\t10000\r\nChromium\t10000\r\nCobalt\t50000\r\nDysprosium\t7000\r\nHafnium\t22000\r\nMercury\t20000\r\nNeodymium\t10424\r\nPlatinum\t13000\r\nScandium\t45000\r\nTechnetium\t6000\r\nThulium\t8000\r\nTitanium\t40000\r\nVanadium\t57000\r\nLarge Ancillary Remote Shield Booster\t1\r\nIFFA Compact Damage Control\t20\r\nRepublic Fleet Large Shield Extender\t3";

      //var openingStock = AssemblyLine.ParseStockFromClipboard(stockText);
      //var closingStockTarget = AssemblyLine.ParseStockFromClipboard(stockText);

      ////var EALResult = EAL.GetBuildPlan(EAL.GetTypeId("Auto-Integrity Preservation Seal"), 3, 300);

      //List<Item> toBuild = [];
      //toBuild.Add(new Item() { TypeName = "Auto-Integrity Preservation Seal", Quantity = 300 });
      //toBuild.Add(new Item() { TypeName = "Rhea", Quantity = 1 });

      //var plan = await AssemblyLine.Instance.GetBuildPlan(toBuild, openingStock, closingStockTarget);

      //var shoppingList = plan.GetPartsToBuy();
      //var jobs = plan.GetJobsToRun();
      //var closingStock = plan.GetClosingStock();

      //Debug.WriteLine("Starting...");

      GameClientCache.LoadCache(Properties.Settings.Default.uiRootAddressCache);
      PluginSettingsCache.LoadCache(Properties.Settings.Default.pluginSettingsCache);

      await Task.WhenAll(EveProcess.ListGameClientProcesses()
          .Where(gc => !string.IsNullOrWhiteSpace(gc.mainWindowTitle))
          .Select(async gc =>
          {
            // make a new Scout control for each process
            EveClient client = new();

            // add it to the UI
            ClientPanel.Children.Add(client);

            // fetch the cache incase we have a valid uiRootAddress
            var cachedGameClient = GameClientCache.GetGameClient(gc.processId, gc.mainWindowId);
            cachedGameClient.GameClient.mainWindowTitle = gc.mainWindowTitle;

            // re-initialise all plugins
            cachedGameClient.Characters
                .ToList()
                .ForEach(c => c.Plugins = CommanderMain.GetNewPluginsForCharacter(c.Name, gc.mainWindowId));

            await client.StartAsync(cachedGameClient);

            return Task.CompletedTask;
          })
          .ToList());

      // Persist the changes
      Properties.Settings.Default.uiRootAddressCache = GameClientCache.SaveCache();
      Properties.Settings.Default.Save();

      // Update online status for all characters based on currently loaded clients
      UpdateCharacterOnlineStates();

      Debug.WriteLine("Loaded.");

      do
      {
        var processes = EveProcess.ListGameClientProcesses();
        await Task.WhenAll(processes
            .Where(gc => !string.IsNullOrWhiteSpace(gc.mainWindowTitle))
            .Select(async gc =>
            {
              // Do we already have a EveClient control for this process?
              var existingClient = ClientPanel.Children.Cast<EveClient>()
                .Where(sc =>
                    sc.CommanderClient?.GameClient.processId == gc.processId
                    && sc.CommanderClient?.GameClient.mainWindowId == gc.mainWindowId
                )
                .ToList();

              if (existingClient.Count < 1)
              {
                // make a new Scout control for each process
                EveClient client = new();

                // add it to the UI
                ClientPanel.Children.Add(client);

                // fetch the cache incase we have a valid uiRootAddress
                var cachedGameClient = GameClientCache.GetGameClient(gc.processId, gc.mainWindowId);

                await client.StartAsync(cachedGameClient);
              }
              else
              {
                // check whether the title has changed
                var changedClients = existingClient
                  .Where(ec =>
                      ec != null
                      && gc.mainWindowTitle != ec.CommanderClient?.GameClient.mainWindowTitle
                  )
                  .ToList();

                foreach (var ec in changedClients)
                {
                  if (ec == null) continue;
                  await Task.Delay(2000);
                  var cachedGameClient = GameClientCache.GetGameClient(gc.processId, gc.mainWindowId);
                  cachedGameClient.GameClient.mainWindowTitle = gc.mainWindowTitle;
                  await ec.StartAsync(cachedGameClient);
                }
              }

              return Task.CompletedTask;
            })
            .ToList());

        // Remove the client controls that are no longer running
        ClientPanel.Children.Cast<EveClient>()
            .Where(ec => !processes.Any(gc =>
                gc.processId == ec.CommanderClient?.GameClient.processId
                && gc.mainWindowId == ec.CommanderClient?.GameClient.mainWindowId
            ))
            .ToList()
            .ForEach(ClientPanel.Children.Remove);

        // Remove any clients that have no characters
        GameClientCache.CleanCache();

        // Persist the changes
        Properties.Settings.Default.uiRootAddressCache = GameClientCache.SaveCache();
        Properties.Settings.Default.Save();

        // Update online status for all characters based on currently loaded clients
        UpdateCharacterOnlineStates();

        var runningClients = ClientPanel.Children.Cast<EveClient>()
          .Select(ec => ec.CommanderClient)
          .Where(ec => ec != null)
          .Cast<CommanderClient>()
          .Select(cc => cc.GameClient);

        var globalPlugins = GetGlobalPlugins();
        var allPlugins = GetAllPlugins();
        foreach (var gp in globalPlugins)
        {
          await gp.DoWork(_isRunning, runningClients, allPlugins);
        }

        await Task.Delay(1500);
      }
      while (true);
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
      if (_isRunning)
        return;

      _isRunning = true;
      while (_isRunning)
      {
        var start = DateTime.UtcNow.Ticks;

        // Execute one step on each client
        var clients = ClientPanel.Children.Cast<EveClient>().ToList();

        foreach (var ec in clients)
        {
          if (!_isRunning)
            break;

          await ec.DoOneStep();
        }

        var elapsed = DateTime.UtcNow.Ticks - start;
        var ms = elapsed / TimeSpan.TicksPerMillisecond;
        ms = ONE_ROUND_TIME - (int)ms;

        if (ms > ONE_ROUND_TIME)
          ms = ONE_ROUND_TIME;

        if (ms > 0)
        {
          await Task.Delay((int)ms + Random.Shared.Next(500));
        }
      }
    }

    private void SetPlugins_Click(object sender, RoutedEventArgs e)
    {
      // open a new character details window as modal
      CharacterWindow characterSelector = new()
      {
        Owner = this,
        WindowStartupLocation = WindowStartupLocation.CenterOwner
      };
      characterSelector.ShowDialog();

      // the selected plugins may have changed
      ClientPanel.Children.Cast<EveClient>()
        .ToList()
        .ForEach(ec => ec.UpdatePlugins());
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
      _isRunning = false;
    }

    private void CloseAll_Click(object sender, RoutedEventArgs e)
    {
      CloseAll();
    }

    private void CloseAll()
    {
      EveProcess.ListGameClientProcesses()
          .ToList()
          .ForEach(gc =>
          {
            var process = Process.GetProcessById(gc.processId);
            if (process.CloseMainWindow())
            {
              process.WaitForExit(5000); // wait 5 seconds for process to exit
              if (!process.HasExited)
              {
                process.Kill();
              }
            }
            else
            {
              process.Kill();
            }
          });
    }

    private static void LoadPluginDlls()
    {
      string pluginPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
      if (Directory.Exists(pluginPath))
      {
        foreach (var dll in Directory.GetFiles(pluginPath, "*.dll"))
        {
          try
          {
            if (!dll.Contains("Plugin"))
            {
              continue;
            }
            // Only load if not already loaded
            var assemblyName = AssemblyName.GetAssemblyName(dll);

            if (!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == assemblyName.Name))
            {
              // check without loading the dll that it contains at least one type that implements one of our interfaces
              var assembly = Assembly.LoadFrom(dll);
              var types = assembly.GetTypes()
                  .Where(p => 
                    typeof(IBotLibPlugin).IsAssignableFrom(p) 
                    || typeof(IAutoRunPlugin).IsAssignableFrom(p)
                    || typeof(IGlobalPlugin).IsAssignableFrom(p)
                    && !p.IsAbstract
                  );

              // load the dll into the current app domain
              if (types.Any())
              {
                // we have at least one type that implements IBotLibPlugin
                // so we load the assembly
                var loadedAssembly = AppDomain.CurrentDomain.Load(assemblyName);
              }
            }
          }
          catch (Exception ex)
          {
            // Handle or log exceptions as needed
          }
        }
      }
    }

    // Update CommanderCharacter.IsOnline for all known characters based on which EveClient controls
    // are currently present in the main window. A character is considered online if any loaded
    // EveClient's CommanderClient contains a character with the same name.
    internal void UpdateCharacterOnlineStates()
    {
      var onlineNames = ClientPanel.Children.Cast<EveClient>()
        .SelectMany(ec => {
          if (ec.CommanderClient?.GameClient.mainWindowTitle == "EVE")
          {
            return ec.CommanderClient?.Characters.Select(c => c.Name) ?? [];
          }
          return [ec.Character.Content.ToString() ?? ""];
        })
        .ToHashSet();

      foreach (var character in GameClientCache.GetAllCharacters())
      {
        character.IsOnline = onlineNames.Contains(character.Name);
      }
    }
  }
}
