using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Windows.Data;
using System.Linq;
using System.Diagnostics;

namespace Commander
{

  public class CharacterListViewModel
  {
    public ObservableCollection<CommanderCharacter> Characters { get; set; }

    public ICollectionView CharactersView { get; set; }

    public CharacterListViewModel()
    {
      Characters = new ObservableCollection<CommanderCharacter>(GameClientCache.GetAllCharacters().Where(c => !string.IsNullOrWhiteSpace(c.Name)).ToList());
      CharactersView = CollectionViewSource.GetDefaultView(Characters);
      Characters.CollectionChanged += (s, e) =>
      {
        // Refresh the view when the collection changes
        CharactersView.Refresh();
      };
    }
  }

  public partial class CharacterWindow : Window
  {
    private CharacterListViewModel _vm;

    public CharacterWindow()
    {
      InitializeComponent();

      _vm = new CharacterListViewModel();
      this.DataContext = _vm;

      // populate location filter options
      var locations = _vm.Characters.Select(c => c.Location).Distinct().OrderBy(l => l).ToList();
      LocationFilterCombo.Items.Add(new ComboBoxItem() { Content = "All", IsSelected = true });
      foreach (var loc in locations)
      {
        LocationFilterCombo.Items.Add(new ComboBoxItem() { Content = loc });
      }

      //foreach (var item in _vm.CharactersView)
      //{
      //  CharacterRow row = new()
      //  {
      //    DataContext = item,
      //  };
      //  row.SelectionChanged += CharacterList_SelectionChanged;
      //  characterList.Children.Add(row);
      //}
    }

    private void PluginButton_Click(object sender, RoutedEventArgs e)
    {
      // Open PluginSelector as a modal
      if (sender is not Button button) return;
      if (button.DataContext is not CommanderCharacter character) return;

      PluginSelector pluginSelector = new()
      {
        Owner = this,
        WindowStartupLocation = WindowStartupLocation.CenterOwner
      };

      pluginSelector.SetPlugins(character.Plugins);
      pluginSelector.ShowDialog();
      character.OnPropertyChanged("EnabledPluginDescription");
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
      Close();
    }

    // Sorting handlers
    private void SortByIsOnline_Click(object sender, RoutedEventArgs e)
    {
      _vm.CharactersView.SortDescriptions.Clear();
      _vm.CharactersView.SortDescriptions.Add(new SortDescription(nameof(CommanderCharacter.IsOnline), ListSortDirection.Descending));
    }

    private void SortByLocation_Click(object sender, RoutedEventArgs e)
    {
      _vm.CharactersView.SortDescriptions.Clear();
      _vm.CharactersView.SortDescriptions.Add(new SortDescription(nameof(CommanderCharacter.Location), ListSortDirection.Ascending));
    }

    private void SortByName_Click(object sender, RoutedEventArgs e)
    {
      _vm.CharactersView.SortDescriptions.Clear();
      _vm.CharactersView.SortDescriptions.Add(new SortDescription(nameof(CommanderCharacter.Name), ListSortDirection.Ascending));
    }

    private void SortByPlugins_Click(object sender, RoutedEventArgs e)
    {
      _vm.CharactersView.SortDescriptions.Clear();
      _vm.CharactersView.SortDescriptions.Add(new SortDescription(nameof(CommanderCharacter.EnabledPluginDescription), ListSortDirection.Descending));
    }

    // Filter handlers
    private void IsOnlineFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (_vm == null) return;
      var selected = (IsOnlineFilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
      // Keep existing location filter if set
      var locationSelected = (LocationFilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();

      _vm.CharactersView.Filter = item =>
      {
        if (item is not CommanderCharacter c) return false;
        if (selected == "Online" && !c.IsOnline) return false;
        if (selected == "Offline" && c.IsOnline) return false;
        if (locationSelected != null && locationSelected != "All" && c.Location != locationSelected) return false;
        return true; // Matches both filters
      };
    }

    private void LocationFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (_vm == null) return;
      var selected = (LocationFilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
      // Keep existing online filter if set
      var onlineSelected = (IsOnlineFilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();

      _vm.CharactersView.Filter = item =>
      {
        if (item is not CommanderCharacter c) return false;
        if (onlineSelected == "Online" && !c.IsOnline) return false;
        if (onlineSelected == "Offline" && c.IsOnline) return false;
        if (selected != null && selected != "All" && c.Location != selected) return false;
        return true; // Matches both filters
      };
    }

    // Select All shown / Clear selection
    private void SelectAllShown_Click(object sender, RoutedEventArgs e)
    {
      if (_vm == null) return;

      // Clear all selection flags first
      foreach (var ch in _vm.Characters)
      {
        ch.IsSelected = false;
      }

      // Mark every item currently visible in the CharactersView as selected (data-backed)
      foreach (var obj in _vm.CharactersView)
      {
        if (obj is CommanderCharacter c)
        {
          c.IsSelected = true;
        }
      }

      ApplyPluginsButton.IsEnabled = _vm.Characters.Any(ch => ch.IsSelected);
    }

    private void ClearSelection_Click(object sender, RoutedEventArgs e)
    {
      if (_vm == null) return;

      // Clear selection by updating the data model
      foreach (var ch in _vm.Characters)
      {
        ch.IsSelected = false;
      }

      ApplyPluginsButton.IsEnabled = false;
    }

    // Enable/disable the Apply button depending on selection and keep model/UI in sync
    private void CharacterList_SelectionChanged(object? sender, EventArgs e)
    {
      //if (e != null)
      //{
      //  foreach (var added in e.AddedItems)
      //  {
      //    if (added is CommanderCharacter ch)
      //    {
      //      ch.IsSelected = true;
      //      var listBoxItem = (ListBoxItem)characterList.ItemContainerGenerator.ContainerFromItem(ch);
      //      if (listBoxItem != null)
      //      {
      //        listBoxItem.IsSelected = true;
      //      }
      //    }
      //  }

      //  foreach (var removed in e.RemovedItems)
      //  {
      //    if (removed is CommanderCharacter ch)
      //    {
      //      ch.IsSelected = false;
      //      var listBoxItem = (ListBoxItem)characterList.ItemContainerGenerator.ContainerFromItem(ch);
      //      if (listBoxItem != null)
      //      {
      //        listBoxItem.IsSelected = false;
      //      }
      //    }
      //  }

      //  _vm.Characters.ToList().ForEach(ch =>
      //  {
      //    var listBoxItem = (ListBoxItem)characterList.ItemContainerGenerator.ContainerFromItem(ch);
      //    if (listBoxItem != null)
      //    {
      //      listBoxItem.IsSelected = ch.IsSelected;
      //      characterList.SelectedItems.Remove(listBoxItem);
      //    }
      //  });

      //}

      ApplyPluginsButton.IsEnabled = (_vm?.Characters?.Any(ch => ch.IsSelected) ?? false);
    }
  }
}
