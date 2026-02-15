# GitHub Copilot Instructions for EveCommander

## Project Overview

**EveCommander** is a WPF-based **read-only intelligence gathering and monitoring tool** for EVE Online clients. It reads game memory to parse the UI state, provides a plugin system for information collection and analysis, and monitors multiple game clients simultaneously.

### ⚠️ CRITICAL: EULA Compliance
**EveCommander is strictly read-only with respect to the EVE Online game client.**
- **NO automation**: No clicking, no input simulation, no bot-like behavior
- **NO game interaction**: Only reading and parsing UI state
- **Information gathering only**: Monitoring, intelligence collection, and analysis
- All code must maintain read-only access to game memory
- Any functionality that could violate EVE Online's EULA is prohibited

### Technology Stack
- **Target Framework**: .NET 9.0
- **C# Version**: 13.0
- **UI Framework**: WPF (Windows Presentation Foundation)
- **Key Libraries**:
  - NHotkey.Wpf for global hotkey management
  - Entity Framework Core 9.0 for data persistence
  - Custom memory reading library (`read-memory-64-bit`)

### Repository Structure
- **Commander**: Main WPF application (entry point)
- **BotLib**: Core library with base classes and utilities (legacy name, no actual "bot" functionality)
- **BotLibPlugins**: Plugin implementations for intelligence gathering and monitoring
- **SDEdotNet**: EVE Online Static Data Export (SDE) entities
- **AssemblyLineLib**: Assembly line and industry functionality
- **Sanderling** (external): Memory reading and UI parsing libraries
  - `read-memory-64-bit`: Native memory reading
  - `eve-parse-ui`: EVE UI tree parsing

---

## Architecture & Design Patterns

### Plugin System
The project uses a **reflection-based plugin architecture** with three plugin types:

1. **IBotLibPlugin** (abstract class): 
   - Character-specific plugins that can be enabled/disabled per character
   - Must implement `DoWork(ParsedUserInterface, GameClient, IEnumerable<IBotLibPlugin>)` → `Task<PluginResult>`
   - Constructor signature: `(string characterName, long windowID)`
   - Examples: `GridScout`, `LocalIntel`, `KeepstarWatcher`

2. **IAutoRunPlugin** (abstract class):
   - Plugins that auto-run when client is detected (use with caution)
   - Must implement `DoWork(ParsedUserInterface, GameClient, bool isRunning, IEnumerable<IBotLibPlugin>)`
   - Runs immediately, cannot be stopped with hotkey

3. **IGlobalPlugin** (abstract class):
   - Global plugins that operate across all clients
   - Must implement `DoWork(bool isRunning, IEnumerable<GameClient>, IEnumerable<IBotLibPlugin>)`

**Plugin Loading**:
- Plugins are loaded from `Plugins/` directory via reflection at startup
- DLLs must contain "Plugin" in filename
- Located in `CommanderMain.LoadPluginDlls()` method

### Settings Management
Plugins can define settings using the **BotLibSetting attribute**:

```csharp
[BotLibSetting(SettingType = BotLibSetting.Type.SingleLineText, Description = "Description here")]
public string MySetting;
```

Supported types: `SingleLineText`, `MultiLineText`, `Integer`, `Decimal`

Settings are:
- Stored in `PluginSettingsCache` (singleton pattern)
- Managed through `SettingsWindow.xaml.cs`
- Discovered via reflection on plugin fields

### UI Element Wrappers
The codebase wraps EVE UI nodes in a convenient pattern:
- `UITreeNodeWithDisplayRegion` → `UIElement` (via `ToUIElement()` extension)
- `UIElement` provides display region info for screen coordinates
- Pattern: Check `HasNoVisibleRegion()` before interacting

### Bot Helper Class
`EveBot` class provides high-level game state queries (note: "Bot" is a legacy name - this class is for read-only information gathering):
- `IsDisconnected()`, `IsDocked()`, `IsInWarp()`, etc.
- `CurrentSystemName()`, `CurrentSystemSecStatus()`
- Access to game UI elements: `GetOverviewEntries()`, `GetLocalCharacters()`, etc.

---

## Code Style & Conventions

