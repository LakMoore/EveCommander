using eve_parse_ui;
using System.Drawing;
using System.Text.RegularExpressions;

namespace BotLib
{
  public class EveBot(ParsedUserInterface UI)
  {
    protected readonly ParsedUserInterface _UI = UI;
    private readonly string DISCONNECT_STRING = "Connection lost";

    public bool IsDisconnected() =>
        _UI.MessageBoxes.Value.Select(m => m.TextHeadline).Any(m => m?.Equals(DISCONNECT_STRING, StringComparison.CurrentCultureIgnoreCase) == true);

    public bool IsDocked() =>
        _UI.StationWindow.Value != null;

    public string CurrentSystemName() =>
        _UI.InfoPanelContainer.Value?.InfoPanelLocationInfo?.CurrentSolarSystemName ?? "Unknown System";

    public double CurrentSystemSecStatus() =>
        (double)(_UI.InfoPanelContainer.Value?.InfoPanelLocationInfo?.SecurityStatusPercent ?? 0) / 100.0;

    public Color CurrentSystemSecStatusColor()
    {
      var converter = new ColorConverter();
      var colorCode = _UI.InfoPanelContainer.Value?.InfoPanelLocationInfo?.SecurityStatusColor ?? "#FF000000";
      var color = converter.ConvertFromString(colorCode);
      if (color == null)
        return Color.White;
      return (Color)color;
    }

    public string? DockedInStationName() =>
        _UI.InfoPanelContainer.Value?.InfoPanelLocationInfo?.ExpandedContent?.CurrentStationName;

    public bool IsAutopilotDestinationSet()
    {
      return _UI.InfoPanelContainer.Value?.InfoPanelRoute?.RouteElementMarkers.Count() > 0;
    }

    public bool IsAutopilotRouteVisible()
    {
      return _UI.InfoPanelContainer.Value?.InfoPanelRoute?.IsExpanded == true;
    }

    public bool IsInSessionChange()
    {
      return _UI.SessionTimeIndicator.Value != null;
    }

    public bool IsInWarp()
    {
      return _UI.ShipUI.Value?.Indication?.ManeuverType == ShipManeuverType.ManeuverWarp;
    }

    public bool IsDocking()
    {
      return _UI.ShipUI.Value?.Indication?.ManeuverType == ShipManeuverType.ManeuverDock;
    }

    public bool IsAligning()
    {
      return _UI.ShipUI.Value?.Indication?.ManeuverType == ShipManeuverType.ManeuverAlign;
    }

    public bool IsApproaching()
    {
      return _UI.ShipUI.Value?.Indication?.ManeuverType == ShipManeuverType.ManeuverApproach;
    }

    public bool IsJumping()
    {
      return _UI.ShipUI.Value?.Indication?.ManeuverType == ShipManeuverType.ManeuverJump;
    }

    public bool IsPlanetsWindowOpen()
    {
      return _UI.PlanetsWindow.Value != null;
    }

    public Colony? GetSelectedColony()
    {
      return _UI.PlanetsWindow.Value?.Colonies.FirstOrDefault(c => c.IsSelected);
    }

    public Colony? GetNextColony()
    {
      if (_UI.PlanetsWindow.Value?.Colonies?.Count() == 0)
        return null;

      var currentColony = GetSelectedColony();
      if (currentColony == null)
        return _UI.PlanetsWindow.Value?.Colonies?.FirstOrDefault();

      return _UI.PlanetsWindow.Value?.Colonies?
          .SkipWhile(c => c != currentColony)
          .Skip(1)
          .FirstOrDefault();
    }

    public bool IsPIExportWindowOpen()
    {
      return _UI.PlanetaryImportExportUI.Value != null;
    }

    public PlanetaryImportExportUI? PIExportWindow()
    {
      return _UI.PlanetaryImportExportUI.Value;
    }

    public bool IsUndocking()
    {
      return _UI.StationWindow.Value?.AbortUndockButton != null;
    }

    public CustomsOfficeList? GetSpaceportList()
    {
      return _UI.PlanetaryImportExportUI.Value?.SpaceportList;
    }

    public CustomsOfficeList? GetCustomsList()
    {
      return _UI.PlanetaryImportExportUI.Value?.CustomsList;
    }

