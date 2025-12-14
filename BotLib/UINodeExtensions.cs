using eve_parse_ui;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;

namespace BotLib
{
  public static class UINodeExtensions
  {
    public static UIElement? ToUIElement(this UITreeNodeWithDisplayRegion? node)
    {
      return node == null ? null : new UIElement(node);
    }


    public static bool HasNoVisibleRegion([NotNullWhen(false)] this UIElement? node)
    {
      if (
          node == null
          || node.TotalDisplayRegionVisible.Width <= 0
          || node.TotalDisplayRegionVisible.Height <= 0
      )
        return true;

      return false;
    }
  }
}
