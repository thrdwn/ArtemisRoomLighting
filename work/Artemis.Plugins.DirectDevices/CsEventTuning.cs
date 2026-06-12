namespace Artemis.Plugins.DirectDevices;

public readonly record struct CsEventTuning(
    int FlashIntensity,
    int FireIntensity,
    int SmokeIntensity,
    int DeathIntensity,
    int ImpactIntensity,
    int BombIntensity,
    int ClutchIntensity,
    int TeamContrast,
    int UtilityBrightness)
{
    public static CsEventTuning Default => new(150, 145, 115, 150, 135, 130, 125, 145, 150);

    public double Flash => Percent(FlashIntensity);
    public double Fire => Percent(FireIntensity);
    public double Smoke => Percent(SmokeIntensity);
    public double Death => Percent(DeathIntensity);
    public double Impact => Percent(ImpactIntensity);
    public double Bomb => Percent(BombIntensity);
    public double Clutch => Percent(ClutchIntensity);
    public double Team => Percent(TeamContrast);
    public double Utility => Percent(UtilityBrightness);

    private static double Percent(int value) => Math.Clamp(value, 0, 200) / 100.0;
}
