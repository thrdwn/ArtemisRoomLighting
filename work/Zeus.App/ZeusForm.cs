namespace ZeusLighting;

internal sealed class ZeusForm : Form
{
    private readonly Panel _nav = new();
    private readonly Panel _content = new();
    private readonly Label _title = new();
    private readonly Label _subtitle = new();
    private readonly Label _status = new();
    private readonly List<Button> _navButtons = [];
    private readonly List<Button> _modeButtons = [];
    private readonly List<ZeusDevice> _devices = SeedDevices();
    private readonly List<ZeusProfile> _profiles = SeedProfiles();
    private readonly List<ZeusBackend> _backends = SeedBackends();
    private readonly RoomCanvas _roomCanvas = new();
    private readonly RoomCanvas _overviewCanvas = new();

    private ZeusDevice? _selectedDevice;
    private string _activePage = "Home";
    private string _activeMode = "Watch";

    public ZeusForm()
    {
        Text = "Zeus Lighting";
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(1280, 800);
        MinimumSize = new Size(1080, 720);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = ZeusTheme.Background;
        ForeColor = ZeusTheme.Text;
        Font = ZeusTheme.BodyFont();

        _selectedDevice = _devices[0];
        BuildShell();
        _roomCanvas.Devices = _devices;
        _roomCanvas.SelectedDevice = _selectedDevice;
        _overviewCanvas.Devices = _devices;
        _overviewCanvas.SelectedDevice = _selectedDevice;

        foreach (RoomCanvas canvas in new[] { _roomCanvas, _overviewCanvas })
        {
            canvas.SelectedDeviceChanged += (_, device) =>
            {
                _selectedDevice = device;
                _roomCanvas.SelectedDevice = device;
                _overviewCanvas.SelectedDevice = device;
                if (_activePage == "Room")
                    Navigate("Room");
                else if (device != null)
                    _status.Text = $"{device.Name} selected. Open Room to place it or change behavior.";
            };
            canvas.DeviceMoved += (_, _) =>
            {
                if (_selectedDevice != null)
                    _status.Text = $"{_selectedDevice.Name} moved to {_selectedDevice.Zone}.";
                _overviewCanvas.Invalidate();
                _roomCanvas.Invalidate();
            };
        }

        Navigate("Home");
    }

    private void BuildShell()
    {
        _title.Text = "Zeus Lighting";
        _title.Location = new Point(24, 16);
        _title.Size = new Size(300, 38);
        _title.Font = ZeusTheme.DisplayFont();
        _title.ForeColor = ZeusTheme.Text;
        Controls.Add(_title);

        _subtitle.Text = "Pick a mode, place each light, test the room.";
        _subtitle.Location = new Point(28, 56);
        _subtitle.Size = new Size(620, 24);
        _subtitle.ForeColor = ZeusTheme.TextMuted;
        Controls.Add(_subtitle);

        _nav.Location = new Point(18, 96);
        _nav.Size = new Size(220, 644);
        _nav.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
        _nav.BackColor = ZeusTheme.Surface;
        Controls.Add(_nav);

        string[] pages = ["Home", "Room", "Devices", "Profiles", "Plugins", "Performance"];
        for (int i = 0; i < pages.Length; i++)
        {
            Button button = new ZeusButton()
            {
                Text = pages[i],
                Tag = pages[i],
                Location = new Point(14, 16 + i * 50),
                Size = new Size(192, 40),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = ZeusTheme.LabelFont(10),
                ForeColor = ZeusTheme.Text
            };
            button.Click += (_, _) => Navigate((string)button.Tag);
            _navButtons.Add(button);
            _nav.Controls.Add(button);
        }

        Label railTitle = new()
        {
            Text = "Connected",
            Location = new Point(18, 350),
            Size = new Size(170, 22),
            Font = ZeusTheme.LabelFont(9.5f),
            ForeColor = ZeusTheme.Text
        };
        _nav.Controls.Add(railTitle);

        AddRailChip("7 devices", 18, 382, ZeusTheme.Success);
        AddRailChip("Artemis ready", 18, 422, ZeusTheme.Blue);
        AddRailChip("WiZ LAN", 18, 462, ZeusTheme.Amber);

        _content.Location = new Point(254, 96);
        _content.Size = new Size(1008, 644);
        _content.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _content.BackColor = ZeusTheme.Surface;
        Controls.Add(_content);

        _status.Location = new Point(28, 762);
        _status.Size = new Size(930, 30);
        _status.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _status.ForeColor = ZeusTheme.TextMuted;
        _status.Text = "Ready. Start a mode or flash a device to identify it.";
        Controls.Add(_status);

        Button watch = HeaderButton("Watch", 580, 27, 92);
        Button cs2 = HeaderButton("CS2", 680, 27, 72);
        Button study = HeaderButton("Study", 760, 27, 82);
        foreach (Button modeButton in new[] { watch, cs2, study })
        {
            modeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            modeButton.Tag = modeButton.Text;
            modeButton.Click += (_, _) =>
            {
                _activeMode = (string)modeButton.Tag;
                _status.Text = $"{_activeMode} mode selected.";
                Navigate(_activePage);
            };
            _modeButtons.Add(modeButton);
            Controls.Add(modeButton);
        }

        Button testRoom = HeaderButton("Test", 850, 27, 92);
        testRoom.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        testRoom.Click += (_, _) => _status.Text = "Room test: flash each zone, then check red, green, blue, orange, teal, warm white, cool white, skin tone, and black.";
        Controls.Add(testRoom);
    }

