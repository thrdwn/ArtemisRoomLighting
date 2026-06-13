namespace ZeusLighting;

internal sealed class ZeusForm : Form
{
    private readonly Panel _nav = new();
    private readonly Panel _content = new();
    private readonly Label _title = new();
    private readonly Label _subtitle = new();
    private readonly Label _status = new();
    private readonly List<Button> _navButtons = [];
    private readonly List<ZeusDevice> _devices = SeedDevices();
    private readonly List<ZeusProfile> _profiles = SeedProfiles();
    private readonly List<ZeusBackend> _backends = SeedBackends();
    private readonly RoomCanvas _roomCanvas = new();

    private ZeusDevice? _selectedDevice;
    private string _activePage = "Home";
    private string _activeMode = "Watch";

    public ZeusForm()
    {
        Text = "Zeus Lighting Prototype";
        ClientSize = new Size(1320, 820);
        MinimumSize = new Size(1140, 720);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(16, 19, 23);
        ForeColor = Color.FromArgb(238, 242, 246);
        Font = new Font("Segoe UI", 9.5f);

        BuildShell();
        Navigate("Home");
        _selectedDevice = _devices[0];
        _roomCanvas.Devices = _devices;
        _roomCanvas.SelectedDevice = _selectedDevice;
        _roomCanvas.SelectedDeviceChanged += (_, device) =>
        {
            _selectedDevice = device;
            if (_activePage == "Room")
                Navigate("Room");
        };
        _roomCanvas.DeviceMoved += (_, _) =>
        {
            if (_selectedDevice != null)
                _status.Text = $"{_selectedDevice.Name} moved to {_selectedDevice.Zone}.";
        };
    }

    private void BuildShell()
    {
        _title.Text = "Zeus";
        _title.Location = new Point(24, 18);
        _title.Size = new Size(280, 38);
        _title.Font = new Font("Segoe UI Semibold", 24);
        _title.ForeColor = Color.White;
        Controls.Add(_title);

        _subtitle.Text = "Lighting that starts simple, then lets you go deep.";
        _subtitle.Location = new Point(28, 58);
        _subtitle.Size = new Size(620, 24);
        _subtitle.ForeColor = Color.FromArgb(170, 181, 192);
        Controls.Add(_subtitle);

        _nav.Location = new Point(18, 102);
        _nav.Size = new Size(216, 642);
        _nav.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
        _nav.BackColor = Color.FromArgb(24, 29, 35);
        Controls.Add(_nav);

        string[] pages = ["Home", "Room", "Devices", "Profiles", "Plugins", "Performance"];
        for (int i = 0; i < pages.Length; i++)
        {
            Button button = new()
            {
                Text = pages[i],
                Tag = pages[i],
                Location = new Point(14, 18 + i * 52),
                Size = new Size(188, 40),
                TextAlign = ContentAlignment.MiddleLeft,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 10),
                ForeColor = Color.White
            };
            button.FlatAppearance.BorderSize = 1;
            button.Click += (_, _) => Navigate((string)button.Tag);
            _navButtons.Add(button);
            _nav.Controls.Add(button);
        }

        Label hint = new()
        {
            Text = "Normal users see names, rooms, modes, and tests. Device IDs and backend details stay behind troubleshooting.",
            Location = new Point(16, 360),
            Size = new Size(180, 100),
            ForeColor = Color.FromArgb(150, 162, 175)
        };
        _nav.Controls.Add(hint);

        _content.Location = new Point(252, 102);
        _content.Size = new Size(1046, 642);
        _content.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _content.BackColor = Color.FromArgb(20, 24, 29);
        Controls.Add(_content);

        _status.Location = new Point(24, 762);
        _status.Size = new Size(920, 30);
        _status.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _status.ForeColor = Color.FromArgb(168, 180, 193);
        _status.Text = "Prototype only: UI, mapping model, profile flow, and backend plan. Live device control comes next.";
        Controls.Add(_status);

        Button switchMode = HeaderButton("Switch mode", 1032, 28, 122);
        switchMode.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        switchMode.Click += (_, _) => Navigate("Home");
        Controls.Add(switchMode);

