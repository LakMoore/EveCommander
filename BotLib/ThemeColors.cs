namespace BotLib;

/// <summary>
/// Shared dark theme colors used across Commander WPF app and plugins
/// These match the colors defined in Theme.Dark.xaml
/// </summary>
public static class ThemeColors
{
  // Color palette (matches Theme.Dark.xaml)
  public static readonly System.Drawing.Color Background = System.Drawing.Color.FromArgb(255, 30, 30, 30);      // #FF1E1E1E
  public static readonly System.Drawing.Color Surface = System.Drawing.Color.FromArgb(255, 37, 37, 38);         // #FF252526
  public static readonly System.Drawing.Color Accent = System.Drawing.Color.FromArgb(255, 0, 122, 204);         // #FF007ACC
  public static readonly System.Drawing.Color Foreground = System.Drawing.Color.FromArgb(255, 240, 240, 240);   // #FFF0F0F0
  public static readonly System.Drawing.Color SecondaryForeground = System.Drawing.Color.FromArgb(255, 191, 191, 191); // #FFBFBFBF
  public static readonly System.Drawing.Color Border = System.Drawing.Color.FromArgb(255, 60, 60, 60);          // #FF3C3C3C
  public static readonly System.Drawing.Color Hover = System.Drawing.Color.FromArgb(255, 46, 46, 46);           // #FF2E2E2E
  public static readonly System.Drawing.Color Selection = System.Drawing.Color.FromArgb(255, 38, 79, 102);      // #FF264F66

  // Semantic colors
  public static readonly System.Drawing.Color Error = System.Drawing.Color.Red;
  public static readonly System.Drawing.Color Warning = System.Drawing.Color.Orange;
  public static readonly System.Drawing.Color Success = System.Drawing.Color.Green;
}
