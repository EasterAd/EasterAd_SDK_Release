using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using EasterAd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// ReSharper disable once RedundantUsingDirective
using System.IO;

namespace EasterAd_Editor.Menu
{
    public class EasterAd : EditorWindow
    {
        private enum RenderPipelineType
        {
            BuiltIn,
            URP,
            HDRP,
            Unknown
        }

        private enum EasterAdWindowTab
        {
            Dashboard,
            Settings,
            Diagnostics
        }

        private enum EasterAdDashboardComponent
        {
            CreateInventory,
            ActiveInventory,
            ArchiveInventory,
            DashboardConnection,
            DashboardProject,
            ManagedScenes,
            SdkSettings,
            DiagnosticsStatus,
            RenderPipelineMigration
        }

        private bool _easterAdEnabled;
        private string _tempGameId = "";
        private string _tempSdkKey = "";
        private bool _tempLogEnable;

        private bool _customInfoEnable;
        private DeviceType _customDeviceType;
        private RuntimePlatform _customPlatform;
        private SystemLanguage _customLanguage;

        private string _currentGameId = "";
        private string _currentSdkKey = "";

        private DeviceType _currentcustomDeviceType;
        private RuntimePlatform _currentcustomPlatform;
        private SystemLanguage _currentcustomLanguage;

        private Vector2 _scrollPosition = Vector2.zero;
        private string _newPlacementAdUnitId = "";
        private string _newInventoryName = "New Inventory";

        private const string PlaneItemPrefabPath = "Packages/com.easterad.easterad/Runtime/Prefabs/PlaneItem.prefab";
        private const string DashboardStyleSheetPath = "Packages/com.easterad.easterad/Editor/Menu/EasterAdDashboard.uss";
        private const string DashboardApiKeyPrefsKey = "EasterAd.Dashboard.ApiKey";
        private const string DashboardBaseUrlPrefsKey = "EasterAd.Dashboard.BaseUrl";
        private const string DashboardOrganizationIdPrefsKey = "EasterAd.Dashboard.OrganizationId";
        private const string DashboardGameIdPrefsKey = "EasterAd.Dashboard.GameId";
        private const string DashboardMenuLayoutPrefsKey = "EasterAd.Dashboard.MenuLayout";
        private const string InventoryFolderKey = "inventory";
        private const string SettingsFolderKey = "settings";
        private const string DiagnosticsFolderKey = "diagnostics";
        private const string MenuSettingsButtonId = "menu-settings";
        private const string ManagedSceneRegistryPath = "ProjectSettings/EasterAdSceneRegistry.json";
        private const int DashboardAutoSyncFailureLimit = 5;
        private const int DashboardMenuRowHeight = 48;

        private string _dashboardBaseUrl = EasterAdDashboardClient.DefaultBaseUrl;
        private string _dashboardApiKey = "";
        private string _dashboardOrganizationId = "";
        private string _dashboardGameId = "";
        private string _dashboardStatus = "";
        private bool _dashboardUpdateBusy;
        private bool _dashboardActionBusy;
        private List<EasterAdDashboardOrganization> _dashboardOrganizations = new List<EasterAdDashboardOrganization>();
        private List<EasterAdDashboardGame> _dashboardGames = new List<EasterAdDashboardGame>();
        private List<EasterAdDashboardAdUnit> _dashboardAdUnits = new List<EasterAdDashboardAdUnit>();
        private int _dashboardOrganizationIndex;
        private int _dashboardGameIndex;
        private int _dashboardAdUnitIndex;
        private string _activeFolderKey = InventoryFolderKey;
        private VisualElement _contentRoot;
        private Label _sectionTitleLabel;
        private Label _sectionSubtitleLabel;
        private Label _dashboardStatusPill;
        private Label _gameSummaryLabel;
        private Label _placementSummaryLabel;
        private int _dashboardAutoSyncFailureCount;
        private bool _dashboardAutoSyncPaused;
        private string _dashboardAutoSyncError = "";
        private List<string> _managedScenePaths = new List<string>();
        private readonly List<Button> _tabButtons = new List<Button>();
        private readonly List<DashboardFolderDefinition> _dashboardFolders = new List<DashboardFolderDefinition>();
        private string _expandedInventorySettingsKey = "";
        private bool _showMenuSettings;

        [Serializable]
        private sealed class ManagedSceneRegistry
        {
            public List<string> managedScenes = new List<string>();
        }

        private sealed class ManagedPlacementReference
        {
            public string ScenePath = "";
            public string ObjectName = "";
            public string AdUnitId = "";
            public Item? Item;
        }

        private sealed class ManagedSceneScanResult
        {
            public List<ManagedPlacementReference> Placements = new List<ManagedPlacementReference>();
            public List<string> RemovedScenePaths = new List<string>();
        }

        private sealed class ManagedSceneConflict
        {
            public string AdUnitId = "";
            public List<ManagedPlacementReference> Placements = new List<ManagedPlacementReference>();
        }

        private sealed class DashboardFolderDefinition
        {
            public DashboardFolderDefinition(string key, string title, string subtitle, params EasterAdDashboardComponent[] components)
            {
                Key = key;
                Title = title;
                Subtitle = subtitle;
                Components.AddRange(components);
            }

            public string Key { get; }
            public string Title { get; set; }
            public string Subtitle { get; }
            public List<EasterAdDashboardComponent> Components { get; } = new List<EasterAdDashboardComponent>();
        }

        private sealed class DashboardComponentDefinition
        {
            public DashboardComponentDefinition(EasterAdDashboardComponent id, string title, Action<VisualElement> build)
            {
                Id = id;
                Title = title;
                Build = build;
            }

            public EasterAdDashboardComponent Id { get; }
            public string Title { get; }
            public Action<VisualElement> Build { get; }
        }

        private sealed class DashboardMenuRowData
        {
            public DashboardMenuRowData(string folderKey)
            {
                FolderKey = folderKey;
                IsComponent = false;
            }

            public DashboardMenuRowData(string folderKey, EasterAdDashboardComponent component)
            {
                FolderKey = folderKey;
                Component = component;
                IsComponent = true;
            }

            public string FolderKey { get; }
            public EasterAdDashboardComponent Component { get; }
            public bool IsComponent { get; }
        }

        [Serializable]
        private sealed class DashboardMenuLayout
        {
            public List<DashboardMenuFolderState> folders = new List<DashboardMenuFolderState>();
        }

        [Serializable]
        private sealed class DashboardMenuFolderState
        {
            public string key = "";
            public string title = "";
            public List<string> components = new List<string>();
        }

        // Add menu item named "My Window" to the Window menu
        [MenuItem("Window/EasterAd")]
        public static void ShowWindow()
        {
            //Show existing window instance. If one doesn't exist, make one.
            EasterAd window = (EasterAd)GetWindow(typeof(EasterAd));
            window.titleContent = new GUIContent("EasterAd");
            window.minSize = new Vector2(960, 620);
            window.Show();
        }

        void OnEnable()
        {
            Selection.selectionChanged -= RefreshUi;
            Selection.selectionChanged += RefreshUi;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            LoadDashboardPreferences();
            LoadManagedSceneRegistry();
            RequestDashboardUpdate();

            if (!EasterAdConfigFiles.TryReadConfig(out string[] config, out string filepath)) { return; }
            if (filepath == EasterAdConfigFiles.LegacyConfigPath)
            {
                Debug.LogWarning("[EasterAd] Legacy ETA_Config.txt detected. Save the EasterAd settings to migrate to EasterAd_Config.txt.");
            }

            if (config.Length < 5)
            {
                Debug.LogWarning("[EasterAd] Config file is incomplete. Open the EasterAd window and save settings again.");
                return;
            }

            _easterAdEnabled = bool.TryParse(config[0], out bool easterAdEnabled) && easterAdEnabled;
            _tempGameId = config[1];
            _tempSdkKey = config[2];
            _tempLogEnable = bool.TryParse(config[3], out bool logEnable) && logEnable;

            _customInfoEnable = bool.TryParse(config[4], out bool customInfoEnable) && customInfoEnable;
            if (_customInfoEnable)
            {
                if (config.Length < 8 ||
                    !Enum.TryParse(config[5], out _customDeviceType) ||
                    !Enum.TryParse(config[6], out _customPlatform) ||
                    !Enum.TryParse(config[7], out _customLanguage))
                {
                    Debug.LogWarning("[EasterAd] Custom config values are invalid. Custom device info is disabled.");
                    _customInfoEnable = false;
                }
            }

            _currentGameId = _tempGameId;
            _currentSdkKey = _tempSdkKey;
            _currentcustomDeviceType = _customDeviceType;
            _currentcustomPlatform = _customPlatform;
            _currentcustomLanguage = _customLanguage;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= RefreshUi;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private static string MaskSecret(string value)
        {
            return string.IsNullOrEmpty(value) ? "" : "********";
        }

        private void CreateGUI()
        {
            BuildWindow();
        }

        private static DashboardFolderDefinition[] CreateDefaultDashboardFolders()
        {
            return new[]
            {
                new DashboardFolderDefinition(
                    InventoryFolderKey,
                    "Inventory",
                    "Manage dashboard inventory and scene placements.",
                    EasterAdDashboardComponent.CreateInventory,
                    EasterAdDashboardComponent.ActiveInventory,
                    EasterAdDashboardComponent.ArchiveInventory),
                new DashboardFolderDefinition(
                    SettingsFolderKey,
                    "Settings",
                    "Configure dashboard access, project scope, managed scenes, and SDK values.",
                    EasterAdDashboardComponent.DashboardConnection,
                    EasterAdDashboardComponent.DashboardProject,
                    EasterAdDashboardComponent.ManagedScenes,
                    EasterAdDashboardComponent.SdkSettings),
                new DashboardFolderDefinition(
                    DiagnosticsFolderKey,
                    "Diagnostics",
                    "Inspect package, render pipeline, migration, and scene placement state.",
                    EasterAdDashboardComponent.DiagnosticsStatus,
                    EasterAdDashboardComponent.RenderPipelineMigration)
            };
        }

        private DashboardComponentDefinition CreateDashboardComponent(EasterAdDashboardComponent component)
        {
            switch (component)
            {
                case EasterAdDashboardComponent.CreateInventory:
                    return new DashboardComponentDefinition(component, "Create New Inventory", BuildCreateInventoryComponent);
                case EasterAdDashboardComponent.ActiveInventory:
                    return new DashboardComponentDefinition(component, "Active Inventory", parent => BuildInventoryListComponent(parent, "Active Inventory", true));
                case EasterAdDashboardComponent.ArchiveInventory:
                    return new DashboardComponentDefinition(component, "Archive", parent => BuildInventoryListComponent(parent, "Archive", false));
                case EasterAdDashboardComponent.DashboardConnection:
                    return new DashboardComponentDefinition(component, "Dashboard Connection", parent => parent.Add(CreateDashboardConnectionPanel()));
                case EasterAdDashboardComponent.DashboardProject:
                    return new DashboardComponentDefinition(component, "Project Settings", parent => parent.Add(CreateDashboardProjectPanel()));
                case EasterAdDashboardComponent.ManagedScenes:
                    return new DashboardComponentDefinition(component, "Managed Scenes", parent => parent.Add(CreateManagedScenesPanel()));
                case EasterAdDashboardComponent.SdkSettings:
                    return new DashboardComponentDefinition(component, "SDK Settings", BuildSdkSettingsComponent);
                case EasterAdDashboardComponent.DiagnosticsStatus:
                    return new DashboardComponentDefinition(component, "Status", BuildDiagnosticsStatusComponent);
                case EasterAdDashboardComponent.RenderPipelineMigration:
                    return new DashboardComponentDefinition(component, "Render Pipeline and Migration", BuildRenderPipelineMigrationComponent);
                default:
                    return new DashboardComponentDefinition(component, component.ToString(), _ => { });
            }
        }

        private static EasterAdDashboardComponent[] GetDashboardComponentValues()
        {
            Array values = Enum.GetValues(typeof(EasterAdDashboardComponent));
            EasterAdDashboardComponent[] components = new EasterAdDashboardComponent[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                components[i] = (EasterAdDashboardComponent)values.GetValue(i);
            }

            return components;
        }

        private static bool DashboardFolderContainsAny(DashboardFolderDefinition folder, params EasterAdDashboardComponent[] components)
        {
            for (int i = 0; i < components.Length; i++)
            {
                if (folder.Components.Contains(components[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private DashboardFolderDefinition GetActiveDashboardFolder()
        {
            for (int i = 0; i < _dashboardFolders.Count; i++)
            {
                if (_dashboardFolders[i].Key == _activeFolderKey)
                {
                    return _dashboardFolders[i];
                }
            }

            return GetFirstDashboardFolder();
        }

        private DashboardFolderDefinition GetFirstDashboardFolder()
        {
            if (_dashboardFolders.Count > 0)
            {
                return _dashboardFolders[0];
            }

            return new DashboardFolderDefinition("empty", "Dashboard", "Add components to folders in Menu Settings.");
        }

        private bool HasDashboardFolder(string key)
        {
            for (int i = 0; i < _dashboardFolders.Count; i++)
            {
                if (_dashboardFolders[i].Key == key)
                {
                    return true;
                }
            }

            return false;
        }

        private void BuildWindow()
        {
            rootVisualElement.Clear();
            _tabButtons.Clear();

            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(DashboardStyleSheetPath);
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }

            rootVisualElement.AddToClassList("ea-root");

            VisualElement body = new VisualElement();
            body.AddToClassList("ea-body");

            VisualElement sidebar = new VisualElement();
            sidebar.AddToClassList("ea-sidebar");
            VisualElement brand = new VisualElement();
            brand.AddToClassList("ea-brand");
            brand.Add(CreateLabel("EasterAd", "ea-brand-title"));
            brand.Add(CreateLabel("Unity SDK", "ea-brand-subtitle"));
            sidebar.Add(brand);
            sidebar.Add(CreateLabel("Folders", "ea-sidebar-group"));
            for (int i = 0; i < _dashboardFolders.Count; i++)
            {
                sidebar.Add(CreateFolderButton(_dashboardFolders[i].Title, _dashboardFolders[i].Key));
            }

            VisualElement sidebarSpacer = new VisualElement();
            sidebarSpacer.AddToClassList("ea-sidebar-spacer");
            sidebar.Add(sidebarSpacer);
            sidebar.Add(CreateMenuSettingsButton());
            body.Add(sidebar);

            VisualElement main = new VisualElement();
            main.AddToClassList("ea-main");

            VisualElement contentHeader = new VisualElement();
            contentHeader.AddToClassList("ea-content-header");
            VisualElement titleBlock = new VisualElement();
            titleBlock.AddToClassList("ea-title-block");
            _sectionTitleLabel = CreateLabel("", "ea-title");
            titleBlock.Add(_sectionTitleLabel);
            _sectionSubtitleLabel = CreateLabel("", "ea-subtitle");
            titleBlock.Add(_sectionSubtitleLabel);
            contentHeader.Add(titleBlock);

            VisualElement summary = new VisualElement();
            summary.AddToClassList("ea-summary");
            _dashboardStatusPill = CreateLabel("", "ea-header-stat");
            _gameSummaryLabel = CreateLabel("", "ea-header-stat");
            _placementSummaryLabel = CreateLabel("", "ea-header-stat");
            summary.Add(_dashboardStatusPill);
            summary.Add(_gameSummaryLabel);
            summary.Add(_placementSummaryLabel);
            contentHeader.Add(summary);
            main.Add(contentHeader);

            _contentRoot = new ScrollView(ScrollViewMode.Vertical);
            _contentRoot.AddToClassList("ea-content");
            main.Add(_contentRoot);
            body.Add(main);

            rootVisualElement.Add(body);
            RebuildContent();
        }

        private Label CreateLabel(string text, string className)
        {
            Label label = new Label(text);
            string[] classNames = className.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < classNames.Length; i++)
            {
                label.AddToClassList(classNames[i]);
            }

            return label;
        }

        private Button CreateFolderButton(string text, string folderKey)
        {
            Button button = new Button(() => SelectFolder(folderKey))
            {
                text = text,
                userData = folderKey
            };
            button.AddToClassList("ea-tab-button");
            _tabButtons.Add(button);
            return button;
        }

        private Button CreateMenuSettingsButton()
        {
            Button button = new Button(SelectMenuSettings)
            {
                text = "Menu Settings",
                userData = MenuSettingsButtonId
            };
            button.AddToClassList("ea-tab-button");
            button.AddToClassList("ea-menu-settings-button");
            _tabButtons.Add(button);
            return button;
        }

        private Button CreateActionButton(string text, Action action)
        {
            Button button = new Button(action)
            {
                text = text
            };
            button.AddToClassList("ea-action-button");
            return button;
        }

        private Button CreateSecondaryButton(string text, Action action)
        {
            Button button = new Button(action)
            {
                text = text
            };
            button.AddToClassList("ea-secondary-button");
            return button;
        }

        private VisualElement CreatePanel(string title)
        {
            VisualElement panel = new VisualElement();
            panel.AddToClassList("ea-panel");
            panel.Add(CreateLabel(title, "ea-panel-title"));
            return panel;
        }

        private VisualElement CreateSplitRow()
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("ea-split-row");
            return row;
        }

        private VisualElement CreateButtonRow()
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("ea-button-row");
            return row;
        }

        private TextField CreateTextField(string label, string value, Action<string> onChanged)
        {
            TextField field = new TextField(label)
            {
                value = value ?? ""
            };
            field.AddToClassList("ea-field");
            field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            return field;
        }

        private TextField CreatePasswordField(string label, string value, Action<string> onChanged)
        {
            TextField field = CreateTextField(label, value, onChanged);
            field.isPasswordField = true;
            return field;
        }

        private FloatField CreateFloatField(string label, float value, Action<float> onChanged)
        {
            FloatField field = new FloatField(label)
            {
                value = value
            };
            field.AddToClassList("ea-field");
            field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            return field;
        }

        private VisualElement CreateSwitch(string label, bool value, Action<bool> onChanged)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("ea-switch-row");

            Label text = CreateLabel(label, "ea-switch-label");
            VisualElement track = new VisualElement();
            track.AddToClassList("ea-switch-track");
            VisualElement thumb = new VisualElement();
            thumb.AddToClassList("ea-switch-thumb");
            track.Add(thumb);

            Action<bool> setVisualState = isOn =>
            {
                value = isOn;
                track.EnableInClassList("ea-switch-track-on", isOn);
                thumb.EnableInClassList("ea-switch-thumb-on", isOn);
            };
            setVisualState(value);

            row.RegisterCallback<ClickEvent>(_ =>
            {
                bool nextValue = !value;
                setVisualState(nextValue);
                onChanged(nextValue);
            });

            row.Add(text);
            row.Add(track);
            return row;
        }

        private void SelectFolder(string folderKey)
        {
            _activeFolderKey = folderKey;
            _showMenuSettings = false;
            RebuildContent();
            RequestDashboardUpdate();
        }

        private void SelectMenuSettings()
        {
            _showMenuSettings = true;
            RebuildContent();
            RequestDashboardUpdate();
        }

        private void RebuildContent()
        {
            if (_contentRoot == null) { return; }

            if (!_showMenuSettings && !HasDashboardFolder(_activeFolderKey))
            {
                _activeFolderKey = GetFirstDashboardFolder().Key;
            }

            RefreshHeaderSummary();
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                bool active = (!_showMenuSettings && _tabButtons[i].userData is string folderKey && folderKey == _activeFolderKey)
                    || (_showMenuSettings && _tabButtons[i].userData is string buttonId && buttonId == MenuSettingsButtonId);
                _tabButtons[i].EnableInClassList("ea-tab-button-active", active);
            }

            _contentRoot.Clear();
            if (_showMenuSettings)
            {
                AddMenuSettingsAlerts(_contentRoot);
                BuildMenuSettingsTab(_contentRoot);
                return;
            }

            DashboardFolderDefinition folder = GetActiveDashboardFolder();
            AddDashboardFolderAlerts(_contentRoot, folder);
            BuildDashboardFolder(_contentRoot, folder);
        }

