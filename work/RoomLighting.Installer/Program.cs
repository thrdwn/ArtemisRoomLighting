using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Principal;

namespace ArtemisRoomLighting.Installer;

internal static class Program
{
    private const string PayloadResource = "ArtemisRoomLighting.Payload.zip";
    internal static bool PreviewMode { get; private set; }

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--verify-payload", StringComparer.OrdinalIgnoreCase))
        {
            VerifyPayload();
            return;
        }

        PreviewMode = args.Contains("--preview", StringComparer.OrdinalIgnoreCase);
        if (!PreviewMode && !IsAdministrator())
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
                    "Administrator permission is required to configure Artemis and create backups.",
                    "Artemis Setup Assistant",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new InstallerForm());
    }

    internal static void ExtractPayload(string destination)
    {
        Directory.CreateDirectory(destination);
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResource)
            ?? throw new InvalidOperationException("The embedded installer payload is missing.");
        using ZipArchive archive = new(stream, ZipArchiveMode.Read);
        archive.ExtractToDirectory(destination, overwriteFiles: true);
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
}
