using eve_parse_ui;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.Versioning;
using WindowsInput;

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
      return _UI.InfoPanelContainer.Value?.InfoPanelRoute?.RouteElementMarkers?.Count > 0;
    }

    public UIElement? AutopilotMenuButton()
    {
      return _UI.InfoPanelContainer.Value?.InfoPanelRoute?.AutopilotMenuButton.ToUIElement();
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
      if (_UI.PlanetsWindow.Value?.Colonies?.Count == 0)
        return null;

      var currentColony = GetSelectedColony();
      if (currentColony == null)
        return _UI.PlanetsWindow.Value?.Colonies?[0];

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

    public bool IsWaitingForNumericInput()
    {
      return _UI.InputModal.Value != null && _UI.InputModal.Value.InputType == InputModal.Type.Numeric;
    }

    public bool IsWaitingForTextInput()
    {
      return _UI.InputModal.Value != null && _UI.InputModal.Value.InputType == InputModal.Type.Text;
    }

    public UIElement? PITransferButton()
    {
      return _UI.PlanetaryImportExportUI.Value?.TransferButton.ToUIElement();
    }

    public bool IsPrimaryInventoryVisible()
    {
      var mainInventory = _UI.InventoryWindows.Value.FirstOrDefault();
      return mainInventory != null && mainInventory.WindowCaption == "Inventory";
    }

    public InventoryWindowLeftTreeEntry? GetPIHoldInventoryEntry()
    {
      return _UI.InventoryWindows.Value
          .SelectMany(i => i
              .LeftTreePanel
              .Entries
              .SelectMany(lte => lte.Children)
              .Where(child => child.Text == "Planetary Commodities Hold")
          )
          .FirstOrDefault();
    }

    public InventoryWindowLeftTreeEntry? GetFleetHangarEntry()
    {
      return _UI.InventoryWindows.Value
          .SelectMany(i => i
              .LeftTreePanel
              .Entries
              .SelectMany(lte => lte.Children)
              .Where(child => child.Text == "Fleet Hangar")
          )
          .FirstOrDefault();
    }

    public IEnumerable<InventoryWindow> InventoryWindows()
    {
      return _UI.InventoryWindows.Value;
    }

    public UIElement? AutopilotNextWaypoint()
    {
      var routeMarkers = _UI.InfoPanelContainer.Value?.InfoPanelRoute?.RouteElementMarkers;
      if (routeMarkers == null || routeMarkers.Count == 0)
        return null;
      return routeMarkers[0].UiNode.ToUIElement();
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

    public UIElement? LogoutButton()
    {
      return _UI.Neocom.Value?.PanelCommands?.FirstOrDefault(cmd =>
          cmd.Text.Equals("Log off", StringComparison.CurrentCultureIgnoreCase) == true
      )?
      .UiNode
      .ToUIElement();
    }

    public UIElement? EveMenuButton()
    {
      return _UI.Neocom.Value?.EveMenuButton.ToUIElement();
    }

    public IReadOnlyList<Colony> GetAllColonies()
    {
      return _UI.PlanetsWindow.Value?.Colonies ?? [];
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

    readonly ColorComponents AutoPilotRouteColor = new()
    {
      R = 94,
      G = 100,
      B = 27,
      A = 200
    };

    public IEnumerable<InfoWindow> GetInfoWindows()
    {
      return _UI.InfoWindows.Value;
    }

    public bool OnAutoPilotRoute(OverviewWindowEntry e)
    {
      if (e != null)
      {
        var icon = e.UiNode.GetDescendantsByType("Sprite")
          .FirstOrDefault(u =>
            u.GetNameFromDictEntries()?.Equals("iconSprite", StringComparison.OrdinalIgnoreCase) == true
          );
        return UIParser.GetColorPercentFromDictEntries(icon) == AutoPilotRouteColor;
      }
      return false;
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

    public MarketOrdersWindow.Tab? MarketOrdersTab()
    {
      return _UI.MarketOrdersWindow.Value?.CurrentTab;
    }

    public object GetLocalCharacters()
    {
        throw new NotImplementedException();
    }
  }
}