        private void BuildDashboardFolder(VisualElement parent, DashboardFolderDefinition folder)
        {
            int visibleComponentCount = 0;
            for (int i = 0; i < folder.Components.Count; i++)
            {
                EasterAdDashboardComponent componentId = folder.Components[i];
                DashboardComponentDefinition component = CreateDashboardComponent(componentId);
                component.Build(parent);
                visibleComponentCount++;
            }

            if (visibleComponentCount > 0) { return; }

            VisualElement emptyPanel = CreatePanel("No Components Enabled");
            emptyPanel.Add(CreateLabel("Drag components into this folder from Menu Settings.", "ea-muted"));
            parent.Add(emptyPanel);
        }

        private void AddDashboardFolderAlerts(VisualElement parent, DashboardFolderDefinition folder)
        {
            VisualElement alerts = new VisualElement();
            alerts.AddToClassList("ea-folder-alerts");
            AddDashboardAutoSyncBanner(alerts);
            AddManagedSceneWarnings(alerts);

            if (ShouldShowDashboardStatusCallout())
            {
                alerts.Add(CreateDashboardStatusCallout());
            }

            if (DashboardFolderContainsAny(
                    folder,
                    EasterAdDashboardComponent.CreateInventory,
                    EasterAdDashboardComponent.ActiveInventory,
                    EasterAdDashboardComponent.ArchiveInventory))
            {
                EasterAdDashboardOrganization organization = GetSelectedDashboardOrganization();
                EasterAdDashboardGame game = GetSelectedDashboardGame();
                if (organization == null || game == null)
                {
                    alerts.Add(CreateLabel("Select an organization and game in Settings before managing inventory.", "ea-callout"));
                }
            }

            if (DashboardFolderContainsAny(
                    folder,
                    EasterAdDashboardComponent.DashboardProject,
                    EasterAdDashboardComponent.SdkSettings))
            {
                EasterAdDashboardGame game = GetSelectedDashboardGame();
                if (game != null && string.IsNullOrEmpty(game.sdkKey))
                {
                    alerts.Add(CreateLabel("Selected game has no SDK Key yet. Applying it keeps runtime sessions in test mode.", "ea-callout"));
                }

                if (Application.isPlaying)
                {
                    alerts.Add(CreateLabel("Runtime Status: " + (EasterAdSdk.OnceInitialized ? "Initialized" : "Not Initialized"), "ea-callout"));
                }
            }

            if (DashboardFolderContainsAny(
                    folder,
                    EasterAdDashboardComponent.DiagnosticsStatus,
                    EasterAdDashboardComponent.RenderPipelineMigration) &&
                AssetDatabase.LoadAssetAtPath<GameObject>(PlaneItemPrefabPath) == null)
            {
                alerts.Add(CreateLabel("Prefab not found. Package may need reinstallation.", "ea-callout ea-callout-warning"));
            }

            if (alerts.childCount > 0)
            {
                parent.Add(alerts);
            }
        }

        private void AddMenuSettingsAlerts(VisualElement parent)
        {
            VisualElement alerts = new VisualElement();
            alerts.AddToClassList("ea-folder-alerts");
            AddDashboardAutoSyncBanner(alerts);

            if (ShouldShowDashboardStatusCallout())
            {
                alerts.Add(CreateDashboardStatusCallout());
            }

            if (alerts.childCount > 0)
            {
                parent.Add(alerts);
            }
        }

        private void BuildMenuSettingsTab(VisualElement parent)
        {
            VisualElement menuPanel = CreatePanel("Menu Layout");
            menuPanel.tooltip = "Drag rows to reorder. Components are grouped under the nearest folder above them.";
            List<DashboardMenuRowData> menuRows = BuildDashboardMenuRows();

            if (menuRows.Count == 0)
            {
                VisualElement empty = new VisualElement();
                empty.AddToClassList("ea-empty");
                empty.Add(CreateLabel("No folders", "ea-empty-title"));
                empty.Add(CreateLabel("Reset the layout to restore the default menu folders.", "ea-muted"));
                menuPanel.Add(empty);
            }
            else
            {
                ListView listView = new ListView
                {
                    itemsSource = menuRows,
                    fixedItemHeight = DashboardMenuRowHeight,
                    makeItem = CreateDashboardMenuListItem,
                    bindItem = (element, index) => BindDashboardMenuListItem(element, menuRows[index]),
                    reorderable = true,
                    reorderMode = ListViewReorderMode.Animated,
                    selectionType = SelectionType.None
                };
                listView.AddToClassList("ea-menu-list");
                listView.style.height = Mathf.Max(DashboardMenuRowHeight, menuRows.Count * DashboardMenuRowHeight);
                listView.itemIndexChanged += (oldIndex, newIndex) =>
                {
                    listView.schedule.Execute(() => ApplyDashboardMenuRows(menuRows)).ExecuteLater(0);
                };
                menuPanel.Add(listView);
            }

            VisualElement actions = CreateButtonRow();
            actions.Add(CreateActionButton("Add Folder", AddDashboardFolder));
            actions.Add(CreateSecondaryButton("Reset Layout", ResetDashboardMenuLayout));
            menuPanel.Add(actions);
            parent.Add(menuPanel);
        }

        private List<DashboardMenuRowData> BuildDashboardMenuRows()
        {
            List<DashboardMenuRowData> rows = new List<DashboardMenuRowData>();
            for (int i = 0; i < _dashboardFolders.Count; i++)
            {
                DashboardFolderDefinition folder = _dashboardFolders[i];
                rows.Add(new DashboardMenuRowData(folder.Key));
                for (int componentIndex = 0; componentIndex < folder.Components.Count; componentIndex++)
                {
                    rows.Add(new DashboardMenuRowData(folder.Key, folder.Components[componentIndex]));
                }
            }

            return rows;
        }

        private VisualElement CreateDashboardMenuListItem()
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("ea-menu-list-row");

            Label symbol = CreateLabel("", "ea-menu-list-symbol");
            symbol.name = "symbol";
            row.Add(symbol);

            TextField titleField = new TextField
            {
                name = "title-field"
            };
            titleField.AddToClassList("ea-menu-list-name-field");
            titleField.RegisterValueChangedCallback(evt => HandleDashboardFolderTitleChanged(titleField, evt));
            row.Add(titleField);

            Label titleLabel = CreateLabel("", "ea-menu-list-title");
            titleLabel.name = "title-label";
            row.Add(titleLabel);

            Button deleteButton = new Button(() =>
            {
                DashboardMenuRowData buttonRowData = row.userData as DashboardMenuRowData;
                if (buttonRowData == null || buttonRowData.IsComponent) { return; }

                DeleteDashboardFolder(buttonRowData.FolderKey);
            })
            {
                name = "delete-button",
                text = "×",
                tooltip = "Remove folder"
            };
            deleteButton.AddToClassList("ea-secondary-button");
            deleteButton.AddToClassList("ea-icon-button");
            deleteButton.AddToClassList("ea-menu-icon-button");
            row.Add(deleteButton);

            return row;
        }

