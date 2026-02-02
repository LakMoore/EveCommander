using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;

namespace Commander
{
    public partial class CharacterWindow : Window
    {
        // Called when the Apply Plugins button is clicked.
        // Show a single PluginSelector to choose plugin names and apply those selections
        // across all selected characters.
        private void ApplyPlugins_Click(object? sender, RoutedEventArgs e)
        {
            var selectedCharacters = GetSelectedCharacters().ToList();
            if (!selectedCharacters.Any())
            {
                MessageBox.Show(this, "No characters selected.", "Apply Plugins", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Build union of plugin names across selected characters
            var allPluginNames = selectedCharacters
                .SelectMany(c => c.Plugins.Select(p => p.Name))
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            // If there are no plugin names available, give a more specific message
            if (!allPluginNames.Any())
            {
                MessageBox.Show(this, "No active game clients found or no plugins currently loaded.", "Apply Plugins", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Determine initial selection: pre-select a plugin if it is enabled on all selected characters
            var initiallySelected = allPluginNames
                .Where(name => selectedCharacters.All(c => c.Plugins.Any(p => p.Name == name && p.IsEnabled)))
                .ToList();

            var pluginSelector = new PluginSelector
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            pluginSelector.SetPluginsForMultiple(allPluginNames, initiallySelected);

            var result = pluginSelector.ShowDialog();
            if (result != true) return;

            var selectedNames = pluginSelector.GetSelectedPluginNames().ToHashSet();

            // Apply selection: for each character, set IsEnabled on plugins whose names were selected.
            foreach (var character in selectedCharacters)
            {
                foreach (var plugin in character.Plugins)
                {
                    plugin.IsEnabled = selectedNames.Contains(plugin.Name);
                }

                character.OnPropertyChanged(nameof(CommanderCharacter.EnabledPluginDescription));
            }
        }

        // Helper to enumerate selected CommanderCharacter instances
        private IEnumerable<CommanderCharacter> GetSelectedCharacters()
        {
            foreach (var item in _vm.Characters.Where(ch => ch.IsSelected))
            {
                if (item is CommanderCharacter c)
                {
                    yield return c;
                }
            }
        }
    }
}