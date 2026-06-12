using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace ArtemisRoomLighting.Installer;

internal static class Program
{
    private const string PayloadResource = "ArtemisRoomLighting.Payload.zip";

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--verify-payload", StringComparer.OrdinalIgnoreCase))
        {
            VerifyPayload();
            return;
        }

        if (!IsAdministrator())
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath!,
                    UseShellExecute = true,
                    Verb = "runas"
                });
            }
            catch
            {
                MessageBox.Show(
                    "Administrator permission is required to install the Artemis plugin.",
                    "Artemis Room Lighting",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new InstallerForm());
    }

    private static bool IsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void VerifyPayload()
    {
        string temp = Path.Combine(Path.GetTempPath(), "ArtemisRoomLightingVerify-" + Guid.NewGuid().ToString("N"));
        try
        {
            ExtractPayload(temp);
            string[] required =
            {
                Path.Combine(temp, "Plugin", "Artemis.Plugins.DirectDevices.dll"),
                Path.Combine(temp, "Plugin", "plugin.json"),
                Path.Combine(temp, "Tools", "SqliteTool.exe"),
                Path.Combine(temp, "LightingSwitches", "Lighting Control.vbs"),
                Path.Combine(temp, "Cs2Gsi", "gamestate_integration_artemis_room_lighting.cfg")
            };
            string? missing = required.FirstOrDefault(path => !File.Exists(path));
            if (missing != null)
                throw new FileNotFoundException("Payload file is missing.", missing);

            Console.WriteLine("INSTALLER_PAYLOAD_OK");
        }
        finally
        {
            if (Directory.Exists(temp))
                Directory.Delete(temp, recursive: true);
        }
    }

    internal static void ExtractPayload(string destination)
    {
        Directory.CreateDirectory(destination);
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResource)
            ?? throw new InvalidOperationException("The embedded installer payload is missing.");
        using ZipArchive archive = new(stream, ZipArchiveMode.Read);
        archive.ExtractToDirectory(destination, overwriteFiles: true);
    }
}

internal sealed class InstallerForm : Form
{
    private const string PluginFolderName = "Artemis.Plugins.DirectDevices-8f080623";
    private readonly TextBox _artemisPath = new();
    private readonly TextBox _cs2CfgPath = new();
    private readonly TextBox _studyIp = new();
    private readonly TextBox _upperIp = new();
    private readonly TextBox _lowerIp = new();
    private readonly CheckBox _enableRazer = new();
    private readonly CheckBox _enableLenovo = new();
    private readonly CheckBox _installCs2 = new();
    private readonly CheckBox _createShortcuts = new();
    private readonly CheckBox _preserveSettings = new();
    private readonly Label _status = new();
    private readonly Button _install = new();

