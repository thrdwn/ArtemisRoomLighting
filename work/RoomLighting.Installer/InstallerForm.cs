using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Reflection;

namespace ArtemisRoomLighting.Installer;

internal sealed class InstallerForm : Form
{
    private static readonly string[] Placements =
    [
        "Screen top", "Screen bottom", "Left", "Right", "Desk", "Rear left", "Rear right", "Room"
    ];
    private static readonly string[] WatchRoles = ["Screen sample", "Soft depth", "Base glow", "Off"];
    private static readonly string[] GameRoles = ["Full game", "Team ambient", "Impact alerts", "Off"];

    private readonly TextBox _artemisPath = new();
    private readonly TabControl _tabs = new();
    private readonly TextBox _cs2Path = new();
    private readonly TextBox _pluginSearch = new();
    private readonly DataGridView _pluginGrid = new();
    private readonly RichTextBox _pluginDetails = new();
    private readonly Button _openPlugin = new();
    private readonly DataGridView _deviceGrid = new();
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
    private readonly Label _status = new();
    private readonly Button _apply = new();
    private readonly BindingList<DeviceAssignment> _devices = [];
    private readonly BindingList<WorkshopEntry> _visiblePlugins = [];
    private readonly List<WorkshopEntry> _allPlugins = [];
    private SetupConfiguration _configuration = new();

    public InstallerForm()
    {
        Text = "Artemis Setup Assistant";
        ClientSize = new Size(1160, 760);
        MinimumSize = new Size(1040, 700);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(19, 21, 25);
        ForeColor = Color.FromArgb(239, 242, 247);
        Font = new Font("Segoe UI", 9.5f);

        Label title = new()
        {
            Text = "Artemis Setup Assistant",
            Location = new Point(22, 15),
            Size = new Size(500, 35),
            Font = new Font("Segoe UI Semibold", 19)
        };
        Label subtitle = new()
        {
            Text = "Use Artemis' existing plugins, map the devices in your room, and choose what each one does.",
            Location = new Point(24, 52),
            Size = new Size(900, 24),
            ForeColor = Color.FromArgb(170, 178, 190)
        };
        Controls.Add(title);
        Controls.Add(subtitle);

        _tabs.Location = new Point(20, 84);
        _tabs.Size = new Size(1120, 606);
        _tabs.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _tabs.TabPages.Add(BuildOverviewPage());
        _tabs.TabPages.Add(BuildPluginsPage());
        _tabs.TabPages.Add(BuildDevicesPage());
        _tabs.TabPages.Add(BuildAdvancedPage());
        Controls.Add(_tabs);

        _status.Location = new Point(22, 704);
        _status.Size = new Size(860, 35);
        _status.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _status.ForeColor = Color.FromArgb(170, 178, 190);
        _status.Text = "Loading Artemis devices and Workshop plugins...";
        Controls.Add(_status);

        _apply.Text = "Apply setup";
        _apply.Location = new Point(982, 700);
        _apply.Size = new Size(156, 42);
        _apply.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        StylePrimaryButton(_apply);
        _apply.Click += ApplyClicked;
        _apply.Enabled = !Program.PreviewMode;
        Controls.Add(_apply);

        Load += async (_, _) => await LoadStateAsync();
    }