        private void BindDashboardMenuListItem(VisualElement row, DashboardMenuRowData rowData)
        {
            row.userData = rowData;
            row.EnableInClassList("ea-menu-list-row-folder", !rowData.IsComponent);
            row.EnableInClassList("ea-menu-list-row-component", rowData.IsComponent);

            Label symbol = row.Q<Label>("symbol");
            TextField titleField = row.Q<TextField>("title-field");
            Label titleLabel = row.Q<Label>("title-label");
            Button deleteButton = row.Q<Button>("delete-button");

            if (symbol != null)
            {
                symbol.text = rowData.IsComponent ? "↳" : "✎";
                symbol.EnableInClassList("ea-menu-list-indent", rowData.IsComponent);
            }

            if (titleField != null)
            {
                titleField.userData = rowData;
                titleField.style.display = rowData.IsComponent ? DisplayStyle.None : DisplayStyle.Flex;
                titleField.tooltip = "Folder name";
            }

            if (titleLabel != null)
            {
                titleLabel.style.display = rowData.IsComponent ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (deleteButton != null)
            {
                deleteButton.style.display = rowData.IsComponent ? DisplayStyle.None : DisplayStyle.Flex;
                deleteButton.SetEnabled(!rowData.IsComponent && _dashboardFolders.Count > 1);
            }

            if (rowData.IsComponent)
            {
                DashboardComponentDefinition componentDefinition = CreateDashboardComponent(rowData.Component);
                row.tooltip = "Drag to reorder this component. Its folder is the nearest folder row above it.";
                if (titleLabel != null)
                {
                    titleLabel.text = componentDefinition.Title;
                }
            }
            else
            {
                DashboardFolderDefinition folder = FindDashboardFolder(rowData.FolderKey);
                row.tooltip = "Drag to reorder this folder boundary. Components below it belong to this folder.";
                if (titleField != null)
                {
                    titleField.SetValueWithoutNotify(folder == null ? "Untitled Folder" : folder.Title);
                }
            }
        }

        private void HandleDashboardFolderTitleChanged(TextField titleField, ChangeEvent<string> evt)
        {
            DashboardMenuRowData rowData = titleField.userData as DashboardMenuRowData;
            if (rowData == null || rowData.IsComponent) { return; }

            DashboardFolderDefinition folder = FindDashboardFolder(rowData.FolderKey);
            if (folder == null) { return; }

            string nextTitle = NormalizeDashboardFolderTitle(evt.newValue);
            if (folder.Title == nextTitle) { return; }

            folder.Title = nextTitle;
            SaveDashboardMenuPreferences();
            UpdateDashboardFolderTitleReferences(folder.Key, folder.Title);
        }

        private void AddDashboardFolder()
        {
            string folderKey = "folder-" + Guid.NewGuid().ToString("N");
            string folderTitle = CreateUniqueDashboardFolderTitle("New Folder");
            DashboardFolderDefinition folder = new DashboardFolderDefinition(folderKey, folderTitle, "Custom dashboard folder.");
            _dashboardFolders.Add(folder);
            _activeFolderKey = folderKey;
            SaveDashboardMenuPreferences();
            BuildWindow();
        }

        private void DeleteDashboardFolder(string folderKey)
        {
            int folderIndex = FindDashboardFolderIndex(folderKey);
            if (folderIndex < 0) { return; }

            if (_dashboardFolders.Count <= 1)
            {
                _dashboardStatus = "At least one folder must remain.";
                RefreshUi();
                return;
            }

            DashboardFolderDefinition folder = _dashboardFolders[folderIndex];
            string message = folder.Components.Count == 0
                ? "Remove '" + folder.Title + "'?"
                : "Remove '" + folder.Title + "'? Its components will move to an adjacent folder.";
            if (!EditorUtility.DisplayDialog("Remove Dashboard Folder", message, "Remove", "Cancel"))
            {
                return;
            }

            int targetIndex = folderIndex > 0 ? folderIndex - 1 : folderIndex + 1;
            DashboardFolderDefinition targetFolder = _dashboardFolders[targetIndex];
            for (int i = 0; i < folder.Components.Count; i++)
            {
                if (!targetFolder.Components.Contains(folder.Components[i]))
                {
                    targetFolder.Components.Add(folder.Components[i]);
                }
            }

            _dashboardFolders.RemoveAt(folderIndex);
            if (_activeFolderKey == folderKey)
            {
                _activeFolderKey = targetFolder.Key;
            }

            SaveDashboardMenuPreferences();
            BuildWindow();
        }

        private string CreateUniqueDashboardFolderTitle(string baseTitle)
        {
            string title = NormalizeDashboardFolderTitle(baseTitle);
            if (!DashboardFolderTitleExists(title)) { return title; }

            for (int i = 2; i < 1000; i++)
            {
                string candidate = title + " " + i;
                if (!DashboardFolderTitleExists(candidate))
                {
                    return candidate;
                }
            }

            return title + " " + Guid.NewGuid().ToString("N").Substring(0, 4);
        }

        private bool DashboardFolderTitleExists(string title)
        {
            for (int i = 0; i < _dashboardFolders.Count; i++)
            {
                if (string.Equals(_dashboardFolders[i].Title, title, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeDashboardFolderTitle(string title)
        {
            return string.IsNullOrWhiteSpace(title) ? "Untitled Folder" : title.Trim();
        }

        private void UpdateDashboardFolderTitleReferences(string folderKey, string title)
        {
            if (!_showMenuSettings && _activeFolderKey == folderKey && _sectionTitleLabel != null)
            {
                _sectionTitleLabel.text = title;
            }

            for (int i = 0; i < _tabButtons.Count; i++)
            {
                Button button = _tabButtons[i];
                if (button == null) { continue; }

                string buttonFolderKey = button.userData as string;
                if (buttonFolderKey == folderKey)
                {
                    button.text = title;
                }
            }
        }

        private void ApplyDashboardMenuRows(List<DashboardMenuRowData> menuRows)
        {
            if (menuRows == null || menuRows.Count == 0) { return; }

            List<DashboardFolderDefinition> nextFolders = new List<DashboardFolderDefinition>();
            HashSet<EasterAdDashboardComponent> assignedComponents = new HashSet<EasterAdDashboardComponent>();
            List<EasterAdDashboardComponent> leadingComponents = new List<EasterAdDashboardComponent>();
            DashboardFolderDefinition currentFolder = null;

            for (int i = 0; i < menuRows.Count; i++)
            {
                DashboardMenuRowData row = menuRows[i];
                if (row == null) { continue; }

                if (!row.IsComponent)
                {
                    currentFolder = CreateDashboardFolderCopy(row.FolderKey);
                    nextFolders.Add(currentFolder);
                    for (int leadingIndex = 0; leadingIndex < leadingComponents.Count; leadingIndex++)
                    {
                        currentFolder.Components.Add(leadingComponents[leadingIndex]);
                    }

                    leadingComponents.Clear();
                    continue;
                }

                if (assignedComponents.Contains(row.Component)) { continue; }

                if (currentFolder == null)
                {
                    leadingComponents.Add(row.Component);
                    assignedComponents.Add(row.Component);
                    continue;
                }

                currentFolder.Components.Add(row.Component);
                assignedComponents.Add(row.Component);
            }

            if (leadingComponents.Count > 0)
            {
                currentFolder = CreateDashboardFallbackFolder(nextFolders);
                for (int i = 0; i < leadingComponents.Count; i++)
                {
                    currentFolder.Components.Add(leadingComponents[i]);
                }
            }

            _dashboardFolders.Clear();
            _dashboardFolders.AddRange(nextFolders);
            EnsureDashboardFolderSelection();
            SaveDashboardMenuPreferences();
            BuildWindow();
        }

        private DashboardFolderDefinition CreateDashboardFolderCopy(string folderKey)
        {
            DashboardFolderDefinition folder = FindDashboardFolder(folderKey);
            if (folder != null)
            {
                return new DashboardFolderDefinition(folder.Key, folder.Title, folder.Subtitle);
            }

            string nextKey = string.IsNullOrEmpty(folderKey) ? "folder-" + Guid.NewGuid().ToString("N") : folderKey;
            return new DashboardFolderDefinition(nextKey, "Untitled Folder", "Custom dashboard folder.");
        }

        private DashboardFolderDefinition CreateDashboardFallbackFolder(List<DashboardFolderDefinition> folders)
        {
            DashboardFolderDefinition firstFolder = _dashboardFolders.Count > 0
                ? _dashboardFolders[0]
                : null;
            DashboardFolderDefinition fallback = firstFolder == null
                ? new DashboardFolderDefinition("folder-" + Guid.NewGuid().ToString("N"), "Menu", "Custom dashboard folder.")
                : new DashboardFolderDefinition(firstFolder.Key, firstFolder.Title, firstFolder.Subtitle);

            folders.Add(fallback);
            return fallback;
        }

        private int FindDashboardFolderIndex(string folderKey)
        {
            for (int i = 0; i < _dashboardFolders.Count; i++)
            {
                if (_dashboardFolders[i].Key == folderKey)
                {
                    return i;
                }
            }

            return -1;
        }

        private DashboardFolderDefinition FindDashboardFolder(string folderKey)
        {
            for (int i = 0; i < _dashboardFolders.Count; i++)
            {
                if (_dashboardFolders[i].Key == folderKey)
                {
                    return _dashboardFolders[i];
                }
            }

            return null;
        }

        private void EnsureDashboardFolderSelection()
        {
            if (_dashboardFolders.Count == 0)
            {
                _dashboardFolders.Add(new DashboardFolderDefinition("folder-" + Guid.NewGuid().ToString("N"), "Menu", "Custom dashboard folder."));
            }

            if (!HasDashboardFolder(_activeFolderKey))
            {
                _activeFolderKey = GetFirstDashboardFolder().Key;
            }
        }

        private void ResetDashboardMenuLayout()
        {
            EditorPrefs.DeleteKey(DashboardMenuLayoutPrefsKey);
            LoadDefaultDashboardMenuLayout();
            _activeFolderKey = GetFirstDashboardFolder().Key;
            BuildWindow();
        }

        private void RefreshHeaderSummary()
        {
            if (_dashboardStatusPill == null) { return; }

            string dashboardState;
            bool showDashboardState = true;
            if (_dashboardAutoSyncPaused)
            {
                dashboardState = "Dashboard update paused";
            }
            else if (_dashboardAutoSyncFailureCount > 0)
            {
                dashboardState = "Dashboard update retry " + _dashboardAutoSyncFailureCount + "/" + DashboardAutoSyncFailureLimit;
            }
            else if (string.IsNullOrEmpty(_dashboardApiKey))
            {
                dashboardState = "Dashboard disconnected";
            }
            else
            {
                dashboardState = "";
                showDashboardState = false;
            }

            string gameState = string.IsNullOrEmpty(_tempGameId)
                ? "No game selected"
                : "Game " + ShortenId(_tempGameId);

            int placementCount = FindSceneItems().Length;

            DashboardFolderDefinition folder = GetActiveDashboardFolder();
            _sectionTitleLabel.text = _showMenuSettings ? "Menu Settings" : folder.Title;
            _sectionSubtitleLabel.text = _showMenuSettings
                ? "Arrange dashboard components into sidebar folders."
                : folder.Subtitle;
            _dashboardStatusPill.text = dashboardState;
            _dashboardStatusPill.style.display = showDashboardState ? DisplayStyle.Flex : DisplayStyle.None;
            _dashboardStatusPill.EnableInClassList("ea-header-stat-warning", !_dashboardAutoSyncPaused && _dashboardAutoSyncFailureCount > 0);
            _dashboardStatusPill.EnableInClassList("ea-header-stat-danger", _dashboardAutoSyncPaused);
            _gameSummaryLabel.text = gameState;
            _placementSummaryLabel.text = placementCount + " placements";
        }

        private static string GetTabTitle(EasterAdWindowTab tab)
        {
            switch (tab)
            {
                case EasterAdWindowTab.Dashboard:
                    return "Inventory";
                case EasterAdWindowTab.Settings:
                    return "Settings";
                case EasterAdWindowTab.Diagnostics:
                    return "Diagnostics";
                default:
                    return "EasterAd";
            }
        }

        private void RefreshUi()
        {
            if (_contentRoot != null)
            {
                RebuildContent();
            }

            Repaint();
        }

        private bool ShouldShowDashboardStatusCallout()
        {
            return !string.IsNullOrEmpty(_dashboardStatus) && !IsSilentDashboardSyncStatus(_dashboardStatus);
        }

        private Label CreateDashboardStatusCallout()
        {
            Label status = CreateLabel(_dashboardStatus, "ea-callout");
            status.EnableInClassList("ea-callout-warning", IsDashboardStatusWarning(_dashboardStatus));
            return status;
        }

        private static bool IsSilentDashboardSyncStatus(string status)
        {
            if (string.IsNullOrEmpty(status)) { return true; }

            return status.StartsWith("Syncing dashboard", StringComparison.Ordinal)
                || string.Equals(status, "Dashboard updated.", StringComparison.Ordinal)
                || (status.StartsWith("Loaded ", StringComparison.Ordinal) && status.EndsWith(" dashboard inventory items.", StringComparison.Ordinal));
        }

        private static bool IsDashboardSyncResultStatus(string status)
        {
            return IsSilentDashboardSyncStatus(status)
                || status.StartsWith("Dashboard update failed", StringComparison.Ordinal);
        }

        private static bool IsDashboardStatusWarning(string status)
        {
            return status.StartsWith("Dashboard request failed", StringComparison.Ordinal)
                || status.StartsWith("Dashboard update failed", StringComparison.Ordinal);
        }

        private async void RequestDashboardUpdate()
        {
            await SyncDashboardAsync(false);
        }

        private void RefreshDashboardFromErrorBanner()
        {
            _dashboardAutoSyncPaused = false;
            _dashboardAutoSyncFailureCount = 0;
            _dashboardAutoSyncError = "";
            RefreshDashboardAsync();
        }

        private void AddDashboardAutoSyncBanner(VisualElement parent)
        {
            if (!_dashboardAutoSyncPaused) { return; }

            VisualElement banner = new VisualElement();
            banner.AddToClassList("ea-error-banner");

            VisualElement textBlock = new VisualElement();
            textBlock.AddToClassList("ea-error-banner-text");
            textBlock.Add(CreateLabel("Dashboard updates paused", "ea-error-banner-title"));
            string message = "Dashboard updates failed " + DashboardAutoSyncFailureLimit + " times.";
            if (!string.IsNullOrEmpty(_dashboardAutoSyncError))
            {
                message += " " + _dashboardAutoSyncError;
            }

            textBlock.Add(CreateLabel(message, "ea-error-banner-message"));
            banner.Add(textBlock);

            Button refreshButton = CreateActionButton("Refresh", RefreshDashboardFromErrorBanner);
            refreshButton.AddToClassList("ea-error-banner-button");
            refreshButton.SetEnabled(!_dashboardUpdateBusy && !_dashboardActionBusy);
            banner.Add(refreshButton);
            parent.Add(banner);
        }

        private void AddManagedSceneWarnings(VisualElement parent)
        {
            ManagedSceneScanResult scan = ScanManagedScenes(true);
            List<ManagedSceneConflict> conflicts = BuildManagedSceneConflicts(scan);
            if (scan.RemovedScenePaths.Count == 0 && conflicts.Count == 0) { return; }

            VisualElement banner = new VisualElement();
            banner.AddToClassList("ea-warning-banner");

            VisualElement textBlock = new VisualElement();
            textBlock.AddToClassList("ea-warning-banner-text");
            textBlock.Add(CreateLabel("Managed scene warning", "ea-warning-banner-title"));

            if (conflicts.Count > 0)
            {
                textBlock.Add(CreateLabel(BuildManagedSceneConflictSummary(conflicts), "ea-warning-banner-message"));
            }

            if (scan.RemovedScenePaths.Count > 0)
            {
                textBlock.Add(CreateLabel(BuildRemovedManagedScenesSummary(scan.RemovedScenePaths), "ea-warning-banner-message"));
            }

            banner.Add(textBlock);
            parent.Add(banner);
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) { return; }

            ManagedSceneScanResult scan = ScanManagedScenes(true);
            List<ManagedSceneConflict> conflicts = BuildManagedSceneConflicts(scan);
            if (conflicts.Count == 0) { return; }

            _dashboardStatus = BuildManagedSceneConflictSummary(conflicts);
            Debug.LogError("[EasterAd] " + _dashboardStatus);
            RefreshUi();
        }

        private static string ShortenId(string value)
        {
            if (string.IsNullOrEmpty(value)) { return ""; }
            return value.Length <= 8 ? value : value.Substring(0, 8);
        }

        private void BuildCreateInventoryComponent(VisualElement parent)
        {
            VisualElement grid = new VisualElement();
            grid.AddToClassList("ea-grid");

            VisualElement createPanel = CreatePanel("Create New Inventory");
            EasterAdDashboardOrganization organization = GetSelectedDashboardOrganization();
            EasterAdDashboardGame game = GetSelectedDashboardGame();

            TextField nameField = CreateTextField("Inventory Name", _newInventoryName, value => _newInventoryName = value);
            nameField.SetEnabled(!_dashboardActionBusy);
            createPanel.Add(nameField);

            VisualElement createActions = CreateButtonRow();
            Button createButton = CreateActionButton("Create New Inventory", CreateDashboardInventoryAsync);
            createButton.SetEnabled(!_dashboardActionBusy && organization != null && game != null && !string.IsNullOrWhiteSpace(_newInventoryName));
            createActions.Add(createButton);
            createPanel.Add(createActions);

            grid.Add(createPanel);
            parent.Add(grid);
        }

        private void BuildInventoryListComponent(VisualElement parent, string title, bool active)
        {
            parent.Add(CreateInventoryListPanel(title, active, FindSceneItems()));
        }

        private void BuildDashboardTab(VisualElement parent)
        {
            Item[] sceneItems = FindSceneItems();
            VisualElement grid = new VisualElement();
            grid.AddToClassList("ea-grid");

            VisualElement createPanel = CreatePanel("Create New Inventory");
            EasterAdDashboardOrganization organization = GetSelectedDashboardOrganization();
            EasterAdDashboardGame game = GetSelectedDashboardGame();
            if (organization == null || game == null)
            {
                createPanel.Add(CreateLabel("Select an organization and game in Settings before managing inventory.", "ea-callout"));
            }

            TextField nameField = CreateTextField("Inventory Name", _newInventoryName, value => _newInventoryName = value);
            nameField.SetEnabled(!_dashboardActionBusy);
            createPanel.Add(nameField);

            VisualElement createActions = CreateButtonRow();
            Button createButton = CreateActionButton("Create New Inventory", CreateDashboardInventoryAsync);
            createButton.SetEnabled(!_dashboardActionBusy && organization != null && game != null && !string.IsNullOrWhiteSpace(_newInventoryName));
            createActions.Add(createButton);
            createPanel.Add(createActions);

            if (ShouldShowDashboardStatusCallout())
            {
                createPanel.Add(CreateDashboardStatusCallout());
            }

            grid.Add(createPanel);
            parent.Add(grid);
            parent.Add(CreateInventoryListPanel("Active Inventory", true, sceneItems));
            parent.Add(CreateInventoryListPanel("Archive", false, sceneItems));
        }

        private VisualElement CreateDashboardConnectionPanel()
        {
            VisualElement connectPanel = CreatePanel("Dashboard Connection");
            TextField baseUrlField = CreateTextField("URL", _dashboardBaseUrl, value =>
            {
                _dashboardBaseUrl = value;
            });
            TextField apiKeyField = CreatePasswordField("API Key", _dashboardApiKey, value =>
            {
                _dashboardApiKey = value;
            });
            baseUrlField.RegisterCallback<FocusOutEvent>(_ => RequestDashboardUpdate());
            apiKeyField.RegisterCallback<FocusOutEvent>(_ => RequestDashboardUpdate());
            baseUrlField.SetEnabled(!_dashboardActionBusy);
            apiKeyField.SetEnabled(!_dashboardActionBusy);
            connectPanel.Add(baseUrlField);
            connectPanel.Add(apiKeyField);

            VisualElement connectButtons = CreateButtonRow();
            Button forgetButton = CreateSecondaryButton("Forget Key", ForgetDashboardKey);
            Button openButton = CreateSecondaryButton("Open Dashboard", OpenDashboard);
            forgetButton.SetEnabled(!_dashboardActionBusy);
            openButton.SetEnabled(!_dashboardActionBusy);
            connectButtons.Add(forgetButton);
            connectButtons.Add(openButton);
            connectPanel.Add(connectButtons);

            return connectPanel;
        }

        private VisualElement CreateDashboardProjectPanel()
        {
            VisualElement dataPanel = CreatePanel("Project Settings");
            AddDashboardOrganizationPicker(dataPanel);
            AddDashboardGamePicker(dataPanel);
            return dataPanel;
        }

        private void AddDashboardOrganizationPicker(VisualElement panel)
        {
            if (_dashboardOrganizations.Count == 0)
            {
                panel.Add(CreateLabel("Connect to load organizations.", "ea-muted"));
                return;
            }

            List<string> labels = new List<string>(BuildOrganizationLabels());
            PopupField<string> organizationPopup = new PopupField<string>(
                "Organization",
                labels,
                Mathf.Clamp(_dashboardOrganizationIndex, 0, labels.Count - 1));
            organizationPopup.AddToClassList("ea-field");
            organizationPopup.RegisterValueChangedCallback(evt =>
            {
                int index = labels.IndexOf(evt.newValue);
                if (index < 0 || index == _dashboardOrganizationIndex) { return; }
                _dashboardOrganizationIndex = index;
                _dashboardOrganizationId = _dashboardOrganizations[index]._id ?? "";
                _dashboardGameId = "";
                _dashboardGames.Clear();
                _dashboardAdUnits.Clear();
                _dashboardGameIndex = 0;
                _dashboardAdUnitIndex = 0;
                SaveDashboardPreferences();
                RefreshUi();
                RequestDashboardUpdate();
            });
            panel.Add(organizationPopup);
        }

        private void AddDashboardGamePicker(VisualElement panel)
        {
            if (_dashboardGames.Count == 0)
            {
                panel.Add(CreateLabel("No games loaded.", "ea-muted"));
                return;
            }

            List<string> labels = new List<string>(BuildGameLabels());
            PopupField<string> gamePopup = new PopupField<string>(
                "Game",
                labels,
                Mathf.Clamp(_dashboardGameIndex, 0, labels.Count - 1));
            gamePopup.AddToClassList("ea-field");
            gamePopup.RegisterValueChangedCallback(evt =>
            {
                int index = labels.IndexOf(evt.newValue);
                if (index < 0 || index == _dashboardGameIndex) { return; }
                _dashboardGameIndex = index;
                _dashboardGameId = _dashboardGames[index]._id ?? "";
                _dashboardAdUnits.Clear();
                _dashboardAdUnitIndex = 0;
                SaveDashboardPreferences();
                RefreshUi();
                RequestDashboardUpdate();
            });
            panel.Add(gamePopup);

            EasterAdDashboardGame game = GetSelectedDashboardGame();
            VisualElement actions = CreateButtonRow();
            Button applyButton = CreateActionButton("Apply to SDK", () =>
            {
                ApplyDashboardGameToSdkSettings(false);
                RefreshUi();
            });
            Button saveButton = CreateSecondaryButton("Apply and Save", () =>
            {
                ApplyDashboardGameToSdkSettings(true);
                RefreshUi();
            });
            applyButton.SetEnabled(!_dashboardActionBusy && game != null);
            saveButton.SetEnabled(!_dashboardActionBusy && game != null);
            actions.Add(applyButton);
            actions.Add(saveButton);
            panel.Add(actions);
        }

        private VisualElement CreateInventoryListPanel(string title, bool active, Item[] sceneItems)
        {
            VisualElement panel = CreatePanel(title);
            HashSet<string> dashboardInventoryIds = new HashSet<string>();
            int count = 0;
            for (int i = 0; i < _dashboardAdUnits.Count; i++)
            {
                EasterAdDashboardAdUnit adUnit = _dashboardAdUnits[i];
                if (adUnit == null) { continue; }

                if (!string.IsNullOrEmpty(adUnit._id))
                {
                    dashboardInventoryIds.Add(adUnit._id);
                }

                Item[] placements = FindScenePlacementsForAdUnit(sceneItems, adUnit._id);
                bool isActive = placements.Length > 0;
                if (isActive != active)
                {
                    continue;
                }

                panel.Add(CreateInventoryRow(adUnit, isActive, placements, sceneItems));
                count++;
            }

            if (active)
            {
                Dictionary<string, List<Item>> sceneOnlyGroups = BuildSceneOnlyInventoryGroups(sceneItems, dashboardInventoryIds);
                foreach (KeyValuePair<string, List<Item>> group in sceneOnlyGroups)
                {
                    panel.Add(CreateSceneOnlyInventoryRow(group.Key, group.Value.ToArray(), sceneItems));
                    count++;
                }
            }

            if (count == 0)
            {
                VisualElement empty = new VisualElement();
                empty.AddToClassList("ea-empty");
                empty.Add(CreateLabel(active ? "No active inventory" : "Archive is empty", "ea-empty-title"));
                empty.Add(CreateLabel(
                    active
                        ? "Create a new inventory or restore one from Archive to make it active in this scene."
                        : "Inventory not placed in the scene is kept here and can be restored into the scene.",
                    "ea-muted"));
                panel.Add(empty);
            }

            return panel;
        }

        private VisualElement CreateInventoryRow(EasterAdDashboardAdUnit adUnit, bool active, Item[] placements, Item[] allSceneItems)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("ea-placement-row");
            row.AddToClassList("ea-inventory-row");

            VisualElement header = new VisualElement();
            header.AddToClassList("ea-placement-header");
            VisualElement titleBlock = new VisualElement();
            titleBlock.AddToClassList("ea-placement-title-block");
            titleBlock.Add(CreateLabel(string.IsNullOrEmpty(adUnit.name) ? "Untitled Inventory" : adUnit.name, "ea-placement-title"));
            titleBlock.Add(CreateLabel(BuildInventorySubtitle(adUnit, active, placements.Length), "ea-placement-subtitle"));
            header.Add(titleBlock);

            VisualElement actions = CreateButtonRow();
            if (active)
            {
                AddInventorySettingsToggle(actions, adUnit._id);
            }
            else
            {
                if (IsServerArchivedDashboardAdUnit(adUnit))
                {
                    Button restoreServerButton = CreateActionButton("Restore Server", () => RestoreDashboardInventoryAsync(adUnit));
                    restoreServerButton.SetEnabled(!_dashboardActionBusy);
                    actions.Add(restoreServerButton);
                }
                else
                {
                    actions.Add(CreateActionButton("Restore Plane", () => CreatePlacementFromInventory(adUnit._id, false)));
                    actions.Add(CreateSecondaryButton("Restore CanvasItem", () => CreatePlacementFromInventory(adUnit._id, true)));
                }
            }

            header.Add(actions);
            row.Add(header);
            if (active && IsInventorySettingsExpanded(adUnit._id))
            {
                row.Add(CreateInventorySettingsPanel(placements, allSceneItems));
            }

            return row;
        }

        private VisualElement CreateSceneOnlyInventoryRow(string adUnitId, Item[] placements, Item[] allSceneItems)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("ea-placement-row");
            row.AddToClassList("ea-inventory-row");
            row.AddToClassList("ea-scene-only-row");

            VisualElement header = new VisualElement();
            header.AddToClassList("ea-placement-header");
            VisualElement titleBlock = new VisualElement();
            titleBlock.AddToClassList("ea-placement-title-block");
            titleBlock.Add(CreateLabel(string.IsNullOrEmpty(adUnitId) ? "Unassigned Scene Placement" : "Scene Inventory " + ShortenId(adUnitId), "ea-placement-title"));
            titleBlock.Add(CreateLabel(BuildSceneOnlyInventorySubtitle(adUnitId, placements.Length), "ea-placement-subtitle"));
            header.Add(titleBlock);

            VisualElement actions = CreateButtonRow();
            AddInventorySettingsToggle(actions, GetSceneOnlyInventoryKey(adUnitId));
            header.Add(actions);
            row.Add(header);

            if (IsInventorySettingsExpanded(GetSceneOnlyInventoryKey(adUnitId)))
            {
                row.Add(CreateInventorySettingsPanel(placements, allSceneItems));
            }

            return row;
        }

        private void AddInventorySettingsToggle(VisualElement actions, string inventoryKey)
        {
            bool expanded = IsInventorySettingsExpanded(inventoryKey);
            Button settingsButton = CreateSecondaryButton(expanded ? "⌃" : "⌄", () =>
            {
                ToggleInventorySettings(inventoryKey);
                RefreshUi();
                RequestDashboardUpdate();
            });
            settingsButton.tooltip = expanded ? "Hide settings" : "Show settings";
            settingsButton.AddToClassList("ea-icon-button");
            actions.Add(settingsButton);
        }

        private VisualElement CreateInventorySettingsPanel(Item[] placements, Item[] allSceneItems)
        {
            VisualElement settings = new VisualElement();
            settings.AddToClassList("ea-inventory-settings");
            for (int i = 0; i < placements.Length; i++)
            {
                VisualElement placementRow = CreatePlacementRow(placements[i], i + 1, allSceneItems);
                if (placementRow != null)
                {
                    settings.Add(placementRow);
                }
            }

            return settings;
        }

        private static string BuildInventorySubtitle(EasterAdDashboardAdUnit adUnit, bool active, int placementCount)
        {
            string type = string.IsNullOrEmpty(adUnit.type) ? "Display" : adUnit.type;
            string id = string.IsNullOrEmpty(adUnit._id) ? "No ID" : ShortenId(adUnit._id);
            string state = active ? BuildPlacementCountText(placementCount) + " in scene" : "Not placed in scene";
            if (IsServerArchivedDashboardAdUnit(adUnit))
            {
                state += " - Server archived";
            }

            return type + " - " + id + " - " + state;
        }

        private static string BuildSceneOnlyInventorySubtitle(string adUnitId, int placementCount)
        {
            string id = string.IsNullOrEmpty(adUnitId) ? "No Ad Unit ID" : ShortenId(adUnitId);
            return "Scene only - " + id + " - " + BuildPlacementCountText(placementCount) + " in scene";
        }

        private static string BuildPlacementCountText(int placementCount)
        {
            return placementCount == 1 ? "1 placement" : placementCount + " placements";
        }

        private static bool IsServerArchivedDashboardAdUnit(EasterAdDashboardAdUnit adUnit)
        {
            return adUnit != null && !string.IsNullOrEmpty(adUnit.deletedAt);
        }

        private static Item[] FindScenePlacementsForAdUnit(Item[] sceneItems, string adUnitId)
        {
            if (string.IsNullOrEmpty(adUnitId)) { return Array.Empty<Item>(); }

            List<Item> placements = new List<Item>();
            for (int i = 0; i < sceneItems.Length; i++)
            {
                Item item = sceneItems[i];
                if (item != null && item.adUnitId == adUnitId)
                {
                    placements.Add(item);
                }
            }

            return placements.ToArray();
        }

        private static Dictionary<string, List<Item>> BuildSceneOnlyInventoryGroups(Item[] sceneItems, HashSet<string> dashboardInventoryIds)
        {
            Dictionary<string, List<Item>> groups = new Dictionary<string, List<Item>>();
            for (int i = 0; i < sceneItems.Length; i++)
            {
                Item item = sceneItems[i];
                if (item == null) { continue; }

                string adUnitId = item.adUnitId ?? "";
                if (!string.IsNullOrEmpty(adUnitId) && dashboardInventoryIds.Contains(adUnitId))
                {
                    continue;
                }

                if (!groups.TryGetValue(adUnitId, out List<Item> placements))
                {
                    placements = new List<Item>();
                    groups.Add(adUnitId, placements);
                }

                placements.Add(item);
            }

            return groups;
        }

        private static string GetSceneOnlyInventoryKey(string adUnitId)
        {
            return "scene:" + (string.IsNullOrEmpty(adUnitId) ? "<unassigned>" : adUnitId);
        }

        private static string NormalizeInventorySettingsKey(string inventoryKey)
        {
            return string.IsNullOrEmpty(inventoryKey) ? "inventory:<empty>" : inventoryKey;
        }

        private bool IsInventorySettingsExpanded(string inventoryKey)
        {
            return _expandedInventorySettingsKey == NormalizeInventorySettingsKey(inventoryKey);
        }

        private void ToggleInventorySettings(string inventoryKey)
        {
            string key = NormalizeInventorySettingsKey(inventoryKey);
            _expandedInventorySettingsKey = _expandedInventorySettingsKey == key ? "" : key;
        }

        private void CreatePlacementFromInventory(string adUnitId, bool canvas)
        {
            if (string.IsNullOrEmpty(adUnitId))
            {
                _dashboardStatus = "Inventory has no Ad Unit ID.";
                RefreshUi();
                return;
            }

            _newPlacementAdUnitId = adUnitId;
            GameObject placement = canvas ? CreateCanvasPlacement() : CreatePlanePlacement();
            SelectCreatedPlacement(placement);
            _dashboardStatus = "";
            RefreshUi();
        }

        private void BuildSettingsTab(VisualElement parent)
        {
            VisualElement dashboardGrid = new VisualElement();
            dashboardGrid.AddToClassList("ea-grid");
            dashboardGrid.Add(CreateDashboardConnectionPanel());
            dashboardGrid.Add(CreateDashboardProjectPanel());
            parent.Add(dashboardGrid);

            parent.Add(CreateManagedScenesPanel());
            BuildSdkSettingsComponent(parent);
        }

        private void BuildSdkSettingsComponent(VisualElement parent)
        {
            VisualElement panel = CreatePanel("SDK Settings");

            VisualElement enableToggle = CreateSwitch("Enable SDK", _easterAdEnabled, value =>
            {
                _easterAdEnabled = value;
                RefreshUi();
            });
            panel.Add(enableToggle);

            VisualElement fields = new VisualElement();
            fields.AddToClassList("ea-stack");
            fields.SetEnabled(_easterAdEnabled);
            fields.Add(CreateTextField("Game ID", _tempGameId, value => _tempGameId = value));
            fields.Add(CreatePasswordField("SDK Key", _tempSdkKey, value => _tempSdkKey = value));

            VisualElement logToggle = CreateSwitch("Enable Log", _tempLogEnable, value => _tempLogEnable = value);
            fields.Add(logToggle);

            VisualElement customInfoToggle = CreateSwitch("Custom Device Info", _customInfoEnable, value =>
            {
                _customInfoEnable = value;
                RefreshUi();
            });
            fields.Add(customInfoToggle);

            VisualElement customInfoFields = new VisualElement();
            customInfoFields.AddToClassList("ea-stack");
            customInfoFields.SetEnabled(_customInfoEnable);
            EnumField deviceTypeField = new EnumField("Device Type", _customDeviceType);
            EnumField platformField = new EnumField("Platform", _customPlatform);
            EnumField languageField = new EnumField("Language", _customLanguage);
            deviceTypeField.AddToClassList("ea-field");
            platformField.AddToClassList("ea-field");
            languageField.AddToClassList("ea-field");
            deviceTypeField.RegisterValueChangedCallback(evt => _customDeviceType = (DeviceType)evt.newValue);
            platformField.RegisterValueChangedCallback(evt => _customPlatform = (RuntimePlatform)evt.newValue);
            languageField.RegisterValueChangedCallback(evt => _customLanguage = (SystemLanguage)evt.newValue);
            customInfoFields.Add(deviceTypeField);
            customInfoFields.Add(platformField);
            customInfoFields.Add(languageField);
            fields.Add(customInfoFields);
            panel.Add(fields);

            VisualElement savedValues = CreateSplitRow();
            savedValues.Add(CreateLabel("Saved Game ID: " + (string.IsNullOrEmpty(_currentGameId) ? "None" : _currentGameId), "ea-muted"));
            savedValues.Add(CreateLabel("Saved SDK Key: " + MaskSecret(_currentSdkKey), "ea-muted"));
            panel.Add(savedValues);

            VisualElement actions = CreateButtonRow();
            actions.Add(CreateActionButton("Save Settings", () =>
            {
                SaveSettings();
                RefreshUi();
            }));

            Button reinitializeButton = CreateSecondaryButton("Re-Initialize SDK", () => EasterAdSdk.Instance.ReInitialize());
            reinitializeButton.SetEnabled(Application.isPlaying && EasterAdSdk.OnceInitialized);
            actions.Add(reinitializeButton);
            panel.Add(actions);

            parent.Add(panel);
        }

        private VisualElement CreateManagedScenesPanel()
        {
            VisualElement panel = CreatePanel("Managed Scenes");
            panel.Add(CreateLabel("Ad Unit ID duplicates are checked only inside these scenes. Scenes with no EasterAd placements are removed automatically when scanned.", "ea-muted"));

            VisualElement actions = CreateButtonRow();
            actions.Add(CreateActionButton("Add Current Scene", () =>
            {
                if (TryGetScenePath(SceneManager.GetActiveScene(), out string scenePath))
                {
                    AddManagedScenePath(scenePath);
                    _dashboardStatus = "Current scene added to Managed Scenes.";
                }
                else
                {
                    _dashboardStatus = "Save the current scene before adding it to Managed Scenes.";
                }

                RefreshUi();
            }));
            actions.Add(CreateSecondaryButton("Import Build Settings", () =>
            {
                int addedCount = AddBuildSettingsScenesToManagedScenes();
                _dashboardStatus = addedCount == 0
                    ? "No new Build Settings scenes were added."
                    : "Added " + addedCount + " Build Settings scenes to Managed Scenes.";
                RefreshUi();
            }));
            actions.Add(CreateSecondaryButton("Scan Scenes", () =>
            {
                ManagedSceneScanResult scan = ScanManagedScenes(true);
                List<ManagedSceneConflict> conflicts = BuildManagedSceneConflicts(scan);
                _dashboardStatus = BuildManagedSceneScanStatus(scan, conflicts);
                RefreshUi();
            }));
            panel.Add(actions);

            if (_managedScenePaths.Count == 0)
            {
                VisualElement empty = new VisualElement();
                empty.AddToClassList("ea-empty");
                empty.Add(CreateLabel("No managed scenes", "ea-empty-title"));
                empty.Add(CreateLabel("Creating or restoring an inventory placement adds the target scene automatically.", "ea-muted"));
                panel.Add(empty);
                return panel;
            }

            for (int i = 0; i < _managedScenePaths.Count; i++)
            {
                panel.Add(CreateManagedSceneRow(_managedScenePaths[i]));
            }

            return panel;
        }

        private VisualElement CreateManagedSceneRow(string scenePath)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("ea-managed-scene-row");

            VisualElement titleBlock = new VisualElement();
            titleBlock.AddToClassList("ea-placement-title-block");
            titleBlock.Add(CreateLabel(GetSceneDisplayName(scenePath), "ea-placement-title"));
            titleBlock.Add(CreateLabel(scenePath, "ea-placement-subtitle"));
            row.Add(titleBlock);

            VisualElement actions = CreateButtonRow();
            Button openButton = CreateSecondaryButton("Open", () =>
            {
                OpenManagedScene(scenePath);
            });
            openButton.SetEnabled(File.Exists(scenePath));
            actions.Add(openButton);
            actions.Add(CreateSecondaryButton("Remove", () =>
            {
                RemoveManagedScenePath(scenePath);
                _dashboardStatus = "Scene removed from Managed Scenes.";
                RefreshUi();
            }));
            row.Add(actions);

            return row;
        }

        private void BuildPlacementsTab(VisualElement parent)
        {
            VisualElement createPanel = CreatePanel("Create Placement");
            TextField adUnitField = CreateTextField("Ad Unit ID", _newPlacementAdUnitId, value => _newPlacementAdUnitId = value);
            createPanel.Add(adUnitField);

            VisualElement actions = CreateButtonRow();
            actions.Add(CreateActionButton("Create Plane", () =>
            {
                GameObject placement = CreatePlanePlacement();
                SelectCreatedPlacement(placement);
                RefreshUi();
            }));
            actions.Add(CreateActionButton("Create CanvasItem", () =>
            {
                GameObject placement = CreateCanvasPlacement();
                SelectCreatedPlacement(placement);
                RefreshUi();
            }));

            GameObject selectedObject = Selection.activeGameObject;
            Button addPlaneButton = CreateSecondaryButton("Add Plane to Selection", () =>
            {
                AddPlacementComponentToSelection<global::EasterAd.Plane>(Selection.activeGameObject);
                RefreshUi();
            });
            Button addCanvasButton = CreateSecondaryButton("Add CanvasItem to Selection", () =>
            {
                AddPlacementComponentToSelection<global::EasterAd.CanvasItem>(Selection.activeGameObject);
                RefreshUi();
            });
            addPlaneButton.SetEnabled(selectedObject != null);
            addCanvasButton.SetEnabled(selectedObject != null && selectedObject.GetComponent<RectTransform>() != null);
            actions.Add(addPlaneButton);
            actions.Add(addCanvasButton);
            createPanel.Add(actions);
            parent.Add(createPanel);

            parent.Add(CreatePlacementListPanel());
            parent.Add(CreatePrefabPanel());
        }

        private VisualElement CreatePlacementListPanel()
        {
            VisualElement panel = CreatePanel("Scene Placements");
            Item[] items = FindSceneItems();
            if (items.Length == 0)
            {
                VisualElement empty = new VisualElement();
                empty.AddToClassList("ea-empty");
                empty.Add(CreateLabel("No placements in this scene", "ea-empty-title"));
                empty.Add(CreateLabel("Create a Plane or CanvasItem placement to start assigning dashboard ad units.", "ea-muted"));
                panel.Add(empty);
                return panel;
            }

            for (int i = 0; i < items.Length; i++)
            {
                VisualElement row = CreatePlacementRow(items[i], i + 1, items);
                if (row != null)
                {
                    panel.Add(row);
                }
            }

            return panel;
        }

        private VisualElement CreatePlacementRow(Item item, int index, Item[] allItems)
        {
            if (item == null) { return null; }

            VisualElement row = new VisualElement();
            row.AddToClassList("ea-placement-row");

            VisualElement header = new VisualElement();
            header.AddToClassList("ea-placement-header");
            VisualElement titleBlock = new VisualElement();
            titleBlock.AddToClassList("ea-placement-title-block");
            titleBlock.Add(CreateLabel(index + ". " + item.gameObject.name, "ea-placement-title"));
            titleBlock.Add(CreateLabel(item.GetType().Name, "ea-placement-subtitle"));
            header.Add(titleBlock);

            VisualElement actions = CreateButtonRow();
            actions.Add(CreateSecondaryButton("Select", () =>
            {
                Selection.activeGameObject = item.gameObject;
                EditorGUIUtility.PingObject(item.gameObject);
                RefreshUi();
            }));
            actions.Add(CreateSecondaryButton("Remove", () =>
            {
                if (!EditorUtility.DisplayDialog("Remove EasterAd Placement", "Remove '" + item.gameObject.name + "' from the scene?", "Remove", "Cancel")) { return; }

                Scene scene = item.gameObject.scene;
                Undo.DestroyObjectImmediate(item.gameObject);
                MarkSceneDirty(scene);
                RefreshUi();
            }));
            header.Add(actions);
            row.Add(header);

            row.Add(CreateTextField("Ad Unit ID", GetSerializedString(item, "adUnitId"), value => SetSerializedString(item, "adUnitId", value)));

            VisualElement switchGrid = new VisualElement();
            switchGrid.AddToClassList("ea-switch-grid");
            switchGrid.Add(CreateSerializedSwitch(item, "loadOnStart", "Load On Start"));
            switchGrid.Add(CreateSerializedSwitch(item, "allowImpression", "Allow Impression"));
            switchGrid.Add(CreateSerializedSwitch(item, "interactable", "Interactable"));
            switchGrid.Add(CreateSerializedSwitch(item, "enableRefresh", "Enable Refresh"));
            switchGrid.Add(CreateSerializedSwitch(item, "hideDuringCapture", "Hide During Capture"));
            row.Add(switchGrid);

            row.Add(CreateFloatField("Refresh Time", GetSerializedFloat(item, "refreshTime"), value => SetSerializedFloat(item, "refreshTime", value)));

            if (Application.isPlaying)
            {
                VisualElement runtime = CreateSplitRow();
                runtime.Add(CreateLabel("Initialized: " + (item.IsInitialized ? "Yes" : "No"), "ea-muted"));
                string statusText = item.IsInitialized ? item.Client.GetStatus().ToString() : "Not Initialized";
                runtime.Add(CreateLabel("Status: " + statusText, "ea-muted"));
                row.Add(runtime);

                VisualElement runtimeActions = CreateButtonRow();
                Button loadButton = CreateSecondaryButton("Load", item.Load);
                Button startButton = CreateSecondaryButton("Start Interaction", () =>
                {
                    string interactionUrl = item.StartInteraction();
                    if (!string.IsNullOrEmpty(interactionUrl)) { Application.OpenURL(interactionUrl); }
                });
                Button endButton = CreateSecondaryButton("End Interaction", item.EndInteraction);
                loadButton.SetEnabled(item.IsInitialized);
                startButton.SetEnabled(item.IsInitialized);
                endButton.SetEnabled(item.IsInitialized);
                runtimeActions.Add(loadButton);
                runtimeActions.Add(startButton);
                runtimeActions.Add(endButton);
                row.Add(runtimeActions);
            }

            return row;
        }

        private VisualElement CreatePrefabPanel()
        {
            VisualElement panel = CreatePanel("Ad Prefab");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlaneItemPrefabPath);
            if (prefab == null)
            {
                panel.Add(CreateLabel("Prefab not found. Package may need reinstallation.", "ea-callout ea-callout-warning"));
                return panel;
            }

            panel.Add(CreateLabel("PlaneItem.prefab is available from the generated EasterAd package.", "ea-muted"));
            panel.Add(CreateSecondaryButton("Select Prefab", () =>
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }));
            return panel;
        }

        private VisualElement CreateSerializedSwitch(Item item, string propertyName, string label)
        {
            return CreateSwitch(label, GetSerializedBool(item, propertyName), value => SetSerializedBool(item, propertyName, value));
        }

        private static string GetSerializedString(Item item, string propertyName)
        {
            SerializedObject serializedItem = new SerializedObject(item);
            SerializedProperty property = serializedItem.FindProperty(propertyName);
            return property == null ? "" : property.stringValue;
        }

        private static bool GetSerializedBool(Item item, string propertyName)
        {
            SerializedObject serializedItem = new SerializedObject(item);
            SerializedProperty property = serializedItem.FindProperty(propertyName);
            return property != null && property.boolValue;
        }

        private static float GetSerializedFloat(Item item, string propertyName)
        {
            SerializedObject serializedItem = new SerializedObject(item);
            SerializedProperty property = serializedItem.FindProperty(propertyName);
            return property == null ? 0f : property.floatValue;
        }

        private static void SetSerializedString(Item item, string propertyName, string value)
        {
            SerializedObject serializedItem = BeginPlacementEdit(item, propertyName);
            if (serializedItem == null) { return; }

            SerializedProperty property = serializedItem.FindProperty(propertyName);
            property.stringValue = value;
            EndPlacementEdit(item, serializedItem);
        }

        private static void SetSerializedBool(Item item, string propertyName, bool value)
        {
            SerializedObject serializedItem = BeginPlacementEdit(item, propertyName);
            if (serializedItem == null) { return; }

            SerializedProperty property = serializedItem.FindProperty(propertyName);
            property.boolValue = value;
            EndPlacementEdit(item, serializedItem);
        }

        private static void SetSerializedFloat(Item item, string propertyName, float value)
        {
            SerializedObject serializedItem = BeginPlacementEdit(item, propertyName);
            if (serializedItem == null) { return; }

            SerializedProperty property = serializedItem.FindProperty(propertyName);
            property.floatValue = value;
            EndPlacementEdit(item, serializedItem);
        }

        private static SerializedObject BeginPlacementEdit(Item item, string propertyName)
        {
            if (item == null) { return null; }

            SerializedObject serializedItem = new SerializedObject(item);
            serializedItem.Update();
            SerializedProperty property = serializedItem.FindProperty(propertyName);
            if (property == null) { return null; }

            Undo.RecordObject(item, "Edit EasterAd Placement");
            return serializedItem;
        }

        private static void EndPlacementEdit(Item item, SerializedObject serializedItem)
        {
            serializedItem.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            MarkSceneDirty(item.gameObject.scene);
        }

        private void BuildDiagnosticsTab(VisualElement parent)
        {
            BuildDiagnosticsStatusComponent(parent);
            BuildRenderPipelineMigrationComponent(parent);
        }

        private void BuildDiagnosticsStatusComponent(VisualElement parent)
        {
            VisualElement panel = CreatePanel("Status");
            panel.Add(CreateLabel("Pipeline: " + DetectRenderPipeline(), "ea-muted"));
            panel.Add(CreateLabel("AdSegmentation Objects: " + FindObjectsByType<AdSegmentationObject>(FindObjectsSortMode.InstanceID).Length, "ea-muted"));
            panel.Add(CreateLabel("Package Prefab: " + (AssetDatabase.LoadAssetAtPath<GameObject>(PlaneItemPrefabPath) == null ? "Missing" : "Available"), "ea-muted"));
            parent.Add(panel);
        }

        private void BuildRenderPipelineMigrationComponent(VisualElement parent)
        {
            parent.Add(CreateImguiPanel("Render Pipeline and Migration", () =>
            {
                DrawRenderPipelineSetupUI();
                DrawAdSegmentationCapacityUI();
                DrawMigrationSection();
            }));
        }

        private VisualElement CreateImguiPanel(string title, Action drawAction)
        {
            VisualElement panel = CreatePanel(title);
            IMGUIContainer container = new IMGUIContainer(() =>
            {
                drawAction();
            });
            container.AddToClassList("ea-imgui");
            panel.Add(container);
            return panel;
        }

        private void LoadDashboardPreferences()
        {
            _dashboardBaseUrl = EditorPrefs.GetString(DashboardBaseUrlPrefsKey, EasterAdDashboardClient.DefaultBaseUrl);
            _dashboardApiKey = EditorPrefs.GetString(DashboardApiKeyPrefsKey, "");
            _dashboardOrganizationId = EditorPrefs.GetString(DashboardOrganizationIdPrefsKey, "");
            _dashboardGameId = EditorPrefs.GetString(DashboardGameIdPrefsKey, "");
            LoadDashboardMenuPreferences();
        }

        private void SaveDashboardPreferences()
        {
            EasterAdDashboardOrganization organization = GetSelectedDashboardOrganization();
            EasterAdDashboardGame game = GetSelectedDashboardGame();
            if (organization != null)
            {
                _dashboardOrganizationId = organization._id ?? "";
            }

            if (game != null)
            {
                _dashboardGameId = game._id ?? "";
            }

            EditorPrefs.SetString(DashboardBaseUrlPrefsKey, string.IsNullOrWhiteSpace(_dashboardBaseUrl) ? EasterAdDashboardClient.DefaultBaseUrl : _dashboardBaseUrl.TrimEnd('/'));
            EditorPrefs.SetString(DashboardApiKeyPrefsKey, _dashboardApiKey ?? "");
            EditorPrefs.SetString(DashboardOrganizationIdPrefsKey, _dashboardOrganizationId ?? "");
            EditorPrefs.SetString(DashboardGameIdPrefsKey, _dashboardGameId ?? "");
        }

        private void LoadDashboardMenuPreferences()
        {
            _dashboardFolders.Clear();

            string layoutJson = EditorPrefs.GetString(DashboardMenuLayoutPrefsKey, "");
            if (!string.IsNullOrEmpty(layoutJson) && TryLoadDashboardMenuLayout(layoutJson))
            {
                return;
            }

            LoadDefaultDashboardMenuLayout();
        }

        private void SaveDashboardMenuPreferences()
        {
            DashboardMenuLayout layout = new DashboardMenuLayout();
            for (int i = 0; i < _dashboardFolders.Count; i++)
            {
                DashboardFolderDefinition folder = _dashboardFolders[i];
                DashboardMenuFolderState folderState = new DashboardMenuFolderState
                {
                    key = folder.Key,
                    title = NormalizeDashboardFolderTitle(folder.Title)
                };

                for (int componentIndex = 0; componentIndex < folder.Components.Count; componentIndex++)
                {
                    folderState.components.Add(folder.Components[componentIndex].ToString());
                }

                layout.folders.Add(folderState);
            }

            EditorPrefs.SetString(DashboardMenuLayoutPrefsKey, JsonUtility.ToJson(layout));
        }

        private bool TryLoadDashboardMenuLayout(string layoutJson)
        {
            try
            {
                DashboardMenuLayout layout = JsonUtility.FromJson<DashboardMenuLayout>(layoutJson);
                if (layout == null || layout.folders == null) { return false; }

                HashSet<EasterAdDashboardComponent> assignedComponents = new HashSet<EasterAdDashboardComponent>();
                for (int i = 0; i < layout.folders.Count; i++)
                {
                    DashboardMenuFolderState folderState = layout.folders[i];
                    if (folderState == null) { continue; }

                    string folderKey = string.IsNullOrEmpty(folderState.key) ? "folder-" + Guid.NewGuid().ToString("N") : folderState.key;
                    string folderTitle = NormalizeDashboardFolderTitle(folderState.title);
                    DashboardFolderDefinition folder = new DashboardFolderDefinition(folderKey, folderTitle, "Custom dashboard folder.");
                    if (folderState.components != null)
                    {
                        for (int componentIndex = 0; componentIndex < folderState.components.Count; componentIndex++)
                        {
                            if (!TryParseDashboardComponent(folderState.components[componentIndex], out EasterAdDashboardComponent component)) { continue; }
                            if (!assignedComponents.Add(component)) { continue; }
                            folder.Components.Add(component);
                        }
                    }

                    _dashboardFolders.Add(folder);
                }

                if (_dashboardFolders.Count == 0) { return false; }
                AssignMissingDashboardComponents(assignedComponents);
                if (!HasDashboardFolder(_activeFolderKey))
                {
                    _activeFolderKey = GetFirstDashboardFolder().Key;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[EasterAd] Failed to read dashboard menu layout: " + ex.Message);
                _dashboardFolders.Clear();
                return false;
            }
        }

        private void AssignMissingDashboardComponents(HashSet<EasterAdDashboardComponent> assignedComponents)
        {
            if (_dashboardFolders.Count == 0)
            {
                _dashboardFolders.Add(new DashboardFolderDefinition("folder-" + Guid.NewGuid().ToString("N"), "Menu", "Custom dashboard folder."));
            }

            DashboardFolderDefinition targetFolder = _dashboardFolders[0];
            EasterAdDashboardComponent[] components = GetDashboardComponentValues();
            for (int i = 0; i < components.Length; i++)
            {
                if (assignedComponents.Contains(components[i])) { continue; }

                targetFolder.Components.Add(components[i]);
                assignedComponents.Add(components[i]);
            }
        }

        private void LoadDefaultDashboardMenuLayout()
        {
            _dashboardFolders.Clear();
            DashboardFolderDefinition[] defaultFolders = CreateDefaultDashboardFolders();
            for (int i = 0; i < defaultFolders.Length; i++)
            {
                _dashboardFolders.Add(defaultFolders[i]);
            }

            if (!HasDashboardFolder(_activeFolderKey))
            {
                _activeFolderKey = GetFirstDashboardFolder().Key;
            }
        }

        private static bool TryParseDashboardComponent(string value, out EasterAdDashboardComponent component)
        {
            return Enum.TryParse(value, out component) && Enum.IsDefined(typeof(EasterAdDashboardComponent), component);
        }

        private void LoadManagedSceneRegistry()
        {
            _managedScenePaths.Clear();
            string path = GetManagedSceneRegistryFullPath();
            if (File.Exists(path))
            {
                try
                {
                    ManagedSceneRegistry registry = JsonUtility.FromJson<ManagedSceneRegistry>(File.ReadAllText(path));
                    if (registry != null && registry.managedScenes != null)
                    {
                        for (int i = 0; i < registry.managedScenes.Count; i++)
                        {
                            AddManagedScenePath(registry.managedScenes[i], false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[EasterAd] Failed to read managed scene registry: " + ex.Message);
                }
            }

            NormalizeManagedScenePaths();
        }

        private void SaveManagedSceneRegistry()
        {
            NormalizeManagedScenePaths();
            ManagedSceneRegistry registry = new ManagedSceneRegistry();
            registry.managedScenes.AddRange(_managedScenePaths);

            string path = GetManagedSceneRegistryFullPath();
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonUtility.ToJson(registry, true));
        }

        private static string GetManagedSceneRegistryFullPath()
        {
            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ManagedSceneRegistryPath));
        }

        private bool AddManagedScenePath(string scenePath, bool save = true)
        {
            string normalizedPath = NormalizeProjectPath(scenePath);
            if (string.IsNullOrEmpty(normalizedPath)) { return false; }

            for (int i = 0; i < _managedScenePaths.Count; i++)
            {
                if (string.Equals(_managedScenePaths[i], normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            _managedScenePaths.Add(normalizedPath);
            NormalizeManagedScenePaths();
            if (save)
            {
                SaveManagedSceneRegistry();
            }

            return true;
        }

        private bool RemoveManagedScenePath(string scenePath, bool save = true)
        {
            string normalizedPath = NormalizeProjectPath(scenePath);
            bool removed = false;
            for (int i = _managedScenePaths.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(_managedScenePaths[i], normalizedPath, StringComparison.OrdinalIgnoreCase)) { continue; }
                _managedScenePaths.RemoveAt(i);
                removed = true;
            }

            if (removed && save)
            {
                SaveManagedSceneRegistry();
            }

            return removed;
        }

        private void NormalizeManagedScenePaths()
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> normalizedPaths = new List<string>();
            for (int i = 0; i < _managedScenePaths.Count; i++)
            {
                string normalizedPath = NormalizeProjectPath(_managedScenePaths[i]);
                if (string.IsNullOrEmpty(normalizedPath) || !seen.Add(normalizedPath)) { continue; }
                normalizedPaths.Add(normalizedPath);
            }

            normalizedPaths.Sort(StringComparer.OrdinalIgnoreCase);
            _managedScenePaths = normalizedPaths;
        }

        private int AddBuildSettingsScenesToManagedScenes()
        {
            int addedCount = 0;
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                EditorBuildSettingsScene scene = scenes[i];
                if (scene == null || string.IsNullOrEmpty(scene.path) || !scene.enabled) { continue; }
                if (AddManagedScenePath(scene.path, false))
                {
                    addedCount++;
                }
            }

            if (addedCount > 0)
            {
                SaveManagedSceneRegistry();
            }

            return addedCount;
        }

        private void OpenManagedScene(string scenePath)
        {
            if (!File.Exists(scenePath))
            {
                _dashboardStatus = "Managed scene file was not found.";
                RefreshUi();
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }
            EditorSceneManager.OpenScene(scenePath);
            RefreshUi();
        }

        private ManagedSceneScanResult ScanManagedScenes(bool pruneEmptyScenes)
        {
            NormalizeManagedScenePaths();
            ManagedSceneScanResult result = new ManagedSceneScanResult();
            Dictionary<string, List<Item>> loadedItemsByScene = BuildLoadedManagedSceneItemMap();

            for (int i = _managedScenePaths.Count - 1; i >= 0; i--)
            {
                string scenePath = _managedScenePaths[i];
                List<ManagedPlacementReference> scenePlacements = new List<ManagedPlacementReference>();
                if (loadedItemsByScene.TryGetValue(scenePath, out List<Item> loadedItems))
                {
                    for (int itemIndex = 0; itemIndex < loadedItems.Count; itemIndex++)
                    {
                        Item item = loadedItems[itemIndex];
                        if (item == null) { continue; }
                        scenePlacements.Add(new ManagedPlacementReference
                        {
                            ScenePath = scenePath,
                            ObjectName = item.gameObject.name,
                            AdUnitId = item.adUnitId ?? "",
                            Item = item
                        });
                    }
                }
                else
                {
                    scenePlacements.AddRange(ScanSceneFilePlacements(scenePath));
                }

                if (scenePlacements.Count == 0)
                {
                    if (pruneEmptyScenes && RemoveManagedScenePath(scenePath, false))
                    {
                        result.RemovedScenePaths.Add(scenePath);
                    }

                    continue;
                }

                result.Placements.AddRange(scenePlacements);
            }

            if (pruneEmptyScenes && result.RemovedScenePaths.Count > 0)
            {
                SaveManagedSceneRegistry();
            }

            return result;
        }

        private Dictionary<string, List<Item>> BuildLoadedManagedSceneItemMap()
        {
            Dictionary<string, List<Item>> itemsByScene = new Dictionary<string, List<Item>>(StringComparer.OrdinalIgnoreCase);
            Item[] items = FindSceneItems();
            for (int i = 0; i < items.Length; i++)
            {
                Item item = items[i];
                if (item == null || !TryGetScenePath(item.gameObject.scene, out string scenePath)) { continue; }
                if (!IsManagedScenePath(scenePath)) { continue; }

                if (!itemsByScene.TryGetValue(scenePath, out List<Item> sceneItems))
                {
                    sceneItems = new List<Item>();
                    itemsByScene.Add(scenePath, sceneItems);
                }

                sceneItems.Add(item);
            }

            return itemsByScene;
        }

        private List<ManagedPlacementReference> ScanSceneFilePlacements(string scenePath)
        {
            List<ManagedPlacementReference> placements = new List<ManagedPlacementReference>();
            if (!File.Exists(scenePath)) { return placements; }

            try
            {
                string[] lines = File.ReadAllLines(scenePath);
                for (int i = 0; i < lines.Length; i++)
                {
                    string trimmed = lines[i].Trim();
                    if (!trimmed.StartsWith("adUnitId:", StringComparison.Ordinal)) { continue; }
                    placements.Add(new ManagedPlacementReference
                    {
                        ScenePath = scenePath,
                        ObjectName = "",
                        AdUnitId = NormalizeSerializedStringValue(trimmed.Substring("adUnitId:".Length))
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[EasterAd] Failed to scan managed scene '" + scenePath + "': " + ex.Message);
            }

            return placements;
        }

        private List<ManagedSceneConflict> BuildManagedSceneConflicts(ManagedSceneScanResult scan)
        {
            Dictionary<string, List<ManagedPlacementReference>> placementsByAdUnitId = new Dictionary<string, List<ManagedPlacementReference>>(StringComparer.Ordinal);
            for (int i = 0; i < scan.Placements.Count; i++)
            {
                ManagedPlacementReference placement = scan.Placements[i];
                string adUnitId = GetManagedPlacementAdUnitId(placement);
                if (string.IsNullOrEmpty(adUnitId)) { continue; }

                if (!placementsByAdUnitId.TryGetValue(adUnitId, out List<ManagedPlacementReference> placements))
                {
                    placements = new List<ManagedPlacementReference>();
                    placementsByAdUnitId.Add(adUnitId, placements);
                }

                placements.Add(placement);
            }

            List<ManagedSceneConflict> conflicts = new List<ManagedSceneConflict>();
            foreach (KeyValuePair<string, List<ManagedPlacementReference>> pair in placementsByAdUnitId)
            {
                if (pair.Value.Count < 2) { continue; }
                conflicts.Add(new ManagedSceneConflict
                {
                    AdUnitId = pair.Key,
                    Placements = pair.Value
                });
            }

            return conflicts;
        }

        private static string GetManagedPlacementAdUnitId(ManagedPlacementReference placement)
        {
            if (placement == null) { return ""; }
            if (placement.Item != null) { return placement.Item.adUnitId ?? ""; }
            return placement.AdUnitId ?? "";
        }

        private static string NormalizeSerializedStringValue(string value)
        {
            string normalizedValue = value == null ? "" : value.Trim();
            if (normalizedValue.Length >= 2 &&
                ((normalizedValue[0] == '"' && normalizedValue[normalizedValue.Length - 1] == '"') ||
                 (normalizedValue[0] == '\'' && normalizedValue[normalizedValue.Length - 1] == '\'')))
            {
                normalizedValue = normalizedValue.Substring(1, normalizedValue.Length - 2);
            }

            return normalizedValue;
        }

        private string BuildManagedSceneScanStatus(ManagedSceneScanResult scan, List<ManagedSceneConflict> conflicts)
        {
            if (conflicts.Count > 0)
            {
                return BuildManagedSceneConflictSummary(conflicts);
            }

            if (scan.RemovedScenePaths.Count > 0)
            {
                return BuildRemovedManagedScenesSummary(scan.RemovedScenePaths);
            }

            return "Managed Scenes scanned. No duplicate Ad Unit IDs were found.";
        }

        private string BuildManagedSceneConflictSummary(List<ManagedSceneConflict> conflicts)
        {
            if (conflicts.Count == 0) { return ""; }

            ManagedSceneConflict conflict = conflicts[0];
            string summary = "Ad Unit ID " + ShortenId(conflict.AdUnitId) + " is used " + conflict.Placements.Count + " times in Managed Scenes: " + BuildManagedPlacementListText(conflict.Placements) + ".";
            if (conflicts.Count > 1)
            {
                summary += " +" + (conflicts.Count - 1) + " more conflicts.";
            }

            return summary;
        }

        private static string BuildRemovedManagedScenesSummary(List<string> removedScenePaths)
        {
            if (removedScenePaths.Count == 0) { return ""; }

            string summary = "Removed " + removedScenePaths.Count + " Managed Scene";
            summary += removedScenePaths.Count == 1 ? " with no placements: " : "s with no placements: ";
            int count = Math.Min(removedScenePaths.Count, 3);
            for (int i = 0; i < count; i++)
            {
                if (i > 0) { summary += ", "; }
                summary += GetSceneDisplayName(removedScenePaths[i]);
            }

            if (removedScenePaths.Count > count)
            {
                summary += ", +" + (removedScenePaths.Count - count) + " more";
            }

            return summary + ".";
        }

        private static string BuildManagedPlacementListText(List<ManagedPlacementReference> placements)
        {
            StringBuilder builder = new StringBuilder();
            int count = Math.Min(placements.Count, 3);
            for (int i = 0; i < count; i++)
            {
                if (i > 0) { builder.Append(", "); }
                builder.Append(GetSceneDisplayName(placements[i].ScenePath));
                if (!string.IsNullOrEmpty(placements[i].ObjectName))
                {
                    builder.Append(" / ");
                    builder.Append(placements[i].ObjectName);
                }
            }

            if (placements.Count > count)
            {
                builder.Append(", +");
                builder.Append(placements.Count - count);
                builder.Append(" more");
            }

            return builder.ToString();
        }

        private bool IsManagedScenePath(string scenePath)
        {
            string normalizedPath = NormalizeProjectPath(scenePath);
            for (int i = 0; i < _managedScenePaths.Count; i++)
            {
                if (string.Equals(_managedScenePaths[i], normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool RegisterManagedSceneForPlacement(Scene scene)
        {
            if (!TryGetScenePath(scene, out string scenePath))
            {
                _dashboardStatus = "Placement created. Save the scene to include it in Managed Scenes.";
                return false;
            }

            return AddManagedScenePath(scenePath);
        }

        private static bool TryGetScenePath(Scene scene, out string scenePath)
        {
            scenePath = "";
            if (!scene.IsValid()) { return false; }

            scenePath = NormalizeProjectPath(scene.path);
            return !string.IsNullOrEmpty(scenePath);
        }

        private static string NormalizeProjectPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) { return ""; }

            string normalizedPath = path.Replace('\\', '/').Trim();
            string projectRoot = Directory.GetCurrentDirectory().Replace('\\', '/').TrimEnd('/');
            if (normalizedPath.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                normalizedPath = normalizedPath.Substring(projectRoot.Length + 1);
            }

            return normalizedPath;
        }

        private static string GetSceneDisplayName(string scenePath)
        {
            string normalizedPath = NormalizeProjectPath(scenePath);
            if (string.IsNullOrEmpty(normalizedPath)) { return "Unsaved Scene"; }
            string fileName = Path.GetFileNameWithoutExtension(normalizedPath);
            return string.IsNullOrEmpty(fileName) ? normalizedPath : fileName;
        }

        private void ForgetDashboardKey()
        {
            EditorPrefs.DeleteKey(DashboardApiKeyPrefsKey);
            EditorPrefs.DeleteKey(DashboardOrganizationIdPrefsKey);
            EditorPrefs.DeleteKey(DashboardGameIdPrefsKey);
            _dashboardApiKey = "";
            _dashboardOrganizationId = "";
            _dashboardGameId = "";
            _dashboardOrganizations.Clear();
            _dashboardGames.Clear();
            _dashboardAdUnits.Clear();
            _dashboardAutoSyncFailureCount = 0;
            _dashboardAutoSyncPaused = false;
            _dashboardAutoSyncError = "";
            _dashboardStatus = "Dashboard API key removed from EditorPrefs.";
            RefreshUi();
        }

        private void OpenDashboard()
        {
            Application.OpenURL((string.IsNullOrWhiteSpace(_dashboardBaseUrl) ? EasterAdDashboardClient.DefaultBaseUrl : _dashboardBaseUrl.TrimEnd('/')) + "/dashboard");
        }

        private async void RefreshDashboardAsync()
        {
            await SyncDashboardAsync(true);
        }

        private async Task SyncDashboardAsync(bool userInitiated)
        {
            if (_dashboardUpdateBusy || _dashboardActionBusy || _dashboardAutoSyncPaused) { return; }
            if (string.IsNullOrWhiteSpace(_dashboardApiKey))
            {
                if (userInitiated)
                {
                    _dashboardStatus = "Dashboard API key is required.";
                    RefreshUi();
                }

                return;
            }

            _dashboardUpdateBusy = true;
            if (userInitiated)
            {
                _dashboardStatus = "";
            }

            RefreshUi();

            try
            {
                using (EasterAdDashboardClient client = CreateDashboardClient())
                {
                    await SyncDashboardDataAsync(client, userInitiated);
                    SaveDashboardPreferences();
                    RegisterDashboardSyncSuccess();
                }
            }
            catch (Exception ex)
            {
                RegisterDashboardSyncFailure(ex);
            }
            finally
            {
                _dashboardUpdateBusy = false;
                RefreshUi();
            }
        }

        private async Task WaitForDashboardUpdateAsync()
        {
            while (_dashboardUpdateBusy)
            {
                await Task.Delay(50);
            }
        }

        private async Task SyncDashboardDataAsync(EasterAdDashboardClient client, bool updateStatus)
        {
            EasterAdDashboardUser user = await client.GetCurrentUserAsync();
            _dashboardOrganizations = await client.GetOrganizationsAsync(user);
            _dashboardOrganizations.RemoveAll(organization => organization == null);
            _dashboardOrganizationIndex = FindDashboardOrganizationIndex(_dashboardOrganizationId);
            _dashboardGames.Clear();
            _dashboardAdUnits.Clear();

            if (_dashboardOrganizations.Count > 0)
            {
                await LoadDashboardGamesAsync(client, updateStatus);
            }
            else if (updateStatus)
            {
                _dashboardStatus = "Dashboard connected, but this API key has no developer organizations.";
            }
        }

        private async Task LoadDashboardGamesAsync(EasterAdDashboardClient client, bool updateStatus)
        {
            EasterAdDashboardOrganization organization = GetSelectedDashboardOrganization();
            if (organization == null) { return; }

            _dashboardGames = await client.GetGamesAsync(organization._id);
            _dashboardGames.RemoveAll(game => game == null || !string.IsNullOrEmpty(game.deletedAt));
            _dashboardGameIndex = FindDashboardGameIndex(_dashboardGameId);
            _dashboardAdUnits.Clear();
            _dashboardAdUnitIndex = 0;

            if (_dashboardGames.Count > 0)
            {
                await LoadDashboardAdUnitsAsync(client);
            }
            else if (updateStatus)
            {
                _dashboardStatus = "Selected dashboard organization has no games.";
            }
        }

        private async Task LoadDashboardAdUnitsAsync(EasterAdDashboardClient client)
        {
            EasterAdDashboardOrganization organization = GetSelectedDashboardOrganization();
            EasterAdDashboardGame game = GetSelectedDashboardGame();
            if (organization == null || game == null) { return; }

            _dashboardAdUnits = await client.GetAdUnitsAsync(organization._id, game._id);
            _dashboardAdUnits.RemoveAll(adUnit => adUnit == null);
            _dashboardAdUnitIndex = Mathf.Clamp(_dashboardAdUnitIndex, 0, Math.Max(0, _dashboardAdUnits.Count - 1));
        }

        private async void CreateDashboardInventoryAsync()
        {
            if (_dashboardActionBusy) { return; }

            EasterAdDashboardOrganization organization = GetSelectedDashboardOrganization();
            EasterAdDashboardGame game = GetSelectedDashboardGame();
            if (organization == null || game == null)
            {
                _dashboardStatus = "Select an organization and game in Settings first.";
                RefreshUi();
                return;
            }

            string name = string.IsNullOrWhiteSpace(_newInventoryName) ? "" : _newInventoryName.Trim();
            if (string.IsNullOrEmpty(name))
            {
                _dashboardStatus = "Inventory name is required.";
                RefreshUi();
                return;
            }

            _dashboardActionBusy = true;
            _dashboardStatus = "Creating dashboard inventory...";
            RefreshUi();

            try
            {
                await WaitForDashboardUpdateAsync();
                using (EasterAdDashboardClient client = CreateDashboardClient())
                {
                    EasterAdDashboardAdUnit created = await client.CreateAdUnitAsync(organization._id, game._id, name);
                    await LoadDashboardAdUnitsAsync(client);
                    if (created != null && !string.IsNullOrEmpty(created._id))
                    {
                        SelectDashboardAdUnitById(created._id);
                        _newPlacementAdUnitId = created._id;
                        GameObject placement = CreatePlanePlacement();
                        SelectCreatedPlacement(placement);
                        _dashboardStatus = "";
                    }
                    else
                    {
                        _dashboardStatus = "Inventory created. Refresh inventory to restore it from Archive.";
                    }

                    _newInventoryName = "New Inventory";
                    SaveDashboardPreferences();
                }
            }
            catch (Exception ex)
            {
                HandleDashboardException(ex);
            }
            finally
            {
                _dashboardActionBusy = false;
                RefreshUi();
            }
        }

        private async void ArchiveDashboardInventoryAsync(EasterAdDashboardAdUnit adUnit)
        {
            if (_dashboardActionBusy || adUnit == null) { return; }

            EasterAdDashboardOrganization organization = GetSelectedDashboardOrganization();
            EasterAdDashboardGame game = GetSelectedDashboardGame();
            if (organization == null || game == null)
            {
                _dashboardStatus = "Select an organization and game in Settings first.";
                RefreshUi();
                return;
            }

            _dashboardActionBusy = true;
            _dashboardStatus = "Moving inventory to archive...";
            RefreshUi();

            try
            {
                await WaitForDashboardUpdateAsync();
                using (EasterAdDashboardClient client = CreateDashboardClient())
                {
                    await client.ArchiveAdUnitAsync(organization._id, game._id, adUnit._id);
                    await LoadDashboardAdUnitsAsync(client);
                    SaveDashboardPreferences();
                    _dashboardStatus = "";
                }
            }
            catch (Exception ex)
            {
                HandleDashboardException(ex);
            }
            finally
            {
                _dashboardActionBusy = false;
                RefreshUi();
            }
        }

        private async void RestoreDashboardInventoryAsync(EasterAdDashboardAdUnit adUnit)
        {
            if (_dashboardActionBusy || adUnit == null) { return; }

            EasterAdDashboardOrganization organization = GetSelectedDashboardOrganization();
            EasterAdDashboardGame game = GetSelectedDashboardGame();
            if (organization == null || game == null)
            {
                _dashboardStatus = "Select an organization and game in Settings first.";
                RefreshUi();
                return;
            }

            _dashboardActionBusy = true;
            _dashboardStatus = "Restoring inventory...";
            RefreshUi();

            try
            {
                await WaitForDashboardUpdateAsync();
                using (EasterAdDashboardClient client = CreateDashboardClient())
                {
                    EasterAdDashboardAdUnit restored = await client.RestoreAdUnitAsync(organization._id, game._id, adUnit._id);
                    await LoadDashboardAdUnitsAsync(client);
                    SelectDashboardAdUnitById(restored == null ? adUnit._id : restored._id);
                    SaveDashboardPreferences();
                    _dashboardStatus = "";
                }
            }
            catch (Exception ex)
            {
                HandleDashboardException(ex);
            }
            finally
            {
                _dashboardActionBusy = false;
                RefreshUi();
            }
        }

        private EasterAdDashboardClient CreateDashboardClient()
        {
            return new EasterAdDashboardClient(_dashboardBaseUrl, _dashboardApiKey);
        }

        private void RegisterDashboardSyncSuccess()
        {
            _dashboardAutoSyncFailureCount = 0;
            _dashboardAutoSyncError = "";
            _dashboardAutoSyncPaused = false;
            if (_dashboardOrganizations.Count > 0 && _dashboardGames.Count > 0 && IsDashboardSyncResultStatus(_dashboardStatus))
            {
                _dashboardStatus = "";
            }
        }

        private void RegisterDashboardSyncFailure(Exception ex)
        {
            _dashboardAutoSyncFailureCount = Math.Min(_dashboardAutoSyncFailureCount + 1, DashboardAutoSyncFailureLimit);
            _dashboardAutoSyncError = FormatDashboardException(ex);

            if (_dashboardAutoSyncFailureCount >= DashboardAutoSyncFailureLimit)
            {
                _dashboardAutoSyncPaused = true;
                _dashboardStatus = _dashboardAutoSyncError;
            }
            else
            {
                _dashboardStatus = "Dashboard update failed (" + _dashboardAutoSyncFailureCount + "/" + DashboardAutoSyncFailureLimit + "). " + _dashboardAutoSyncError;
            }

            Debug.LogWarning("[EasterAd] " + _dashboardStatus);
        }

        private void ApplyDashboardGameToSdkSettings(bool save)
        {
            EasterAdDashboardGame game = GetSelectedDashboardGame();
            if (game == null) { return; }

            _easterAdEnabled = true;
            _tempGameId = game._id;
            _tempSdkKey = game.sdkKey ?? "";

            if (save)
            {
                SaveSettings();
                _dashboardStatus = "Dashboard game applied and saved to StreamingAssets.";
            }
            else
            {
                _dashboardStatus = "Dashboard game applied to SDK settings. Click Save Settings to write StreamingAssets.";
            }
        }

        private void HandleDashboardException(Exception ex)
        {
            _dashboardStatus = FormatDashboardException(ex);
            Debug.LogWarning("[EasterAd] " + _dashboardStatus);
        }

        private static string FormatDashboardException(Exception ex)
        {
            if (ex is EasterAdDashboardApiException apiException)
            {
                return "Dashboard request failed (HTTP " + (int)apiException.StatusCode + "). Check API key permissions and network access.";
            }

            return "Dashboard request failed: " + ex.Message;
        }

        private EasterAdDashboardOrganization GetSelectedDashboardOrganization()
        {
            if (_dashboardOrganizations.Count == 0) { return null; }
            _dashboardOrganizationIndex = Mathf.Clamp(_dashboardOrganizationIndex, 0, _dashboardOrganizations.Count - 1);
            return _dashboardOrganizations[_dashboardOrganizationIndex];
        }

        private EasterAdDashboardGame GetSelectedDashboardGame()
        {
            if (_dashboardGames.Count == 0) { return null; }
            _dashboardGameIndex = Mathf.Clamp(_dashboardGameIndex, 0, _dashboardGames.Count - 1);
            return _dashboardGames[_dashboardGameIndex];
        }

        private int FindDashboardOrganizationIndex(string organizationId)
        {
            if (string.IsNullOrEmpty(organizationId)) { return 0; }

            for (int i = 0; i < _dashboardOrganizations.Count; i++)
            {
                EasterAdDashboardOrganization organization = _dashboardOrganizations[i];
                if (organization != null && organization._id == organizationId)
                {
                    return i;
                }
            }

            return 0;
        }

        private int FindDashboardGameIndex(string gameId)
        {
            if (string.IsNullOrEmpty(gameId)) { return 0; }

            for (int i = 0; i < _dashboardGames.Count; i++)
            {
                EasterAdDashboardGame game = _dashboardGames[i];
                if (game != null && game._id == gameId)
                {
                    return i;
                }
            }

            return 0;
        }

        private void SelectDashboardAdUnitById(string adUnitId)
        {
            if (string.IsNullOrEmpty(adUnitId)) { return; }

            for (int i = 0; i < _dashboardAdUnits.Count; i++)
            {
                EasterAdDashboardAdUnit adUnit = _dashboardAdUnits[i];
                if (adUnit != null && adUnit._id == adUnitId)
                {
                    _dashboardAdUnitIndex = i;
                    return;
                }
            }
        }

        private string[] BuildOrganizationLabels()
        {
            string[] labels = new string[_dashboardOrganizations.Count];
            for (int i = 0; i < _dashboardOrganizations.Count; i++)
            {
                EasterAdDashboardOrganization organization = _dashboardOrganizations[i];
                labels[i] = string.IsNullOrEmpty(organization.organizationType)
                    ? organization.name
                    : organization.name + " (" + organization.organizationType + ")";
            }

            return labels;
        }

        private string[] BuildGameLabels()
        {
            string[] labels = new string[_dashboardGames.Count];
            for (int i = 0; i < _dashboardGames.Count; i++)
            {
                EasterAdDashboardGame game = _dashboardGames[i];
                labels[i] = game.name + " - " + game.currentStatus;
            }

            return labels;
        }

        private void DrawMigrationSection()
        {
            bool hasLegacyAssets = EasterAdMigrationHelper.HasLegacyAssets();
            bool hasUnifiedShader = EasterAdMigrationHelper.HasUnifiedShader();

            if (hasLegacyAssets)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Migration Available", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Legacy assets detected in 'Assets/EasterAd/'.\n" +
                    "The new system (v1.2.0+) uses assets directly from the package.\n\n" +
                    "All assets are now managed under:\n" +
                    "Packages/EasterAd SDK/Runtime/",
                    MessageType.Warning
                );

                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("1. Migrate Prefab References", GUILayout.Height(30)))
                {
                    EasterAdMigrationHelper.MigratePrefabReferences();
                }

                GUI.enabled = !hasLegacyAssets || hasUnifiedShader;
                if (GUILayout.Button("2. Clean Up Legacy Assets", GUILayout.Height(30)))
                {
                    EasterAdMigrationHelper.CleanupLegacyAssets();
                }
                GUI.enabled = true;

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField("Status: " + EasterAdMigrationHelper.GetMigrationStatus(), EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
            else if (hasUnifiedShader && !EditorPrefs.GetBool("EasterAd_UnifiedShaderNoticeShown", false))
            {
                EditorGUILayout.HelpBox(
                    "Using new unified shader system.\n" +
                    "All render pipelines are supported automatically.",
                    MessageType.Info
                );
                EditorPrefs.SetBool("EasterAd_UnifiedShaderNoticeShown", true);
            }
        }

        private void DrawSdkSettingsSection()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("SDK Settings", EditorStyles.boldLabel);
            _easterAdEnabled = EditorGUILayout.Toggle("Enable SDK", _easterAdEnabled);

            using (new EditorGUI.DisabledScope(!_easterAdEnabled))
            {
                EditorGUILayout.BeginHorizontal();
                _tempGameId = EditorGUILayout.TextField("Game ID", _tempGameId);
                EditorGUILayout.LabelField("Saved: " + _currentGameId, EditorStyles.miniLabel, GUILayout.Width(180));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                _tempSdkKey = EditorGUILayout.PasswordField("SDK Key", _tempSdkKey);
                EditorGUILayout.LabelField("Saved: " + MaskSecret(_currentSdkKey), EditorStyles.miniLabel, GUILayout.Width(180));
                EditorGUILayout.EndHorizontal();

                _tempLogEnable = EditorGUILayout.Toggle("Enable Log", _tempLogEnable);

                _customInfoEnable = EditorGUILayout.BeginToggleGroup("Custom Info", _customInfoEnable);

                EditorGUILayout.BeginHorizontal();
                _customDeviceType = (DeviceType)EditorGUILayout.EnumPopup("Device Type", _customDeviceType);
                EditorGUILayout.LabelField("Saved: " + _currentcustomDeviceType, EditorStyles.miniLabel, GUILayout.Width(180));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                _customPlatform = (RuntimePlatform)EditorGUILayout.EnumPopup("Platform", _customPlatform);
                EditorGUILayout.LabelField("Saved: " + _currentcustomPlatform, EditorStyles.miniLabel, GUILayout.Width(180));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                _customLanguage = (SystemLanguage)EditorGUILayout.EnumPopup("Language", _customLanguage);
                EditorGUILayout.LabelField("Saved: " + _currentcustomLanguage, EditorStyles.miniLabel, GUILayout.Width(180));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndToggleGroup();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Settings", GUILayout.Height(28)))
            {
                SaveSettings();
            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying || !EasterAdSdk.OnceInitialized))
            {
                if (GUILayout.Button("Re-Initialize SDK", GUILayout.Height(28)))
                {
                    EasterAdSdk.Instance.ReInitialize();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("Runtime Status", EasterAdSdk.OnceInitialized ? "Initialized" : "Not Initialized");
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawPlacementManagerSection()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Placement Manager", EditorStyles.boldLabel);

            Item[] items = FindSceneItems();
            if (items.Length == 0)
            {
                EditorGUILayout.HelpBox("No Plane or CanvasItem placements are in the current scene.", MessageType.Info);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
                return;
            }

            for (int i = 0; i < items.Length; i++)
            {
                DrawPlacementRow(items[i], i + 1, items);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawPlacementRow(Item item, int index, Item[] allItems)
        {
            if (item == null) { return; }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(index + ". " + item.gameObject.name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(item.GetType().Name, EditorStyles.miniLabel, GUILayout.Width(100));
            if (GUILayout.Button("Select", GUILayout.Width(64)))
            {
                Selection.activeGameObject = item.gameObject;
                EditorGUIUtility.PingObject(item.gameObject);
            }

            if (GUILayout.Button("Remove", GUILayout.Width(72)))
            {
                if (EditorUtility.DisplayDialog("Remove EasterAd Placement", "Remove '" + item.gameObject.name + "' from the scene?", "Remove", "Cancel"))
                {
                    Scene scene = item.gameObject.scene;
                    Undo.DestroyObjectImmediate(item.gameObject);
                    MarkSceneDirty(scene);
                }
            }
            EditorGUILayout.EndHorizontal();

            SerializedObject serializedItem = new SerializedObject(item);
            serializedItem.Update();

            EditorGUI.BeginChangeCheck();
            DrawPlacementProperty(serializedItem, "adUnitId", "Ad Unit ID");
            DrawPlacementProperty(serializedItem, "loadOnStart", "Load On Start");
            DrawPlacementProperty(serializedItem, "allowImpression", "Allow Impression");
            DrawPlacementProperty(serializedItem, "interactable", "Interactable");
            DrawPlacementProperty(serializedItem, "enableRefresh", "Enable Refresh");
            DrawPlacementProperty(serializedItem, "refreshTime", "Refresh Time");
            DrawPlacementProperty(serializedItem, "hideDuringCapture", "Hide During Capture");

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(item, "Edit EasterAd Placement");
                serializedItem.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(item);
                MarkSceneDirty(item.gameObject.scene);
            }
            else
            {
                serializedItem.ApplyModifiedProperties();
            }

            if (HasDuplicateAdUnitId(allItems, item))
            {
                EditorGUILayout.HelpBox("Another placement in this scene uses the same Ad Unit ID. Runtime duplicate guards will reject one of them.", MessageType.Warning);
            }

            if (Application.isPlaying)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Initialized", item.IsInitialized ? "Yes" : "No", GUILayout.Width(180));
                string statusText = item.IsInitialized ? item.Client.GetStatus().ToString() : "Not Initialized";
                EditorGUILayout.LabelField("Status", statusText, GUILayout.Width(220));

                using (new EditorGUI.DisabledScope(!item.IsInitialized))
                {
                    if (GUILayout.Button("Load", GUILayout.Width(60)))
                    {
                        item.Load();
                    }

                    if (GUILayout.Button("Start Interaction", GUILayout.Width(120)))
                    {
                        string interactionUrl = item.StartInteraction();
                        if (!string.IsNullOrEmpty(interactionUrl)) { Application.OpenURL(interactionUrl); }
                    }

                    if (GUILayout.Button("End Interaction", GUILayout.Width(110))) { item.EndInteraction(); }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawCreatePlacementSection()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Create Placement", EditorStyles.boldLabel);
            _newPlacementAdUnitId = EditorGUILayout.TextField("Ad Unit ID", _newPlacementAdUnitId);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Plane Ad", GUILayout.Height(28)))
            {
                GameObject placement = CreatePlanePlacement();
                SelectCreatedPlacement(placement);
            }

            if (GUILayout.Button("Create CanvasItem Ad", GUILayout.Height(28)))
            {
                GameObject placement = CreateCanvasPlacement();
                SelectCreatedPlacement(placement);
            }
            EditorGUILayout.EndHorizontal();

            GameObject selectedObject = Selection.activeGameObject;
            using (new EditorGUI.DisabledScope(selectedObject == null))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Add Plane to Selection", GUILayout.Height(24)))
                {
                    AddPlacementComponentToSelection<global::EasterAd.Plane>(selectedObject);
                }

                bool canAddCanvasItem = selectedObject != null && selectedObject.GetComponent<RectTransform>() != null;
                using (new EditorGUI.DisabledScope(!canAddCanvasItem))
                {
                    if (GUILayout.Button("Add CanvasItem to Selection", GUILayout.Height(24)))
                    {
                        AddPlacementComponentToSelection<global::EasterAd.CanvasItem>(selectedObject);
                    }
                }
                EditorGUILayout.EndHorizontal();

                if (selectedObject != null && selectedObject.GetComponent<RectTransform>() == null)
                {
                    EditorGUILayout.HelpBox("CanvasItem can be added to selected UI objects with RectTransform.", MessageType.Info);
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawPrefabSection()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Ad Prefab", EditorStyles.boldLabel);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlaneItemPrefabPath);

            if (prefab != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField("Plane Item Prefab", prefab, typeof(GameObject), false);
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    Selection.activeObject = prefab;
                    EditorGUIUtility.PingObject(prefab);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.HelpBox(
                    "Drag this prefab into your scene to place an ad.\n" +
                    "The unified shader automatically supports all render pipelines.",
                    MessageType.Info
                );
            }
            else
            {
                EditorGUILayout.HelpBox("Prefab not found. Package may need reinstallation.", MessageType.Error);
            }

            EditorGUILayout.EndVertical();
        }

        // private void ImportPackage(string packageName)
        // {
        //     if (!string.IsNullOrEmpty(packageName))
        //     {
        //         AssetDatabase.ImportPackage(packageName, true);
        //     }
        //     else
        //     {
        //         Debug.Log(packageName + " is not found.");
        //     }
        // }

        private static Item[] FindSceneItems()
        {
            Item[] items = FindObjectsByType<Item>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
            return Array.FindAll(items, item => item != null && item.gameObject.scene.IsValid());
        }

        private static void DrawPlacementProperty(SerializedObject serializedObject, string propertyName, string label)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null) { return; }

            EditorGUILayout.PropertyField(property, new GUIContent(label));
        }

        private static bool HasDuplicateAdUnitId(Item[] items, Item target)
        {
            if (string.IsNullOrEmpty(target.adUnitId)) { return false; }

            int count = 0;
            foreach (Item item in items)
            {
                if (item == null || item.adUnitId != target.adUnitId) { continue; }
                count++;
                if (count > 1) { return true; }
            }

            return false;
        }

        private GameObject CreatePlanePlacement()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlaneItemPrefabPath);
            GameObject placement;

            if (prefab != null)
            {
                placement = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(placement, "Create EasterAd Plane Placement");
            }
            else
            {
                placement = GameObject.CreatePrimitive(PrimitiveType.Quad);
                placement.name = "EasterAd Plane";
                Undo.RegisterCreatedObjectUndo(placement, "Create EasterAd Plane Placement");
                Undo.AddComponent<global::EasterAd.Plane>(placement);
                Undo.AddComponent<MaterialManager>(placement);
                Undo.AddComponent<AdSegmentationObject>(placement);
            }

            placement.name = "EasterAd Plane";
            global::EasterAd.Plane plane = placement.GetComponent<global::EasterAd.Plane>();
            if (plane == null)
            {
                plane = Undo.AddComponent<global::EasterAd.Plane>(placement);
            }

            ApplyDefaultPlacementSettings(plane);
            MarkSceneDirty(placement.scene);
            RegisterManagedSceneForPlacement(placement.scene);
            return placement;
        }

        private GameObject CreateCanvasPlacement()
        {
            Canvas canvas = FindOrCreateCanvas();
            GameObject placement = new GameObject("EasterAd CanvasItem", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(placement, "Create EasterAd CanvasItem Placement");
            Undo.SetTransformParent(placement.transform, canvas.transform, "Parent EasterAd CanvasItem");

            RectTransform rectTransform = placement.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = new Vector2(320, 180);

            Type rawImageType = ResolveUnityUiType("UnityEngine.UI.RawImage");
            if (rawImageType != null)
            {
                Undo.AddComponent(placement, rawImageType);
            }

            global::EasterAd.CanvasItem canvasItem = Undo.AddComponent<global::EasterAd.CanvasItem>(placement);
            ApplyDefaultPlacementSettings(canvasItem);
            MarkSceneDirty(placement.scene);
            RegisterManagedSceneForPlacement(placement.scene);
            return placement;
        }

        private static Canvas FindOrCreateCanvas()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null) { return canvas; }

            GameObject canvasObject = new GameObject("Canvas", typeof(Canvas));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create Canvas");
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            Type canvasScalerType = ResolveUnityUiType("UnityEngine.UI.CanvasScaler");
            if (canvasScalerType != null)
            {
                Undo.AddComponent(canvasObject, canvasScalerType);
            }

            Type graphicRaycasterType = ResolveUnityUiType("UnityEngine.UI.GraphicRaycaster");
            if (graphicRaycasterType != null)
            {
                Undo.AddComponent(canvasObject, graphicRaycasterType);
            }

            MarkSceneDirty(canvasObject.scene);
            return canvas;
        }

        private static Type ResolveUnityUiType(string typeName)
        {
            Type type = Type.GetType(typeName + ", UnityEngine.UI");
            if (type == null)
            {
                Debug.LogError("[EasterAd] Unity UI type not found: " + typeName + ". Enable the Unity UI package to create CanvasItem placements.");
            }

            return type;
        }

        private void AddPlacementComponentToSelection<T>(GameObject? selectedObject) where T : Component
        {
            if (selectedObject == null) { return; }

            Item selectedItem = selectedObject.GetComponent<Item>();
            if (selectedItem != null && selectedObject.GetComponent<T>() == null)
            {
                _dashboardStatus = "Selected object already has a placement component.";
                return;
            }

            T component = selectedObject.GetComponent<T>();
            if (component == null)
            {
                component = Undo.AddComponent<T>(selectedObject);
            }

            if (component is Item item)
            {
                ApplyDefaultPlacementSettings(item);
            }

            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            MarkSceneDirty(selectedObject.scene);
            RegisterManagedSceneForPlacement(selectedObject.scene);
            Selection.activeGameObject = selectedObject;
        }

        private void ApplyDefaultPlacementSettings(Item item)
        {
            Undo.RecordObject(item, "Configure EasterAd Placement");
            SerializedObject serializedItem = new SerializedObject(item);
            serializedItem.Update();

            SerializedProperty adUnitIdProperty = serializedItem.FindProperty("adUnitId");
            if (adUnitIdProperty != null && !string.IsNullOrEmpty(_newPlacementAdUnitId))
            {
                adUnitIdProperty.stringValue = _newPlacementAdUnitId;
            }

            SerializedProperty loadOnStartProperty = serializedItem.FindProperty("loadOnStart");
            if (loadOnStartProperty != null) { loadOnStartProperty.boolValue = true; }

            SerializedProperty allowImpressionProperty = serializedItem.FindProperty("allowImpression");
            if (allowImpressionProperty != null) { allowImpressionProperty.boolValue = true; }

            SerializedProperty enableRefreshProperty = serializedItem.FindProperty("enableRefresh");
            if (enableRefreshProperty != null) { enableRefreshProperty.boolValue = true; }

            SerializedProperty hideDuringCaptureProperty = serializedItem.FindProperty("hideDuringCapture");
            if (hideDuringCaptureProperty != null) { hideDuringCaptureProperty.boolValue = true; }

            serializedItem.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(item);
        }

        private static void SelectCreatedPlacement(GameObject placement)
        {
            if (placement == null) { return; }

            Selection.activeGameObject = placement;
            EditorGUIUtility.PingObject(placement);
        }

        private static void MarkSceneDirty(Scene scene)
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        private void SaveSettings()
        {
            if (_easterAdEnabled && _tempGameId == "")
            {
                try
                {
                    EasterAdSdk.DestroyCall();
                }
                finally
                {
                    Debug.LogError("Game ID is required");
                }
            }
            else
            {
                StringBuilder config = new StringBuilder();
                config.AppendLine(_easterAdEnabled.ToString());
                config.AppendLine(_tempGameId);
                config.AppendLine(_tempSdkKey);
                config.AppendLine(_tempLogEnable.ToString());
                config.AppendLine(_customInfoEnable.ToString());
                if (_customInfoEnable)
                {
                    config.AppendLine(_customDeviceType.ToString());
                    config.AppendLine(_customPlatform.ToString());
                    config.AppendLine(_customLanguage.ToString());
                }
                AssetDatabase.Refresh();

                if (Directory.Exists(Application.streamingAssetsPath) == false)
                {
                    Directory.CreateDirectory(Application.streamingAssetsPath);
                }

                string filepath = EasterAdConfigFiles.CurrentConfigPath;
                File.WriteAllText(filepath, config.ToString());
                AssetDatabase.Refresh();

                _currentGameId = _tempGameId;
                _currentSdkKey = _tempSdkKey;
                _currentcustomDeviceType = _customDeviceType;
                _currentcustomPlatform = _customPlatform;
                _currentcustomLanguage = _customLanguage;
            }
        }

        /// <summary>
        /// Render Pipeline 감지 및 Feature 설치 UI 표시
        /// </summary>
        private void DrawRenderPipelineSetupUI()
        {
            RenderPipelineType pipelineType = DetectRenderPipeline();

            // URP이고 Feature가 설치되어 있으면 UI 숨김
            if (pipelineType == RenderPipelineType.URP)
            {
                var featureManagerType = System.Type.GetType("EasterAd_Editor.Menu.AdSegmentationFeatureManager, EasterAd.Editor.URP");
                if (featureManagerType != null)
                {
                    var isInstalledMethod = featureManagerType.GetMethod("IsFeatureInstalled",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    bool isFeatureInstalled = (bool)isInstalledMethod.Invoke(null, null);

                    if (isFeatureInstalled)
                    {
                        // 설치 완료 → UI 표시 안 함
                        return;
                    }
                }
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Render Pipeline Setup", EditorStyles.boldLabel);

            switch (pipelineType)
            {
                case RenderPipelineType.URP:
                    DrawURPSetupUI();
                    break;

                case RenderPipelineType.HDRP:
                    EditorGUILayout.HelpBox(
                        "🚧 HDRP Support Coming Soon\n" +
                        "AdSegmentation feature for HDRP is under development.",
                        MessageType.Info
                    );
                    break;

                case RenderPipelineType.BuiltIn:
                    EditorGUILayout.HelpBox(
                        "🚧 Built-in Render Pipeline Support Coming Soon\n" +
                        "AdSegmentation feature for Built-in RP is under development.",
                        MessageType.Info
                    );
                    break;

                case RenderPipelineType.Unknown:
                    EditorGUILayout.HelpBox(
                        "⚠ Unknown Render Pipeline\n" +
                        "Could not detect the current render pipeline.",
                        MessageType.Warning
                    );
                    break;
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// URP 설정 UI 표시
        /// </summary>
        private void DrawURPSetupUI()
        {
            // Reflection으로 AdSegmentationFeatureManager 찾기
            var featureManagerType = System.Type.GetType("EasterAd_Editor.Menu.AdSegmentationFeatureManager, EasterAd.Editor.URP");

            if (featureManagerType == null)
            {
                EditorGUILayout.HelpBox(
                    "✅ Universal Render Pipeline (URP) Detected\n\n" +
                    "⚠ URP Editor Assembly Not Found\n" +
                    "The EasterAd.Editor.URP assembly is not loaded. " +
                    "This is normal if URP package is not installed.",
                    MessageType.Warning
                );
                return;
            }

            // IsFeatureInstalled() 메서드 호출
            var isInstalledMethod = featureManagerType.GetMethod("IsFeatureInstalled",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            bool isFeatureInstalled = (bool)isInstalledMethod.Invoke(null, null);

            if (!isFeatureInstalled)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.HelpBox(
                    "Universal Render Pipeline (URP) detected.\n\n" +
                    "AdSegmentation Renderer Feature is not installed. Install it to enable GPU-based visibility measurement.",
                    MessageType.Info
                );

                if (GUILayout.Button("Install AdSegmentation Renderer Feature", GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog(
                            "Install Renderer Feature",
                            "This will modify the active URP Renderer Data asset by adding the EasterAd AdSegmentation Renderer Feature.",
                            "Install",
                            "Cancel"))
                    {
                        var installMethod = featureManagerType.GetMethod("InstallFeature",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        bool success = installMethod != null && (bool)installMethod.Invoke(null, null);
                        if (success)
                        {
                            EditorUtility.DisplayDialog(
                                "Installation Complete",
                                "AdSegmentation Renderer Feature has been installed successfully!",
                                "OK"
                            );
                        }
                        else
                        {
                            EditorUtility.DisplayDialog(
                                "Installation Failed",
                                "Failed to install AdSegmentation Renderer Feature.\n" +
                                "Please check the Console for error messages.",
                                "OK"
                            );
                        }
                    }
                }

                EditorGUILayout.EndVertical();
            }
            // Feature 설치됨 → UI 표시 안 함
        }

        private void DrawAdSegmentationCapacityUI()
        {
            int segmentationObjectCount = FindObjectsByType<AdSegmentationObject>(FindObjectsSortMode.InstanceID).Length;
            if (segmentationObjectCount < 200) return;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("AdSegmentation Capacity", EditorStyles.boldLabel);
            if (segmentationObjectCount > 255)
            {
                EditorGUILayout.HelpBox(
                    $"AdSegmentation supports up to 255 ad objects. Current scene has {segmentationObjectCount}. Some ads cannot be measured.",
                    MessageType.Error
                );
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"Current scene has {segmentationObjectCount}/255 AdSegmentation objects. Keep production scenes well below the limit.",
                    MessageType.Warning
                );
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 현재 활성화된 Render Pipeline 감지
        /// </summary>
        private RenderPipelineType DetectRenderPipeline()
        {
            var currentPipeline = GraphicsSettings.currentRenderPipeline;

            if (currentPipeline == null)
            {
                return RenderPipelineType.BuiltIn;
            }

            string pipelineTypeName = currentPipeline.GetType().Name;

            if (pipelineTypeName.Contains("Universal"))
            {
                return RenderPipelineType.URP;
            }
            else if (pipelineTypeName.Contains("HDRenderPipeline") || pipelineTypeName.Contains("HDRP"))
            {
                return RenderPipelineType.HDRP;
            }
            else
            {
                return RenderPipelineType.Unknown;
            }
        }
    }

    [CustomEditor(typeof(EasterAdSdk))]
    internal sealed class EasterAdSdkEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawScriptReference();
            EditorGUILayout.HelpBox("EasterAd SDK settings are managed in Window > EasterAd. Scene placement of EasterAdSdk is optional when the SDK is enabled there.", MessageType.Info);

            if (GUILayout.Button("Open EasterAd Window", GUILayout.Height(28)))
            {
                EasterAd.ShowWindow();
            }
        }

        private void DrawScriptReference()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour((MonoBehaviour)target), typeof(MonoScript), false);
            }
        }
    }

    [CustomEditor(typeof(Item), true)]
    [CanEditMultipleObjects]
    internal sealed class EasterAdItemEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawScriptReference();
            EditorGUILayout.HelpBox("Placement settings are managed in Window > EasterAd. Use the Placement Manager there to edit Ad Unit ID, loading, interaction, refresh, and capture options.", MessageType.Info);

            if (GUILayout.Button("Open EasterAd Window", GUILayout.Height(28)))
            {
                EasterAd.ShowWindow();
            }
        }

        private void DrawScriptReference()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                MonoScript script = null;
                if (target is MonoBehaviour monoBehaviour)
                {
                    script = MonoScript.FromMonoBehaviour(monoBehaviour);
                }

                EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
            }
        }
    }
}