### General C# Style
- **Nullable reference types**: Enabled (`<Nullable>enable</Nullable>`)
- **Implicit usings**: Enabled (`<ImplicitUsings>enable</ImplicitUsings>`)
- **Modern C# features**: Use C# 13.0 features (collection expressions, primary constructors, etc.)

### Naming Conventions
- **Private fields**: Use `_camelCase` prefix (e.g., `_isRunning`, `_commanderClient`)
- **Public properties**: PascalCase (e.g., `CharacterName`, `WindowID`)
- **Constants**: UPPER_SNAKE_CASE (e.g., `ONE_ROUND_TIME`, `DISCONNECT_STRING`)
- **Local variables**: camelCase
- **Methods**: PascalCase with descriptive names

### WPF Patterns
- **Code-behind files**: Keep minimal, focus on UI interaction logic
- **Event handlers**: Format as `ElementName_EventName` (e.g., `OkButton_Click`, `EveRoot_MouseEnter`)
- **XAML naming**: Use `x:Name` for elements that need code-behind access
- **DataContext**: Use ViewModels where appropriate (see `CharacterListViewModel`)

### Async/Await
- **Always use async/await**: All I/O operations must be asynchronous
- **Task returns**: Methods that perform work return `Task` or `Task<T>`
- **Platform-specific**: Mark Windows-specific code with `[SupportedOSPlatform("windows5.0")]`

### Comments
- **Comments are encouraged**: Well-commented code is easier to maintain and understand
- **XML documentation**: Use for all public APIs, methods, and complex functionality
- **Inline comments**: Use to explain intent, algorithm choices, and important details
- **TODO comments**: Format as `// TODO: description` to track future work
- **Header comments**: Consider adding file/class headers explaining purpose and responsibilities
- **Complex logic**: Always comment non-obvious algorithms, workarounds, or EVE-specific behaviors

### Error Handling
- **Exceptions**: Catch specific exceptions, avoid catching `Exception` unless necessary
- **Error reporting**: Return `PluginResult` with error state for plugin failures
- **Logging**: Use `Debug.WriteLine()` for diagnostic output

---

## Plugin Development Guidelines

### Creating a New Plugin

1. **Inherit from appropriate base class**:
   ```csharp
   internal class MyPlugin(string characterName, long windowID) 
       : IBotLibPlugin(characterName, windowID)
   ```

2. **Implement required members**:
   ```csharp
   public override string Name => "My Plugin";
   
   [SupportedOSPlatform("windows5.0")]
   public override async Task<PluginResult> DoWork(
       ParsedUserInterface uiRoot, 
       GameClient gameClient, 
       IEnumerable<IBotLibPlugin> allPlugins)
   {
       var bot = new EveBot(uiRoot);
       
       // Check game state
       if (bot.IsInSessionChange() || bot.IsInWarp())
           return true; // Wait
           
       // Perform work...
       
       return new PluginResult
       {
           WorkDone = true,
           Message = "Status message",
           Background = Color.Green,
           Foreground = Color.White
       };
   }
   ```

3. **Add settings if needed**:
   ```csharp
   [BotLibSetting(SettingType = BotLibSetting.Type.SingleLineText, 
                  Description = "Target system name")]
   public string? TargetSystem;
   ```

4. **Use `IsCompleted` flag**: Set `_IsCompleted = true` when plugin finishes its task

### Plugin Best Practices
- **Check game state first**: Always verify session state, warp state, docked state
- **Return quickly**: Avoid long-running operations (main loop is ~3 seconds)
- **Communicate via PluginResult**: Use Message, Background, Foreground for user feedback
- **Coordinate between characters**: Use `allPlugins` parameter to check other characters' state
- **Handle missing UI elements**: Always check for null before accessing parsed UI nodes

---

## Working with EVE UI Parsing

### UI Tree Navigation
The `ParsedUserInterface` object contains the entire EVE UI tree:
- `uiRoot.UiTree`: Root node of UI hierarchy
- `uiRoot.MessageBoxes`: Message/notification boxes
- `uiRoot.StationWindow`: Docking station UI
- `uiRoot.ShipUI`: Ship HUD and controls
- `uiRoot.InfoPanelContainer`: Location, route, and system info
- `uiRoot.OverviewWindow`: Space overview windows

