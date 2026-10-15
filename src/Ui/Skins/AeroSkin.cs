namespace Orbit;

// Windows 7 Aero: a translucent glass frame and title bar with a glossy highlight, Segoe UI 9 pt, soft glossy
// blue-white buttons, Explorer-style settings (a navigation pane, a breadcrumb bar), the 7-style progress bars and sliders.
// Until this style is drawn it is Windows 95 (everything not overridden here comes from Win95Skin, in this palette).
public sealed partial class AeroSkin : Win95Skin
{
    public override RetroStyle Style => RetroStyle.Aero;
    public override string Id => "aero";
    public override string Tag => "aero";
    public override string Name => "Windows 7";
}