    public InventoryWindow? getPrimaryInventoryWindow()
    {
      return _UI.InventoryWindows.Value
        .FirstOrDefault(iw => iw.UiNode.GetNameFromDictEntries() == "InventoryStation");

    }

    public bool IsPrimaryInventoryVisible()
    {
      return getPrimaryInventoryWindow() != null;
    }

    public InventoryWindowLeftTreeEntry? GetPIHoldInventoryEntry()
    {
      return getPrimaryInventoryWindow()?
        .LeftTreePanel
        .Entries
        .SelectMany(lte => lte.Children)  // PI Hold is nested inside the cargo bay
        .FirstOrDefault(child => child.Text == "Planetary Commodities Hold");
    }

    public InventoryWindowLeftTreeEntry? GetFleetHangarEntry()
    {
      return getPrimaryInventoryWindow()?
        .LeftTreePanel
        .Entries
        .SelectMany(lte => lte.Children)  // Fleet Hangar is nested inside the cargo bay
        .FirstOrDefault(child => child.Text == "Fleet Hangar");
    }

    public InventoryWindowLeftTreeEntry? GetCargoBay()
    {
      return getPrimaryInventoryWindow()?
        .LeftTreePanel
        .Entries
        .FirstOrDefault(entry => entry.UiNode.GetNameFromDictEntries() == "ShipHangar");
    }

    public IEnumerable<InventoryWindow> InventoryWindows()
    {
      return _UI.InventoryWindows.Value;
    }

    public UIElement? AutopilotNextWaypoint()
    {
      var routeMarkers = _UI.InfoPanelContainer.Value?.InfoPanelRoute?.RouteElementMarkers;
      if (routeMarkers == null || !routeMarkers.Any())
        return null;
      return routeMarkers.First().UiNode.ToUIElement();
    }

    public IEnumerable<InfoPanelRouteElementMarker> AutopilotRoute()
    {
      return _UI.InfoPanelContainer.Value?.InfoPanelRoute?.RouteElementMarkers ?? [];
    }

    public IEnumerable<ContextMenuEntry> ContextMenuEntries()
    {
      return _UI.ContextMenus.Value
          .SelectMany(menu => menu.Entries)
          .Where(entry => entry != null);
    }

    public IEnumerable<ListWindow> ListWindows()
    {
      return _UI.ListWindows.Value;
    }

    public ExpandedUtilMenu? UtilMenu()
    {
      return _UI.ExpandedUtilMenu.Value;
    }

    public bool IsOutsideView()
    {
      var button = _UI.StationWindow.Value?.DockedModeButton;
      if (button == null)
        return false;

      return UIParser.GetAllContainedDisplayTexts(button)
          .Any(t =>
              t.Contains("inside", StringComparison.OrdinalIgnoreCase)
          );
    }

    public bool CharacterSelectionScreenVisible()
    {
      return _UI.CharacterSelectionScreen.Value != null;
    }

    public IEnumerable<CharacterSlot> CharacterSelectionSlots()
    {
      return _UI.CharacterSelectionScreen.Value?.CharacterSlots ?? [];
    }

    public bool DoesCharacterSelectionScreenShowAlphaStatus()
    {
      return _UI.CharacterSelectionScreen.Value?.AccountIsAlpha == true;
    }

    public IEnumerable<Colony>? GetAllColonies()
    {
      return _UI.PlanetsWindow.Value?.Colonies;
    }

    public IReadOnlyList<UIElement> GetStandaloneBookmarks()
    {
      return _UI.StandaloneBookmarkWindow.Value?.Entries
          .Select(e => e.ToUIElement())
          .Where(e => e != null)
          .Cast<UIElement>()
          .ToList() ?? [];
    }

    public int GetShipSpeed()
    {
      return _UI.ShipUI.Value?.CurrentSpeed ?? 0;
    }

    public IEnumerable<OverviewWindowEntry> GetOverviewEntries()
    {
      return _UI.OverviewWindows.Value
          .SelectMany(window => window.Entries) ?? [];
    }

    public IEnumerable<OverviewWindowEntry> GetUniqueOverviewEntriesByNameAndType()
    {
      return _UI.OverviewWindows.Value
          .SelectMany(window => window.Entries)
          .DistinctBy(entry => new { entry.ObjectType, entry.ObjectName }) ?? [];
    }

