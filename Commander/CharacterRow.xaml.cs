using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Commander
{
  /// <summary>
  /// Interaction logic for CharacterRow.xaml
  /// </summary>
  public partial class CharacterRow : UserControl
  {
    public event EventHandler? SelectionChanged;

    public CharacterRow()
    {
      InitializeComponent();
    }

    private void IsSelectedCheckBox_Click(object sender, RoutedEventArgs e)
    {
      // update IsSelected in _vm
      if (sender is CheckBox checkBox)
      {
        if (checkBox.DataContext is CommanderCharacter ch)
        {
          ch.IsSelected = checkBox.IsChecked ?? false;
          SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
      }
    }

    private void StackPanel_Click(object sender, MouseButtonEventArgs e)
    {
      if (sender is StackPanel panel)
      {
        CheckBox? checkBox = panel.Children.OfType<CheckBox>().Where(chkbox => chkbox.Name == "Selected").FirstOrDefault();
        if (checkBox != null)
        {
          checkBox.IsChecked = !checkBox.IsChecked;
          SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
      }
    }

    private void PluginButton_Click(object sender, RoutedEventArgs e)
    {
      // Open PluginSelector as a modal for this character
      if (sender is not Button button) return;
      if (button.DataContext is not CommanderCharacter character) return;

      // Find the parent CharacterWindow to set as owner
      Window? parentWindow = Window.GetWindow(this);

      PluginSelector pluginSelector = new()
      {
        Owner = parentWindow,
        WindowStartupLocation = WindowStartupLocation.CenterOwner
      };

      pluginSelector.SetPlugins(character.Plugins);
      var result = pluginSelector.ShowDialog();

      // Only apply changes if OK was clicked
      if (result == true)
      {
        var selectedNames = pluginSelector.GetSelectedPluginNames().ToHashSet();

        // Apply selection to this character's plugins
        foreach (var plugin in character.Plugins)
        {
          plugin.IsEnabled = selectedNames.Contains(plugin.Name);
        }

        character.OnPropertyChanged(nameof(CommanderCharacter.EnabledPluginDescription));
      }
    }
  }
}
