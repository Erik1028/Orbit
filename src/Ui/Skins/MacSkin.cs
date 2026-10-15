namespace Orbit;

// Classic Mac OS (System 7 / Mac OS 8 platinum): platinum grey, the pinstriped title bar with its close and zoom
// boxes, a Chicago-like bitmap font (Geneva-like for small text), chunky black-and-white controls, the default button's thick ring.
// Until this style is drawn it is Windows 95 (everything not overridden here comes from Win95Skin, in this palette).
public sealed partial class MacSkin : Win95Skin
{
    public override RetroStyle Style => RetroStyle.Mac;
    public override string Id => "mac";
    public override string Tag => "mac";
    public override string Name => "Classic Mac";
}
