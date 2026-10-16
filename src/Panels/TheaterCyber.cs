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
    const int GridPx = 1024, GridStep = 16;              // GridPx wide, WallAspect times as tall (the wall goes all the way round)

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
            _grid ??= new Surface(_g, GridPx, (int)(GridPx * WallAspect));
            DrawGrid(flash);
            _gridLevel = flash;
            _dim.SetTexture(_grid.Tex);
            _gridOn = true;
        }
    }

    // the grid on the dark wall, fading out above and below like the black one (WallAlpha): in bands of a grid step, each band's
    // black and its lines at that band's alpha (premultiplied: a transparent clear, then fills at alpha)
    void DrawGrid(float flash)
    {
        int h = _grid.H;
        _p.Begin(_grid.Bitmap, 0x00000000);
        uint line = Theme.Mix(0xFF1E1619, CyberHud.Cyan, 0.35f * flash), major = Theme.Mix(0xFF33262A, CyberHud.Cyan, 0.5f * flash);
        for (int y = 0; y < h; y += GridStep)
        {
            float a = WallAlpha(MathF.Abs(y + GridStep / 2f - h / 2f) / (h / 2f));
            if (a <= 0.003f) continue;
            _p.Fill(new RectangleF(0, y, GridPx, GridStep), Theme.WithAlpha(0xFF000000, a));
            int row = y / GridStep;
            _p.Fill(new RectangleF(0, y, GridPx, 1), Theme.WithAlpha(row % 4 == 0 ? major : line, a));
            for (int i = 0; i < GridPx / GridStep; i++)
                _p.Fill(new RectangleF(i * GridStep, y, 1, GridStep), Theme.WithAlpha(i % 4 == 0 ? major : line, a));
        }
        _p.End();
    }

    // --snapshot: the wall's texture at a flash level
    public Surface GridForTest(float flash)
    {
        _grid ??= new Surface(_g, GridPx, (int)(GridPx * WallAspect));
        DrawGrid(flash);
        return _grid;
    }

    void DisposeCyber() { _grid?.Dispose(); _grid = null; }
}