    private void Navigate(string page)
    {
        _activePage = page;
        foreach (Button button in _navButtons)
        {
            bool active = (button.Tag as string) == page;
            if (button is ZeusButton zeusButton)
                zeusButton.Active = active;
            button.Invalidate();
        }
        foreach (Button button in _modeButtons)
        {
            if (button is ZeusButton zeusButton)
                zeusButton.Active = string.Equals((string?)button.Tag, _activeMode, StringComparison.OrdinalIgnoreCase);
            button.Invalidate();
        }

        _content.Controls.Clear();
        _content.Controls.Add(page switch
        {
            "Room" => BuildRoomPage(),
            "Devices" => BuildDevicesPage(),
            "Profiles" => BuildProfilesPage(),
            "Plugins" => BuildPluginsPage(),
            "Performance" => BuildPerformancePage(),
            _ => BuildHomePage()
        });
    }

    private Control BuildHomePage()
    {
        Panel page = Page("Start", "Choose the experience, then place lights in the room.");

        Panel room = Card(24, 104, 720, 396);
        AddLabel(room, "Room at a glance", 18, 16, 260, 28, 14, true);
        AddMuted(room, "Drag on the Room page. Click a bubble here to select it.", 20, 46, 430, 22);
        AddStatusChip(room, _activeMode + " active", 566, 18, 118, ZeusTheme.Action);

        if (_overviewCanvas.Parent != null)
            _overviewCanvas.Parent.Controls.Remove(_overviewCanvas);
        _overviewCanvas.Location = new Point(18, 82);
        _overviewCanvas.Size = new Size(682, 250);
        _overviewCanvas.Devices = _devices;
        _overviewCanvas.SelectedDevice = _selectedDevice;
        room.Controls.Add(_overviewCanvas);

        Button flashAll = SmallButton(room, "Flash all", 18, 344, 96);
        flashAll.Click += (_, _) => _status.Text = "Flash all: devices would pulse one by one so you can name each physical light.";
        Button editRoom = SmallButton(room, "Place devices", 126, 344, 126);
        editRoom.Click += (_, _) => Navigate("Room");
        Button testColors = SmallButton(room, "Color test", 264, 344, 112);
        testColors.Click += (_, _) => _status.Text = "Color test queued: orange, teal, warm white, cool white, skin tone, black.";
        page.Controls.Add(room);

        Panel flow = Card(24, 522, 720, 92);
        AddLabel(flow, "Setup path", 18, 18, 126, 24, 12, true);
        AddMiniStep(flow, "1", "Flash", "Find each device", 142);
        AddMiniStep(flow, "2", "Name", "Friendly labels", 286);
        AddMiniStep(flow, "3", "Place", "Map the room", 430);
        AddMiniStep(flow, "4", "Run", "Start profile", 574);
        page.Controls.Add(flow);

        return page;
    }

