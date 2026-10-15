namespace Orbit;

// Windows XP Luna: blue title bars with rounded top corners, Trebuchet MS bold titles, Tahoma 8 pt,
// rounded buttons with the orange hover rim, the cream dialog face #ECE9D8, green-block progress bars, the blue scroll bars.
// Until this style is drawn it is Windows 95 (everything not overridden here comes from Win95Skin, in this palette).
public sealed partial class XpSkin : Win95Skin
{
    public override RetroStyle Style => RetroStyle.XP;
    public override string Id => "xp";
    public override string Tag => "xp";
    public override string Name => "Windows XP";
}
