namespace Orbit;

// A green phosphor terminal: #33FF66-ish on a black CRT, a VGA-like monospace font, ASCII bars [#####-----], F-key
// buttons, a text-mode setup utility, faint scanlines and glow - with hit areas as big as the 95 ones.
// Until this style is drawn it is Windows 95 (everything not overridden here comes from Win95Skin, in this palette).
public sealed partial class TerminalSkin : Win95Skin
{
    public override RetroStyle Style => RetroStyle.Terminal;
    public override string Id => "terminal";
    public override string Tag => "term";
    public override string Name => "Terminal";
}