    private Control BuildRoomPage()
    {
        Panel page = Page("Room map", "Put devices where they physically are. The light behavior follows this map.");

        Panel mapCard = Card(24, 104, 480, 470);
        AddLabel(mapCard, "Physical placement", 18, 16, 260, 28, 14, true);
        AddMuted(mapCard, "Click a bubble, then drag it to the real location.", 20, 46, 350, 22);
        AddStatusChip(mapCard, $"{_devices.Count(d => d.Enabled)} active", 344, 18, 104, ZeusTheme.Success);

        if (_roomCanvas.Parent != null)
            _roomCanvas.Parent.Controls.Remove(_roomCanvas);
        _roomCanvas.Location = new Point(18, 82);
        _roomCanvas.Size = new Size(442, 300);
        _roomCanvas.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
        _roomCanvas.Devices = _devices;
        _roomCanvas.SelectedDevice = _selectedDevice;
        mapCard.Controls.Add(_roomCanvas);

        FlowLayoutPanel chips = new()
        {
            Location = new Point(18, 394),
            Size = new Size(442, 58),
            BackColor = Color.Transparent,
            AutoScroll = true,
            WrapContents = false
        };
        foreach (ZeusDevice device in _devices)
        {
            ZeusChip chip = new()
            {
                Text = device.Name,
                Size = new Size(Math.Clamp(device.Name.Length * 8 + 36, 110, 190), 32),
                Margin = new Padding(0, 0, 8, 8),
                FillColor = ReferenceEquals(device, _selectedDevice) ? ZeusTheme.Action : DeviceAccent(device.Kind),
                ForeColor = ReferenceEquals(device, _selectedDevice) ? ZeusTheme.TextInverse : ZeusTheme.TextInverse,
                Cursor = Cursors.Hand
            };
            chip.Click += (_, _) =>
            {
                _selectedDevice = device;
                _roomCanvas.SelectedDevice = device;
                _overviewCanvas.SelectedDevice = device;
                _status.Text = $"{device.Name} selected.";
                Navigate("Room");
            };
            chips.Controls.Add(chip);
        }
        mapCard.Controls.Add(chips);
        page.Controls.Add(mapCard);

        Panel editor = Card(520, 104, 304, 470);
        if (_selectedDevice == null)
        {
            AddLabel(editor, "Select a device", 18, 18, 240, 28, 13, true);
            AddMuted(editor, "Click any bubble on the room map.", 18, 56, 250, 40);
        }
        else
        {
            ZeusDevice device = _selectedDevice;
            AddLabel(editor, device.Name, 18, 16, 230, 28, 13, true);
            AddMuted(editor, $"{device.Kind} via {device.Backend}", 18, 46, 250, 22);

            Button flash = SmallButton(editor, "Flash", 214, 18, 70);
            flash.Click += (_, _) => _status.Text = $"{device.Name} would flash so the user can identify it physically.";
            AddStatusChip(editor, device.Enabled ? "Enabled" : "Off", 18, 74, 86, device.Enabled ? ZeusTheme.Success : ZeusTheme.Danger);

            AddMuted(editor, "Zone", 18, 116, 70, 22);
            ComboBox zone = Combo(editor, ["Screen top", "Screen bottom", "Desk", "Rear left", "Rear right", "Left wall", "Right wall", "Ceiling", "Room"], device.Zone, 96, 112, 176);
            zone.SelectedIndexChanged += (_, _) =>
            {
                device.Zone = zone.SelectedItem?.ToString() ?? device.Zone;
                ApplyZonePosition(device);
                _roomCanvas.Invalidate();
                _status.Text = $"{device.Name} assigned to {device.Zone}.";
            };

            AddMuted(editor, "Watch", 18, 162, 70, 22);
            Combo(editor, ["Screen zone", "Soft depth", "Bias glow", "Off"], device.WatchRole, 96, 158, 176)
                .SelectedIndexChanged += (_, _) => _status.Text = "Watch behavior changed. Live profile save comes next.";

            AddMuted(editor, "Game", 18, 208, 70, 22);
            Combo(editor, ["Full game mix", "Impact only", "Objective only", "Health/damage", "Off"], device.GameRole, 96, 204, 176)
                .SelectedIndexChanged += (_, _) => _status.Text = "Game behavior changed. Live profile save comes next.";

            AddMuted(editor, "Brightness", 18, 256, 90, 22);
            TrackBar brightness = new()
            {
                Minimum = 0,
                Maximum = 150,
                TickFrequency = 25,
                Value = Math.Clamp(device.Brightness, 0, 150),
                Location = new Point(96, 246),
                Size = new Size(176, 44)
            };
            brightness.Scroll += (_, _) =>
            {
                device.Brightness = brightness.Value;
                _status.Text = $"{device.Name} brightness set to {device.Brightness}%.";
            };
            editor.Controls.Add(brightness);

            AddMuted(editor, "FPS cap", 18, 306, 90, 22);
            Combo(editor, ["5", "10", "20", "30", "60"], device.FpsCap.ToString(), 96, 302, 176)
                .SelectedIndexChanged += (_, _) => _status.Text = "FPS cap changed. Zeus will cap per backend to avoid load.";

            Button colorTest = SmallButton(editor, "Color test", 18, 366, 116);
            colorTest.Click += (_, _) => _status.Text = $"{device.Name} would test red, green, blue, orange, teal, warm white, cool white, and black.";
            Button off = SmallButton(editor, device.Enabled ? "Disable" : "Enable", 148, 366, 116);
            off.Click += (_, _) =>
            {
                device.Enabled = !device.Enabled;
                _status.Text = $"{device.Name} is now {(device.Enabled ? "enabled" : "disabled")}.";
                Navigate("Room");
            };

            AddMuted(editor, "Role preview", 18, 424, 260, 22);
            AddMuted(editor, $"{device.Name} is a {device.Zone.ToLowerInvariant()} device for {_activeMode}.", 18, 446, 250, 22);
        }
        page.Controls.Add(editor);

        Button addStrip = SmallButton(page, "Add monitor strip", 24, 586, 150);
        addStrip.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        addStrip.Click += (_, _) => AddDevice("Monitor LED Strip", "Strip", "WLED / OpenRGB", "Screen back", 0.5f, 0.28f);
        Button addCeiling = SmallButton(page, "Add ceiling light", 186, 586, 144);
        addCeiling.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        addCeiling.Click += (_, _) => AddDevice("Ceiling Light", "Room light", "Hue / WiZ", "Ceiling", 0.5f, 0.08f);
        Button testZones = SmallButton(page, "Test zones", 342, 586, 108);
        testZones.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        testZones.Click += (_, _) => _status.Text = "Zone test would sweep top, bottom, left, right, rear, ceiling, desk, then full room.";

        return page;
    }

