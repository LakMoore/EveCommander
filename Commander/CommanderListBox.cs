using System.Collections;
using System.Windows.Controls;

namespace Commander
{
  class CommanderListBox : ListBox
  {

    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
      base.OnSelectionChanged(e);
    }

    public void SetSelectedItem(CommanderCharacter item, bool IsSelected)
    {
      IEnumerable<CommanderCharacter> items = [.. this.SelectedItems.Cast<CommanderCharacter>()];
      item.IsSelected = IsSelected;
      if (IsSelected)
      {
        if (items.Contains(item)) return;
        items = items.Append(item).Where(x => x.IsSelected).Distinct();
        this.UnselectAll();
        this.SetSelectedItems(items);
      }
      else
      {
        this.UnselectAll();
        this.SelectedItems.Clear();
        this.SetSelectedItems(items
          .Where(x => x != item && x.IsSelected).Distinct()
          );
      }
    }
  }
}
