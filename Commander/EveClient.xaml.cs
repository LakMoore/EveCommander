using BotLib;
using eve_parse_ui;
using Microsoft.EntityFrameworkCore;
using read_memory_64_bit;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Commander
{
  /// <summary>
  /// Interaction logic for EveClient.xaml
  /// </summary>
  public partial class EveClient : UserControl
  {
    private static readonly int MaxConcurrentUiRootScans =
      int.TryParse(Environment.GetEnvironmentVariable("SANDERLING_UIROOT_MAX_CONCURRENT_SCANS"), out var configuredScanLimit)
        ? Math.Max(1, configuredScanLimit)
        : 2;

    private static readonly SemaphoreSlim UiRootScanThrottle = new(MaxConcurrentUiRootScans, MaxConcurrentUiRootScans);

    private readonly string[] ALIVE_SPINNER = ["-", "\\", "|", "/"];
    private int aliveSpinnerIndex = 0;
    private ClientGroup? _commanderClient;
    private string? CurrentCharacterName = null;
    private long _lastErrorTime = 0;
    private Point? _mouseDownPoint;
    private static readonly string ExceptionLogPath = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "Commander",
      "exceptions.log");

    // TODO: get this from the SDE
    private readonly List<int> cloakIDs = [11370, 11577, 11578, 14234, 14776,
            14778, 14780, 14782, 15790, 16126, 20561, 20563, 20565, 32260];

    private readonly string Clone_Station = "J121006 - Fallen Stars Memorial Cantina";

    public EveClient()
    {
      InitializeComponent();
    }

    internal ClientGroup? CommanderClient
    {
      get
      {
        return _commanderClient;
      }
    }

    internal async Task DoOneStep()
    {
      if (_lastErrorTime > 0)
      {
        if (DateTime.UtcNow.Ticks - _lastErrorTime < TimeSpan.TicksPerSecond * 15)
        {
          return;
        }
        _lastErrorTime = 0;
      }

      try
      {
        await CommandClient();
      }
      catch (Exception ex)
      {
        _lastErrorTime = DateTime.UtcNow.Ticks;
        Background = new SolidColorBrush(Colors.Red);
        Result.Content = ex.Message;
        Result.Width = Double.NaN;
        Result.Foreground = new SolidColorBrush(Colors.White);
        Debug.WriteLine(ex);
        LogExceptionToFile("DoOneStep", ex);

        try
        {
          //await Server.SendError(ex);
        }
        catch (Exception ex2)
        {
          Debug.WriteLine(ex2);
          LogExceptionToFile("DoOneStep:ServerSendError", ex2);
        }
      }
    }

    /// <summary>
    /// Ensure we have a valid UI root address and try to capture the character(s)
    /// SLOW PROCESS!!!
    /// Client state can change while this is scanning
    /// </summary>
    /// <param name="cachedGameClient"></param>
    /// <returns></returns>
    internal async Task StartAsync(ClientGroup cachedGameClient)
    {
      _commanderClient = cachedGameClient;

      var characterName = (_commanderClient.GameClient.mainWindowTitle?.Length > 6
        ? _commanderClient.GameClient.mainWindowTitle[6..]
        : _commanderClient.GameClient.mainWindowTitle) ?? string.Empty;

      CurrentCharacterName = characterName;
      Character.Content = characterName;

      await ScanForRootAddress();

      // if maintitle is simply "Eve" then we are on the character select screen
      if (characterName == "EVE")
      {
        // Do one step to capture character details from the slots
        await CommandClient();

        // let's see if we found any characters
        var chars = _commanderClient.Characters
          .Where(c => !string.IsNullOrEmpty(c.Name))
          .Select(c => c.Name.Split(' ')?[0])
          .Where(c => !string.IsNullOrEmpty(c));

        if (chars?.Count() > 1)
        {
          Character.Content = chars.Aggregate((a, b) => $"{a}, {b}");
        }
        else if (chars?.Count() == 1)
        {
          // this should not be possible
          Character.Content = chars.First();
        }
        else
        {
          // something has changed on the client, this EveClient object
          // is probably about to be destroyed and a new one created
          return;
        }
      }
      else
      {
        UpdateGameClientForCharacter(characterName, _commanderClient.GameClient.mainWindowId);
      }

      UpdatePlugins();
    }

    private async Task ScanForRootAddress()
    {
      if (_commanderClient?.GameClient.uiRootAddress == 0)
      {
        MemoryScanPanel.Width = Double.NaN;
        DetailsPanel.Width = 0;
        await FindUIRootAddress(_commanderClient.GameClient);
      }

      MemoryScanPanel.Width = 0;
      DetailsPanel.Width = Double.NaN;
    }

    private void UpdateGameClientForCharacter(
      string characterName,
      long newWindowId,
      string? location = null,
      bool? isAlphaClone = null
    )
    {
      if (_commanderClient == null)
        return;

      var character = _commanderClient.Characters.FirstOrDefault(c => c.Name == characterName);

      if (character != null)
      {
        if (location != null) character.Location = location;
        if (isAlphaClone != null) character.IsAlphaClone = isAlphaClone;
      }
      else
      {
        var gameClient = GameClientCache.GetGameClientForCharacter(characterName);

        // but there is another client cached for this character
        // so move those characters to this client
        if (gameClient != null)
        {
          foreach (var tempCharacter in gameClient.Characters)
          {
            _commanderClient.Characters.Add(tempCharacter);
            if (location != null) tempCharacter.Location = location;
            if (isAlphaClone != null) tempCharacter.IsAlphaClone = isAlphaClone;
            if (tempCharacter.Plugins == null || !tempCharacter.Plugins.Any())
            {
              tempCharacter.Plugins = CommanderMain.GetNewPluginsForCharacter(tempCharacter.Name, newWindowId);
            }
          }
          gameClient.Characters.Clear();
        }
        else
        {
          // nothing in the cache!
          // Add this character to the list
          var newCharacter = new CommanderCharacter
          {
            Name = characterName,
            Location = location ?? "Unknown",
            IsAlphaClone = isAlphaClone,
            Plugins = CommanderMain.GetNewPluginsForCharacter(characterName, newWindowId)
          };
          _commanderClient.Characters.Add(newCharacter);
        }
      }
    }

    private async Task FindUIRootAddress(GameClient cachedGameClient)
    {
      DetailsPanel.Width = 0;
      MemoryScanPanel.Width = Double.NaN;
      Result.Content = "";
      Result.Width = 0;

      await UiRootScanThrottle.WaitAsync();
      try
      {
        var address = await Task.Run(() => MemoryReader.FindUIRootAddressFromProcessId(cachedGameClient.processId));
        if (address != null)
        {
          cachedGameClient.uiRootAddress = address ?? 0;
          Debug.WriteLine("Got uiRoot = " + address);

          Properties.Settings.Default.uiRootAddressCache = GameClientCache.SaveCache();
          Properties.Settings.Default.Save(); // Persist the changes
        }
      }
      finally
      {
        UiRootScanThrottle.Release();
      }
    }

    [SupportedOSPlatform("windows5.0")]
    private async Task CommandClient()
    {
      if (CommanderClient?.GameClient == null)
        return;

      await ScanForRootAddress();

      if (CommanderClient?.GameClient.uiRootAddress > 0)
      {
        var rootNode = await Task.Run(() =>
        {
          try
          {
            return MemoryReader.ReadMemory(CommanderClient.GameClient.processId, CommanderClient.GameClient.uiRootAddress);
          }
          catch (Exception ex)
          {
            LogExceptionToFile("CommandClient:ReadMemory", ex);
            Dispatcher.Invoke(() =>
            {
              Result.Content = ex.Message;
              Result.Width = Double.NaN;
            });
          }
          return null;
        });

        if (rootNode == null)
        {
          // TODO: we should probably only do this if the exception was something that indicates
          // the address is no longer valid, not for random exceptions that could be transient
          //CommanderClient.GameClient.uiRootAddress = 0; // force a rescan for the address
          if (Result.Width == 0)
          {
            Result.Content = "Null RootNode!";
            Result.Width = Double.NaN;
          }
          return;
        }

        ParsedUserInterface _uiRoot;
        try
        {
          _uiRoot = UIParser.ParseUserInterface(rootNode);
        }
        catch (FatalUiRootParseException ex)
        {
          CommanderClient.GameClient.uiRootAddress = 0; // force a rescan for the address
          Result.Content = "Fatal parser exception. UI root will be re-scanned: " + ex.Message;
          Result.Width = Double.NaN;
          return;
        }
        catch (Exception ex)
        {
          LogExceptionToFile("CommandClient:UIParser", ex);
          Result.Content = "Exception in Parser: " + ex.Message;
          Result.Width = Double.NaN;
          return;
        }

        // update alive indicator
        aliveSpinnerIndex = (aliveSpinnerIndex + 1) % ALIVE_SPINNER.Length;
        Alive.Content = ALIVE_SPINNER[aliveSpinnerIndex];

        if (_uiRoot == null)
          return;

        var bot = new EveBot(_uiRoot);

        SolarSystem.Content =
            $"{bot.CurrentSystemName()} ({string.Format("{0:0.0}", bot.CurrentSystemSecStatus())})";
        var color = bot.CurrentSystemSecStatusColor();
        SolarSystem.Foreground = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));

        if (bot.CharacterSelectionScreenVisible())
        {
          var characterSlots = bot.CharacterSelectionSlots()
              .ToList();

          foreach (var characterSlot in characterSlots)
          {
            if (characterSlot != null && !string.IsNullOrEmpty(characterSlot.Name))
            {
              var name = characterSlot.Name;
              var location = characterSlot.SystemName;
              if (characterSlot.IsUndocked == true)
              {
                location += " (Undocked)";
              }
              bool? isAlphaClone = null;
              if (bot.DoesCharacterSelectionScreenShowAlphaStatus())
              {
                isAlphaClone = true;
              }

              UpdateGameClientForCharacter(name, CommanderClient.GameClient.mainWindowId, location, isAlphaClone);
            }
          }
        }

        if (!CommanderMain.IsRunning())
        {
          return;
        }

        var autoRunPlugins = CommanderMain.GetAutoRunPlugins().Where(p => !p.IsPaused);
        foreach (var plugin in autoRunPlugins)
        {
          plugin.CharacterName = CurrentCharacterName;
          var result = await plugin.DoWork(_uiRoot, CommanderClient.GameClient, CommanderMain.IsRunning(), CommanderMain.GetAllPlugins());
          if (!string.IsNullOrWhiteSpace(result.Message))
          {
            LogActionToFile(result.Message);
            Result.Content = result.Message;
            Result.Width = Double.NaN;
          }
          if (result.Background != null)
          {
            var b = (System.Drawing.Color)result.Background;
            Background = new SolidColorBrush(Color.FromArgb(b.A, b.R, b.G, b.B));
          }
          if (result.Foreground != null)
          {
            var f = (System.Drawing.Color)result.Foreground;
            Result.Foreground = new SolidColorBrush(Color.FromArgb(f.A, f.R, f.G, f.B));
          }
          if (result.WorkDone)
          {
            return;
          }
        }

        // if we get here, we should be logged in with a character name
        var selectedBotCharacter = CommanderClient.Characters
            .FirstOrDefault(c => c.Name == CurrentCharacterName);

        if (selectedBotCharacter is not CommanderCharacter selectedCharacter)
        {
          Result.Content = "Unknown Character";
          Result.Width = Double.NaN;
          Background = new SolidColorBrush(Colors.Red);
          Result.Foreground = new SolidColorBrush(Colors.Black);
          return;
        }

        PluginDescription.Content = selectedCharacter.EnabledPluginDescription;

        var allPlugins = CommanderMain.GetAllPlugins();

        var availablePlugins = selectedCharacter.Plugins
          .Where(p => p.IsEnabled && !p.IsPaused)
          .ToList();

        // update the pause indicator if any of the enabled plugins are paused
        var isSomethingPaused = selectedCharacter.Plugins.Any(p => p.IsEnabled && p.IsPaused);
        UpdatePauseIndicator(isSomethingPaused);

        // order of execution is important
        foreach (var plugin in availablePlugins)
        {
          var result = await plugin.DoWork(
            _uiRoot,
            CommanderClient.GameClient,
            allPlugins
          );

          if (!string.IsNullOrWhiteSpace(result.Message))
          {
            LogActionToFile(result.Message);
            Result.Content = result.Message;
            Result.Width = Double.NaN;
          }

          if (result.Background == null)
          {
            Background = Brushes.Transparent;
          }
          else
          {
            var b = (System.Drawing.Color)result.Background;
            Background = new SolidColorBrush(Color.FromArgb(b.A, b.R, b.G, b.B));
          }

          if (result.Foreground == null)
          {
            Foreground = Brushes.Black;
          }
          else
          {
            var b = (System.Drawing.Color)result.Foreground;
            var br = new SolidColorBrush(Color.FromArgb(b.A, b.R, b.G, b.B));
            Result.Foreground = br;
            Foreground = br;
          }

          if (result.WorkDone == true)
          {
            return;
          }
        }

        // If we get here, all plugins are finished
        Background = new SolidColorBrush(Colors.Green);

        //// send the report
        //await SendReport(gridscoutOverview, wormholeCode);
      } else
      {
        Result.Content = "Zero Root Address!";
        Result.Width = Double.NaN;
        return;
      }

      //long deltaTime = DateTime.Now.Ticks - lastPilotCountChangeTime;
      //if (deltaTime < GRID_CHANGE_NOTIFICATION_DURATION)
      //{
      //    // Lerp the colour from orange to transparent over time
      //    byte alpha = (byte)(255f - (255f * (double)deltaTime / GRID_CHANGE_NOTIFICATION_DURATION));
      //    Background = new SolidColorBrush(Color.FromArgb(alpha, 255, 128, 0));
      //}
      //else
      //{
      //    ScanChanges.Content = "";
      //    ScanChanges.Width = 0;
      //}

      return;

    }

    private void UpdatePauseIndicator(bool isSomethingPaused)
    {
      PauseIndicator.Visibility = isSomethingPaused ? Visibility.Visible : Visibility.Collapsed;

      if (isSomethingPaused)
      {
        PauseIndicator.Content = "Paused";
      }
      else
      {
        PauseIndicator.Content = string.Empty;
      }
    }

    private void LogExceptionToFile(string context, Exception ex)
    {
      try
      {
        var logDirectory = Path.GetDirectoryName(ExceptionLogPath);
        if (!string.IsNullOrWhiteSpace(logDirectory))
        {
          Directory.CreateDirectory(logDirectory);
        }

        var characterName = CurrentCharacterName ?? "Unknown";
        var windowId = CommanderClient?.GameClient.mainWindowId ?? 0;
        var processId = CommanderClient?.GameClient.processId ?? 0;

        var logEntry =
          $"[{DateTime.UtcNow:O}] Context={context}; Character={characterName}; WindowId={windowId}; ProcessId={processId}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}";

        File.AppendAllText(ExceptionLogPath, logEntry);
      }
      catch (Exception logWriteException)
      {
        Debug.WriteLine(logWriteException);
      }
    }

    private void LogActionToFile(string actionText)
    {
      try
      {
        var logDirectory = Path.GetDirectoryName(ExceptionLogPath);
        if (!string.IsNullOrWhiteSpace(logDirectory))
        {
          Directory.CreateDirectory(logDirectory);
        }

        var characterName = CurrentCharacterName ?? "Unknown";

        var logEntry = $"[{DateTime.UtcNow:O}] {actionText}\n";

        File.AppendAllText(Path.Combine(logDirectory!, characterName + ".log"), logEntry);
      }
      catch (Exception logWriteException)
      {
        Debug.WriteLine(logWriteException);
      }
    }

    private void UserControl_MouseEnter(object sender, MouseEventArgs e)
    {
      BaseGrid.Background = new SolidColorBrush(Colors.LightBlue);
    }

    private void UserControl_MouseLeave(object sender, MouseEventArgs e)
    {
      BaseGrid.Background = new SolidColorBrush(Colors.Transparent);
    }

    private void UserControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
      _mouseDownPoint = e.GetPosition(this);
    }

    private async void UserControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
      if (
          _mouseDownPoint != null
          && _mouseDownPoint.Equals(e.GetPosition(this))
          && _commanderClient != null
      )
      {
        WinApi.ShowWindow((nint)_commanderClient.GameClient.mainWindowId);

        // open a new Visualise window
        if (CommanderClient != null && Debugger.IsAttached)
        {
          var _uiRoot = await Task.Run(() =>
          {
            UITreeNode rootNode = MemoryReader.ReadMemory(CommanderClient.GameClient.processId, CommanderClient.GameClient.uiRootAddress)!;
            if (rootNode == null) return null;
            return UIParser.ParseUserInterface(rootNode);
          });

          if (_uiRoot != null)
          {
            var visualise = new VisualiseUI();
            visualise.Show();
            await visualise.VisualiseAsync(_uiRoot);
          }
        }
      }
      _mouseDownPoint = null;
    }

    internal void UpdatePlugins()
    {
      PluginDescription.Content = _commanderClient?
          .Characters
          .FirstOrDefault(c => c.Name == CurrentCharacterName)?
          .EnabledPluginDescription ?? "Missing Character for Plugins";
    }

    /// <summary>
    /// Show an alert overlay that fades from the specified color to transparent
    /// </summary>
    /// <param name="alertColor">The alert color (e.g., Colors.Red, Colors.Orange)</param>
    /// <param name="durationMs">Duration of the fade animation in milliseconds (default: 2000ms)</param>
    public async void ShowAlert(Color alertColor, int durationMs = 2000)
    {
      // Set the alert color
      AlertOverlay.Background = new SolidColorBrush(alertColor);
      AlertOverlay.Opacity = 1.0;

      // Animate the fade to transparent
      var startTime = DateTime.Now.Ticks;
      var duration = durationMs * TimeSpan.TicksPerMillisecond;

      while (DateTime.Now.Ticks - startTime < duration)
      {
        var elapsed = DateTime.Now.Ticks - startTime;
        var progress = (double)elapsed / duration;

        // Lerp from 1.0 to 0.0
        AlertOverlay.Opacity = 1.0 - progress;

        await Task.Delay(16); // ~60fps
      }

      // Ensure fully transparent at the end
      AlertOverlay.Opacity = 0;
      AlertOverlay.Background = new SolidColorBrush(Colors.Transparent);
    }
  }
}
