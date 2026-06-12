namespace Artemis.Plugins.DirectDevices;

public sealed record WizLightAddresses(string StudyIp, string LowerIp, string UpperIp)
{
    public string StudyEndpoint => EndpointOrLoopback(StudyIp);
    public string LowerEndpoint => EndpointOrLoopback(LowerIp);
    public string UpperEndpoint => EndpointOrLoopback(UpperIp);

    public IEnumerable<(string Name, string Ip)> Enumerate()
    {
        if (!string.IsNullOrWhiteSpace(StudyIp))
            yield return ("Study Lamp", StudyIp.Trim());
        if (!string.IsNullOrWhiteSpace(LowerIp))
            yield return ("Lower Light", LowerIp.Trim());
        if (!string.IsNullOrWhiteSpace(UpperIp))
            yield return ("Upper Light", UpperIp.Trim());
    }

    private static string EndpointOrLoopback(string? ip)
    {
        return string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip.Trim();
    }
}