    public IEnumerable<OverviewWindowEntry> GetNonFriendlyPilotsOnGrid()
    {
      // Get all overview entries
      var entries = GetOverviewEntries();

      // Filter to only non-friendly entries with standing hints
      return entries.Where(entry =>
      {
        if (string.IsNullOrEmpty(entry.FlagStateHint))
          return false;

        // If it has a standing hint but it's not friendly, it's non-friendly
        return !IsFriendlyStandingHint(entry.FlagStateHint);
      });
    }

    public bool IsFriendlyStandingHint(string? standingHint)
    {
      if (string.IsNullOrWhiteSpace(standingHint))
      {
        return false;
      }

      string normalizedHint = standingHint.Trim().ToLower();

      // Check for friendly standing patterns
      return Regex.IsMatch(normalizedHint, 
        @"in your fleet|in your gang|in your capsuleer corporation|in your corporation|in your alliance|good standing|excellent standing",
        RegexOptions.IgnoreCase);
    }

    // TODO: get this from the SDE
    private readonly List<int> cloakIDs = [11370, 11577, 11578, 14234, 14776,
      14778, 14780, 14782, 15790, 16126, 20561, 20563, 20565, 32260];

    public ShipUIModuleButton? GetCloakModule()
    {
      return _UI.ShipUI.Value?.ModuleButtons?.FirstOrDefault(mb => cloakIDs.Contains(mb.TypeID ?? -1));
    }

    public bool IsCloaked()
    {
      var cloakModule = GetCloakModule();

      if (cloakModule == null)
        return false;

      return cloakModule.IsActive == true;
    }

    public List<string> DefensiveBuffs()
    {
      return _UI.ShipUI.Value?.DefensiveBuffs ?? [];
    }

    public bool IsTethered()
    {
      return DefensiveBuffs().Contains("tethering");
    }

    public Hitpoints? GetShipHitpoints()
    {
      return _UI.ShipUI.Value?.HitpointsPercent;
    }

    public IEnumerable<InfoWindow> GetInfoWindows()
    {
      return _UI.InfoWindows.Value;
    }

    public bool HasInvuln()
    {
      return _UI.ShipUI.Value?.IsInvulnerable == true;
    }

    public IEnumerable<OverviewWindow> GetAllOverviewWindows()
    {
      return _UI.OverviewWindows.Value;
    }

    public bool IsPersonalAssetsWindowOpen()
    {
      return _UI.AssetsWindow.Value != null;
    }

    public IEnumerable<AssetLocation> PersonalAssetLocations()
    {
      return _UI.AssetsWindow.Value?.AssetLocations ?? [];
    }

    public ProbeScannerWindow? GetProbeScannerWindow()
    {
      return _UI.ProbeScannerWindow.Value;
    }

    public string SelectedMarketItemName()
    {
      return _UI.RegionalMarketWindow.Value?.SelectedItemName ?? string.Empty;
    }

    public UIElement? MarketSearchField()
    {
      return _UI.RegionalMarketWindow.Value?.SearchField.ToUIElement();
    }

    public string MarketSearchFieldText()
    {
      var searchField = MarketSearchField();
      if (searchField == null)
      {
        return string.Empty;
      }
      return UIParser.GetAllContainedDisplayTexts(searchField).Aggregate((a, b) => a + b) ?? string.Empty;
    }

    public UIElement? FindMarketSearchResult(string itemName)
    {
      return _UI.RegionalMarketWindow.Value?
        .SearchResults?
        .FirstOrDefault(result => result.Text.Equals(itemName, StringComparison.CurrentCultureIgnoreCase) == true)?
        .Region?.ToUIElement() ?? null;
    }

    public string BuyMarketActionWindowType()
    {
      return _UI.BuyMarketActionWindow.Value?.TypeName ?? string.Empty;
    }

    public string ModifyMarketActionWindowType()
    {
      return _UI.ModifyMarketActionWindow.Value?.TypeName ?? string.Empty;
    }

    public string BuyMarketActionLocationName()
    {
      return _UI.BuyMarketActionWindow.Value?.LocationLink != null ?
          UIParser.GetAllContainedDisplayTexts(_UI.BuyMarketActionWindow.Value.LocationLink).Aggregate((a, b) => a + b) ?? string.Empty
          : string.Empty;
    }