    private Control BuildDevicesPage()
    {
        Panel page = Page("Devices", "Every device has a friendly name, physical type, backend, and a Flash button.");

        FlowLayoutPanel list = new()
        {
            Location = new Point(24, 104),
            Size = new Size(984, 470),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = ZeusTheme.Surface,
            AutoScroll = true
        };
        page.Controls.Add(list);

        foreach (IGrouping<string, ZeusDevice> group in _devices.GroupBy(d => d.Kind).OrderBy(g => g.Key))
        {
            Panel groupPanel = Card(0, 0, 304, 126 + group.Count() * 58);
            groupPanel.Margin = new Padding(8);
            AddLabel(groupPanel, group.Key, 16, 14, 220, 26, 12, true);
            int y = 50;
            foreach (ZeusDevice device in group)
            {
                Label name = AddLabel(groupPanel, device.Name, 16, y, 180, 22, 9.5f, true);
                name.ForeColor = device.Enabled ? ZeusTheme.Text : ZeusTheme.TextMuted;
                AddMuted(groupPanel, $"{device.Backend} - {device.Zone}", 16, y + 22, 190, 20);
                Button flash = SmallButton(groupPanel, "Flash", 216, y + 6, 68);
                flash.Click += (_, _) =>
                {
                    _selectedDevice = device;
                    _status.Text = $"{device.Name} would flash now. This is how a normal user identifies it.";
                };
                Button place = SmallButton(groupPanel, "Place", 216, y + 34, 68);
                place.Click += (_, _) =>
                {
                    _selectedDevice = device;
                    _roomCanvas.SelectedDevice = device;
                    _overviewCanvas.SelectedDevice = device;
                    Navigate("Room");
                };
                y += 58;
            }
            list.Controls.Add(groupPanel);
        }

        Button discover = SmallButton(page, "Discover devices", 24, 586, 146);
        discover.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        discover.Click += (_, _) => _status.Text = "Discovery would scan native WiZ, Artemis, OpenRGB, Razer, WLED, Hue, and Windows Dynamic Lighting backends.";
        Button identify = SmallButton(page, "Identify all", 182, 586, 112);
        identify.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        identify.Click += (_, _) => _status.Text = "Identify all would flash devices one at a time and ask the user to name each one.";
        return page;
    }