    private static readonly string ArtemisData = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Artemis");
    private static readonly string PluginTarget = Path.Combine(ArtemisData, "Plugins", PluginFolderName);
    private static readonly string InstallRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "ArtemisRoomLighting");

    public InstallerForm()
    {
        Text = "Artemis Room Lighting Setup";
        ClientSize = new Size(700, 620);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(24, 26, 30);
        ForeColor = Color.FromArgb(242, 245, 249);
        Font = new Font("Segoe UI", 10);

        Label title = AddLabel("Artemis Room Lighting", 24, 20, 640, 38);
        title.Font = new Font("Segoe UI Semibold", 19);
        Label subtitle = AddLabel(
            "Install or update ambient lighting, CS2 events, WiZ lights, and supported keyboards.",
            26,
            62,
            640,
            26);
        subtitle.ForeColor = Color.FromArgb(174, 181, 192);

        AddField("Artemis application", _artemisPath, DetectArtemis(), 105);
        AddField("CS2 cfg folder", _cs2CfgPath, DetectCs2Cfg(), 169);
        AddField("Study WiZ IP (optional)", _studyIp, "", 233);
        AddField("Upper/rear WiZ IP (optional)", _upperIp, "", 297);
        AddField("Lower/rear WiZ IP (optional)", _lowerIp, "", 361);

        _enableRazer.Text = "Enable supported Razer keyboard, mouse, and dock";
        _enableRazer.Checked = true;
        ConfigureCheck(_enableRazer, 26, 430, 390);

        _enableLenovo.Text = "Enable Lenovo Legion 4-zone keyboard";
        ConfigureCheck(_enableLenovo, 26, 463, 350);

        _installCs2.Text = "Install CS2 game-state integration";
        _installCs2.Checked = !string.IsNullOrWhiteSpace(_cs2CfgPath.Text);
        ConfigureCheck(_installCs2, 390, 430, 280);

        _createShortcuts.Text = "Create Start menu controls";
        _createShortcuts.Checked = true;
        ConfigureCheck(_createShortcuts, 390, 463, 260);

        bool existing = Directory.Exists(PluginTarget);
        _preserveSettings.Text = "Keep this PC's existing lighting settings";
        _preserveSettings.Checked = existing;
        _preserveSettings.Enabled = existing;
        ConfigureCheck(_preserveSettings, 26, 502, 370);
        _preserveSettings.CheckedChanged += (_, _) => UpdateFirstRunControls();

        Button browseArtemis = AddBrowseButton(625, 102);
        browseArtemis.Click += (_, _) => BrowseFile(_artemisPath, "Artemis.UI.Windows.exe");
        Button browseCs2 = AddBrowseButton(625, 166);
        browseCs2.Click += (_, _) => BrowseFolder(_cs2CfgPath);

        _status.Location = new Point(26, 545);
        _status.Size = new Size(500, 44);
        _status.ForeColor = Color.FromArgb(174, 181, 192);
        _status.Text = existing
            ? "Existing installation found. Settings will be preserved by default."
            : "Enter only the devices available on this PC. Blank WiZ positions stay disabled.";
        Controls.Add(_status);

        _install.Text = existing ? "Update" : "Install";
        _install.Location = new Point(548, 544);
        _install.Size = new Size(126, 44);
        _install.FlatStyle = FlatStyle.Flat;
        _install.BackColor = Color.FromArgb(44, 103, 190);
        _install.ForeColor = Color.White;
        _install.Click += InstallClicked;
        Controls.Add(_install);

        UpdateFirstRunControls();
    }

    private async void InstallClicked(object? sender, EventArgs e)
    {
        try
        {
            ValidateInputs();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ToggleUi(false);
        try
        {
            await Task.Run(Install);
            SetStatus("Installation complete. Artemis and Lighting Control are starting.");
            StartProcess(_artemisPath.Text);
            string control = Path.Combine(InstallRoot, "LightingSwitches", "Lighting Control.vbs");
            if (File.Exists(control))
                StartProcess("wscript.exe", Quote(control));

            MessageBox.Show(
                "Artemis Room Lighting was installed successfully.",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            SetStatus("Installation failed.");
            MessageBox.Show(ex.ToString(), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            ToggleUi(true);
        }
    }

    private void Install()
    {
        SetStatus("Stopping Artemis...");
        StopArtemis();

        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string backup = Path.Combine(InstallRoot, "Backups", timestamp);
        Directory.CreateDirectory(backup);
        BackupExisting(backup);

        string temp = Path.Combine(Path.GetTempPath(), "ArtemisRoomLighting-" + Guid.NewGuid().ToString("N"));
        try
        {
            SetStatus("Extracting installer...");
            Program.ExtractPayload(temp);

            SetStatus("Installing plugin and controls...");
            InstallPayload(temp);

            string tool = Path.Combine(InstallRoot, "Tools", "SqliteTool.exe");
            RunTool(tool, "enable-direct");
            if (!_preserveSettings.Checked)
                ConfigureNewInstall(tool);

            if (_installCs2.Checked && Directory.Exists(_cs2CfgPath.Text))
            {
                File.Copy(
                    Path.Combine(InstallRoot, "Cs2Gsi", "gamestate_integration_artemis_room_lighting.cfg"),
                    Path.Combine(_cs2CfgPath.Text, "gamestate_integration_artemis_room_lighting.cfg"),
                    overwrite: true);
                string legacyGsi = Path.Combine(_cs2CfgPath.Text, "gamestate_integration_codex_direct_artemis.cfg");
                if (File.Exists(legacyGsi))
                    File.Delete(legacyGsi);
            }

            if (_createShortcuts.Checked)
                CreateShortcuts();
        }
        finally
        {
            if (Directory.Exists(temp))
                Directory.Delete(temp, recursive: true);
        }
    }

    private void InstallPayload(string extracted)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PluginTarget)!);
        ReplaceDirectory(Path.Combine(extracted, "Plugin"), PluginTarget);

        Directory.CreateDirectory(InstallRoot);
        foreach (string name in new[] { "Tools", "LightingSwitches", "Cs2Gsi" })
            ReplaceDirectory(Path.Combine(extracted, name), Path.Combine(InstallRoot, name));

        if (Environment.ProcessPath is { } setupPath)
            File.Copy(setupPath, Path.Combine(InstallRoot, "ArtemisRoomLightingSetup.exe"), overwrite: true);
    }

    private void ConfigureNewInstall(string tool)
    {
        bool hasStudy = !string.IsNullOrWhiteSpace(_studyIp.Text);
        bool hasUpper = !string.IsNullOrWhiteSpace(_upperIp.Text);
        bool hasLower = !string.IsNullOrWhiteSpace(_lowerIp.Text);
        string upperRole = hasUpper ? "PositionalAmbient" : "Off";
        string lowerRole = hasLower ? "PositionalAmbient" : "Off";
        string upperGameRole = hasUpper ? "ObjectiveAlerts" : "Off";
        string lowerGameRole = hasLower ? "FullGameMix" : "Off";
        string boolean(bool value) => value ? "1" : "0";

        RunTool(
            tool,
            "set-device-setup",
            _studyIp.Text.Trim(),
            _lowerIp.Text.Trim(),
            _upperIp.Text.Trim(),
            boolean(_enableRazer.Checked),
            boolean(_enableLenovo.Checked));
        RunTool(
            tool,
            "set-watch-config",
            boolean(hasStudy),
            upperRole,
            lowerRole,
            upperGameRole,
            lowerGameRole,
            boolean(_enableRazer.Checked),
            boolean(_enableRazer.Checked),
            boolean(_enableRazer.Checked),
            boolean(_enableLenovo.Checked),
            "1",
            "20",
            "30");
        RunTool(tool, "set-watch-tuning", "135", "120", "115", "108");
        RunTool(tool, "set-cs-event-tuning", "150", "145", "115", "150", "135", "130", "125", "145", "150");
        RunTool(tool, "set-control-mode", "Watch", "1");
    }

    private void BackupExisting(string backup)
    {
        string database = Path.Combine(ArtemisData, "artemis.db");
        if (File.Exists(database))
            File.Copy(database, Path.Combine(backup, "artemis.db"), overwrite: true);
        if (Directory.Exists(PluginTarget))
            CopyDirectory(PluginTarget, Path.Combine(backup, PluginFolderName));
    }

    private static void StopArtemis()
    {
        foreach (Process process in Process.GetProcesses().Where(process => process.ProcessName.Contains("Artemis", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(8000);
            }
            catch
            {
            }
        }
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

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the configuration tool.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Configuration failed: {error}{output}");
    }

    private void CreateShortcuts()
    {
        RemoveLegacyShortcuts();
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            "Artemis Room Lighting");
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (UnauthorizedAccessException)
        {
            folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                "Artemis Room Lighting");
            Directory.CreateDirectory(folder);
        }
        string switches = Path.Combine(InstallRoot, "LightingSwitches");

        CreateShortcut(
            Path.Combine(folder, "Lighting Control.lnk"),
            Path.Combine(Environment.SystemDirectory, "wscript.exe"),
            Quote(Path.Combine(switches, "Lighting Control.vbs")),
            switches,
            "Choose lighting modes and devices");
        CreateShortcut(
            Path.Combine(folder, "Watch Mode.lnk"),
            Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            "/c " + Quote(Path.Combine(switches, "Watch Mode.cmd")),
            switches,
            "Start cinematic screen-reactive lighting");
        CreateShortcut(
            Path.Combine(folder, "Study Mode.lnk"),
            Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            "/c " + Quote(Path.Combine(switches, "Study Only Ambient.cmd")),
            switches,
            "Start bright study lighting");
        CreateShortcut(
            Path.Combine(folder, "Repair or Update.lnk"),
            Path.Combine(InstallRoot, "ArtemisRoomLightingSetup.exe"),
            "",
            InstallRoot,
            "Repair or update Artemis Room Lighting");
    }

    private static void RemoveLegacyShortcuts()
    {
        string legacy = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            "Lighting Profiles");
        if (!Directory.Exists(legacy))
            return;

        foreach (string name in new[]
                 {
                     "Lighting Control.lnk",
                     "Lighting - Study Only Ambient.lnk",
                     "Lighting - Watch Mode.lnk",
                     "Lighting - Mode Settings.lnk",
                     "Lighting - Game Auto (CS2 + Valorant).lnk"
                 })
        {
            string path = Path.Combine(legacy, name);
            if (File.Exists(path))
                File.Delete(path);
        }

        if (!Directory.EnumerateFileSystemEntries(legacy).Any())
            Directory.Delete(legacy);
    }

    private static void CreateShortcut(string path, string target, string arguments, string workingDirectory, string description)
    {
        Type shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Windows Script Host is unavailable.");
        object shell = Activator.CreateInstance(shellType)!;
        object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, [path])!;
        Type shortcutType = shortcut.GetType();
        shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, [target]);
        shortcutType.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, [arguments]);
        shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, [workingDirectory]);
        shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, [description]);
        shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        Marshal.FinalReleaseComObject(shortcut);
        Marshal.FinalReleaseComObject(shell);
    }

    private void ValidateInputs()
    {
        if (!File.Exists(_artemisPath.Text))
            throw new InvalidOperationException("Select a valid Artemis.UI.Windows.exe.");
        if (!File.Exists(Path.Combine(ArtemisData, "artemis.db")))
            throw new InvalidOperationException("Artemis must be opened once before installing this plugin.");
        if (_installCs2.Checked && !Directory.Exists(_cs2CfgPath.Text))
            throw new InvalidOperationException("Select a valid CS2 cfg folder or turn off CS2 integration.");

        foreach ((string Name, string Value) light in new[]
                 {
                     ("Study WiZ", _studyIp.Text),
                     ("Upper WiZ", _upperIp.Text),
                     ("Lower WiZ", _lowerIp.Text)
                 })
        {
            if (!string.IsNullOrWhiteSpace(light.Value) &&
                (!IPAddress.TryParse(light.Value.Trim(), out IPAddress? address) || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork))
            {
                throw new InvalidOperationException($"{light.Name} must be a valid IPv4 address.");
            }
        }
    }

    private void UpdateFirstRunControls()
    {
        bool enabled = !_preserveSettings.Checked;
        _studyIp.Enabled = enabled;
        _upperIp.Enabled = enabled;
        _lowerIp.Enabled = enabled;
        _enableRazer.Enabled = enabled;
        _enableLenovo.Enabled = enabled;
    }

    private void ToggleUi(bool enabled)
    {
        if (InvokeRequired)
        {
            Invoke(() => ToggleUi(enabled));
            return;
        }

        _install.Enabled = enabled;
        _preserveSettings.Enabled = enabled && Directory.Exists(PluginTarget);
        _installCs2.Enabled = enabled;
        _createShortcuts.Enabled = enabled;
        UpdateFirstRunControls();
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

    private Label AddLabel(string text, int x, int y, int width, int height)
    {
        Label label = new()
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, height)
        };
        Controls.Add(label);
        return label;
    }

    private void AddField(string label, TextBox box, string value, int y)
    {
        Label caption = AddLabel(label, 26, y, 230, 25);
        caption.ForeColor = Color.FromArgb(210, 215, 223);
        box.Text = value;
        box.Location = new Point(260, y - 3);
        box.Size = new Size(352, 28);
        box.BackColor = Color.FromArgb(37, 40, 46);
        box.ForeColor = Color.White;
        box.BorderStyle = BorderStyle.FixedSingle;
        Controls.Add(box);
    }

    private Button AddBrowseButton(int x, int y)
    {
        Button button = new()
        {
            Text = "...",
            Location = new Point(x, y),
            Size = new Size(48, 29),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(45, 49, 57),
            ForeColor = Color.White
        };
        Controls.Add(button);
        return button;
    }

    private void ConfigureCheck(CheckBox checkBox, int x, int y, int width)
    {
        checkBox.Location = new Point(x, y);
        checkBox.Size = new Size(width, 28);
        checkBox.FlatStyle = FlatStyle.Flat;
        checkBox.ForeColor = ForeColor;
        Controls.Add(checkBox);
    }

    private static void BrowseFile(TextBox target, string fileName)
    {
        using OpenFileDialog dialog = new()
        {
            Filter = $"{fileName}|{fileName}|Applications|*.exe",
            FileName = fileName
        };
        if (dialog.ShowDialog() == DialogResult.OK)
            target.Text = dialog.FileName;
    }

    private static void BrowseFolder(TextBox target)
    {
        using FolderBrowserDialog dialog = new() { SelectedPath = target.Text };
        if (dialog.ShowDialog() == DialogResult.OK)
            target.Text = dialog.SelectedPath;
    }

    private static string DetectArtemis()
    {
        string[] candidates =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Artemis", "Artemis.UI.Windows.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Artemis", "Artemis.UI.Windows.exe")
        };
        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    private static string DetectCs2Cfg()
    {
        string suffix = Path.Combine("steamapps", "common", "Counter-Strike Global Offensive", "game", "csgo", "cfg");
        List<string> libraries = new()
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam")
        };
        string libraryFile = Path.Combine(libraries[0], "steamapps", "libraryfolders.vdf");
        if (File.Exists(libraryFile))
        {
            foreach (string line in File.ReadLines(libraryFile))
            {
                int marker = line.IndexOf("\"path\"", StringComparison.OrdinalIgnoreCase);
                if (marker < 0)
                    continue;
                string[] quoted = line.Split('"', StringSplitOptions.RemoveEmptyEntries);
                string? path = quoted.LastOrDefault()?.Replace(@"\\", @"\");
                if (!string.IsNullOrWhiteSpace(path))
                    libraries.Add(path);
            }
        }

        return libraries.Select(root => Path.Combine(root, suffix)).FirstOrDefault(Directory.Exists) ?? "";
    }

    private static void ReplaceDirectory(string source, string destination)
    {
        if (Directory.Exists(destination))
            Directory.Delete(destination, recursive: true);
        CopyDirectory(source, destination);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        foreach (string directory in Directory.GetDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    private static void StartProcess(string fileName, string? arguments = null)
    {
        ProcessStartInfo start = new()
        {
            FileName = fileName,
            UseShellExecute = true
        };
        if (!string.IsNullOrWhiteSpace(arguments))
            start.Arguments = arguments;
        Process.Start(start);
    }

    private static string Quote(string value) => "\"" + value + "\"";
}
