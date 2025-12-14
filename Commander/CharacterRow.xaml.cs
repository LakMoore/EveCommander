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

    }

    private void Selected_Click(object sender, RoutedEventArgs e)
    {

    }
  }
}
