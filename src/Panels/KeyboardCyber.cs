using System;
using System.Drawing;

namespace Orbit;

// The keyboard's Cyber motion (CyberFx): the key under a laser gets a cyan outline and the watch buttons' corner marks; a key let
// go sends its corner brackets out a few px as its press glow fades (0.25 s, the glow's own frames - Reduced too).
public sealed partial class KeyboardPanel
{
    void CyberKey(RectangleF kr, bool hover, bool down, float glow)
    {
        if (hover && !down)
        {
            _p.Stroke(RectangleF.Inflate(kr, -0.6f, -0.6f), CyberHud.Cyan, 1.5f, 11.4f);
            CyberHud.Brackets(_p, kr, CyberHud.Cyan, 9, 6, 2);
        }
        if (glow > 0.01f && !down && Motion.Any)
            CyberFx.Corners(_p, kr, Theme.WithAlpha(CyberHud.Cyan, glow), 10, 2, 1 + 7 * (1 - glow));
    }

    // --animshot: key i let go just now (its glow)
    public void CyberFlashForTest(int i)
    {
        if (i >= 0 && i < _keys.Count) _flash[_keys[i]] = 1;
        _lastDt = 1 / 120f;                         // Draw fades the glow by the last Update's step: the tool's
    }

    // --animshot: where key i is (texture px)
    public RectangleF KeyRectForTest(int i) => i >= 0 && i < _keys.Count ? _keys[i].R : RectangleF.Empty;
}
