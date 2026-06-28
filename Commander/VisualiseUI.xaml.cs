using eve_parse_ui;
using System.Windows;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Text.Json;
using System.Globalization;

namespace Commander
{
  /// <summary>
  /// Interaction logic for VisualiseUI.xaml
  /// </summary>
  public partial class VisualiseUI : Window
  {
    private UITreeNodeNoDisplayRegion? _selectedNode;

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

      foreach (var child in node.Children ?? [])
      {
        child.Parent = node;
      }

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
            .Select(de => de.Key + " = " + FormatDictEntryValue(de.Value));

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
                .Select(de => de.Key + " = " + FormatDictEntryValue(de.Value));
            var nOther = n.otherDictEntriesKeys ?? Enumerable.Empty<string>();
            var combined = string.Join("\n", new[] { nDescription }.Concat(nDict).Concat(nOther));
            return combined;
          }).ToArray();
        }

        var fullPath = path;
        var ancestorsArray = ancestors?.ToArray() ?? Array.Empty<UITreeNodeNoDisplayRegion>();
        frame.Tag = (fullPath, perEntryTags, ancestorsArray);
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

      // Support both legacy string tag and new (fullPath, perEntryTags, ancestors) tuple
      string fullPath = string.Empty;
      string[] perEntryTags = Array.Empty<string>();
      UITreeNodeNoDisplayRegion[] ancestors = Array.Empty<UITreeNodeNoDisplayRegion>();

      if (frame.Tag is ValueTuple<string, string[], UITreeNodeNoDisplayRegion[]> tuple)
      {
        fullPath = tuple.Item1 ?? string.Empty;
        perEntryTags = tuple.Item2 ?? Array.Empty<string>();
        ancestors = tuple.Item3 ?? Array.Empty<UITreeNodeNoDisplayRegion>();
      }
      else if (frame.Tag is ValueTuple<string, string[]> oldTuple)
      {
        fullPath = oldTuple.Item1 ?? string.Empty;
        perEntryTags = oldTuple.Item2 ?? Array.Empty<string>();
      }
      else if (frame.Tag is string s)
      {
        fullPath = s;
        var lines = s.Split(new[] { '\n' }, System.StringSplitOptions.None);
        var pathLine = lines.Length > 0 ? lines[0] : string.Empty;
        var rawEntries = pathLine.Split(new[] { " > " }, System.StringSplitOptions.None);
        perEntryTags = [.. rawEntries.Select(r => r?.Trim()).Where(r => !string.IsNullOrEmpty(r)).Cast<string>()];
      }

      var rawEntriesSplit = fullPath.Split(new[] { " > " }, System.StringSplitOptions.None);
      var entries = rawEntriesSplit.Select(s => s?.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToArray();
      _selectedNode = ancestors.LastOrDefault();

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

      var exportButton = new Button { Content = "Export", Margin = new Thickness(2), Padding = new Thickness(12, 4, 12, 4), HorizontalAlignment = HorizontalAlignment.Right };
      var wrap = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2) };
      var topRow = new DockPanel { LastChildFill = true };
      DockPanel.SetDock(exportButton, Dock.Right);
      topRow.Children.Add(exportButton);
      topRow.Children.Add(wrap);

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

      // Helper to remove everything after a specific button
      void RemoveElementsAfterButton(Button button)
      {
        var buttonIndex = wrap.Children.IndexOf(button);
        if (buttonIndex >= 0)
        {
          // Remove all elements after this button
          var childrenToRemove = wrap.Children.Cast<UIElement>().Skip(buttonIndex + 1).ToList();
          foreach (var child in childrenToRemove)
          {
            wrap.Children.Remove(child);
          }
        }
      }

      exportButton.Click += (_, __) =>
      {
        if (_selectedNode == null)
        {
          System.Windows.MessageBox.Show(this, "Select a UI element before exporting.", "Export", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
          return;
        }

        var json = JsonSerializer.Serialize(CreateExportDocument(_selectedNode), new JsonSerializerOptions { WriteIndented = true });
        ShowJsonWindow(json);
      };

      // Helper to add separator before adding new element
      void AddSeparator()
      {
        if (wrap.Children.Count > 0)
        {
          var sep = new TextBlock { Text = " > ", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2) };
          wrap.Children.Add(sep);
        }
      }

      // Recursive helper to add a button for a node and setup children navigation
      void AddNodeButton(UITreeNodeNoDisplayRegion node, string nodeDesc, string nodeTag)
      {
        AddSeparator();
        var nodeBtn = new Button { Content = nodeDesc, Margin = new Thickness(2) };

        nodeBtn.Click += (_, __) =>
        {
          _selectedNode = node;
          detailsText.Text = $"Selected: {nodeDesc}\r\n\r\nTag:\r\n{nodeTag}";
          RemoveElementsAfterButton(nodeBtn);

          // If this node has children, show ComboBox
          var children = node.Children?.ToList();
          if (children != null && children.Any())
          {
            AddSeparator();
            var childCombo = new ComboBox { Margin = new Thickness(2), MinWidth = 150 };

            foreach (var child in children)
            {
              var childType = child.pythonObjectTypeName;
              var childName = child.GetNameFromDictEntries();
              var childDesc = childType + (childName != null ? " [" + childName + "]" : string.Empty);
              childCombo.Items.Add(new ComboBoxItem { Content = childDesc, Tag = child });
            }

            childCombo.SelectionChanged += (_, __) =>
            {
              if (childCombo.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is UITreeNodeNoDisplayRegion selectedChild)
              {
                _selectedNode = selectedChild;
                // Remove the combobox (and its preceding separator)
                var comboIndex = wrap.Children.IndexOf(childCombo);
                if (comboIndex > 0 && wrap.Children[comboIndex - 1] is TextBlock)
                {
                  wrap.Children.RemoveAt(comboIndex - 1); // Remove separator
                }
                wrap.Children.Remove(childCombo);

                // Build tag for this child
                var childType = selectedChild.pythonObjectTypeName;
                var childName = selectedChild.GetNameFromDictEntries();
                var childDesc = childType + (childName != null ? " [" + childName + "]" : string.Empty);
                var childDict = (selectedChild.dictEntriesOfInterest ?? Enumerable.Empty<KeyValuePair<string, object>>())
                    .Select(de => de.Key + " = " + FormatDictEntryValue(de.Value));
                var childOther = selectedChild.otherDictEntriesKeys ?? Enumerable.Empty<string>();
                var childTag = string.Join("\n", new[] { childDesc }.Concat(childDict).Concat(childOther));

                // Recursively add button for this child
                AddNodeButton(selectedChild, childDesc, childTag);

                // Auto-click to continue navigation
                var addedBtn = wrap.Children.OfType<Button>().LastOrDefault();
                if (addedBtn != null)
                {
                  addedBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
              }
            };

            wrap.Children.Add(childCombo);
          }
        };

        wrap.Children.Add(nodeBtn);
      }

      // Helper to rebuild path from button click
      void RebuildPathFromIndex(int clickedIndex, Button clickedButton)
      {
        // Clear everything after the clicked button
        RemoveElementsAfterButton(clickedButton);

        _selectedNode = ancestors[clickedIndex];

        // Show details for clicked node
        string details;
        if (perEntryTags != null && clickedIndex < perEntryTags.Length)
        {
          details = perEntryTags[clickedIndex];
        }
        else
        {
          details = fullPath;
        }
        detailsText.Text = $"Selected: {entries[clickedIndex]}\r\n\r\nTag:\r\n{details}";

        // If this node has children, add ComboBox
        if (ancestors != null && clickedIndex < ancestors.Length)
        {
          var node = ancestors[clickedIndex];
          var children = node.Children?.ToList();
          if (children != null && children.Any())
          {
            AddSeparator();

            var combo = new ComboBox { Margin = new Thickness(2), MinWidth = 150 };
            foreach (var child in children)
            {
              var childType = child.pythonObjectTypeName;
              var childName = child.GetNameFromDictEntries();
              var childDesc = childType + (childName != null ? " [" + childName + "]" : string.Empty);
              combo.Items.Add(new ComboBoxItem { Content = childDesc, Tag = child });
            }

            combo.SelectionChanged += (_, __) =>
            {
              if (combo.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is UITreeNodeNoDisplayRegion selectedNode)
              {
                // Remove the combobox (and its preceding separator)
                var comboIndex = wrap.Children.IndexOf(combo);
                if (comboIndex > 0 && wrap.Children[comboIndex - 1] is TextBlock)
                {
                  wrap.Children.RemoveAt(comboIndex - 1); // Remove separator
                }
                wrap.Children.Remove(combo);

                // Build tag for this child
                var childType = selectedNode.pythonObjectTypeName;
                var childName = selectedNode.GetNameFromDictEntries();
                var childDesc = childType + (childName != null ? " [" + childName + "]" : string.Empty);
                var childDict = (selectedNode.dictEntriesOfInterest ?? Enumerable.Empty<KeyValuePair<string, object>>())
                    .Select(de => de.Key + " = " + FormatDictEntryValue(de.Value));
                var childOther = selectedNode.otherDictEntriesKeys ?? Enumerable.Empty<string>();
                var childTag = string.Join("\n", new[] { childDesc }.Concat(childDict).Concat(childOther));

                // Use recursive helper to add button and setup children
                AddNodeButton(selectedNode, childDesc, childTag);

                // Auto-click to continue navigation
                var addedBtn = wrap.Children.OfType<Button>().LastOrDefault();
                if (addedBtn != null)
                {
                  addedBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
              }
            };

            wrap.Children.Add(combo);
          }
        }
      }

      // Build initial path buttons
      for (int i = 0; i < entries.Length; i++)
      {
        var entry = entries[i]!;
        var btn = new Button { Content = entry, Tag = i, Margin = new Thickness(2) };
        var localIndex = i;
        btn.Click += (_, __) =>
        {
          RebuildPathFromIndex(localIndex, btn);
        };
        wrap.Children.Add(btn);

        if (i < entries.Length - 1)
        {
          var sep = new TextBlock { Text = " > ", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2) };
          wrap.Children.Add(sep);
        }
      }
      // Add controls to grid: wrap on top row, detailsText on bottom row
      Grid.SetRow(topRow, 0);
      outerGrid.Children.Add(topRow);

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
        if (frame.Tag is ValueTuple<string, string[], UITreeNodeNoDisplayRegion[]> t)
        {
          path = t.Item1;
        }
        else if (frame.Tag is ValueTuple<string, string[]> oldT)
        {
          path = oldT.Item1;
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

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
      if (_selectedNode == null)
      {
        System.Windows.MessageBox.Show(this, "Select a UI element before exporting.", "Export", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        return;
      }

      var json = JsonSerializer.Serialize(CreateExportDocument(_selectedNode), new JsonSerializerOptions { WriteIndented = true });
      ShowJsonWindow(json);
    }

    private static ExportUiNode CreateExportDocument(UITreeNodeNoDisplayRegion selectedNode)
    {
      var ancestors = new List<UITreeNodeNoDisplayRegion>();
      for (var current = selectedNode; current != null; current = current.Parent)
      {
        ancestors.Add(current);
      }

      ancestors.Reverse();
      return CreatePrunedExportNode(ancestors, 0);
    }

    private static ExportUiNode CreatePrunedExportNode(IReadOnlyList<UITreeNodeNoDisplayRegion> ancestors, int index)
    {
      var node = ancestors[index];
      var children = new List<ExportUiNode>();

      if (index + 1 < ancestors.Count)
      {
        children.Add(CreatePrunedExportNode(ancestors, index + 1));
      }
      else
      {
        children.AddRange((node.Children ?? []).Select(CreateFullExportNode));
      }

      return CreateExportNode(node, children);
    }

    private static ExportUiNode CreateFullExportNode(UITreeNodeNoDisplayRegion node)
    {
      return CreateExportNode(node, (node.Children ?? []).Select(CreateFullExportNode).ToList());
    }

    private static ExportUiNode CreateExportNode(UITreeNodeNoDisplayRegion node, List<ExportUiNode> children)
    {
        var dictEntries = (node.dictEntriesOfInterest ?? Enumerable.Empty<KeyValuePair<string, object>>())
          .ToDictionary(item => item.Key, item => FormatDictEntryValue(item.Value));

      return new ExportUiNode
      {
        PythonObjectTypeName = node.pythonObjectTypeName,
        Name = node.GetNameFromDictEntries(),
        DictEntriesOfInterest = dictEntries,
        Children = children
      };
    }

    private void ShowJsonWindow(string json)
    {
      var jsonWindow = new Window
      {
        Title = "Export JSON",
        Owner = this,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Width = Math.Max(600, this.ActualWidth * 0.75),
        Height = Math.Max(400, this.ActualHeight * 0.75)
      };

      jsonWindow.Content = new TextBox
      {
        Text = json,
        IsReadOnly = true,
        TextWrapping = TextWrapping.Wrap,
        AcceptsReturn = true,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        FontFamily = new FontFamily("Consolas"),
        Margin = new Thickness(10)
      };

      jsonWindow.ShowDialog();
    }

    private static string? FormatDictEntryValue(object? value)
    {
      if (value == null)
        return null;

      return value switch
      {
        double doubleValue => doubleValue.ToString("G17", CultureInfo.InvariantCulture),
        float floatValue => floatValue.ToString("G9", CultureInfo.InvariantCulture),
        decimal decimalValue => decimalValue.ToString(CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
      };
    }

    private sealed record ExportUiNode
    {
      public required string PythonObjectTypeName { get; init; }
      public string? Name { get; init; }
      public Dictionary<string, string?> DictEntriesOfInterest { get; init; } = new();
      public List<ExportUiNode> Children { get; init; } = [];
    }
  }
}