### Common UI Access Patterns
```csharp
// Check for specific window
if (uiRoot.StationWindow != null)
{
    // Docked
}

// Get overview entries
var overviews = bot.GetAllOverviewWindows();
var entries = overviews.First().Entries;

// Get bookmarks
var bookmarks = bot.GetStandaloneBookmarks()
    .SelectMany(UIParser.GetAllContainedDisplayTextsWithRegion)
    .Where(b => b.Text.StartsWith('#'))
    .ToList();

// Get local pilot list
var locals = bot.GetLocalCharacters();
```

### Display Regions
When working with UI elements that have screen positions:
- `TotalDisplayRegion`: Absolute screen coordinates
- `TotalDisplayRegionVisible`: Visible portion (accounting for scrolling/clipping)
- Use `Margin = new Thickness(region.X, region.Y, 0, 0)` for WPF positioning

---

## Commander Application Specifics

### Main Loop
- **Entry point**: `CommanderMain.StartAsync()`
- **Update frequency**: ~3 seconds per round (`ONE_ROUND_TIME`)
- **Hotkeys**: 
  - `Ctrl+F8`: Stop monitoring
  - `Backslash`: Cycle through clients

### Character Management
- **Storage**: `GameClientCache.GetAllCharacters()` (static cache)
- **Online state**: Updated via `UpdateCharacterOnlineStates()`
- **Selection**: Characters can be selected in `CharacterWindow` for bulk operations
- **Plugins per character**: Stored in `CommanderCharacter.Plugins` property

### Client Discovery
- **Process scanning**: Finds EVE client processes
- **UI root address**: Discovered via `FindUIRootAddress()` in `EveClient.xaml.cs`
- **Cached clients**: `GameClientCache` maintains discovered clients

### UI Components
- **EveClient**: User control representing one EVE client
- **CharacterRow**: Display for character info in lists
- **PluginSelector**: Modal for choosing enabled plugins
- **SettingsWindow**: Plugin settings configuration UI
- **VisualiseUI**: Debug tool for viewing parsed UI tree

---

## Testing & Debugging

### Visual UI Debugger
The `VisualiseUI` window allows inspection of the parsed UI tree:
- Click frames to see full path and properties
- Hover to highlight elements
- Used for understanding UI structure when developing new features

### Debug Output
Use `Debug.WriteLine()` for diagnostic output visible in Visual Studio Output window

### Error Handling in Main Loop
The main loop catches exceptions per client and displays errors in the UI:
- Red background with exception message
- 15-second cooldown before retry

---

## Memory Management

### Unsafe Code
- `BotLib` allows unsafe blocks (`<AllowUnsafeBlocks>True</AllowUnsafeBlocks>`)
- Used for memory reading operations in `read-memory-64-bit` library

### Long-Running Tasks
- Avoid creating many Task instances
- Reuse `EveBot` instances where possible
- Be mindful of closure captures in async methods

---

## Dependencies & Project References

### Internal Dependencies
```
Commander → BotLib, SDEdotNet, AssemblyLineLib
BotLib → eve-parse-ui (Sanderling)
eve-parse-ui → read-memory-64-bit (Sanderling)
BotLibPlugins → BotLib
```

### External NuGet Packages
- **NHotkey.Wpf** 3.0.0: Global hotkeys
- **Microsoft.EntityFrameworkCore.Tools** 9.0.7: EF Core tooling

### Sanderling Libraries
The project references the `Sanderling` repository (forked):
- Located in a sibling directory: `../Sanderling/`
- Contains memory reading and EVE UI parsing logic
- **Fork maintained**: origin: `https://github.com/LakMoore/Sanderling`, upstream: `https://github.com/Arcitectus/Sanderling`
- Modifications are allowed but should be made carefully
- Keep changes minimal and well-documented to facilitate upstream merges
- Consider whether changes should be contributed back to upstream

---

## Git Workflow & Conventions

### Active Repositories
1. **EveCommander**: `https://github.com/LakMoore/EveCommander` (origin)
2. **Sanderling**: 
   - origin: `https://github.com/LakMoore/Sanderling`
   - upstream: `https://github.com/Arcitectus/Sanderling`

