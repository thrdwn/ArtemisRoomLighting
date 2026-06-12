using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Artemis.Plugins.DirectDevices;

internal sealed class WindowsHidFeatureSender : IDisposable
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;

    private readonly string _devicePath;
    private SafeFileHandle? _handle;

    public WindowsHidFeatureSender(string devicePath)
    {
        _devicePath = devicePath;
    }

    public void SetFeature(byte[] report)
    {
        SafeFileHandle handle = GetHandle();
        if (HidD_SetFeature(handle, report, report.Length))
            return;

        int error = Marshal.GetLastWin32Error();
        ResetHandle();
        throw new Win32Exception(error, $"Unable to write HID feature report to {_devicePath}");
    }

    public void Dispose()
    {
        ResetHandle();
    }

    private SafeFileHandle GetHandle()
    {
        if (_handle is { IsClosed: false, IsInvalid: false })
            return _handle;

        int lastError = 0;
        foreach (uint desiredAccess in new[] { GenericRead | GenericWrite, GenericWrite, 0u })
        {
            SafeFileHandle handle = CreateFileW(
                _devicePath,
                desiredAccess,
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                OpenExisting,
                FileAttributeNormal,
                IntPtr.Zero);

            if (!handle.IsInvalid)
            {
                _handle = handle;
                return handle;
            }

            lastError = Marshal.GetLastWin32Error();
            handle.Dispose();
        }

        throw new Win32Exception(lastError, $"Unable to open HID class device for feature reports ({_devicePath})");
    }

    private void ResetHandle()
    {
        _handle?.Dispose();
        _handle = null;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_SetFeature(SafeFileHandle hidDeviceObject, byte[] reportBuffer, int reportBufferLength);
}