    private Control BuildProfilesPage()
    {
        Panel page = Page("Profiles and auto-switch", "Profiles are mode recipes. Auto rules switch them when apps launch.");

        FlowLayoutPanel list = new()
        {
            Location = new Point(24, 104),
            Size = new Size(620, 470),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left,
            BackColor = ZeusTheme.Surface,
            AutoScroll = true
        };
        page.Controls.Add(list);

        foreach (ZeusProfile profile in _profiles)
        {
            Panel card = Card(0, 0, 578, 136);
            card.Margin = new Padding(8);
            AddLabel(card, profile.Name, 16, 14, 260, 26, 12, true);
            AddMuted(card, profile.Subtitle, 16, 42, 420, 22);
            AddMuted(card, "Trigger: " + profile.Trigger, 16, 70, 520, 20);
            AddMuted(card, "Devices: " + profile.PrimaryDevices, 16, 92, 520, 20);
            AddMuted(card, "Room: " + profile.RoomBehavior, 16, 114, 520, 20);
            Button run = SmallButton(card, profile.Name == _activeMode ? "Running" : "Run", 482, 16, 70);
            if (run is ZeusButton runButton)
                runButton.Active = profile.Name == _activeMode;
            run.Click += (_, _) =>
            {
                _activeMode = profile.Name;
                _status.Text = $"{profile.Name} profile selected. Auto-switch would watch for {profile.Trigger}.";
                Navigate("Profiles");
            };
            list.Controls.Add(card);
        }

        Panel cs = Card(676, 104, 332, 470);
        AddLabel(cs, "CS2 event palette", 18, 18, 260, 28, 13, true);
        AddEvent(cs, "Bomb planted", "All lights amber pulse. Dock and keyboard timer accelerate.", 64);
        AddEvent(cs, "Bomb defused", "Cool white/blue sweep, then calm reset.", 118);
        AddEvent(cs, "Explosion", "Full-room red/orange blast, fast fade, rear lights peak.", 172);
        AddEvent(cs, "Flashbang", "White burst with quick recovery to avoid eye strain.", 226);
        AddEvent(cs, "Molly/fire", "Red/orange flicker on room, keyboard heat zone.", 280);
        AddEvent(cs, "Health/ammo", "Keyboard bars only; rear lights stay intensity-focused.", 334);
        AddEvent(cs, "Clutch/MVP", "Heartbeat tension, then victory sweep if round is won.", 388);
        page.Controls.Add(cs);
        return page;
    }

    private Control BuildPluginsPage()
    {
        Panel page = Page("Backends and plugins", "Zeus should hide complexity but still let power users choose sources.");

        FlowLayoutPanel list = new()
        {
            Location = new Point(24, 104),
            Size = new Size(984, 470),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = ZeusTheme.Surface,
            AutoScroll = true
        };
        page.Controls.Add(list);

        foreach (ZeusBackend backend in _backends)
        {
            Panel card = Card(0, 0, 304, 178);
            card.Margin = new Padding(8);
            AddLabel(card, backend.Name, 16, 14, 220, 26, 12, true);
            Label status = AddMuted(card, backend.Status, 16, 42, 240, 22);
            status.ForeColor = backend.Recommended ? ZeusTheme.Success : ZeusTheme.Warning;
            AddMuted(card, backend.Purpose, 16, 72, 250, 42);
            AddMuted(card, "Examples: " + backend.Examples, 16, 120, 250, 34);
            Button config = SmallButton(card, backend.Recommended ? "Use" : "Configure", 198, 132, 82);
            config.Click += (_, _) => _status.Text = $"{backend.Name}: Zeus would configure this backend without exposing raw IDs first.";
            list.Controls.Add(card);
        }

        Button import = SmallButton(page, "Import Artemis setup", 24, 586, 168);
        import.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        import.Click += (_, _) => _status.Text = "Import would read Artemis devices/profiles and convert them into Zeus names, zones, and behaviors.";
        Button marketplace = SmallButton(page, "Plugin marketplace", 204, 586, 166);
        marketplace.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        marketplace.Click += (_, _) => _status.Text = "Marketplace would show friendly categories first: Devices, Games, Ambilight, Effects, Advanced.";
        return page;
    }

