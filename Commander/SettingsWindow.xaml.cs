using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BotLib;
using Commander.Services;
using Commander.Models;

namespace Commander
{
  public partial class SettingsWindow : Window
  {
    private Dictionary<string, Dictionary<string, Object?>> _originalCache = [];
    private Dictionary<string, Dictionary<string, Control>> _controls = [];
    private DiscordAuthService? _discordAuth;

    // Discord UI elements
    private TextBlock? _discordStatusText;
    private StackPanel? _discordUserPanel;
    private Image? _discordAvatar;
    private TextBlock? _discordUsername;
    private TextBlock? _discordUserId;
    private TextBlock? _tokenExpiryText;
    private Button? _connectDiscordButton;
    private Button? _disconnectDiscordButton;
    private Button? _reauthDiscordButton;
    private TextBlock? _discordErrorText;

    public SettingsWindow()
    {
      InitializeComponent();
      if (FindName("OpenLogFolderButton") is Button openLogFolderButton)
      {
        openLogFolderButton.Click += OpenLogFolderButton_Click;
      }

      // copy current cache so cancel can revert
      _originalCache = PluginSettingsCache.GetCache().ToDictionary(kvp => kvp.PluginName, kvp => kvp.Settings.ToDictionary(k => k.Key, v => v.Value));

      // find all the IBotLibPlugins and build the framework for the UI from the PluginInfo

      // use reflection to find all the classes that inherit from IBotLibPlugin
      var allPluginsWithSettings = AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(s => s.GetTypes())
        .Where(p => typeof(IBotLibPlugin).IsAssignableFrom(p) && !p.IsAbstract)
        .Select(t => Activator.CreateInstance(t, "DummyCharacter", 0L) as IBotLibPlugin)
        .Where(p => p != null && p.GetSettingsInfo().Any())
        .Cast<IBotLibPlugin>()
        .ToList();

      InitializeDiscordAuth();
      LoadUi(allPluginsWithSettings);
    }

    private void LoadUi(List<IBotLibPlugin> allPluginsWithSettings)
    {
      var cache = PluginSettingsCache.GetCache();
      PluginsStack.Children.Clear();

      // Add Discord UI first
      BuildDiscordUI();

      foreach (var plugin in allPluginsWithSettings)
      {
        var grp = new GroupBox() { Header = plugin.Name, Margin = new Thickness(5) };
        var panel = new StackPanel();
        var controlDict = new Dictionary<string, Control>();

        foreach (var settingInfo in plugin.GetSettingsInfo())
        {
          var sp = new StackPanel() { Orientation = Orientation.Horizontal, Margin = new Thickness(2) };
          sp.Children.Add(new TextBlock() { Text = settingInfo.Key, Width = 200, VerticalAlignment = VerticalAlignment.Center });

          var value = cache
            .FirstOrDefault(p => p.PluginName == plugin.Name)?
            .Settings
            .FirstOrDefault(s => s.Key == settingInfo.Key)?
            .Value ?? string.Empty;

          if (settingInfo.SettingType == BotLibSetting.Type.Boolean)
          {
            var isChecked = value is bool boolValue
              ? boolValue
              : bool.TryParse(value?.ToString(), out var parsedBoolValue) && parsedBoolValue;

            var checkBox = new CheckBox() { 
              IsChecked = isChecked, 
              VerticalAlignment = VerticalAlignment.Center 
            };

            // if the setting has a description, add a tooltip
            if (!string.IsNullOrWhiteSpace(settingInfo.Description))
            {
              checkBox.ToolTip = settingInfo.Description;
            }

            sp.Children.Add(checkBox);
            controlDict[settingInfo.Key] = checkBox;
          }
          else
          {
            var tb = new TextBox() { Text = value?.ToString() ?? string.Empty, Width = 300 };

            // configure textbox based on setting type
            if (settingInfo.SettingType == BotLibSetting.Type.MultiLineText)
            {
              tb.Height = 100;
              tb.AcceptsReturn = true;
              tb.TextWrapping = TextWrapping.Wrap;
              tb.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            }
            if (settingInfo.SettingType == BotLibSetting.Type.Integer)
            {
              tb.PreviewTextInput += (s, e) =>
              {
                e.Handled = !e.Text.All(c => char.IsDigit(c));
              };
            }
            if (settingInfo.SettingType == BotLibSetting.Type.Decimal)
            {
              tb.PreviewTextInput += (s, e) =>
              {
                e.Handled = !e.Text.All(c => char.IsDigit(c) || c == '.' || c == ',');
              };
            }

            // if the setting has a description, add a tooltip
            if (!string.IsNullOrWhiteSpace(settingInfo.Description))
            {
              tb.ToolTip = settingInfo.Description;
            }

            sp.Children.Add(tb);
            controlDict[settingInfo.Key] = tb;
          }

          panel.Children.Add(sp);
        }

        grp.Content = panel;
        PluginsStack.Children.Add(grp);
        _controls[plugin.Name] = controlDict;
      }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
      // write back values into the cache
      var cache = PluginSettingsCache.GetCache();
      foreach (var plugin in _controls)
      {
        var pluginEntry = cache.FirstOrDefault(p => p.PluginName == plugin.Key) ?? new PluginSettings() { PluginName = plugin.Key, Settings = [] };

        foreach (var kv in plugin.Value)
        {
          var setting = pluginEntry.Settings.FirstOrDefault(
            s => s.Key == kv.Key,
            new PluginSetting() { Key = kv.Key, Value = null }
          );
          if (kv.Value is TextBox textBox)
          {
            setting.Value = textBox.Text;
          }
          else if (kv.Value is CheckBox checkBox)
          {
            setting.Value = checkBox.IsChecked == true;
          }
          pluginEntry.Settings.Add(setting);
        }
      }

      // persist
      Properties.Settings.Default.pluginSettingsCache = PluginSettingsCache.SaveCache();
      Properties.Settings.Default.Save();

      this.DialogResult = true;
      this.Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
      // revert the cache
      var cache = PluginSettingsCache.GetCache();
      cache.Clear();
      foreach (var kv in _originalCache)
      {
        cache.Add(new PluginSettings()
        {
          PluginName = kv.Key,
          Settings = [.. kv.Value.Select(pair => new PluginSetting { Key = pair.Key, Value = pair.Value })]
        });
      }

      this.DialogResult = false;
      this.Close();
    }