    private TabPage BuildOverviewPage()
    {
        TabPage page = CreatePage("Requirements");
        Label intro = AddLabel(
            page,
            "This assistant does not replace Artemis device support. Install the provider made for your hardware, " +
            "let Artemis detect it, then return here to map it.",
            24,
            20,
            1030,
            48);
        intro.Font = new Font("Segoe UI Semibold", 11);

        AddLabel(page, "Required", 24, 84, 200, 26).Font = new Font("Segoe UI Semibold", 12);
        AddLabel(
            page,
            "Windows 10/11 x64\nArtemis 1.2025.1222.3 or newer, opened at least once\nInternet access for the Workshop catalog\nThe vendor app or SDK required by the chosen device provider",
            24,
            116,
            520,
            105);

        AddLabel(page, "Artemis application", 24, 246, 210, 24);
        ConfigureTextBox(_artemisPath, 24, 272, 760);
        page.Controls.Add(_artemisPath);
        Button browse = AddButton(page, "Browse", 800, 270, 100, 31);
        browse.Click += (_, _) => BrowseArtemis();

        AddLabel(page, "Main display capture", 24, 328, 220, 24);
        ConfigureNumeric(_displayWidth, 24, 356, 110, 640, 7680, 1920);
        page.Controls.Add(_displayWidth);
        AddLabel(page, "x", 140, 359, 20, 24);
        ConfigureNumeric(_displayHeight, 164, 356, 110, 480, 4320, 1080);
        page.Controls.Add(_displayHeight);
        ConfigureTextBox(_displayName, 296, 356, 210);
        page.Controls.Add(_displayName);
        _displayName.PlaceholderText = @"\\.\DISPLAY1";

        AddLabel(page, "Watch capture FPS", 540, 328, 170, 24);
        ConfigureNumeric(_watchFps, 540, 356, 110, 1, 60, 30);
        page.Controls.Add(_watchFps);
        _blackout.Text = "Turn mapped lights off for sampled black zones";
        ConfigureCheck(_blackout, page, 540, 399, 390);

        Button openDevices = AddButton(page, "Open Artemis devices", 24, 466, 190, 38);
        openDevices.Click += (_, _) => OpenArtemisRoute("artemis://settings/devices");
        Button openSurface = AddButton(page, "Open Surface Editor", 228, 466, 190, 38);
        openSurface.Click += (_, _) => OpenArtemisRoute("artemis://surface-editor");
        Button openWorkshop = AddButton(page, "Open Workshop", 432, 466, 160, 38);
        openWorkshop.Click += (_, _) => OpenArtemisRoute("artemis://workshop");
        return page;
    }