    private Control BuildPerformancePage()
    {
        Panel page = Page("Performance", "One sampler, per-backend FPS caps, and predictable load.");

        Panel design = Card(24, 104, 472, 470);
        AddLabel(design, "Low-load design", 18, 18, 260, 28, 13, true);
        AddStep(design, 1, "One shared screen capture", "All ambient devices use the same downsampled frame.", 66);
        AddStep(design, 2, "Zone extraction", "Top, bottom, left, right, desk, rear, and custom strips.", 124);
        AddStep(design, 3, "Color calibration", "Per-device correction for orange, teal, warm white, and black.", 182);
        AddStep(design, 4, "Device FPS caps", "WiZ bulbs lower FPS; keyboards and strips can go higher.", 240);
        AddStep(design, 5, "Auto pause", "Reduce capture when no profile needs screen color.", 298);
        AddStep(design, 6, "Backend conflict guard", "Warn when Artemis and OpenRGB control the same device.", 356);
        page.Controls.Add(design);

        Panel caps = Card(526, 104, 482, 470);
        AddLabel(caps, "Suggested caps", 18, 18, 260, 28, 13, true);
        AddCap(caps, "WiZ lamps", "10 to 20 FPS", "Smooth enough for bulbs; avoids network spam.", 66);
        AddCap(caps, "Razer keyboard", "30 to 60 FPS", "Per-key effects and game bars can run faster.", 124);
        AddCap(caps, "Razer mouse/dock", "30 FPS", "Good response without wasting frames.", 182);
        AddCap(caps, "WLED / monitor strip", "30 to 60 FPS", "Best for real Ambilight strips.", 240);
        AddCap(caps, "Artemis/OpenRGB bridge", "Backend dependent", "Zeus should show warnings and measured latency.", 298);
        AddCap(caps, "CPU/GPU budget", "Visible in UI", "Users can pick Quality, Balanced, or Low load.", 356);
        page.Controls.Add(caps);
        return page;
    }

    private Panel Page(string title, string subtitle)
    {
        Panel page = new() { Dock = DockStyle.Fill, BackColor = ZeusTheme.Surface };
        AddLabel(page, title, 24, 24, 520, 34, 19, true);
        AddMuted(page, subtitle, 26, 64, 800, 24);
        return page;
    }

    private static Panel Card(int x, int y, int width, int height)
    {
        return new ZeusCard
        {
            Location = new Point(x, y),
            Size = new Size(width, height),
            FillColor = ZeusTheme.SurfaceRaised,
            StrokeColor = Color.FromArgb(62, 75, 88)
        };
    }

