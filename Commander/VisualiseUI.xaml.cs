using eve_parse_ui;
using System.Windows;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Commander
{
  /// <summary>
  /// Interaction logic for VisualiseUI.xaml
  /// </summary>
  public partial class VisualiseUI : Window
  {
    public VisualiseUI()
    {
      InitializeComponent();
    }

    public async Task VisualiseAsync(ParsedUserInterface root)
    {
      await DrawAllChildrenAsync(root.UiTree, new List<UITreeNodeNoDisplayRegion>());
    }

    private async Task DrawAllChildrenAsync(UITreeNodeNoDisplayRegion node, List<UITreeNodeNoDisplayRegion> ancestors)
    {
      var thisNodeType = node.pythonObjectTypeName;
      var thisNodeName = node.GetNameFromDictEntries();

      var description = thisNodeType;
      if (thisNodeName != null)
      {
        description += " [" + thisNodeName + "]";
      }

      var newAncestors = new List<UITreeNodeNoDisplayRegion>(ancestors) { node };

      var newPath = string.Join(" > ", newAncestors.Select(n =>
      {
        var t = n.pythonObjectTypeName;
        var nname = n.GetNameFromDictEntries();
        return nname != null ? t + " [" + nname + "]" : t;
      }));

      DrawNode(node, newPath, newAncestors);
      foreach (var item in node.Children ?? [])
      {
        await DrawAllChildrenAsync(item, newAncestors);
      }
    }

    // < Frame BorderBrush = "Black" BorderThickness = "0.2" Width = "100" Height = "100" HorizontalAlignment = "Left" VerticalAlignment = "Top" Margin = "100,100,0,0" ></ Frame >
    private void DrawNode(UITreeNodeNoDisplayRegion? node, string path, List<UITreeNodeNoDisplayRegion>? ancestors)
    {

      if (node is UITreeNodeWithDisplayRegion uiTreeNodeWithDisplayRegion)
      {
        //var region = uiTreeNodeWithDisplayRegion.TotalDisplayRegion;
        var region = uiTreeNodeWithDisplayRegion.TotalDisplayRegionVisible;

        var margin = new Thickness(region.X, region.Y, 0, 0);

        var dictEntriesSeq = (node.dictEntriesOfInterest ?? Enumerable.Empty<KeyValuePair<string, object>>())
            .Select(de => de.Key + " = " + de.Value?.ToString());

        var otherEntriesSeq = (node.otherDictEntriesKeys ?? Enumerable.Empty<string>());

        var frame = new Frame
        {
          BorderBrush = new SolidColorBrush(Colors.Black),
          BorderThickness = new Thickness(0.2),
          HorizontalAlignment = HorizontalAlignment.Left,
          VerticalAlignment = VerticalAlignment.Top,
          Width = region.Width,
          Height = region.Height,
          Margin = margin
        };
        // Build per-entry tag strings for the whole path (ancestors)
        string[] perEntryTags = Array.Empty<string>();
        if (ancestors != null)
        {
          perEntryTags = ancestors.Select(n =>
          {
            var nType = n.pythonObjectTypeName;
            var nName = n.GetNameFromDictEntries();
            var nDescription = nType + (nName != null ? " [" + nName + "]" : string.Empty);
            var nDict = (n.dictEntriesOfInterest ?? Enumerable.Empty<KeyValuePair<string, object>>())
                .Select(de => de.Key + " = " + de.Value?.ToString());
            var nOther = n.otherDictEntriesKeys ?? Enumerable.Empty<string>();
            var combined = string.Join("\n", new[] { nDescription }.Concat(nDict).Concat(nOther));
            return combined;
          }).ToArray();
        }

        var fullPath = path;
        frame.Tag = (fullPath, perEntryTags);
        frame.MouseEnter += EveRoot_MouseEnter;
        frame.MouseLeave += EveRoot_MouseLeave;
        frame.MouseLeftButtonUp += Frame_MouseLeftButtonUp;

        EveRoot.Children.Add(frame);
        //await Task.Delay(1);
      }
    }

    private void Frame_MouseLeftButtonUp(object? sender, MouseButtonEventArgs e)
    {
      if (sender is not Frame frame)
        return;

      // Support both legacy string tag and new (fullPath, perEntryTags) tuple
      string fullPath = string.Empty;
      string[] perEntryTags = Array.Empty<string>();

      if (frame.Tag is ValueTuple<string, string[]> tuple)
      {
        fullPath = tuple.Item1 ?? string.Empty;
        perEntryTags = tuple.Item2 ?? Array.Empty<string>();
      }
      else if (frame.Tag is string s)
      {
        fullPath = s;
        var lines = s.Split(new[] { '\n' }, System.StringSplitOptions.None);
        var pathLine = lines.Length > 0 ? lines[0] : string.Empty;
        var rawEntries = pathLine.Split(new[] { " > " }, System.StringSplitOptions.None);
        // perEntryTags will be filled lazily from the legacy tag when a button is clicked
        perEntryTags = [.. rawEntries.Select(r => r?.Trim()).Where(r => !string.IsNullOrEmpty(r)).Cast<string>()];
      }

      var rawEntriesSplit = fullPath.Split(new[] { " > " }, System.StringSplitOptions.None);
      var entries = rawEntriesSplit.Select(s => s?.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToArray();

      // Build dialog window with wrapping buttons and a scrollable text area for tag details
      var win = new Window
      {
        Title = "Path",
        Owner = this,
        // Allow the user to resize the dialog; we'll size it initially but not force SizeToContent
        SizeToContent = SizeToContent.Manual,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Content = null,
        MaxWidth = this.ActualWidth * 0.9,
        Width = Math.Max(400, this.ActualWidth * 0.6),
        Height = Math.Max(300, this.ActualHeight * 0.5)
      };

      // Use a Grid so the details text can stretch and stay anchored to the bottom
      var outerGrid = new Grid { Margin = new Thickness(10) };
      outerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
      outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

      var wrap = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2) };

      // Text box to display tag details (scrollable)
      var detailsText = new TextBox
      {
        Text = string.Empty,
        IsReadOnly = true,
        TextWrapping = TextWrapping.Wrap,
        AcceptsReturn = true,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        Margin = new Thickness(2),
        MinWidth = 300
      };
      // allow detailsText to stretch
      detailsText.VerticalAlignment = VerticalAlignment.Stretch;
      detailsText.HorizontalAlignment = HorizontalAlignment.Stretch;

      for (int i = 0; i < entries.Length; i++)
      {
        var entry = entries[i]!;
        var btn = new Button { Content = entry, Tag = entry, Margin = new Thickness(2) };
        var localIndex = i;
        btn.Click += (_, __) =>
        {
          // When a path button is clicked, show the tag information related to that path entry in the details textbox
          string details;
          if (perEntryTags != null && localIndex < perEntryTags.Length)
          {
            details = perEntryTags[localIndex];
          }
          else
          {
            // Fallback: show the full frame tag content
            details = fullPath;
          }
          detailsText.Text = $"Selected: {entry}\r\n\r\nTag:\r\n{details}";
        };
        wrap.Children.Add(btn);

        if (i < entries.Length - 1)
        {
          var sep = new TextBlock { Text = " > ", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2) };
          wrap.Children.Add(sep);
        }
      }
      // Add controls to grid: wrap on top row, detailsText on bottom row
      Grid.SetRow(wrap, 0);
      outerGrid.Children.Add(wrap);

      Grid.SetRow(detailsText, 1);
      outerGrid.Children.Add(detailsText);

      win.Content = outerGrid;
      win.ShowDialog();
    }

    private void EveRoot_MouseLeave(object sender, MouseEventArgs e)
    {
      if (sender is Frame frame)
      {
        frame.BorderBrush = new SolidColorBrush(Colors.Black);
      }
    }

    private void EveRoot_MouseEnter(object sender, MouseEventArgs e)
    {
      if (sender is Frame frame)
      {
        frame.BorderBrush = new SolidColorBrush(Colors.Red);

        string? path = null;
        if (frame.Tag is ValueTuple<string, string[]> t)
        {
          path = t.Item1;
        }
        else if (frame.Tag is string s)
        {
          path = s;
        }

        if (path != null)
        {
          // display the full path
          Path.Text = path;
          Path.Width = this.ActualWidth / 3;

          // If the mouse is on the left of the screen
          var mousePosition = Mouse.GetPosition(this);
          if (mousePosition.X < this.ActualWidth / 2)
          {
            // Locate the Path Label to the right half of the screen
            Path.HorizontalAlignment = HorizontalAlignment.Right;
          }
          else
          {
            Path.HorizontalAlignment = HorizontalAlignment.Left;
          }
        }
      }
    }
  }
}
