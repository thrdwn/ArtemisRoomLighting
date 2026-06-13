using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Reflection;

namespace ArtemisRoomLighting.Installer;

internal sealed class InstallerForm : Form
{
    private static readonly string[] StepNames =
    [
        "Activity",
        "Devices",
        "Support",
        "Room",
        "Review"
    ];

    private readonly Panel _stepRail = new();
    private readonly Panel _content = new();
    private readonly Label _status = new();
    private readonly Button _back = new();
    private readonly Button _next = new();
    private readonly Button _apply = new();
    private readonly Button _advanced = new();
    private readonly List<Button> _stepButtons = [];
    private readonly List<Button> _activityButtons = [];

    private readonly TextBox _artemisPath = new();
    private readonly TextBox _cs2Path = new();
    private readonly TextBox _pluginSearch = new();
    private readonly DataGridView _pluginGrid = new();
    private readonly RichTextBox _pluginDetails = new();
    private readonly Button _openPlugin = new();
    private readonly DataGridView _advancedDeviceGrid = new();
    private readonly RoomMapControl _roomMap = new();
    private readonly NumericUpDown _displayWidth = new();
    private readonly NumericUpDown _displayHeight = new();
    private readonly TextBox _displayName = new();
    private readonly NumericUpDown _watchFps = new();
    private readonly CheckBox _blackout = new();
    private readonly CheckBox _installCs2 = new();
    private readonly CheckBox _createShortcuts = new();
    private readonly CheckBox _directBridge = new();
    private readonly TextBox _studyIp = new();
    private readonly TextBox _upperIp = new();
    private readonly TextBox _lowerIp = new();

    private readonly FlowLayoutPanel _deviceGroups = new();
    private readonly FlowLayoutPanel _pluginRecommendations = new();
    private readonly FlowLayoutPanel _roomDeviceList = new();
    private readonly FlowLayoutPanel _reviewList = new();
    private readonly Label _activityDescription = new();
    private readonly Label _deviceEditorTitle = new();
    private readonly Label _deviceEditorMeta = new();
    private readonly ComboBox _deviceKind = new();
    private readonly ComboBox _physicalZone = new();
    private readonly CheckBox _useWatch = new();
    private readonly CheckBox _useGame = new();
    private readonly CheckBox _useStudy = new();
    private readonly ComboBox _watchBehavior = new();
    private readonly ComboBox _gameBehavior = new();
    private readonly ComboBox _studyBehavior = new();
    private readonly NumericUpDown _deviceIntensity = new();

    private readonly BindingList<DeviceAssignment> _devices = [];
    private readonly BindingList<WorkshopEntry> _visiblePlugins = [];
    private readonly List<WorkshopEntry> _allPlugins = [];

    private SetupConfiguration _configuration = new();
    private DeviceAssignment? _selectedDevice;
    private string _selectedPreset = "Watch";
    private int _stepIndex;
    private int _previousStepIndex;
    private bool _showingAdvanced;
    private bool _syncingDeviceEditor;
    private bool _pluginGridConfigured;
    private bool _advancedDeviceGridConfigured;

    public InstallerForm()
    {
        Text = "Artemis Setup Assistant";
        ClientSize = new Size(1180, 780);
        MinimumSize = new Size(1080, 720);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(18, 21, 25);
        ForeColor = Color.FromArgb(239, 242, 247);
        Font = new Font("Segoe UI", 9.5f);

        BuildShell();
        WireSharedEvents();
        ShowStep(0);
        Load += async (_, _) => await LoadStateAsync();
    }

    private void BuildShell()
    {
        Label title = new()
        {
            Text = "Artemis Setup Assistant",
            Location = new Point(24, 16),
            Size = new Size(520, 34),
            Font = new Font("Segoe UI Semibold", 19),
            ForeColor = Color.White
        };
        Controls.Add(title);

        Label subtitle = new()
        {
            Text = "Set up PC lighting without learning every Artemis screen first.",
            Location = new Point(26, 52),
            Size = new Size(780, 24),
            ForeColor = Color.FromArgb(172, 181, 194)
        };
        Controls.Add(subtitle);

        _advanced.Text = "Advanced";
        _advanced.Location = new Point(1044, 28);
        _advanced.Size = new Size(112, 34);
        _advanced.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        StyleButton(_advanced, primary: false);
        _advanced.Click += (_, _) => ShowAdvanced();
        Controls.Add(_advanced);

        _stepRail.Location = new Point(20, 92);
        _stepRail.Size = new Size(214, 602);
        _stepRail.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
        _stepRail.BackColor = Color.FromArgb(27, 31, 37);
        Controls.Add(_stepRail);

        for (int i = 0; i < StepNames.Length; i++)
        {
            Button step = new()
            {
                Text = $"{i + 1}. {StepNames[i]}",
                Tag = i,
                Location = new Point(14, 18 + i * 54),
                Size = new Size(186, 42),
                TextAlign = ContentAlignment.MiddleLeft,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9.5f)
            };
            step.FlatAppearance.BorderSize = 1;
            step.Click += (_, _) => ShowStep((int)step.Tag);
            _stepButtons.Add(step);
            _stepRail.Controls.Add(step);
        }

        Label railHint = new()
        {
            Text = "Advanced settings are available only when you need them.",
            Location = new Point(16, 318),
            Size = new Size(178, 82),
            ForeColor = Color.FromArgb(150, 160, 174)
        };
        _stepRail.Controls.Add(railHint);

        _content.Location = new Point(252, 92);
        _content.Size = new Size(904, 602);
        _content.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _content.BackColor = Color.FromArgb(24, 28, 34);
        Controls.Add(_content);

        _status.Location = new Point(24, 712);
        _status.Size = new Size(760, 42);
        _status.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _status.ForeColor = Color.FromArgb(170, 178, 190);
        _status.Text = "Loading Artemis devices and Workshop plugins...";
        Controls.Add(_status);

        _back.Text = "Back";
        _back.Location = new Point(814, 710);
        _back.Size = new Size(96, 40);
        _back.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        StyleButton(_back, primary: false);
        _back.Click += (_, _) => BackClicked();
        Controls.Add(_back);

        _next.Text = "Next";
        _next.Location = new Point(918, 710);
        _next.Size = new Size(96, 40);
        _next.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        StyleButton(_next, primary: false);
        _next.Click += (_, _) => ShowStep(_stepIndex + 1);
        Controls.Add(_next);

