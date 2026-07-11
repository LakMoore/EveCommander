using System.Xml.Serialization;

namespace BotLib
{
  public record BotCharacter
  {
    public required string Name { get; set; }
    public required string Location { get; set; }
    public bool? IsAlphaClone { get; set; }
    private IEnumerable<IBotLibPlugin> _plugins = [];

    [XmlIgnore]
    public IEnumerable<IBotLibPlugin> Plugins
    {
      get => _plugins;
      set
      {
        if (ReferenceEquals(_plugins, value))
          return;

        foreach (var plugin in _plugins.OfType<IDisposable>())
        {
          plugin.Dispose();
        }

        _plugins = value ?? [];
      }
    }

    public string EnabledPluginDescription => Plugins
        .Where(p => p.IsEnabled)
        .Select(p => p.Name)
        .Count() switch
    {
      0 => "No plugins enabled",
      1 => Plugins.First(p => p.IsEnabled).Name,
      _ => $"{Plugins.Count(p => p.IsEnabled)} plugins enabled"
    };
  }
}
