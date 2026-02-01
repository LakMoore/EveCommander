using eve_parse_ui;
using System.Diagnostics.CodeAnalysis;

namespace BotLib
{
  /// <summary>
  /// Just a pretty alias
  /// </summary>
  public record UIElement : UITreeNodeWithDisplayRegion
  {
    [SetsRequiredMembers]
    public UIElement(UITreeNodeWithDisplayRegion original) : base(original)
    {
    }
  }
}
