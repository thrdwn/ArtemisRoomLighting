using HidSharp;
using HidSharp.Reports;

namespace Artemis.Plugins.DirectDevices;

public static class HidDeviceLocator
{
    public static IEnumerable<HidDevice> Find(IEnumerable<HidTarget> targets)
    {
        foreach (HidTarget target in targets)
        {
            foreach (HidDevice device in DeviceList.Local.GetHidDevices(target.VendorId, target.ProductId))
            {
                if (!Matches(target, device))
                    continue;

                yield return device;
                break;
            }
        }
    }

    private static bool Matches(HidTarget target, HidDevice device)
    {
        if (target.InterfaceNumber.HasValue)
        {
            string path = device.DevicePath.ToLowerInvariant();
            string needle = $"mi_{target.InterfaceNumber.Value:x2}";
            if (!path.Contains(needle))
                return false;
        }

        if (target.FeatureReportLength.HasValue && device.GetMaxFeatureReportLength() != target.FeatureReportLength.Value)
            return false;

        if (target.UsagePage.HasValue && target.Usage.HasValue && !HasUsage(device, target.UsagePage.Value, target.Usage.Value))
            return false;

        return true;
    }

    private static bool HasUsage(HidDevice device, int usagePage, int usage)
    {
        uint expected = ((uint)usagePage << 16) | (uint)usage;

        try
        {
            ReportDescriptor descriptor = device.GetReportDescriptor();
            foreach (DeviceItem item in descriptor.DeviceItems)
            {
                foreach (uint value in item.Usages.GetAllValues())
                {
                    if (value == expected)
                        return true;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}