    private TabPage BuildPluginsPage()
    {
        TabPage page = CreatePage("Plugins");
        AddLabel(
            page,
            "The catalog comes from the official Artemis Workshop. It includes Razer and other device providers, " +
            "game integrations, Ambilight brushes, modules, and effects.",
            20,
            16,
            1030,
            42);
        ConfigureTextBox(_pluginSearch, 20, 63, 420);
        page.Controls.Add(_pluginSearch);
        _pluginSearch.PlaceholderText = "Search plugins, device brands, games, or effects";
        _pluginSearch.TextChanged += (_, _) => FilterPlugins();
        Button refresh = AddButton(page, "Refresh", 454, 61, 100, 31);
        refresh.Click += async (_, _) => await LoadPluginsAsync();

        ConfigureGrid(_pluginGrid);
        _pluginGrid.Location = new Point(20, 106);
        _pluginGrid.Size = new Size(690, 430);
        _pluginGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
        _pluginGrid.AutoGenerateColumns = false;
        _pluginGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(WorkshopEntry.Name),
            HeaderText = "Plugin",
            Width = 250
        });
        _pluginGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(WorkshopEntry.Type),
            HeaderText = "Type",
            Width = 145
        });
        _pluginGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(WorkshopEntry.Status),
            HeaderText = "Status",
            Width = 145
        });
        _pluginGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(WorkshopEntry.Version),
            HeaderText = "Latest",
            Width = 120
        });
        _pluginGrid.DataSource = _visiblePlugins;
        _pluginGrid.SelectionChanged += (_, _) => ShowSelectedPlugin();
        page.Controls.Add(_pluginGrid);

        _pluginDetails.Location = new Point(740, 108);
        _pluginDetails.Size = new Size(340, 330);
        _pluginDetails.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _pluginDetails.ForeColor = Color.FromArgb(205, 211, 220);
        _pluginDetails.BackColor = Color.FromArgb(26, 29, 34);
        _pluginDetails.BorderStyle = BorderStyle.None;
        _pluginDetails.ReadOnly = true;
        _pluginDetails.ScrollBars = RichTextBoxScrollBars.Vertical;
        page.Controls.Add(_pluginDetails);

        _openPlugin.Text = "Open selected in Artemis";
        _openPlugin.Location = new Point(740, 458);
        _openPlugin.Size = new Size(220, 38);
        StylePrimaryButton(_openPlugin);
        _openPlugin.Click += (_, _) =>
        {
            if (SelectedPlugin() is WorkshopEntry plugin)
                OpenArtemisRoute($"artemis://workshop/entries/plugins/details/{plugin.Id}");
        };
        page.Controls.Add(_openPlugin);
        return page;
    }

    private TabPage BuildDevicesPage()
    {
        TabPage page = CreatePage("Devices and room map");
        AddLabel(
            page,
            "Drag each enabled device to its physical position. Screen sample follows that zone; Soft depth gives " +
            "rear lights a restrained room extension; Impact alerts reserves a device for strong game events.",
            18,
            12,
            1050,
            45);

        _roomMap.Location = new Point(18, 66);
        _roomMap.Size = new Size(425, 470);
        _roomMap.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
        _roomMap.SelectedDeviceChanged += (_, device) => SelectDeviceRow(device);
        _roomMap.DeviceMoved += (_, _) => _deviceGrid.Refresh();
        page.Controls.Add(_roomMap);
        Button refreshDevices = AddButton(page, "Refresh detected devices", 18, 542, 205, 34);
        refreshDevices.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        refreshDevices.Click += (_, _) => ReloadDevices();

        ConfigureGrid(_deviceGrid);
        _deviceGrid.Location = new Point(458, 66);
        _deviceGrid.Size = new Size(620, 470);
        _deviceGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _deviceGrid.AutoGenerateColumns = false;
        _deviceGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(DeviceAssignment.Enabled),
            HeaderText = "On",
            Width = 42
        });
        _deviceGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(DeviceAssignment.FriendlyName),
            HeaderText = "Device",
            Width = 150
        });
        _deviceGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(DeviceAssignment.ProviderName),
            HeaderText = "Provider",
            ReadOnly = true,
            Width = 115
        });
        _deviceGrid.Columns.Add(new DataGridViewComboBoxColumn
        {
            DataPropertyName = nameof(DeviceAssignment.Placement),
            HeaderText = "Position",
            DataSource = Placements,
            Width = 100
        });
        _deviceGrid.Columns.Add(new DataGridViewComboBoxColumn
        {
            DataPropertyName = nameof(DeviceAssignment.WatchRole),
            HeaderText = "Watch",
            DataSource = WatchRoles,
            Width = 105
        });
        _deviceGrid.Columns.Add(new DataGridViewComboBoxColumn
        {
            DataPropertyName = nameof(DeviceAssignment.GameRole),
            HeaderText = "Games",
            DataSource = GameRoles,
            Width = 105
        });
        _deviceGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(DeviceAssignment.Intensity),
            HeaderText = "%",
            Width = 48
        });
        _deviceGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(DeviceAssignment.RedScale),
            HeaderText = "R",
            Width = 45
        });
        _deviceGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(DeviceAssignment.GreenScale),
            HeaderText = "G",
            Width = 45
        });
        _deviceGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(DeviceAssignment.BlueScale),
            HeaderText = "B",
            Width = 45
        });
        _deviceGrid.DataSource = _devices;
        _deviceGrid.SelectionChanged += (_, _) =>
        {
            if (_deviceGrid.CurrentRow?.DataBoundItem is DeviceAssignment device)
                _roomMap.SelectedDevice = device;
        };
        _deviceGrid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_deviceGrid.IsCurrentCellDirty)
                _deviceGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _deviceGrid.CellValueChanged += DeviceValueChanged;
        _deviceGrid.DataError += (_, e) => e.ThrowException = false;
        page.Controls.Add(_deviceGrid);
        return page;
    }

    private TabPage BuildAdvancedPage()
    {
        TabPage page = CreatePage("Advanced compatibility");
        AddLabel(
            page,
            "Use the official Artemis provider whenever one exists. The bundled direct bridge is an optional fallback " +
            "for WiZ LAN bulbs or hardware that cannot be made reliable through a Workshop provider.",
            22,
            18,
            1020,
            48);

        _directBridge.Text = "Install/keep the optional direct WiZ compatibility bridge";
        ConfigureCheck(_directBridge, page, 22, 86, 520);
        _directBridge.CheckedChanged += (_, _) => UpdateDirectControls();

        AddLabel(page, "Study WiZ IPv4", 22, 135, 180, 24);
        ConfigureTextBox(_studyIp, 22, 162, 220);
        page.Controls.Add(_studyIp);
        AddLabel(page, "Upper/rear WiZ IPv4", 272, 135, 190, 24);
        ConfigureTextBox(_upperIp, 272, 162, 220);
        page.Controls.Add(_upperIp);
        AddLabel(page, "Lower/rear WiZ IPv4", 522, 135, 190, 24);
        ConfigureTextBox(_lowerIp, 522, 162, 220);
        page.Controls.Add(_lowerIp);

        AddLabel(page, "Counter-Strike 2 cfg folder", 22, 226, 260, 24);
        ConfigureTextBox(_cs2Path, 22, 253, 720);
        page.Controls.Add(_cs2Path);
        Button browseCs = AddButton(page, "Browse", 758, 251, 100, 31);
        browseCs.Click += (_, _) => BrowseFolder(_cs2Path);
        _installCs2.Text = "Install/update the CS2 Game State Integration file";
        ConfigureCheck(_installCs2, page, 22, 297, 460);

        _createShortcuts.Text = "Create Start menu shortcuts for setup, Workshop, devices, and Surface Editor";
        ConfigureCheck(_createShortcuts, page, 22, 349, 650);

        Label note = AddLabel(
            page,
            "Provider conflicts: do not control the same physical device through both an official provider and the " +
            "direct bridge. The direct bridge installs with its Razer and Lenovo adapters disabled by default.",
            22,
            412,
            830,
            60);
        note.ForeColor = Color.FromArgb(235, 184, 105);
        return page;
    }

    private async Task LoadStateAsync()
    {
        _configuration = ArtemisState.LoadConfiguration();
        _artemisPath.Text = ArtemisState.DetectArtemis();
        _cs2Path.Text = ArtemisState.DetectCs2Cfg();
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
            foreach (DeviceAssignment device in ArtemisState.LoadDevices(_configuration))
            {
                device.PropertyChanged += (_, _) => _roomMap.Invalidate();
                _devices.Add(device);
            }
            _roomMap.Devices = _devices;
            _status.Text = _devices.Count == 0
                ? "No Artemis devices found. Install/enable a provider, restart Artemis, then reopen this assistant."
                : $"Detected {_devices.Count} Artemis devices. Loading the Workshop catalog...";
        }
        catch (Exception ex)
        {
            _status.Text = "Could not read Artemis devices: " + ex.Message;
        }

        await LoadPluginsAsync();
        _tabs.SelectedIndex = 0;
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
            int providers = _allPlugins.Count(plugin =>
                plugin.Categories.Any(category => category.Contains("Device", StringComparison.OrdinalIgnoreCase)));
            _status.Text = $"Workshop ready: {_allPlugins.Count} plugins, including {providers} device providers.";
        }
        catch (Exception ex)
        {
            _status.Text = "Workshop is temporarily unavailable. Installed devices can still be mapped. " + ex.Message;
        }
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
            ? "Select a plugin to see its purpose and requirements."
            : $"{plugin.Name}\n\n{plugin.Summary}\n\nType: {string.Join(", ", plugin.Categories)}\n" +
              $"Author: {plugin.Author}\nStatus: {plugin.Status}\n\nRequirements\n{plugin.Requirements}";
    }

    private WorkshopEntry? SelectedPlugin() => _pluginGrid.CurrentRow?.DataBoundItem as WorkshopEntry;

    private async void ApplyClicked(object? sender, EventArgs e)
    {
        try
        {
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
            _deviceGrid.EndEdit();
            SetupConfiguration configuration = ReadConfiguration();
            await Task.Run(() => ApplySetup(configuration));
            _status.Text = "Setup applied. Artemis is opening the Surface Editor for a final visual check.";
            ArtemisState.StartArtemis(_artemisPath.Text, "artemis://surface-editor");
            MessageBox.Show(
                "Your setup was applied. Fine-tune device outlines in the Artemis Surface Editor if needed.",
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
        return new SetupConfiguration
        {
            Version = 1,
            DisplayWidth = (int)_displayWidth.Value,
            DisplayHeight = (int)_displayHeight.Value,
            DisplayName = string.IsNullOrWhiteSpace(_displayName.Text) ? @"\\.\DISPLAY1" : _displayName.Text.Trim(),
            WatchFps = (int)_watchFps.Value,
            BlackoutOnBlack = _blackout.Checked,
            InstallCs2Gsi = _installCs2.Checked,
            CreateShortcuts = _createShortcuts.Checked,
            UseDirectBridge = _directBridge.Checked,
            StudyIp = _studyIp.Text.Trim(),
            UpperIp = _upperIp.Text.Trim(),
            LowerIp = _lowerIp.Text.Trim(),
            Devices = _devices.ToList()
        };
    }

    private void ValidateSetup()
    {
        if (!File.Exists(_artemisPath.Text))
            throw new InvalidOperationException("Select a valid Artemis.UI.Windows.exe.");
        if (!File.Exists(ArtemisState.DatabasePath))
            throw new InvalidOperationException("Open Artemis once before applying this setup.");
        if (_installCs2.Checked && !Directory.Exists(_cs2Path.Text))
            throw new InvalidOperationException("Select a valid CS2 cfg folder or turn off CS2 GSI installation.");
        if (_devices.Count == 0)
            throw new InvalidOperationException("No Artemis devices were detected. Install and enable a device provider first.");
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

    private void DeviceValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _deviceGrid.Rows[e.RowIndex].DataBoundItem is not DeviceAssignment device)
            return;
        string property = _deviceGrid.Columns[e.ColumnIndex].DataPropertyName;
        if (property == nameof(DeviceAssignment.Placement))
            ApplyPlacementPreset(device);
        _roomMap.Invalidate();
    }

    private static void ApplyPlacementPreset(DeviceAssignment device)
    {
        (device.RoomX, device.RoomY) = device.Placement switch
        {
            "Screen top" => (0.5, 0.12),
            "Screen bottom" => (0.5, 0.61),
            "Left" => (0.12, 0.45),
            "Right" => (0.88, 0.45),
            "Desk" => (0.5, 0.78),
            "Rear left" => (0.18, 0.88),
            "Rear right" => (0.82, 0.88),
            _ => (0.5, 0.5)
        };
    }

    private void SelectDeviceRow(DeviceAssignment? device)
    {
        if (device == null)
            return;
        foreach (DataGridViewRow row in _deviceGrid.Rows)
        {
            if (!ReferenceEquals(row.DataBoundItem, device))
                continue;
            row.Selected = true;
            _deviceGrid.CurrentCell = row.Cells[Math.Min(1, row.Cells.Count - 1)];
            break;
        }
    }

    private void UpdateDirectControls()
    {
        bool enabled = _directBridge.Checked;
        _studyIp.Enabled = enabled;
        _upperIp.Enabled = enabled;
        _lowerIp.Enabled = enabled;
    }

    private void ReloadDevices()
    {
        SetupConfiguration current = ReadConfiguration();
        List<DeviceAssignment> refreshed = ArtemisState.LoadDevices(current);
        _devices.RaiseListChangedEvents = false;
        _devices.Clear();
        foreach (DeviceAssignment device in refreshed)
        {
            device.PropertyChanged += (_, _) => _roomMap.Invalidate();
            _devices.Add(device);
        }
        _devices.RaiseListChangedEvents = true;
        _devices.ResetBindings();
        _roomMap.Devices = _devices;
        _status.Text = $"Detected {_devices.Count} Artemis devices.";
    }

    private void OpenArtemisRoute(string route)
    {
        if (!File.Exists(_artemisPath.Text))
        {
            MessageBox.Show("Select the Artemis application first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        ArtemisState.StartArtemis(_artemisPath.Text, route);
    }

    private void ToggleUi(bool enabled)
    {
        if (InvokeRequired)
        {
            Invoke(() => ToggleUi(enabled));
            return;
        }
        _apply.Enabled = enabled;
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

    private static TabPage CreatePage(string text) => new()
    {
        Text = text,
        BackColor = Color.FromArgb(26, 29, 34),
        ForeColor = Color.FromArgb(239, 242, 247)
    };

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
            BackColor = Color.FromArgb(48, 53, 62),
            ForeColor = Color.White
        };
        parent.Controls.Add(button);
        return button;
    }

    private static void StylePrimaryButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(115, 159, 224);
        button.BackColor = Color.FromArgb(69, 116, 187);
        button.ForeColor = Color.White;
    }

    private static void ConfigureTextBox(TextBox box, int x, int y, int width)
    {
        box.Location = new Point(x, y);
        box.Size = new Size(width, 29);
        box.BackColor = Color.FromArgb(38, 42, 49);
        box.ForeColor = Color.White;
        box.BorderStyle = BorderStyle.FixedSingle;
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
        input.Value = value;
        input.BackColor = Color.FromArgb(38, 42, 49);
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
        grid.BackgroundColor = Color.FromArgb(22, 25, 29);
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.RowHeadersVisible = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        grid.RowTemplate.Height = 29;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 50, 58);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(45, 50, 58);
        grid.EnableHeadersVisualStyles = false;
        grid.DefaultCellStyle.BackColor = Color.FromArgb(30, 34, 40);
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
}