        _apply.Text = "Apply setup";
        _apply.Location = new Point(1022, 710);
        _apply.Size = new Size(134, 40);
        _apply.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        StyleButton(_apply, primary: true);
        _apply.Click += ApplyClicked;
        _apply.Enabled = !Program.PreviewMode;
        Controls.Add(_apply);
    }

    private void WireSharedEvents()
    {
        _roomMap.SelectedDeviceChanged += (_, device) => SelectDevice(device);
        _roomMap.DeviceMoved += (_, _) =>
        {
            SyncDeviceEditor();
            RefreshRoomDeviceList();
            RefreshReview();
        };

        _pluginSearch.TextChanged += (_, _) => FilterPlugins();
        _openPlugin.Click += (_, _) =>
        {
            if (SelectedPlugin() is WorkshopEntry plugin)
                OpenPlugin(plugin);
        };
        _directBridge.CheckedChanged += (_, _) => UpdateDirectControls();

        _deviceKind.SelectedIndexChanged += (_, _) => DeviceEditorChanged();
        _physicalZone.SelectedIndexChanged += (_, _) => DeviceEditorChanged();
        _useWatch.CheckedChanged += (_, _) => DeviceEditorChanged();
        _useGame.CheckedChanged += (_, _) => DeviceEditorChanged();
        _useStudy.CheckedChanged += (_, _) => DeviceEditorChanged();
        _watchBehavior.SelectedIndexChanged += (_, _) => DeviceEditorChanged();
        _gameBehavior.SelectedIndexChanged += (_, _) => DeviceEditorChanged();
        _studyBehavior.SelectedIndexChanged += (_, _) => DeviceEditorChanged();
        _deviceIntensity.ValueChanged += (_, _) => DeviceEditorChanged();
    }

    private void ShowStep(int index)
    {
        _showingAdvanced = false;
        _stepIndex = Math.Clamp(index, 0, StepNames.Length - 1);
        _content.Controls.Clear();
        _content.Controls.Add(_stepIndex switch
        {
            0 => BuildActivityPage(),
            1 => BuildDevicesPage(),
            2 => BuildSupportPage(),
            3 => BuildRoomPage(),
            _ => BuildReviewPage()
        });
        UpdateNavigation();
    }

    private void ShowAdvanced()
    {
        _previousStepIndex = _stepIndex;
        _showingAdvanced = true;
        _content.Controls.Clear();
        _content.Controls.Add(BuildAdvancedPage());
        UpdateNavigation();
    }

    private void BackClicked()
    {
        if (_showingAdvanced)
        {
            ShowStep(_previousStepIndex);
            return;
        }
        ShowStep(_stepIndex - 1);
    }

    private void UpdateNavigation()
    {
        for (int i = 0; i < _stepButtons.Count; i++)
        {
            bool active = !_showingAdvanced && i == _stepIndex;
            _stepButtons[i].BackColor = active ? Color.FromArgb(70, 117, 187) : Color.FromArgb(38, 44, 52);
            _stepButtons[i].ForeColor = Color.White;
            _stepButtons[i].FlatAppearance.BorderColor = active ? Color.FromArgb(165, 202, 255) : Color.FromArgb(65, 73, 86);
        }

        _back.Enabled = _showingAdvanced || _stepIndex > 0;
        _next.Enabled = !_showingAdvanced && _stepIndex < StepNames.Length - 1;
        _apply.Visible = !_showingAdvanced;
        _advanced.Enabled = !_showingAdvanced;
    }

    private Control BuildActivityPage()
    {
        Panel page = CreatePage(
            "What are you doing?",
            "The assistant will choose safe defaults. You can still change individual devices later.");

        string[,] activities =
        {
            { "Watch", "Movies, YouTube, streams", "Immersive screen color with softer room depth." },
            { "Game", "CS2 / Valorant", "Game effects on selected devices, with rear lights used for impact." },
            { "Study", "Desk and work", "Bright front light, calmer peripherals, no dramatic room chase." },
            { "Custom", "Mix it yourself", "Keep the current choices and tune each device manually." }
        };

        _activityButtons.Clear();
        for (int i = 0; i < activities.GetLength(0); i++)
        {
            Button button = new()
            {
                Text = $"{activities[i, 0]}\n{activities[i, 1]}",
                Tag = activities[i, 0],
                Location = new Point(24 + (i % 2) * 300, 118 + (i / 2) * 116),
                Size = new Size(270, 92),
                TextAlign = ContentAlignment.MiddleCenter,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 11)
            };
            button.FlatAppearance.BorderSize = 1;
            button.Click += (_, _) => ApplyPreset((string)button.Tag);
            _activityButtons.Add(button);
            page.Controls.Add(button);
        }

        Panel summary = CreateCard(642, 118, 220, 208);
        AddLabel(summary, "Current setup", 16, 14, 180, 24).Font = new Font("Segoe UI Semibold", 11);
        _activityDescription.Location = new Point(16, 52);
        _activityDescription.Size = new Size(188, 132);
        _activityDescription.ForeColor = Color.FromArgb(204, 213, 226);
        summary.Controls.Add(_activityDescription);
        page.Controls.Add(summary);

        RefreshActivityButtons();
        return page;
    }

    private Control BuildDevicesPage()
    {
        Panel page = CreatePage(
            "Your devices",
            "Detected devices are grouped by what they probably are. You can rename and tune them on the room step.");

        _deviceGroups.Location = new Point(24, 106);
        _deviceGroups.Size = new Size(594, 438);
        _deviceGroups.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
        _deviceGroups.AutoScroll = true;
        _deviceGroups.BackColor = Color.FromArgb(22, 25, 30);
        _deviceGroups.Padding = new Padding(8);
        page.Controls.Add(_deviceGroups);

        Panel actions = CreateCard(646, 106, 218, 250);
        AddLabel(actions, "Device actions", 16, 16, 180, 24).Font = new Font("Segoe UI Semibold", 11);
        Button scan = AddButton(actions, "Scan again", 16, 58, 182, 38);
        scan.Click += (_, _) => ReloadDevices();
        Button artemisDevices = AddButton(actions, "Open Artemis devices", 16, 110, 182, 38);
        artemisDevices.Click += (_, _) => OpenArtemisRoute("artemis://settings/devices");
        Button support = AddButton(actions, "Find missing support", 16, 162, 182, 38);
        support.Click += (_, _) => ShowStep(2);
        page.Controls.Add(actions);

        RefreshDeviceGroups();
        return page;
    }

    private Control BuildSupportPage()
    {
        Panel page = CreatePage(
            "Recommended support",
            "Pick the cards that match your hardware or games. Artemis still installs and updates the plugins.");

        _pluginRecommendations.Location = new Point(24, 106);
        _pluginRecommendations.Size = new Size(838, 440);
        _pluginRecommendations.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _pluginRecommendations.AutoScroll = true;
        _pluginRecommendations.BackColor = Color.FromArgb(22, 25, 30);
        _pluginRecommendations.Padding = new Padding(8);
        page.Controls.Add(_pluginRecommendations);

        Button workshop = AddButton(page, "Open Artemis Workshop", 24, 552, 190, 36);
        workshop.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        workshop.Click += (_, _) => OpenArtemisRoute("artemis://workshop");

        RefreshRecommendations();
        return page;
    }

    private Control BuildRoomPage()
    {
        Panel page = CreatePage(
            "Place and tune",
            "Drag devices to match the room, then choose what each one does in Watch, Game, and Study.");

        _roomMap.Location = new Point(24, 104);
        _roomMap.Size = new Size(510, 440);
        _roomMap.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
        _roomMap.Devices = _devices;
        page.Controls.Add(_roomMap);

        _roomDeviceList.Location = new Point(558, 104);
        _roomDeviceList.Size = new Size(304, 146);
        _roomDeviceList.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _roomDeviceList.AutoScroll = true;
        _roomDeviceList.BackColor = Color.FromArgb(22, 25, 30);
        _roomDeviceList.Padding = new Padding(6);
        page.Controls.Add(_roomDeviceList);

        Control editor = BuildDeviceEditor();
        editor.Location = new Point(558, 266);
        editor.Size = new Size(304, 278);
        editor.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        page.Controls.Add(editor);

        RefreshRoomDeviceList();
        if (_selectedDevice == null && _devices.Count > 0)
            SelectDevice(_devices[0]);
        else
            SyncDeviceEditor();
        return page;
    }

    private Control BuildReviewPage()
    {
        Panel page = CreatePage(
            "Ready to apply",
            "The assistant will back up Artemis, save the room map, and build the guided profiles.");

        _reviewList.Location = new Point(24, 106);
        _reviewList.Size = new Size(838, 438);
        _reviewList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _reviewList.AutoScroll = true;
        _reviewList.BackColor = Color.FromArgb(22, 25, 30);
        _reviewList.Padding = new Padding(8);
        page.Controls.Add(_reviewList);

        RefreshReview();
        return page;
    }

    private Control BuildAdvancedPage()
    {
        Panel page = CreatePage(
            "Advanced",
            "Power settings, raw catalog search, paths, WiZ fallback, and calibration live here.");

        AddLabel(page, "Artemis app", 24, 104, 180, 22);
        ConfigureTextBox(_artemisPath, 24, 130, 640);
        page.Controls.Add(_artemisPath);
        Button browseArtemis = AddButton(page, "Browse", 676, 128, 92, 31);
        browseArtemis.Click += (_, _) => BrowseArtemis();

        AddLabel(page, "Main display", 24, 174, 160, 22);
        ConfigureNumeric(_displayWidth, 24, 200, 96, 640, 7680, 1920);
        page.Controls.Add(_displayWidth);
        AddLabel(page, "x", 126, 203, 18, 22);
        ConfigureNumeric(_displayHeight, 148, 200, 96, 480, 4320, 1080);
        page.Controls.Add(_displayHeight);
        ConfigureTextBox(_displayName, 266, 200, 180);
        page.Controls.Add(_displayName);
        _displayName.PlaceholderText = @"\\.\DISPLAY1";
        AddLabel(page, "Watch FPS", 482, 174, 120, 22);
        ConfigureNumeric(_watchFps, 482, 200, 96, 1, 60, 30);
        page.Controls.Add(_watchFps);
        _blackout.Text = "Black zones turn mapped lights off";
        ConfigureCheck(_blackout, page, 602, 198, 250);

        _directBridge.Text = "Use optional direct WiZ compatibility bridge";
        ConfigureCheck(_directBridge, page, 24, 250, 360);
        AddLabel(page, "Study WiZ IPv4", 24, 292, 160, 22);
        ConfigureTextBox(_studyIp, 24, 318, 180);
        page.Controls.Add(_studyIp);
        AddLabel(page, "Upper/rear WiZ IPv4", 228, 292, 180, 22);
        ConfigureTextBox(_upperIp, 228, 318, 180);
        page.Controls.Add(_upperIp);
        AddLabel(page, "Lower/rear WiZ IPv4", 432, 292, 180, 22);
        ConfigureTextBox(_lowerIp, 432, 318, 180);
        page.Controls.Add(_lowerIp);

        AddLabel(page, "CS2 cfg folder", 24, 366, 160, 22);
        ConfigureTextBox(_cs2Path, 24, 392, 640);
        page.Controls.Add(_cs2Path);
        Button browseCs = AddButton(page, "Browse", 676, 390, 92, 31);
        browseCs.Click += (_, _) => BrowseFolder(_cs2Path);
        _installCs2.Text = "Install/update CS2 Game State Integration";
        ConfigureCheck(_installCs2, page, 24, 432, 360);
        _createShortcuts.Text = "Create Start menu shortcuts";
        ConfigureCheck(_createShortcuts, page, 392, 432, 280);

        AddLabel(page, "RGB calibration", 568, 104, 200, 22).Font = new Font("Segoe UI Semibold", 10);
        ConfigureAdvancedDeviceGrid();
        _advancedDeviceGrid.Location = new Point(568, 130);
        _advancedDeviceGrid.Size = new Size(294, 270);
        _advancedDeviceGrid.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        page.Controls.Add(_advancedDeviceGrid);

        AddLabel(page, "Raw plugin catalog", 24, 482, 200, 22).Font = new Font("Segoe UI Semibold", 10);
        ConfigureTextBox(_pluginSearch, 24, 508, 310);
        _pluginSearch.PlaceholderText = "Search plugins";
        page.Controls.Add(_pluginSearch);
        Button refresh = AddButton(page, "Refresh", 348, 506, 92, 31);
        refresh.Click += async (_, _) => await LoadPluginsAsync();
        ConfigurePluginGrid();
        _pluginGrid.Location = new Point(24, 548);
        _pluginGrid.Size = new Size(528, 38);
        _pluginGrid.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        page.Controls.Add(_pluginGrid);
        _pluginDetails.Location = new Point(568, 508);
        _pluginDetails.Size = new Size(294, 78);
        _pluginDetails.BackColor = Color.FromArgb(30, 34, 40);
        _pluginDetails.ForeColor = Color.FromArgb(214, 221, 232);
        _pluginDetails.BorderStyle = BorderStyle.None;
        _pluginDetails.ReadOnly = true;
        page.Controls.Add(_pluginDetails);
        _openPlugin.Text = "Open selected";
        _openPlugin.Location = new Point(748, 468);
        _openPlugin.Size = new Size(114, 31);
        StyleButton(_openPlugin, primary: false);
        page.Controls.Add(_openPlugin);

        UpdateDirectControls();
        return page;
    }

    private Control BuildDeviceEditor()
    {
        Panel editor = CreateCard(0, 0, 304, 278);
        _deviceEditorTitle.Location = new Point(16, 12);
        _deviceEditorTitle.Size = new Size(270, 24);
        _deviceEditorTitle.Font = new Font("Segoe UI Semibold", 10.5f);
        _deviceEditorTitle.ForeColor = Color.White;
        editor.Controls.Add(_deviceEditorTitle);
        _deviceEditorMeta.Location = new Point(16, 38);
        _deviceEditorMeta.Size = new Size(270, 20);
        _deviceEditorMeta.ForeColor = Color.FromArgb(150, 160, 174);
        editor.Controls.Add(_deviceEditorMeta);

        AddLabel(editor, "Kind", 16, 70, 80, 20);
        ConfigureCombo(_deviceKind, SetupLabels.DeviceKinds, 94, 68, 178);
        editor.Controls.Add(_deviceKind);
        AddLabel(editor, "Position", 16, 106, 80, 20);
        ConfigureCombo(_physicalZone, SetupLabels.PhysicalZones, 94, 104, 178);
        editor.Controls.Add(_physicalZone);

        _useWatch.Text = "Watch";
        ConfigureCheck(_useWatch, editor, 16, 142, 72);
        ConfigureCombo(_watchBehavior, SetupLabels.WatchBehaviors, 94, 140, 178);
        editor.Controls.Add(_watchBehavior);

        _useGame.Text = "Game";
        ConfigureCheck(_useGame, editor, 16, 178, 72);
        ConfigureCombo(_gameBehavior, SetupLabels.GameBehaviors, 94, 176, 178);
        editor.Controls.Add(_gameBehavior);

        _useStudy.Text = "Study";
        ConfigureCheck(_useStudy, editor, 16, 214, 72);
        ConfigureCombo(_studyBehavior, SetupLabels.StudyBehaviors, 94, 212, 178);
        editor.Controls.Add(_studyBehavior);

        AddLabel(editor, "Brightness", 16, 250, 80, 20);
        ConfigureNumeric(_deviceIntensity, 94, 246, 80, 0, 150, 100);
        editor.Controls.Add(_deviceIntensity);
        return editor;
    }

    private async Task LoadStateAsync()
    {
        _configuration = ArtemisState.LoadConfiguration();
        _selectedPreset = string.IsNullOrWhiteSpace(_configuration.SelectedActivity) ? "Watch" : _configuration.SelectedActivity;
        _artemisPath.Text = ArtemisState.DetectArtemis();
        _cs2Path.Text = string.IsNullOrWhiteSpace(_configuration.AdvancedSettings.Cs2CfgPath)
            ? ArtemisState.DetectCs2Cfg()
            : _configuration.AdvancedSettings.Cs2CfgPath;
        _displayWidth.Value = Math.Clamp(_configuration.DisplayWidth, 640, 7680);
        _displayHeight.Value = Math.Clamp(_configuration.DisplayHeight, 480, 4320);
        _displayName.Text = _configuration.DisplayName;
        _watchFps.Value = Math.Clamp(_configuration.WatchFps, 1, 60);
        _blackout.Checked = _configuration.BlackoutOnBlack;
        _installCs2.Checked = _configuration.InstallCs2Gsi || Directory.Exists(_cs2Path.Text);
        _createShortcuts.Checked = _configuration.CreateShortcuts;
        _directBridge.Checked = _configuration.UseDirectBridge || Directory.Exists(ArtemisState.DirectPluginTarget);
        _studyIp.Text = _configuration.StudyIp;
        _upperIp.Text = _configuration.UpperIp;
        _lowerIp.Text = _configuration.LowerIp;
        UpdateDirectControls();

        try
        {
            bool hasSavedDevices = _configuration.Devices.Count > 0;
            foreach (DeviceAssignment device in ArtemisState.LoadDevices(_configuration))
            {
                EnsureDeviceDefaults(device);
                device.PropertyChanged += (_, _) => _roomMap.Invalidate();
                _devices.Add(device);
            }
            _roomMap.Devices = _devices;
            if (!hasSavedDevices)
                ApplyPreset("Watch", updateStatus: false);
            _status.Text = _devices.Count == 0
                ? "No Artemis devices found yet. Use Support to install a provider, then scan again."
                : $"Detected {_devices.Count} Artemis devices. Loading recommendations...";
        }
        catch (Exception ex)
        {
            _status.Text = "Could not read Artemis devices: " + ex.Message;
        }

        await LoadPluginsAsync();
        ShowStep(0);
    }

    private async Task LoadPluginsAsync()
    {
        _status.Text = "Loading the official Artemis Workshop catalog...";
        try
        {
            Dictionary<string, (string Name, string Version, string HelpPage)> installed =
                ArtemisState.LoadInstalledPlugins();
            HashSet<string> enabled = ArtemisState.LoadEnabledPluginGuids();
            List<WorkshopEntry> plugins = await WorkshopClient.LoadPluginsAsync();
            _allPlugins.Clear();
            foreach (WorkshopEntry plugin in plugins)
            {
                if (installed.TryGetValue(plugin.PluginGuid, out (string Name, string Version, string HelpPage) local))
                {
                    plugin.Installed = true;
                    plugin.Enabled = enabled.Contains(plugin.PluginGuid);
                    plugin.HelpPage = local.HelpPage;
                }
                _allPlugins.Add(plugin);
            }
            FilterPlugins();
            RefreshRecommendations();
            _status.Text = $"Workshop ready: {_allPlugins.Count} plugins loaded.";
        }
        catch (Exception ex)
        {
            _status.Text = "Workshop is temporarily unavailable. Installed devices can still be mapped. " + ex.Message;
            RefreshRecommendations();
        }
    }

    private void ApplyPreset(string preset, bool updateStatus = true)
    {
        _selectedPreset = preset;
        foreach (DeviceAssignment device in _devices)
        {
            EnsureDeviceDefaults(device);
            bool emulator = DeviceText(device).Contains("emulator", StringComparison.OrdinalIgnoreCase);
            bool rear = IsRearDevice(device);
            bool study = IsStudyDevice(device);
            bool light = device.DeviceKind == "Light";
            bool peripheral = device.DeviceKind is "Keyboard" or "Mouse" or "Dock";

            device.PhysicalZone = PresetZone(device);
            device.Placement = SetupLabels.PlacementFromZone(device.PhysicalZone);
            ApplyPlacementPreset(device);
            device.RedScale = device.RedScale <= 0 ? 1 : device.RedScale;
            device.GreenScale = device.GreenScale <= 0 ? 1 : device.GreenScale;
            device.BlueScale = device.BlueScale <= 0 ? 1 : device.BlueScale;

            if (preset == "Watch")
            {
                _blackout.Checked = true;
                _watchFps.Value = 30;
                _installCs2.Checked = false;
                device.ModeAssignments.Watch = rear ? SetupLabels.WatchSoftDepth : SetupLabels.WatchScreenColor;
                device.ModeAssignments.Game = SetupLabels.GameOff;
                device.ModeAssignments.Study = light ? SetupLabels.StudyCalmGlow : SetupLabels.StudyOff;
                device.Intensity = rear ? 45 : study ? 100 : light ? 90 : 85;
            }
            else if (preset == "Game")
            {
                _blackout.Checked = true;
                _watchFps.Value = 30;
                _installCs2.Checked = Directory.Exists(_cs2Path.Text);
                device.ModeAssignments.Watch = SetupLabels.WatchOff;
                device.ModeAssignments.Game = rear ? SetupLabels.GameImpactOnly : (light || peripheral ? SetupLabels.GameMainEffects : SetupLabels.GameTeamMood);
                device.ModeAssignments.Study = SetupLabels.StudyOff;
                device.Intensity = rear ? 65 : study ? 100 : 90;
            }
            else if (preset == "Study")
            {
                _blackout.Checked = false;
                _watchFps.Value = 20;
                _installCs2.Checked = false;
                device.ModeAssignments.Watch = SetupLabels.WatchOff;
                device.ModeAssignments.Game = SetupLabels.GameOff;
                device.ModeAssignments.Study = study ? SetupLabels.StudyBrightDesk : peripheral ? SetupLabels.StudyCalmGlow : SetupLabels.StudyOff;
                device.Intensity = study ? 120 : peripheral ? 35 : 0;
            }

            if (emulator)
            {
                device.ModeAssignments.Watch = SetupLabels.WatchOff;
                device.ModeAssignments.Game = SetupLabels.GameOff;
                device.ModeAssignments.Study = SetupLabels.StudyOff;
            }
            SyncInternalRolesForSelectedActivity(device);
        }

        RefreshActivityButtons();
        RefreshDeviceGroups();
        RefreshRoomDeviceList();
        SyncDeviceEditor();
        RefreshReview();
        _roomMap.Invalidate();
        if (updateStatus)
            _status.Text = $"{preset} selected. Continue through the steps or press Apply setup when ready.";
    }

    private void RefreshActivityButtons()
    {
        foreach (Button button in _activityButtons)
        {
            bool active = string.Equals(button.Tag as string, _selectedPreset, StringComparison.OrdinalIgnoreCase);
            button.BackColor = active ? Color.FromArgb(70, 117, 187) : Color.FromArgb(42, 48, 56);
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = active ? Color.FromArgb(165, 202, 255) : Color.FromArgb(70, 79, 92);
        }

        int activeDevices = _devices.Count(device => device.Enabled);
        _activityDescription.Text = _selectedPreset switch
        {
            "Game" => $"Game effects are prepared for {activeDevices} devices.\n\nRear lights stay tasteful unless an impact event needs punch.",
            "Study" => $"Study mode is prepared for {activeDevices} devices.\n\nThe desk light stays bright; other devices stay calm or off.",
            "Custom" => $"Custom keeps your current choices.\n\nUse the Room step to decide exactly what each device does.",
            _ => $"Watch mode is prepared for {activeDevices} devices.\n\nScreen-facing lights follow color closely; rear lights add depth."
        };
    }

    private void RefreshDeviceGroups()
    {
        if (!IsHandleCreated)
            return;
        _deviceGroups.SuspendLayout();
        _deviceGroups.Controls.Clear();
        if (_devices.Count == 0)
        {
            _deviceGroups.Controls.Add(CreateInfoCard("No devices found", "Open Artemis, install a device provider, then scan again.", 540));
        }
        else
        {
            foreach (IGrouping<string, DeviceAssignment> group in _devices.GroupBy(DeviceGroup).OrderBy(group => group.Key))
            {
                _deviceGroups.Controls.Add(CreateGroupCard(group.Key, group.ToList()));
            }
        }
        _deviceGroups.ResumeLayout();
    }

    private Control CreateGroupCard(string title, IReadOnlyList<DeviceAssignment> devices)
    {
        Panel card = CreateCard(0, 0, 542, 70 + devices.Count * 44);
        card.Margin = new Padding(8);
        AddLabel(card, $"{title}  ({devices.Count})", 14, 12, 300, 24).Font = new Font("Segoe UI Semibold", 10.5f);
        int y = 44;
        foreach (DeviceAssignment device in devices)
        {
            CheckBox enabled = new()
            {
                Text = device.FriendlyName,
                Checked = device.Enabled,
                Location = new Point(14, y),
                Size = new Size(310, 28),
                ForeColor = Color.FromArgb(228, 233, 240),
                FlatStyle = FlatStyle.Flat,
                Tag = device
            };
            enabled.CheckedChanged += (_, _) =>
            {
                if (enabled.Tag is DeviceAssignment assignment)
                {
                    assignment.Enabled = enabled.Checked;
                    if (!assignment.Enabled)
                    {
                        assignment.ModeAssignments.Watch = SetupLabels.WatchOff;
                        assignment.ModeAssignments.Game = SetupLabels.GameOff;
                        assignment.ModeAssignments.Study = SetupLabels.StudyOff;
                    }
                    else
                    {
                        EnsureModeDefaults(assignment);
                    }
                    SyncInternalRolesForSelectedActivity(assignment);
                    RefreshReview();
                    _roomMap.Invalidate();
                }
            };
            card.Controls.Add(enabled);
            Label meta = AddLabel(card, $"{device.DeviceKind} - {device.ProviderName}", 330, y + 4, 190, 20);
            meta.ForeColor = Color.FromArgb(145, 154, 168);
            y += 44;
        }
        return card;
    }

    private void RefreshRecommendations()
    {
        if (!IsHandleCreated)
            return;
        _pluginRecommendations.SuspendLayout();
        _pluginRecommendations.Controls.Clear();
        foreach (PluginRecommendation recommendation in PluginRecommendations())
            _pluginRecommendations.Controls.Add(CreateRecommendationCard(recommendation));
        _pluginRecommendations.ResumeLayout();
    }

    private Control CreateRecommendationCard(PluginRecommendation recommendation)
    {
        WorkshopEntry? plugin = FindPlugin(recommendation.Keywords);
        string status = plugin == null
            ? "Open Workshop"
            : plugin.Installed ? (plugin.Enabled ? "Ready" : "Installed - enable in Artemis") : "Missing";
        Panel card = CreateCard(0, 0, 390, 132);
        card.Margin = new Padding(8);
        AddLabel(card, recommendation.Title, 14, 12, 240, 24).Font = new Font("Segoe UI Semibold", 10.5f);
        Label statusLabel = AddLabel(card, status, 260, 14, 112, 20);
        statusLabel.ForeColor = status == "Ready" ? Color.FromArgb(128, 222, 160) : Color.FromArgb(235, 190, 110);
        Label summary = AddLabel(card, recommendation.Summary, 14, 42, 350, 42);
        summary.ForeColor = Color.FromArgb(176, 186, 200);
        Button open = AddButton(card, plugin == null ? "Open Workshop" : "Open in Artemis", 14, 90, 142, 30);
        open.Click += (_, _) =>
        {
            if (plugin != null)
                OpenPlugin(plugin);
            else
                OpenArtemisRoute("artemis://workshop");
        };
        return card;
    }

    private void RefreshRoomDeviceList()
    {
        if (!IsHandleCreated)
            return;
        _roomDeviceList.SuspendLayout();
        _roomDeviceList.Controls.Clear();
        if (_devices.Count == 0)
        {
            _roomDeviceList.Controls.Add(CreateInfoCard("No devices", "Use Support and scan again.", 250));
        }
        foreach (DeviceAssignment device in _devices)
        {
            Button button = new()
            {
                Text = $"{device.FriendlyName}\n{device.PhysicalZone}",
                Tag = device,
                Width = 132,
                Height = 52,
                Margin = new Padding(5),
                TextAlign = ContentAlignment.MiddleLeft,
                FlatStyle = FlatStyle.Flat,
                BackColor = ReferenceEquals(device, _selectedDevice) ? Color.FromArgb(70, 117, 187) : Color.FromArgb(38, 44, 52),
                ForeColor = Color.White
            };
            button.FlatAppearance.BorderColor = device.Enabled ? Color.FromArgb(104, 144, 204) : Color.FromArgb(72, 80, 92);
            button.Click += (_, _) => SelectDevice((DeviceAssignment)button.Tag);
            _roomDeviceList.Controls.Add(button);
        }
        _roomDeviceList.ResumeLayout();
    }

    private void SelectDevice(DeviceAssignment? device)
    {
        _selectedDevice = device;
        _roomMap.SelectedDevice = device;
        SyncDeviceEditor();
        RefreshRoomDeviceList();
    }

    private void SyncDeviceEditor()
    {
        _syncingDeviceEditor = true;
        try
        {
            bool enabled = _selectedDevice != null;
            foreach (Control control in new Control[]
                     {
                         _deviceKind, _physicalZone, _useWatch, _useGame, _useStudy,
                         _watchBehavior, _gameBehavior, _studyBehavior, _deviceIntensity
                     })
            {
                control.Enabled = enabled;
            }

            if (_selectedDevice == null)
            {
                _deviceEditorTitle.Text = "Select a device";
                _deviceEditorMeta.Text = "";
                return;
            }

            DeviceAssignment device = _selectedDevice;
            _deviceEditorTitle.Text = device.FriendlyName;
            _deviceEditorMeta.Text = $"{device.ProviderName} - {device.DeviceKind}";
            SetComboValue(_deviceKind, device.DeviceKind);
            SetComboValue(_physicalZone, device.PhysicalZone);
            _useWatch.Checked = device.ModeAssignments.Watch != SetupLabels.WatchOff;
            _useGame.Checked = device.ModeAssignments.Game != SetupLabels.GameOff;
            _useStudy.Checked = device.ModeAssignments.Study != SetupLabels.StudyOff;
            SetComboValue(_watchBehavior, device.ModeAssignments.Watch);
            SetComboValue(_gameBehavior, device.ModeAssignments.Game);
            SetComboValue(_studyBehavior, device.ModeAssignments.Study);
            _deviceIntensity.Value = Math.Clamp(device.Intensity, 0, 150);
        }
        finally
        {
            _syncingDeviceEditor = false;
        }
    }

    private void DeviceEditorChanged()
    {
        if (_syncingDeviceEditor || _selectedDevice == null)
            return;

        DeviceAssignment device = _selectedDevice;
        device.DeviceKind = SelectedText(_deviceKind, device.DeviceKind);
        device.PhysicalZone = SelectedText(_physicalZone, device.PhysicalZone);
        device.Placement = SetupLabels.PlacementFromZone(device.PhysicalZone);
        ApplyPlacementPreset(device);
        device.ModeAssignments.Watch = _useWatch.Checked
            ? SelectedText(_watchBehavior, SetupLabels.WatchScreenColor)
            : SetupLabels.WatchOff;
        device.ModeAssignments.Game = _useGame.Checked
            ? SelectedText(_gameBehavior, SetupLabels.GameMainEffects)
            : SetupLabels.GameOff;
        device.ModeAssignments.Study = _useStudy.Checked
            ? SelectedText(_studyBehavior, SetupLabels.StudyBrightDesk)
            : SetupLabels.StudyOff;
        device.Intensity = (int)_deviceIntensity.Value;
        SyncInternalRolesForSelectedActivity(device);
        RefreshDeviceGroups();
        RefreshRoomDeviceList();
        RefreshReview();
        _roomMap.Invalidate();
    }

    private void RefreshReview()
    {
        if (!IsHandleCreated)
            return;
        SyncAllDevicesForSelectedActivity();
        _reviewList.SuspendLayout();
        _reviewList.Controls.Clear();
        int activeDevices = _devices.Count(device => device.Enabled);
        _reviewList.Controls.Add(CreateInfoCard(
            $"{_selectedPreset} setup",
            $"{activeDevices} devices will be active. Artemis database backup is created before changes.",
            794));
        _reviewList.Controls.Add(CreateInfoCard(
            "Plugins",
            RecommendationSummary(),
            794));
        foreach (IGrouping<string, DeviceAssignment> group in _devices.Where(device => device.Enabled).GroupBy(DeviceGroup))
        {
            string body = string.Join(Environment.NewLine, group.Select(device =>
                $"{device.FriendlyName}: {FriendlyRoleSummary(device)} at {device.Intensity}%"));
            _reviewList.Controls.Add(CreateInfoCard(group.Key, body, 794, Math.Max(86, 52 + group.Count() * 22)));
        }
        if (activeDevices == 0)
            _reviewList.Controls.Add(CreateInfoCard("Nothing selected", "Go back to Devices or Room and turn on at least one device.", 794));
        _reviewList.ResumeLayout();
    }

    private string RecommendationSummary()
    {
        int ready = PluginRecommendations().Count(recommendation =>
        {
            WorkshopEntry? plugin = FindPlugin(recommendation.Keywords);
            return plugin is { Installed: true, Enabled: true };
        });
        return $"{ready} recommended support cards are ready. Missing cards can be opened in Artemis from the Support step.";
    }

    private void FilterPlugins()
    {
        string filter = _pluginSearch.Text.Trim();
        IEnumerable<WorkshopEntry> result = _allPlugins;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            result = result.Where(plugin =>
                plugin.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                plugin.Summary.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                plugin.Author.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                plugin.Categories.Any(category => category.Contains(filter, StringComparison.OrdinalIgnoreCase)));
        }

        _visiblePlugins.RaiseListChangedEvents = false;
        _visiblePlugins.Clear();
        foreach (WorkshopEntry plugin in result)
            _visiblePlugins.Add(plugin);
        _visiblePlugins.RaiseListChangedEvents = true;
        _visiblePlugins.ResetBindings();
        ShowSelectedPlugin();
    }

    private void ShowSelectedPlugin()
    {
        WorkshopEntry? plugin = SelectedPlugin();
        _openPlugin.Enabled = plugin != null;
        _pluginDetails.Text = plugin == null
            ? "Select a plugin."
            : $"{plugin.Name}\n{plugin.Status}\n{plugin.Requirements}";
    }

    private WorkshopEntry? SelectedPlugin() => _pluginGrid.CurrentRow?.DataBoundItem as WorkshopEntry;

    private async void ApplyClicked(object? sender, EventArgs e)
    {
        try
        {
            SyncAllDevicesForSelectedActivity();
            ValidateSetup();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ToggleUi(false);
        try
        {
            SetupConfiguration configuration = ReadConfiguration();
            await Task.Run(() => ApplySetup(configuration));
            _status.Text = "Setup applied. Artemis is opening the Surface Editor for a final visual check.";
            ArtemisState.StartArtemis(_artemisPath.Text, "artemis://surface-editor");
            MessageBox.Show(
                "Your setup was applied. Artemis will open so you can visually confirm the device outlines.",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _status.Text = "Setup failed. The previous Artemis database is available in Backups.";
            MessageBox.Show(ex.ToString(), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            ToggleUi(true);
        }
    }

    private void ApplySetup(SetupConfiguration configuration)
    {
        SetStatus("Stopping Artemis and creating a backup...");
        ArtemisState.StopArtemis();
        ArtemisState.Backup();

        string temp = Path.Combine(Path.GetTempPath(), "ArtemisSetupAssistant-" + Guid.NewGuid().ToString("N"));
        try
        {
            Program.ExtractPayload(temp);
            Directory.CreateDirectory(ArtemisState.InstallRoot);
            foreach (string name in new[] { "Tools", "LightingSwitches", "Cs2Gsi" })
                ArtemisState.ReplaceDirectory(Path.Combine(temp, name), Path.Combine(ArtemisState.InstallRoot, name));

            string tool = Path.Combine(ArtemisState.InstallRoot, "Tools", "SqliteTool.exe");
            if (configuration.UseDirectBridge)
            {
                ArtemisState.ReplaceDirectory(Path.Combine(temp, "Plugin"), ArtemisState.DirectPluginTarget);
                RunTool(tool, "enable-direct");
                RunTool(
                    tool,
                    "set-device-setup",
                    configuration.StudyIp,
                    configuration.LowerIp,
                    configuration.UpperIp,
                    "0",
                    "0");
            }
            else
            {
                RunTool(tool, "disable-direct");
            }

            SetStatus("Saving the room map and building guided profiles...");
            ArtemisState.SaveConfiguration(configuration);
            ArtemisState.ApplyDeviceMapping(configuration.Devices);
            RunTool(tool, "configure-ecosystem", ArtemisState.ConfigurationPath);

            if (configuration.InstallCs2Gsi)
            {
                File.Copy(
                    Path.Combine(ArtemisState.InstallRoot, "Cs2Gsi", "gamestate_integration_artemis_room_lighting.cfg"),
                    Path.Combine(_cs2Path.Text, "gamestate_integration_artemis_room_lighting.cfg"),
                    overwrite: true);
            }

            if (configuration.CreateShortcuts)
                CreateShortcuts();

            if (Environment.ProcessPath is { } setupPath)
                File.Copy(setupPath, Path.Combine(ArtemisState.InstallRoot, "ArtemisSetupAssistant.exe"), overwrite: true);
        }
        finally
        {
            if (Directory.Exists(temp))
                Directory.Delete(temp, recursive: true);
        }
    }

    private SetupConfiguration ReadConfiguration()
    {
        AdvancedSettings advanced = new()
        {
            WatchFps = (int)_watchFps.Value,
            BlackoutOnBlack = _blackout.Checked,
            InstallCs2Gsi = _installCs2.Checked,
            CreateShortcuts = _createShortcuts.Checked,
            UseDirectBridge = _directBridge.Checked,
            StudyIp = _studyIp.Text.Trim(),
            UpperIp = _upperIp.Text.Trim(),
            LowerIp = _lowerIp.Text.Trim(),
            Cs2CfgPath = _cs2Path.Text.Trim()
        };
        return new SetupConfiguration
        {
            Version = 2,
            SelectedActivity = _selectedPreset,
            DisplayWidth = (int)_displayWidth.Value,
            DisplayHeight = (int)_displayHeight.Value,
            DisplayName = string.IsNullOrWhiteSpace(_displayName.Text) ? @"\\.\DISPLAY1" : _displayName.Text.Trim(),
            WatchFps = advanced.WatchFps,
            BlackoutOnBlack = advanced.BlackoutOnBlack,
            InstallCs2Gsi = advanced.InstallCs2Gsi,
            CreateShortcuts = advanced.CreateShortcuts,
            UseDirectBridge = advanced.UseDirectBridge,
            StudyIp = advanced.StudyIp,
            UpperIp = advanced.UpperIp,
            LowerIp = advanced.LowerIp,
            AdvancedSettings = advanced,
            Devices = _devices.ToList()
        };
    }

    private void ValidateSetup()
    {
        if (!File.Exists(_artemisPath.Text))
            throw new InvalidOperationException("Select a valid Artemis.UI.Windows.exe in Advanced.");
        if (!File.Exists(ArtemisState.DatabasePath))
            throw new InvalidOperationException("Open Artemis once before applying this setup.");
        if (_installCs2.Checked && !Directory.Exists(_cs2Path.Text))
            throw new InvalidOperationException("Select a valid CS2 cfg folder in Advanced or turn off CS2 integration.");
        if (_devices.Count == 0)
            throw new InvalidOperationException("No Artemis devices were detected. Install and enable a device provider first.");
        if (!_devices.Any(device => device.Enabled))
            throw new InvalidOperationException("Turn on at least one device before applying.");
        if (_directBridge.Checked)
        {
            ValidateIp("Study WiZ", _studyIp.Text);
            ValidateIp("Upper WiZ", _upperIp.Text);
            ValidateIp("Lower WiZ", _lowerIp.Text);
        }
    }

    private static void ValidateIp(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        if (!IPAddress.TryParse(value.Trim(), out IPAddress? address) ||
            address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            throw new InvalidOperationException($"{name} must be a valid IPv4 address.");
    }

    private void CreateShortcuts()
    {
        string folder;
        try
        {
            folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                "Artemis Setup Assistant");
            Directory.CreateDirectory(folder);
        }
        catch (UnauthorizedAccessException)
        {
            folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                "Artemis Setup Assistant");
            Directory.CreateDirectory(folder);
        }

        string setup = Path.Combine(ArtemisState.InstallRoot, "ArtemisSetupAssistant.exe");
        ArtemisState.CreateShortcut(
            Path.Combine(folder, "Setup Assistant.lnk"),
            setup,
            "",
            ArtemisState.InstallRoot,
            "Configure Artemis plugins, device positions, and lighting roles");
        ArtemisState.CreateShortcut(
            Path.Combine(folder, "Artemis Devices.lnk"),
            _artemisPath.Text,
            "--route=artemis://settings/devices",
            Path.GetDirectoryName(_artemisPath.Text)!,
            "Open Artemis device settings");
        ArtemisState.CreateShortcut(
            Path.Combine(folder, "Artemis Surface Editor.lnk"),
            _artemisPath.Text,
            "--route=artemis://surface-editor",
            Path.GetDirectoryName(_artemisPath.Text)!,
            "Map devices on the Artemis surface");
        ArtemisState.CreateShortcut(
            Path.Combine(folder, "Artemis Workshop.lnk"),
            _artemisPath.Text,
            "--route=artemis://workshop",
            Path.GetDirectoryName(_artemisPath.Text)!,
            "Browse Artemis plugins and profiles");
    }

    private void ReloadDevices()
    {
        SetupConfiguration current = ReadConfiguration();
        List<DeviceAssignment> refreshed = ArtemisState.LoadDevices(current);
        _devices.RaiseListChangedEvents = false;
        _devices.Clear();
        foreach (DeviceAssignment device in refreshed)
        {
            EnsureDeviceDefaults(device);
            device.PropertyChanged += (_, _) => _roomMap.Invalidate();
            _devices.Add(device);
        }
        _devices.RaiseListChangedEvents = true;
        _devices.ResetBindings();
        _roomMap.Devices = _devices;
        if (_selectedDevice != null)
            _selectedDevice = _devices.FirstOrDefault(device => device.DeviceId == _selectedDevice.DeviceId);
        _status.Text = $"Detected {_devices.Count} Artemis devices.";
        RefreshDeviceGroups();
        RefreshRoomDeviceList();
        SyncDeviceEditor();
        RefreshReview();
    }

    private void SyncAllDevicesForSelectedActivity()
    {
        foreach (DeviceAssignment device in _devices)
            SyncInternalRolesForSelectedActivity(device);
    }

    private void SyncInternalRolesForSelectedActivity(DeviceAssignment device)
    {
        device.Placement = SetupLabels.PlacementFromZone(device.PhysicalZone);
        device.WatchRole = _selectedPreset switch
        {
            "Watch" => SetupLabels.ToWatchRole(device.ModeAssignments.Watch),
            "Study" => SetupLabels.StudyBehaviorToWatchRole(device.ModeAssignments.Study),
            "Custom" => SetupLabels.ToWatchRole(device.ModeAssignments.Watch),
            _ => "Off"
        };
        device.GameRole = _selectedPreset switch
        {
            "Game" => SetupLabels.ToGameRole(device.ModeAssignments.Game),
            "Custom" => SetupLabels.ToGameRole(device.ModeAssignments.Game),
            _ => "Off"
        };
        device.Enabled = _selectedPreset switch
        {
            "Watch" => device.ModeAssignments.Watch != SetupLabels.WatchOff,
            "Game" => device.ModeAssignments.Game != SetupLabels.GameOff,
            "Study" => device.ModeAssignments.Study != SetupLabels.StudyOff,
            _ => device.ModeAssignments.Watch != SetupLabels.WatchOff ||
                 device.ModeAssignments.Game != SetupLabels.GameOff ||
                 device.ModeAssignments.Study != SetupLabels.StudyOff
        };
    }

    private void EnsureDeviceDefaults(DeviceAssignment device)
    {
        if (string.IsNullOrWhiteSpace(device.FriendlyName))
            device.FriendlyName = device.ShortId;
        if (string.IsNullOrWhiteSpace(device.DeviceKind) || device.DeviceKind == "Unknown")
            device.DeviceKind = SetupLabels.GuessDeviceKind(DeviceText(device));
        if (string.IsNullOrWhiteSpace(device.PhysicalZone))
            device.PhysicalZone = SetupLabels.ZoneFromPlacement(device.Placement);
        if (device.ModeAssignments == null)
            device.ModeAssignments = new ModeAssignments();
        if (string.IsNullOrWhiteSpace(device.ModeAssignments.Watch))
            device.ModeAssignments.Watch = SetupLabels.ToFriendlyWatch(device.WatchRole);
        if (string.IsNullOrWhiteSpace(device.ModeAssignments.Game))
            device.ModeAssignments.Game = SetupLabels.ToFriendlyGame(device.GameRole);
        if (string.IsNullOrWhiteSpace(device.ModeAssignments.Study))
            device.ModeAssignments.Study = device.DeviceKind == "Light" ? SetupLabels.StudyBrightDesk : SetupLabels.StudyCalmGlow;
    }

    private void EnsureModeDefaults(DeviceAssignment device)
    {
        if (device.ModeAssignments.Watch == SetupLabels.WatchOff)
            device.ModeAssignments.Watch = IsRearDevice(device) ? SetupLabels.WatchSoftDepth : SetupLabels.WatchScreenColor;
        if (device.ModeAssignments.Game == SetupLabels.GameOff)
            device.ModeAssignments.Game = IsRearDevice(device) ? SetupLabels.GameImpactOnly : SetupLabels.GameMainEffects;
        if (device.ModeAssignments.Study == SetupLabels.StudyOff)
            device.ModeAssignments.Study = IsStudyDevice(device) ? SetupLabels.StudyBrightDesk : SetupLabels.StudyCalmGlow;
    }

    private static void ApplyPlacementPreset(DeviceAssignment device)
    {
        (device.RoomX, device.RoomY) = device.PhysicalZone switch
        {
            "Screen top" => (0.5, 0.14),
            "Screen bottom" => (0.5, 0.58),
            "Left" => (0.13, 0.45),
            "Right" => (0.87, 0.45),
            "Desk" => (0.5, 0.67),
            "Rear left" => (0.25, 0.88),
            "Rear right" => (0.75, 0.88),
            _ => (0.5, 0.42)
        };
    }

    private static string PresetZone(DeviceAssignment device)
    {
        string text = DeviceText(device);
        if (text.Contains("study", StringComparison.OrdinalIgnoreCase))
            return "Screen top";
        if (text.Contains("upper", StringComparison.OrdinalIgnoreCase))
            return "Rear right";
        if (text.Contains("lower", StringComparison.OrdinalIgnoreCase))
            return "Rear right";
        if (device.DeviceKind is "Keyboard" or "Mouse" or "Dock")
            return "Desk";
        return SetupLabels.ZoneFromPlacement(device.PhysicalZone);
    }

    private static string DeviceGroup(DeviceAssignment device)
    {
        if (device.DeviceKind == "Light" && device.PhysicalZone.Contains("Rear", StringComparison.OrdinalIgnoreCase))
            return "Room lights";
        if (device.DeviceKind == "Light")
            return "Lights";
        if (device.DeviceKind is "Keyboard" or "Mouse" or "Dock")
            return "Keyboard and mouse";
        return "Unknown";
    }

    private static bool IsRearDevice(DeviceAssignment device)
    {
        string text = DeviceText(device);
        return text.Contains("upper", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("lower", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("rear", StringComparison.OrdinalIgnoreCase) ||
               device.PhysicalZone.Contains("Rear", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStudyDevice(DeviceAssignment device)
    {
        string text = DeviceText(device);
        return text.Contains("study", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("desk lamp", StringComparison.OrdinalIgnoreCase) ||
               device.PhysicalZone.Equals("Screen top", StringComparison.OrdinalIgnoreCase);
    }

    private static string DeviceText(DeviceAssignment device)
    {
        return $"{device.FriendlyName} {device.DeviceId} {device.ProviderName}";
    }

    private static string FriendlyRoleSummary(DeviceAssignment device)
    {
        List<string> roles = [];
        if (device.ModeAssignments.Watch != SetupLabels.WatchOff)
            roles.Add("Watch: " + device.ModeAssignments.Watch);
        if (device.ModeAssignments.Game != SetupLabels.GameOff)
            roles.Add("Game: " + device.ModeAssignments.Game);
        if (device.ModeAssignments.Study != SetupLabels.StudyOff)
            roles.Add("Study: " + device.ModeAssignments.Study);
        return roles.Count == 0 ? "Off" : string.Join(", ", roles);
    }

    private static IReadOnlyList<PluginRecommendation> PluginRecommendations() =>
    [
        new("Razer devices", "Keyboard, mouse, dock, and Chroma-compatible hardware.", ["Razer Devices", "Razer"]),
        new("Smart room lights", "Hue, WLED, Nanoleaf, OpenRGB, or similar room providers.", ["Philips Hue", "WLED", "Nanoleaf", "OpenRGB Devices"]),
        new("Ambilight", "Required before Guided Watch can clone a screen-reactive profile.", ["Ambilight Smoothed", "Ambilight"]),
        new("Counter-Strike 2", "CS2 profile support plus the optional GSI file from this assistant.", ["Counter-Strike 2", "Counter Strike 2", "Counter"]),
        new("Valorant", "Use when you want Valorant-oriented profiles or effects.", ["Valorant"])
    ];

    private WorkshopEntry? FindPlugin(IEnumerable<string> keywords)
    {
        foreach (string keyword in keywords)
        {
            WorkshopEntry? exact = _allPlugins.FirstOrDefault(plugin =>
                plugin.Name.Equals(keyword, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
                return exact;
            WorkshopEntry? partial = _allPlugins.FirstOrDefault(plugin =>
                plugin.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            if (partial != null)
                return partial;
        }
        return null;
    }

    private void OpenPlugin(WorkshopEntry plugin)
    {
        OpenArtemisRoute($"artemis://workshop/entries/plugins/details/{plugin.Id}");
    }

    private void OpenArtemisRoute(string route)
    {
        if (!File.Exists(_artemisPath.Text))
        {
            MessageBox.Show("Select the Artemis application in Advanced first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        ArtemisState.StartArtemis(_artemisPath.Text, route);
    }

    private static void RunTool(string tool, params string[] arguments)
    {
        ProcessStartInfo start = new()
        {
            FileName = tool,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments)
            start.ArgumentList.Add(argument);

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start the Artemis configuration tool.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Configuration failed: {error}{output}");
    }

    private void UpdateDirectControls()
    {
        bool enabled = _directBridge.Checked;
        _studyIp.Enabled = enabled;
        _upperIp.Enabled = enabled;
        _lowerIp.Enabled = enabled;
    }

    private void ToggleUi(bool enabled)
    {
        if (InvokeRequired)
        {
            Invoke(() => ToggleUi(enabled));
            return;
        }
        _content.Enabled = enabled;
        _stepRail.Enabled = enabled;
        _back.Enabled = enabled && (_showingAdvanced || _stepIndex > 0);
        _next.Enabled = enabled && !_showingAdvanced && _stepIndex < StepNames.Length - 1;
        _advanced.Enabled = enabled && !_showingAdvanced;
        _apply.Enabled = enabled && !Program.PreviewMode;
    }

    private void SetStatus(string text)
    {
        if (InvokeRequired)
        {
            Invoke(() => SetStatus(text));
            return;
        }
        _status.Text = text;
    }

    private void ConfigurePluginGrid()
    {
        if (_pluginGridConfigured)
            return;
        _pluginGridConfigured = true;
        ConfigureGrid(_pluginGrid);
        _pluginGrid.AutoGenerateColumns = false;
        _pluginGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(WorkshopEntry.Name),
            HeaderText = "Plugin",
            Width = 220
        });
        _pluginGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(WorkshopEntry.Type),
            HeaderText = "Type",
            Width = 130
        });
        _pluginGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(WorkshopEntry.Status),
            HeaderText = "Status",
            Width = 140
        });
        _pluginGrid.DataSource = _visiblePlugins;
        _pluginGrid.SelectionChanged += (_, _) => ShowSelectedPlugin();
    }

    private void ConfigureAdvancedDeviceGrid()
    {
        if (_advancedDeviceGridConfigured)
            return;
        _advancedDeviceGridConfigured = true;
        ConfigureGrid(_advancedDeviceGrid);
        _advancedDeviceGrid.AutoGenerateColumns = false;
        _advancedDeviceGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(DeviceAssignment.FriendlyName),
            HeaderText = "Device",
            Width = 150
        });
        _advancedDeviceGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(DeviceAssignment.RedScale),
            HeaderText = "R",
            Width = 45
        });
        _advancedDeviceGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(DeviceAssignment.GreenScale),
            HeaderText = "G",
            Width = 45
        });
        _advancedDeviceGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(DeviceAssignment.BlueScale),
            HeaderText = "B",
            Width = 45
        });
        _advancedDeviceGrid.DataSource = _devices;
        _advancedDeviceGrid.DataError += (_, e) => e.ThrowException = false;
    }

    private static Panel CreatePage(string title, string subtitle)
    {
        Panel page = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(24, 28, 34)
        };
        Label titleLabel = new()
        {
            Text = title,
            Location = new Point(24, 22),
            Size = new Size(560, 34),
            Font = new Font("Segoe UI Semibold", 18),
            ForeColor = Color.White
        };
        Label subtitleLabel = new()
        {
            Text = subtitle,
            Location = new Point(26, 62),
            Size = new Size(812, 28),
            ForeColor = Color.FromArgb(172, 181, 194)
        };
        page.Controls.Add(titleLabel);
        page.Controls.Add(subtitleLabel);
        return page;
    }

    private static Panel CreateCard(int x, int y, int width, int height)
    {
        return new Panel
        {
            Location = new Point(x, y),
            Size = new Size(width, height),
            BackColor = Color.FromArgb(33, 38, 45)
        };
    }

    private static Control CreateInfoCard(string title, string body, int width, int height = 96)
    {
        Panel panel = CreateCard(0, 0, width, height);
        panel.Margin = new Padding(8);
        Label titleLabel = new()
        {
            Text = title,
            Location = new Point(14, 12),
            Size = new Size(width - 28, 24),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 10.5f)
        };
        Label bodyLabel = new()
        {
            Text = body,
            Location = new Point(14, 42),
            Size = new Size(width - 28, height - 48),
            ForeColor = Color.FromArgb(176, 186, 200)
        };
        panel.Controls.Add(titleLabel);
        panel.Controls.Add(bodyLabel);
        return panel;
    }

    private static Label AddLabel(Control parent, string text, int x, int y, int width, int height)
    {
        Label label = new()
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, height),
            ForeColor = Color.FromArgb(218, 223, 231)
        };
        parent.Controls.Add(label);
        return label;
    }

    private static Button AddButton(Control parent, string text, int x, int y, int width, int height)
    {
        Button button = new()
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, height),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(48, 54, 64),
            ForeColor = Color.White
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(75, 85, 100);
        parent.Controls.Add(button);
        return button;
    }

    private static void StyleButton(Button button, bool primary)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = primary ? Color.FromArgb(126, 171, 235) : Color.FromArgb(75, 85, 100);
        button.BackColor = primary ? Color.FromArgb(70, 117, 187) : Color.FromArgb(48, 54, 64);
        button.ForeColor = Color.White;
    }

    private static void ConfigureTextBox(TextBox box, int x, int y, int width)
    {
        box.Location = new Point(x, y);
        box.Size = new Size(width, 29);
        box.BackColor = Color.FromArgb(38, 43, 51);
        box.ForeColor = Color.White;
        box.BorderStyle = BorderStyle.FixedSingle;
    }

    private static void ConfigureCombo(ComboBox combo, IEnumerable<string> values, int x, int y, int width)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.Items.Clear();
        combo.Items.AddRange(values.Cast<object>().ToArray());
        combo.Location = new Point(x, y);
        combo.Size = new Size(width, 29);
        combo.BackColor = Color.FromArgb(38, 43, 51);
        combo.ForeColor = Color.White;
    }

    private static void ConfigureNumeric(
        NumericUpDown input,
        int x,
        int y,
        int width,
        decimal minimum,
        decimal maximum,
        decimal value)
    {
        input.Location = new Point(x, y);
        input.Size = new Size(width, 29);
        input.Minimum = minimum;
        input.Maximum = maximum;
        input.Value = Math.Clamp(value, minimum, maximum);
        input.BackColor = Color.FromArgb(38, 43, 51);
        input.ForeColor = Color.White;
    }

    private static void ConfigureCheck(CheckBox check, Control parent, int x, int y, int width)
    {
        check.Location = new Point(x, y);
        check.Size = new Size(width, 28);
        check.FlatStyle = FlatStyle.Flat;
        check.ForeColor = Color.FromArgb(230, 234, 240);
        parent.Controls.Add(check);
    }

    private static void ConfigureGrid(DataGridView grid)
    {
        grid.BackgroundColor = Color.FromArgb(22, 25, 30);
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.RowHeadersVisible = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.RowTemplate.Height = 28;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 51, 60);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(45, 51, 60);
        grid.EnableHeadersVisualStyles = false;
        grid.DefaultCellStyle.BackColor = Color.FromArgb(30, 35, 42);
        grid.DefaultCellStyle.ForeColor = Color.FromArgb(229, 233, 239);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(67, 97, 140);
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
    }

    private void BrowseArtemis()
    {
        using OpenFileDialog dialog = new()
        {
            Filter = "Artemis.UI.Windows.exe|Artemis.UI.Windows.exe|Applications|*.exe",
            FileName = "Artemis.UI.Windows.exe"
        };
        if (dialog.ShowDialog() == DialogResult.OK)
            _artemisPath.Text = dialog.FileName;
    }

    private static void BrowseFolder(TextBox target)
    {
        using FolderBrowserDialog dialog = new() { SelectedPath = target.Text };
        if (dialog.ShowDialog() == DialogResult.OK)
            target.Text = dialog.SelectedPath;
    }

    private static void SetComboValue(ComboBox combo, string value)
    {
        int index = combo.Items.IndexOf(value);
        combo.SelectedIndex = index >= 0 ? index : 0;
    }

    private static string SelectedText(ComboBox combo, string fallback)
    {
        return combo.SelectedItem?.ToString() ?? fallback;
    }

    private sealed record PluginRecommendation(string Title, string Summary, string[] Keywords);
}