    private void OpenLogFolderButton_Click(object sender, RoutedEventArgs e)
    {
      try
      {
        var logDirectory = PluginLoggingService.GetLogDirectory();
        Directory.CreateDirectory(logDirectory);

        Process.Start(new ProcessStartInfo
        {
          FileName = logDirectory,
          UseShellExecute = true
        });
      }
      catch (System.ComponentModel.Win32Exception ex)
      {
        MessageBox.Show(this, $"Unable to open the log folder.\n\n{ex.Message}", "Open Log Folder", MessageBoxButton.OK, MessageBoxImage.Error);
      }
      catch (InvalidOperationException ex)
      {
        MessageBox.Show(this, $"Unable to open the log folder.\n\n{ex.Message}", "Open Log Folder", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    private void BuildDiscordUI()
    {
      var discordGroupBox = new GroupBox
      {
        Header = "Discord Integration",
        Margin = new Thickness(5)
      };

      var mainPanel = new StackPanel { Margin = new Thickness(10) };

      // Status Display
      var statusPanel = new StackPanel
      {
        Orientation = Orientation.Horizontal,
        Margin = new Thickness(0, 0, 0, 10)
      };
      statusPanel.Children.Add(new TextBlock
      {
        Text = "Status: ",
        FontWeight = FontWeights.Bold,
        Width = 100
      });
      _discordStatusText = new TextBlock { Text = "Not Connected" };
      statusPanel.Children.Add(_discordStatusText);
      mainPanel.Children.Add(statusPanel);

      // User Info Panel (hidden by default)
      _discordUserPanel = new StackPanel
      {
        Visibility = Visibility.Collapsed,
        Margin = new Thickness(0, 0, 0, 10)
      };

      var avatarPanel = new StackPanel
      {
        Orientation = Orientation.Horizontal,
        Margin = new Thickness(0, 5, 0, 0)
      };
      _discordAvatar = new Image
      {
        Width = 48,
        Height = 48,
        Margin = new Thickness(0, 0, 10, 0)
      };
      avatarPanel.Children.Add(_discordAvatar);

      var userInfoPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
      _discordUsername = new TextBlock { FontWeight = FontWeights.Bold };
      _discordUserId = new TextBlock
      {
        FontSize = 10,
        Foreground = Brushes.Gray
      };
      userInfoPanel.Children.Add(_discordUsername);
      userInfoPanel.Children.Add(_discordUserId);
      avatarPanel.Children.Add(userInfoPanel);
      _discordUserPanel.Children.Add(avatarPanel);

      _tokenExpiryText = new TextBlock
      {
        FontSize = 10,
        Margin = new Thickness(0, 5, 0, 0)
      };
      _discordUserPanel.Children.Add(_tokenExpiryText);
      mainPanel.Children.Add(_discordUserPanel);

      // Action Buttons
      var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal };

      _connectDiscordButton = new Button
      {
        Content = "Connect Discord",
        Width = 120,
        Margin = new Thickness(0, 0, 10, 0)
      };
      _connectDiscordButton.Click += ConnectDiscord_Click;
      buttonPanel.Children.Add(_connectDiscordButton);

      _disconnectDiscordButton = new Button
      {
        Content = "Disconnect",
        Width = 120,
        Visibility = Visibility.Collapsed
      };
      _disconnectDiscordButton.Click += DisconnectDiscord_Click;
      buttonPanel.Children.Add(_disconnectDiscordButton);

      _reauthDiscordButton = new Button
      {
        Content = "Re-authenticate",
        Width = 120,
        Margin = new Thickness(10, 0, 0, 0),
        Visibility = Visibility.Collapsed
      };
      _reauthDiscordButton.Click += ReauthDiscord_Click;
      buttonPanel.Children.Add(_reauthDiscordButton);

      mainPanel.Children.Add(buttonPanel);

      // Error Messages
      _discordErrorText = new TextBlock
      {
        Foreground = Brushes.Red,
        Margin = new Thickness(0, 10, 0, 0),
        TextWrapping = TextWrapping.Wrap,
        Visibility = Visibility.Collapsed
      };
      mainPanel.Children.Add(_discordErrorText);

      discordGroupBox.Content = mainPanel;
      PluginsStack.Children.Add(discordGroupBox);
    }

    private async void InitializeDiscordAuth()
    {
      try
      {
        // Initialize the global DiscordAuthManager
        await DiscordAuthManager.InitializeAsync();

        // Get the service instance for UI event handling
        _discordAuth = DiscordAuthManager.GetService();

        if (_discordAuth != null)
        {
          _discordAuth.AuthenticationCompleted += OnDiscordAuthCompleted;
          _discordAuth.AuthenticationFailed += OnDiscordAuthFailed;
          _discordAuth.AuthenticationRevoked += OnDiscordAuthRevoked;
        }

        UpdateDiscordUI();
      }
      catch (Exception ex)
      {
        if (_discordStatusText != null)
        {
          _discordStatusText.Text = $"Error: {ex.Message}";
          _discordStatusText.Foreground = Brushes.Red;
        }
      }
    }

    private void UpdateDiscordUI()
    {
      if (_discordAuth?.CurrentUser == null)
      {
        if (_discordStatusText != null) _discordStatusText.Text = "Not Connected";
        if (_discordStatusText != null) _discordStatusText.Foreground = Brushes.Gray;
        if (_discordUserPanel != null) _discordUserPanel.Visibility = Visibility.Collapsed;
        if (_connectDiscordButton != null) _connectDiscordButton.Visibility = Visibility.Visible;
        if (_disconnectDiscordButton != null) _disconnectDiscordButton.Visibility = Visibility.Collapsed;
        if (_reauthDiscordButton != null) _reauthDiscordButton.Visibility = Visibility.Collapsed;
        if (_discordErrorText != null) _discordErrorText.Visibility = Visibility.Collapsed;
      }
      else
      {
        var user = _discordAuth.CurrentUser;
        if (_discordStatusText != null) _discordStatusText.Text = "Connected";
        if (_discordStatusText != null) _discordStatusText.Foreground = Brushes.Green;
        if (_discordUsername != null) _discordUsername.Text = user.GetDisplayName();
        if (_discordUserId != null) _discordUserId.Text = $"ID: {user.DiscordUserId}";

        var expiresIn = user.TokenExpiry - DateTime.UtcNow;
        if (expiresIn.TotalMinutes < 0)
        {
          if (_tokenExpiryText != null) _tokenExpiryText.Text = "Token expired - please re-authenticate";
          if (_tokenExpiryText != null) _tokenExpiryText.Foreground = Brushes.Red;
          if (_reauthDiscordButton != null) _reauthDiscordButton.Visibility = Visibility.Visible;
        }
        else if (expiresIn.TotalDays < 1)
        {
          if (_tokenExpiryText != null) _tokenExpiryText.Text = $"Token expires in {expiresIn.Hours} hours";
          if (_tokenExpiryText != null) _tokenExpiryText.Foreground = Brushes.Orange;
          if (_reauthDiscordButton != null) _reauthDiscordButton.Visibility = Visibility.Collapsed;
        }
        else
        {
          if (_tokenExpiryText != null) _tokenExpiryText.Text = $"Token expires in {expiresIn.Days} days";
          if (_tokenExpiryText != null) _tokenExpiryText.Foreground = Brushes.Gray;
          if (_reauthDiscordButton != null) _reauthDiscordButton.Visibility = Visibility.Collapsed;
        }

        try
        {
          var avatarUrl = user.GetAvatarUrl();
          var bitmap = new BitmapImage(new Uri(avatarUrl));
          if (_discordAvatar != null) _discordAvatar.Source = bitmap;
        }
        catch
        {
          if (_discordAvatar != null) _discordAvatar.Source = null;
        }

        if (_discordUserPanel != null) _discordUserPanel.Visibility = Visibility.Visible;
        if (_connectDiscordButton != null) _connectDiscordButton.Visibility = Visibility.Collapsed;
        if (_disconnectDiscordButton != null) _disconnectDiscordButton.Visibility = Visibility.Visible;
        if (_discordErrorText != null) _discordErrorText.Visibility = Visibility.Collapsed;
      }
    }

    private async void ConnectDiscord_Click(object sender, RoutedEventArgs e)
    {
      if (_discordAuth == null)
      {
        ShowDiscordError("Discord authentication service not initialized.");
        return;
      }

      if (_connectDiscordButton != null) _connectDiscordButton.IsEnabled = false;
      if (_discordStatusText != null) _discordStatusText.Text = "Waiting for authentication...";
      if (_discordStatusText != null) _discordStatusText.Foreground = Brushes.Orange;

      try
      {
        await _discordAuth.AuthenticateAsync();
      }
      catch (Exception ex)
      {
        ShowDiscordError($"Authentication failed: {ex.Message}");
        if (_connectDiscordButton != null) _connectDiscordButton.IsEnabled = true;
        if (_discordStatusText != null) _discordStatusText.Text = "Not Connected";
        if (_discordStatusText != null) _discordStatusText.Foreground = Brushes.Gray;
      }
    }

    private async void DisconnectDiscord_Click(object sender, RoutedEventArgs e)
    {
      if (_discordAuth == null)
        return;

      var result = MessageBox.Show(
        "Are you sure you want to disconnect your Discord account?",
        "Disconnect Discord",
        MessageBoxButton.YesNo,
        MessageBoxImage.Question
      );

      if (result == MessageBoxResult.Yes)
      {
        if (_disconnectDiscordButton != null) _disconnectDiscordButton.IsEnabled = false;
        await _discordAuth.RevokeAuthenticationAsync();
        if (_disconnectDiscordButton != null) _disconnectDiscordButton.IsEnabled = true;
      }
    }

    private async void ReauthDiscord_Click(object sender, RoutedEventArgs e)
    {
      if (_discordAuth == null)
        return;

      // PKCE doesn't support refresh - need full re-auth
      if (_reauthDiscordButton != null) _reauthDiscordButton.IsEnabled = false;
      await _discordAuth.RevokeAuthenticationAsync();
      if (_connectDiscordButton != null) _connectDiscordButton.IsEnabled = false;
      if (_discordStatusText != null) _discordStatusText.Text = "Waiting for authentication...";
      if (_discordStatusText != null) _discordStatusText.Foreground = Brushes.Orange;

      try
      {
        await _discordAuth.AuthenticateAsync();
      }
      catch (Exception ex)
      {
        ShowDiscordError($"Re-authentication failed: {ex.Message}");
        if (_connectDiscordButton != null) _connectDiscordButton.IsEnabled = true;
        if (_discordStatusText != null) _discordStatusText.Text = "Not Connected";
        if (_discordStatusText != null) _discordStatusText.Foreground = Brushes.Gray;
      }

      if (_reauthDiscordButton != null) _reauthDiscordButton.IsEnabled = true;
    }

    private void OnDiscordAuthCompleted(object? sender, DiscordUserInfo user)
    {
      Dispatcher.Invoke(() =>
      {
        if (_connectDiscordButton != null) _connectDiscordButton.IsEnabled = true;
        UpdateDiscordUI();
      });
    }

    private void OnDiscordAuthFailed(object? sender, string error)
    {
      Dispatcher.Invoke(() =>
      {
        ShowDiscordError(error);
        if (_connectDiscordButton != null) _connectDiscordButton.IsEnabled = true;
        if (_discordStatusText != null) _discordStatusText.Text = "Not Connected";
        if (_discordStatusText != null) _discordStatusText.Foreground = Brushes.Gray;
      });
    }

    private void OnDiscordAuthRevoked(object? sender, EventArgs e)
    {
      Dispatcher.Invoke(() =>
      {
        UpdateDiscordUI();
      });
    }

    private void ShowDiscordError(string message)
    {
      if (_discordErrorText != null) _discordErrorText.Text = message;
      if (_discordErrorText != null) _discordErrorText.Visibility = Visibility.Visible;
    }
  }
}