        Button testRoom = HeaderButton("Test room", 1164, 28, 112);
        testRoom.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        testRoom.Click += (_, _) => _status.Text = "Test pattern: each zone would flash one by one, then show red, green, blue, orange, and skin-tone checks.";
        Controls.Add(testRoom);
    }

    private void Navigate(string page)
    {
        _activePage = page;
        foreach (Button button in _navButtons)
        {
            bool active = (button.Tag as string) == page;
            button.BackColor = active ? Color.FromArgb(55, 111, 166) : Color.FromArgb(35, 41, 49);
            button.FlatAppearance.BorderColor = active ? Color.FromArgb(145, 201, 232) : Color.FromArgb(70, 80, 92);
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
        Panel page = Page("Control center", "One click when you want it, detailed freedom when you need it.");

        string[,] modes =
        {
            { "Watch", "Movies, YouTube, shows", "Screen zones drive lights. Black can turn lights off. Rear lights stay cinematic." },
            { "CS2", "Competitive immersion", "Bomb, flash, fire, smoke, health, ammo, clutch, death, MVP, and round state." },
            { "Valorant", "Agent and match mood", "Map mood, spike pressure, damage, flash, death, and victory moments." },
            { "Study", "Bright and calm", "Study lamp stays comfortable; room and peripherals can remain soft or off." }
        };

        for (int i = 0; i < modes.GetLength(0); i++)
        {
            Panel card = Card(24 + (i % 2) * 330, 104 + (i / 2) * 152, 300, 126);
            card.BackColor = modes[i, 0] == _activeMode ? Color.FromArgb(38, 72, 94) : Color.FromArgb(29, 35, 42);
            AddLabel(card, modes[i, 0], 16, 14, 230, 26, 13, true);
            AddMuted(card, modes[i, 1], 16, 42, 250, 22);
            AddMuted(card, modes[i, 2], 16, 70, 262, 42);
            Button start = SmallButton(card, modes[i, 0] == _activeMode ? "Running" : "Start", 208, 14, 74);
            string mode = modes[i, 0];
            start.Click += (_, _) =>
            {
                _activeMode = mode;
                _status.Text = $"{mode} mode selected. One-click switch would apply device roles and start auto rules.";
                Navigate("Home");
            };
            page.Controls.Add(card);
        }

        Panel steps = Card(684, 104, 324, 278);
        AddLabel(steps, "Visual setup", 18, 18, 260, 28, 13, true);
        AddStep(steps, 1, "Identify devices", "Press Flash. Name the light you see.", 58);
        AddStep(steps, 2, "Place them", "Drag each bubble onto the room map.", 110);
        AddStep(steps, 3, "Choose behavior", "Screen color, game events, calm glow, or off.", 162);
        AddStep(steps, 4, "Test odd colors", "Orange, teal, skin tones, black scenes.", 214);
        page.Controls.Add(steps);

        Panel current = Card(24, 428, 984, 152);
        AddLabel(current, "Your current idea", 18, 16, 320, 28, 13, true);
        AddMuted(
            current,
            "3 WiZ lights, 3 Razer devices, and Lenovo laptop lighting. Watch mode uses positional screen zones. CS2 uses game state plus room intensity. New strips, ceiling lights, and future devices become more draggable bubbles.",
            18,
            50,
            610,
            58);
        AddLoadPill(current, "Screen sampler", "Low-res shared capture", 662, 26);
        AddLoadPill(current, "WiZ bulbs", "10 to 20 FPS practical", 662, 72);
        AddLoadPill(current, "Keyboard", "Per-key high detail", 662, 118);
        page.Controls.Add(current);

        return page;
    }

    private Control BuildRoomPage()
    {
        Panel page = Page("Room map", "Put devices where they physically are. The light behavior follows this map.");

        _roomCanvas.Location = new Point(24, 104);
        _roomCanvas.Size = new Size(656, 470);
        _roomCanvas.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
        _roomCanvas.Devices = _devices;
        _roomCanvas.SelectedDevice = _selectedDevice;
        page.Controls.Add(_roomCanvas);

        Panel editor = Card(704, 104, 304, 470);
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

            AddMuted(editor, "Zone", 18, 86, 70, 22);
            ComboBox zone = Combo(editor, ["Screen top", "Screen bottom", "Desk", "Rear left", "Rear right", "Left wall", "Right wall", "Ceiling", "Room"], device.Zone, 96, 82, 176);
            zone.SelectedIndexChanged += (_, _) =>
            {
                device.Zone = zone.SelectedItem?.ToString() ?? device.Zone;
                ApplyZonePosition(device);
                _roomCanvas.Invalidate();
                _status.Text = $"{device.Name} assigned to {device.Zone}.";
            };

            AddMuted(editor, "Watch", 18, 132, 70, 22);
            Combo(editor, ["Screen zone", "Soft depth", "Bias glow", "Off"], device.WatchRole, 96, 128, 176)
                .SelectedIndexChanged += (_, _) => _status.Text = "Watch behavior changed. Live profile save comes next.";

            AddMuted(editor, "Game", 18, 178, 70, 22);
            Combo(editor, ["Full game mix", "Impact only", "Objective only", "Health/damage", "Off"], device.GameRole, 96, 174, 176)
                .SelectedIndexChanged += (_, _) => _status.Text = "Game behavior changed. Live profile save comes next.";

            AddMuted(editor, "Brightness", 18, 226, 90, 22);
            TrackBar brightness = new()
            {
                Minimum = 0,
                Maximum = 150,
                TickFrequency = 25,
                Value = Math.Clamp(device.Brightness, 0, 150),
                Location = new Point(96, 216),
                Size = new Size(176, 44)
            };
            brightness.Scroll += (_, _) =>
            {
                device.Brightness = brightness.Value;
                _status.Text = $"{device.Name} brightness set to {device.Brightness}%.";
            };
            editor.Controls.Add(brightness);

            AddMuted(editor, "FPS cap", 18, 278, 90, 22);
            Combo(editor, ["5", "10", "20", "30", "60"], device.FpsCap.ToString(), 96, 274, 176)
                .SelectedIndexChanged += (_, _) => _status.Text = "FPS cap changed. Zeus will cap per backend to avoid load.";

            Button colorTest = SmallButton(editor, "Color test", 18, 338, 116);
            colorTest.Click += (_, _) => _status.Text = $"{device.Name} would test red, green, blue, orange, teal, warm white, cool white, and black.";
            Button off = SmallButton(editor, device.Enabled ? "Disable" : "Enable", 148, 338, 116);
            off.Click += (_, _) =>
            {
                device.Enabled = !device.Enabled;
                _status.Text = $"{device.Name} is now {(device.Enabled ? "enabled" : "disabled")}.";
                Navigate("Room");
            };

            AddMuted(editor, "Instruction", 18, 398, 260, 22);
            AddMuted(editor, "The user only needs to flash, name, drag, test color, then pick what this device does in each mode.", 18, 422, 250, 42);
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
            BackColor = Color.FromArgb(20, 24, 29),
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
                name.ForeColor = device.Enabled ? Color.White : Color.FromArgb(140, 150, 160);
                AddMuted(groupPanel, $"{device.Backend} - {device.Zone}", 16, y + 22, 190, 20);
                Button flash = SmallButton(groupPanel, "Flash", 216, y + 6, 68);
                flash.Click += (_, _) =>
                {
                    _selectedDevice = device;
                    _status.Text = $"{device.Name} would flash now. This is how a normal user identifies it.";
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
            BackColor = Color.FromArgb(20, 24, 29),
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
            BackColor = Color.FromArgb(20, 24, 29),
            AutoScroll = true
        };
        page.Controls.Add(list);

        foreach (ZeusBackend backend in _backends)
        {
            Panel card = Card(0, 0, 304, 178);
            card.Margin = new Padding(8);
            AddLabel(card, backend.Name, 16, 14, 220, 26, 12, true);
            Label status = AddMuted(card, backend.Status, 16, 42, 240, 22);
            status.ForeColor = backend.Recommended ? Color.FromArgb(126, 219, 165) : Color.FromArgb(230, 188, 112);
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
        Panel page = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 24, 29) };
        AddLabel(page, title, 24, 24, 520, 34, 19, true);
        AddMuted(page, subtitle, 26, 64, 800, 24);
        return page;
    }

    private static Panel Card(int x, int y, int width, int height)
    {
        return new Panel
        {
            Location = new Point(x, y),
            Size = new Size(width, height),
            BackColor = Color.FromArgb(29, 35, 42)
        };
    }

    private static Label AddLabel(Control parent, string text, int x, int y, int width, int height, float size = 9.5f, bool semibold = false)
    {
        Label label = new()
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, height),
            ForeColor = Color.FromArgb(238, 242, 246),
            Font = new Font("Segoe UI" + (semibold ? " Semibold" : ""), size)
        };
        parent.Controls.Add(label);
        return label;
    }

    private static Label AddMuted(Control parent, string text, int x, int y, int width, int height)
    {
        Label label = AddLabel(parent, text, x, y, width, height);
        label.ForeColor = Color.FromArgb(168, 180, 193);
        return label;
    }

    private static Button HeaderButton(string text, int x, int y, int width)
    {
        Button button = new()
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(45, 54, 65),
            ForeColor = Color.White
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(82, 98, 115);
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
            BackColor = Color.FromArgb(38, 45, 53),
            ForeColor = Color.White
        };
        combo.Items.AddRange(values);
        combo.SelectedItem = values.Contains(selected) ? selected : values[0];
        parent.Controls.Add(combo);
        return combo;
    }

    private static void AddStep(Control parent, int number, string title, string body, int y)
    {
        Label badge = AddLabel(parent, number.ToString(), 18, y, 26, 26, 10, true);
        badge.TextAlign = ContentAlignment.MiddleCenter;
        badge.BackColor = Color.FromArgb(59, 120, 168);
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
        capLabel.ForeColor = Color.FromArgb(130, 215, 176);
        AddMuted(parent, body, 18, y + 24, 390, 28);
    }

    private static void AddLoadPill(Control parent, string title, string body, int x, int y)
    {
        Panel pill = new()
        {
            Location = new Point(x, y),
            Size = new Size(286, 36),
            BackColor = Color.FromArgb(38, 45, 53)
        };
        AddLabel(pill, title, 10, 7, 116, 22, 9, true);
        AddMuted(pill, body, 132, 7, 144, 22);
        parent.Controls.Add(pill);
    }

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