    private static Label AddLabel(Control parent, string text, int x, int y, int width, int height, float size = 9.5f, bool semibold = false)
    {
        Label label = new()
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, height),
            ForeColor = ZeusTheme.Text,
            Font = semibold ? ZeusTheme.LabelFont(size) : ZeusTheme.BodyFont(size)
        };
        parent.Controls.Add(label);
        return label;
    }

    private static Label AddMuted(Control parent, string text, int x, int y, int width, int height)
    {
        Label label = AddLabel(parent, text, x, y, width, height);
        label.ForeColor = ZeusTheme.TextMuted;
        return label;
    }

    private static Button HeaderButton(string text, int x, int y, int width)
    {
        Button button = new ZeusButton()
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, 36),
            ForeColor = ZeusTheme.Text
        };
        return button;
    }

    private static Button SmallButton(Control parent, string text, int x, int y, int width)
    {
        Button button = HeaderButton(text, x, y, width);
        button.Size = new Size(width, 32);
        parent.Controls.Add(button);
        return button;
    }

    private static ComboBox Combo(Control parent, string[] values, string selected, int x, int y, int width)
    {
        ComboBox combo = new()
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(x, y),
            Size = new Size(width, 29),
            BackColor = ZeusTheme.SurfaceInteractive,
            ForeColor = ZeusTheme.Text
        };
        combo.Items.AddRange(values);
        combo.SelectedItem = values.Contains(selected) ? selected : values[0];
        parent.Controls.Add(combo);
        return combo;
    }

    private static void AddStep(Control parent, int number, string title, string body, int y)
    {
        AddStatusChip(parent, number.ToString(), 18, y, 28, ZeusTheme.Action);
        AddLabel(parent, title, 58, y - 2, 260, 22, 10.5f, true);
        AddMuted(parent, body, 58, y + 22, 360, 24);
    }

    private static void AddEvent(Control parent, string title, string body, int y)
    {
        AddLabel(parent, title, 18, y, 150, 22, 10, true);
        AddMuted(parent, body, 18, y + 24, 284, 28);
    }

    private static void AddCap(Control parent, string title, string cap, string body, int y)
    {
        AddLabel(parent, title, 18, y, 150, 22, 10, true);
        Label capLabel = AddMuted(parent, cap, 178, y, 130, 22);
        capLabel.ForeColor = ZeusTheme.Success;
        AddMuted(parent, body, 18, y + 24, 390, 28);
    }

    private static void AddLoadPill(Control parent, string title, string body, int x, int y)
    {
        Panel pill = new ZeusCard()
        {
            Location = new Point(x, y),
            Size = new Size(286, 36),
            FillColor = ZeusTheme.SurfaceInteractive,
            StrokeColor = Color.FromArgb(70, 84, 98)
        };
        AddLabel(pill, title, 10, 7, 116, 22, 9, true);
        AddMuted(pill, body, 132, 7, 144, 22);
        parent.Controls.Add(pill);
    }

    private void AddRailChip(string text, int x, int y, Color color)
    {
        ZeusChip chip = new()
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(170, 30),
            FillColor = color,
            StrokeColor = color,
            ForeColor = ZeusTheme.TextInverse
        };
        _nav.Controls.Add(chip);
    }

    private static ZeusChip AddStatusChip(Control parent, string text, int x, int y, int width, Color color)
    {
        ZeusChip chip = new()
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, 28),
            FillColor = color,
            StrokeColor = color,
            ForeColor = ZeusTheme.TextInverse
        };
        parent.Controls.Add(chip);
        return chip;
    }

    private void AddModeRow(Control parent, string mode, string title, string body, int y)
    {
        bool active = string.Equals(_activeMode, mode, StringComparison.OrdinalIgnoreCase);
        int rowWidth = Math.Max(260, parent.Width - 36);
        int buttonX = rowWidth - 88;
        Panel row = new ZeusCard
        {
            Location = new Point(18, y),
            Size = new Size(rowWidth, 58),
            FillColor = active ? Color.FromArgb(43, 75, 95) : ZeusTheme.SurfaceInteractive,
            StrokeColor = active ? ZeusTheme.ActionHover : Color.FromArgb(70, 84, 98)
        };
        AddLabel(row, title, 14, 8, buttonX - 28, 22, 10.2f, true);
        AddMuted(row, body, 14, 31, buttonX - 28, 20);
        Button button = SmallButton(row, active ? "Active" : "Start", buttonX, 13, 74);
        if (button is ZeusButton zeusButton)
            zeusButton.Active = active;
        button.Click += (_, _) =>
        {
            _activeMode = mode;
            _status.Text = $"{mode} mode selected.";
            Navigate("Home");
        };
        parent.Controls.Add(row);
    }

    private static void AddMiniStep(Control parent, string number, string title, string body, int x)
    {
        AddStatusChip(parent, number, x, 24, 30, ZeusTheme.Action);
        AddLabel(parent, title, x + 42, 20, 90, 22, 10.5f, true);
        AddMuted(parent, body, x + 42, 44, 96, 20);
    }

    private static Color DeviceAccent(string kind) => kind switch
    {
        "Room light" => ZeusTheme.Amber,
        "Keyboard" => ZeusTheme.Blue,
        "Mouse" => ZeusTheme.Purple,
        "Dock" => ZeusTheme.Success,
        "Laptop" => ZeusTheme.Fire,
        "Strip" => ZeusTheme.Teal,
        _ => ZeusTheme.TextMuted
    };

    private void AddDevice(string name, string kind, string backend, string zone, float x, float y)
    {
        ZeusDevice device = new()
        {
            Name = name,
            Kind = kind,
            Backend = backend,
            Zone = zone,
            WatchRole = kind == "Room light" ? "Soft depth" : "Screen zone",
            GameRole = kind == "Room light" ? "Impact only" : "Full game mix",
            Brightness = kind == "Room light" ? 55 : 100,
            FpsCap = kind == "Room light" ? 20 : 60,
            X = x,
            Y = y
        };
        _devices.Add(device);
        _selectedDevice = device;
        _roomCanvas.Devices = _devices;
        _status.Text = $"{name} added. Drag it into place and test colors.";
        Navigate("Room");
    }

    private static void ApplyZonePosition(ZeusDevice device)
    {
        (device.X, device.Y) = device.Zone switch
        {
            "Screen top" => (0.5f, 0.18f),
            "Screen bottom" => (0.5f, 0.46f),
            "Desk" => (0.5f, 0.62f),
            "Rear left" => (0.26f, 0.88f),
            "Rear right" => (0.76f, 0.88f),
            "Left wall" => (0.12f, 0.45f),
            "Right wall" => (0.88f, 0.45f),
            "Ceiling" => (0.5f, 0.08f),
            _ => (0.5f, 0.42f)
        };
    }

    private static List<ZeusDevice> SeedDevices() =>
    [
        new() { Name = "Study Lamp", Kind = "Room light", Backend = "WiZ native LAN", Zone = "Screen top", WatchRole = "Screen zone", GameRole = "Full game mix", Brightness = 100, FpsCap = 20, X = 0.50f, Y = 0.18f },
        new() { Name = "Upper Rear Light", Kind = "Room light", Backend = "WiZ native LAN", Zone = "Rear right", WatchRole = "Soft depth", GameRole = "Impact only", Brightness = 38, FpsCap = 15, X = 0.72f, Y = 0.86f },
        new() { Name = "Lower Rear Light", Kind = "Room light", Backend = "WiZ native LAN", Zone = "Rear right", WatchRole = "Soft depth", GameRole = "Health/damage", Brightness = 32, FpsCap = 15, X = 0.82f, Y = 0.90f },
        new() { Name = "Razer Keyboard", Kind = "Keyboard", Backend = "Razer / Artemis", Zone = "Desk", WatchRole = "Screen zone", GameRole = "Full game mix", Brightness = 85, FpsCap = 60, X = 0.46f, Y = 0.64f },
        new() { Name = "Razer Mouse", Kind = "Mouse", Backend = "Razer / Artemis", Zone = "Desk", WatchRole = "Screen zone", GameRole = "Health/damage", Brightness = 85, FpsCap = 30, X = 0.67f, Y = 0.66f },
        new() { Name = "Razer Dock", Kind = "Dock", Backend = "Razer / Artemis", Zone = "Desk", WatchRole = "Bias glow", GameRole = "Objective only", Brightness = 80, FpsCap = 30, X = 0.58f, Y = 0.60f },
        new() { Name = "Lenovo Laptop Keyboard", Kind = "Laptop", Backend = "Lenovo / Artemis", Zone = "Right wall", WatchRole = "Screen zone", GameRole = "Off", Brightness = 45, FpsCap = 30, X = 0.84f, Y = 0.50f }
    ];

    private static List<ZeusProfile> SeedProfiles() =>
    [
        new("Watch", "Movies, YouTube, anime, streams", "Browser fullscreen or manual", "Study lamp, keyboard, mouse, rear lights", "Rear lights become soft depth, black scenes can go dark", "One 160x90 sampler, 30 FPS target"),
        new("CS2", "Game-state plus room intensity", "cs2.exe", "Keyboard per-key, dock bomb, study major events", "Rear lights become impact and pressure lights", "GSI event stream plus optional screen sample"),
        new("Valorant", "Map mood and spike pressure", "VALORANT-Win64-Shipping.exe", "Keyboard, mouse, dock, study lamp", "Room lights reflect spike, damage, flash, death", "Log/API adapter plus screen fallback"),
        new("Study", "Comfortable desk lighting", "Default desktop or manual", "Study lamp and optional peripherals", "Room lights off or very low", "No continuous screen capture unless enabled")
    ];

    private static List<ZeusBackend> SeedBackends() =>
    [
        new("Native WiZ LAN", "Recommended", "Direct control for study, upper, lower, and future WiZ bulbs.", "WiZ bulbs, ceiling lights", true),
        new("Artemis import", "Recommended", "Use existing Artemis device providers and profiles as a compatibility layer.", "Razer, Lenovo, CS2 profile", true),
        new("OpenRGB SDK", "Optional", "Talk to OpenRGB for broad desktop RGB devices without making users use OpenRGB UI.", "Keyboards, mice, strips, motherboards", false),
        new("Razer Chroma", "Optional", "Native Razer path for per-key control when it is more reliable than a bridge.", "Keyboard, mouse, dock", false),
        new("WLED", "Recommended for strips", "Best path for future monitor-back strips and room LED strips.", "ESP32 LED strips", true),
        new("SignalRGB-style game packs", "Future", "Plugin compatibility layer for game effects where licensing allows it.", "CS2, Valorant, other games", false)
    ];
}