    public string ModalTitle()
    {
      return _UI.InputModal.Value?.Title ?? string.Empty;
    }

    public IEnumerable<MarketOrder>? MarketBuyOrders()
    {
      return _UI.RegionalMarketWindow.Value?.Buyers;
    }

    public IEnumerable<MarketOrder>? MarketSellOrders()
    {
      return _UI.RegionalMarketWindow.Value?.Sellers;
    }

    public bool IsMarkeOrdersOpen()
    {
      return _UI.MarketOrdersWindow.Value != null;
    }

    public UIElement? ModifyMarketActionWindow()
    {
      return _UI.ModifyMarketActionWindow.Value?.UiNode.ToUIElement();
    }

    public IEnumerable<MessageBox> MessageBoxes()
    {
      return _UI.MessageBoxes.Value;
    }
    public MarketOrdersWindow? MarketOrders()
    {
      return _UI.MarketOrdersWindow.Value;
    }

    public MarketOrdersWindow.Tab? MarketOrdersTab()
    {
      return _UI.MarketOrdersWindow.Value?.CurrentTab;
    }

    public ChatWindow? GetLocalChatWindow()
    {
      return _UI.ChatWindows.Value
          .FirstOrDefault(chat => chat.Name?.Equals("Local", StringComparison.OrdinalIgnoreCase) == true);
    }

    public IEnumerable<ChatUserEntry> GetLocalCharacters()
    {
      var localChat = GetLocalChatWindow();

      if (localChat == null)
      {
        return [];
      }

      return localChat.Userlist?.VisibleUsers ?? [];
    }

    public (int? manufacturing, int? science, int? reaction) GetAvailableSlots()
    {
      var manufacturing = _UI.IndustryWindow.Value?.ManufacturingSlotsAvailable;
      var science = _UI.IndustryWindow.Value?.ScienceSlotsAvailable;
      var reaction = _UI.IndustryWindow.Value?.ReactionSlotsAvailable;
      return (manufacturing, science, reaction);
    }

    public UIElement? IndustrySearchTextbox()
    {
      return _UI.IndustryWindow.Value?.SearchTextbox?.ToUIElement();
    }

    public string IndustrySearchTextboxValue()
    {
      return UIParser.GetAllContainedDisplayTexts(
        _UI.IndustryWindow.Value?.SearchTextbox?.GetDescendantsByName("textLabel").FirstOrDefault()
      ).FirstOrDefault() ?? string.Empty;
    }

    public IEnumerable<BlueprintEntry> GetBlueprintEntries()
    {
      return _UI.IndustryWindow.Value?.AvailableBlueprintEntries ?? [];
    }

    public IndustryWindow? GetIndustryWindow()
    {
      return _UI.IndustryWindow.Value;
    }

    public IndustryCombo? GetIndustryInputLocation()
    {
      return _UI.IndustryWindow.Value?.InventoryInputContainer;
    }

    public IndustryCombo? GetIndustryOutputLocation()
    {
      return _UI.IndustryWindow.Value?.InventoryOutputContainer;
    }

    public IEnumerable<ComboBoxEntry> GetComboBoxEntries()
    {
      return _UI.ComboBoxEntries.Value;
    }

    public UIElement? getIndustryRunCount()
    {
      return _UI.IndustryWindow.Value?.Runs.ToUIElement();
    }

    public bool IndustryMissingSkills()
    {
      return _UI.IndustryWindow.Value?.MissingSkills == true;
    }

    public IndustryTab? BlueprintIndustryTab()
    {
      return _UI.IndustryWindow.Value?.BlueprintTab;
    }

    public IndustryCombo? IndustryBlueprintCombo()
    {
      return _UI.IndustryWindow.Value?.BlueprintCombo;
    }

    public int IndustryDurationInMinutes()
    {
      return _UI.IndustryWindow.Value?.DurationInMinutes ?? 0;
    }

    public int? IndustryOutputQuantity()
    {
      return _UI.IndustryWindow.Value?.OutputQuantity;
    }

    public IndustryTab? JobsIndustryTab()
    {
      return _UI.IndustryWindow.Value?.JobsTab;
    }
  }
}
