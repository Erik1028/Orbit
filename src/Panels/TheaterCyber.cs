using System;
using System.Drawing;

namespace Orbit;

// Theater mode in the Cyber style: the dark wall behind the big screen is a faint HUD grid (dark-red hairlines, every fourth one a
// little brighter) instead of plain black; while the screen comes in the grid flashes cyan and settles (Full). The screen's own
// lock-on and scan, and the switch's unfold over the old one, are CyberScreenFx's (the panels').
public sealed partial class Theater
{
    Surface _grid;
    bool _gridOn;
    float _gridLevel = -1;
    const int GridPx = 512, GridStep = 16;

    // with the wall shown this frame: its texture for the style, the grid's flash while the screen comes in
    void CyberWall()
    {
        bool cy = Theme.Cyber && _p != null;
        if (!cy)
        {
            if (_gridOn) { _dim.SetTexture(_black.Tex); _gridOn = false; }
            return;
        }
        float flash = CyberFx.Full && On ? 1 - CyberFx.Clamp01(_t) : 0;
        flash = MathF.Round(flash * 20) / 20;
        if (_grid == null || flash != _gridLevel || !_gridOn)
        {
            _grid ??= new Surface(_g, GridPx, GridPx);
            DrawGrid(flash);
            _gridLevel = flash;
            _dim.SetTexture(_grid.Tex);
            _gridOn = true;
        }
    }

    void DrawGrid(float flash)
    {
        _p.Begin(_grid.Bitmap, 0xFF000000);
        uint line = Theme.Mix(0xFF1E1619, CyberHud.Cyan, 0.35f * flash), major = Theme.Mix(0xFF33262A, CyberHud.Cyan, 0.5f * flash);
        for (int i = 0; i < GridPx / GridStep; i++)
        {
            uint c = i % 4 == 0 ? major : line;
            _p.Fill(new RectangleF(i * GridStep, 0, 1, GridPx), c);
            _p.Fill(new RectangleF(0, i * GridStep, GridPx, 1), c);
        }
        _p.End();
    }

    // --snapshot: the wall's texture at a flash level
    public Surface GridForTest(float flash)
    {
        _grid ??= new Surface(_g, GridPx, GridPx);
        DrawGrid(flash);
        return _grid;
    }

    void DisposeCyber() { _grid?.Dispose(); _grid = null; }
}