### Commit Messages
Use conventional commits style where appropriate, but prioritize clarity

### Branch Strategy
Working on `main` branch for both repositories

---

## Common Tasks & Patterns

### Adding a New Plugin
1. Create class in `BotLibPlugins` project
2. Inherit from `IBotLibPlugin`
3. Use primary constructor: `(string characterName, long windowID)`
4. Implement `Name` and `DoWork` method
5. Build solution - plugin will be auto-discovered

### Adding a Setting to Existing Plugin
1. Add public field to plugin class
2. Decorate with `[BotLibSetting(...)]` attribute
3. Setting will appear automatically in Settings window

### Accessing Game State
1. Create `EveBot` instance from `ParsedUserInterface`
2. Use convenience methods: `IsInWarp()`, `IsDocked()`, etc.
3. For custom queries, access `_UI` (ParsedUserInterface) directly

### Reading Game UI Information
1. Find UI element using `bot.Get...()` methods or manual traversal
2. Convert to `UIElement` using `.ToUIElement()`
3. Check `HasNoVisibleRegion()` to verify element is visible
4. Extract text, positions, or other display information for analysis

---

## What NOT to Do

❌ **Don't implement any game automation features** - read-only access only  
❌ **Don't add input simulation or clicking functionality** - violates EVE EULA  
❌ **Don't block the main UI thread** - always use async/await  
❌ **Don't use `Thread.Sleep()`** - use `await Task.Delay()` instead  
❌ **Don't catch generic `Exception`** unless absolutely necessary  
❌ **Don't create UI elements in loops** without proper disposal/cleanup  
❌ **Don't access UI elements from background threads** - use `Dispatcher` when needed  
❌ **Don't hardcode paths** - use `Path.Combine()` and `AppDomain.CurrentDomain.BaseDirectory`

---

## Quick Reference: Key Methods

### EveBot Common Methods
```csharp
bool IsDisconnected()
bool IsDocked()
bool IsInWarp()
bool IsCloaked()
bool IsTethered()
bool IsInSessionChange()
string CurrentSystemName()
double CurrentSystemSecStatus()
IEnumerable<OverviewEntry> GetOverviewEntries()
IEnumerable<OverviewWindow> GetAllOverviewWindows()
Module? GetCloakModule()
IEnumerable<Pilot> GetLocalCharacters()
```

### Plugin Lifecycle
```csharp
DoWork() → called every ~3 seconds while monitoring is active
IsEnabled → get/set to enable/disable plugin
IsCompleted → plugin sets to true when finished
```

### PluginResult Properties
```csharp
bool WorkDone
string Message
Color Background
Color Foreground
```

---

## Additional Context

### EVE Online Game Specifics
- **Security status**: 0.0-1.0 scale (high-sec to null-sec)
- **Wormholes**: System connections with codes (e.g., "J121006")
- **Tethering**: Safety mechanic near structures
- **Cloaking**: Invisibility module
- **Overview**: Main tactical display in EVE

### Performance Considerations
- Main loop runs every 3 seconds (`ONE_ROUND_TIME`)
- UI parsing can be slow (~500ms to several seconds)
- Multiple clients running simultaneously
- Memory footprint is significant due to game memory reading

---

## Questions to Consider When Coding

1. **Is this plugin character-specific or global?** → Choose appropriate base class
2. **Does this need settings?** → Add `BotLibSetting` attributes
3. **What game states should I handle?** → Check session change, warp, docked states
4. **Am I blocking the UI?** → Use async/await
5. **Does this coordinate between characters?** → Check `allPlugins` parameter
6. **Is the UI element visible?** → Use `HasNoVisibleRegion()`
7. **Should this auto-run?** → Be very cautious with `IAutoRunPlugin`

---

## Getting Help

When working with this codebase:
1. **Check existing plugins** for patterns and examples
2. **Use VisualiseUI** to understand EVE UI structure
3. **Look at EveBot methods** for common game state queries
4. **Review PluginResult usage** in existing plugins for status reporting
5. **Examine CharacterWindow and PluginSelector** for UI patterns

---

*This document should be updated when significant architectural changes occur or new patterns emerge.*
