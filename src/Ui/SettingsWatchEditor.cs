using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Vortice.DirectWrite;

namespace Orbit;

// The watch layout editor (Wrist tab, custom face): the live watch with its grid (6 x 4, or 4 x 3 on a page that has it). Click a
// widget to select it, drag it to move it, drag its corner to resize it; a palette adds new ones. Its pages along the top: the one
// edited is the one the watch shows; add one (from a preset, on either grid), remove one (a second click confirms). Both styles.
public sealed partial class SettingsView
{
    public bool EditingWatch;           // the Wrist tab shows the layout editor
    WatchWidget _wselW;                 // the selected widget (by reference: another window may edit the same list)
    (int x, int y) _wcell = (-1, -1);   // an empty cell that was clicked: the next widget goes there
    WatchWidget _wdrag;                 // being moved / resized
    bool _wresize;
    int _wgrabX, _wgrabY, _wgrabW, _wgrabH;
    string _wmsg;
    int _wpal, _wpreset;
    bool _wadd;                         // the "new page" choices are open (modern)
    int _waddPreset, _waddGrid;         // their preset (PagePresets) and grid (0: 6x4, the default; 1: 4x3)
    double _wremoveArmed = -10;         // "Remove page" clicked once: a second click within 4 s removes it
    (int page, string grid, List<WatchWidget> list) _gridUndo = (-1, null, null);   // the page before its grid changed (switching straight back)

    // the modern editor's watch picture (the page strip above it, the palette under it)
    public static readonly RectangleF EditorPrev = new(245, 118, 440, 302.5f);
    public const float PaletteY = 456, PalettePitch = 30;

    // the page being edited: the one the watch shows
    int EditPage => _h.Wrist.PageNow;
    GridSpec EditGridSpec => WatchPages.Grid(C.Wrist, EditPage);

    List<WatchWidget> WatchLayout()
    {
        var w = C.Wrist;
        int p = EditPage;
        if (p > 0) return w.Pages[p - 1].Widgets ??= new List<WatchWidget>();
        w.Widgets ??= WatchPages.Grid(w, 0).Is43 ? WatchWidgets.PagePreset("everyday", GridSpec.G43) : WatchWidgets.Seed(w, "full");
        return w.Widgets;
    }

    // page p's widgets and grid (page 1: its grid null when 6x4, as a face from before pages had it)
    void SetPage(int p, List<WatchWidget> list, string grid)
    {
        var w = C.Wrist;
        if (p <= 0) { w.Widgets = list; w.Grid = grid == "6x4" ? null : grid; }
        else { w.Pages[p - 1].Widgets = list; w.Pages[p - 1].Grid = grid; }
    }

    void ShowPage(int i)
    {
        _h.Wrist.GoToPage(i);
        _wselW = null;
        _wcell = (-1, -1);
        _wmsg = null;
        _wadd = false;
        _wremoveArmed = -10;
    }

    // a new page after the others (Empty: no name - it shows as "Page n"), shown at once
    void AddPage(Ui ui, string preset, string grid)
    {
        var w = C.Wrist;
        if (w.Widgets == null) WatchLayout();          // page 1 set up first (a face that never had one)
        // one of the faces of before pages: on the 6 x 4 grid, as it was
        bool old = Array.IndexOf(WatchWidgets.Presets, preset) >= 0;
        var page = old ? new WatchPage { Name = WatchWidgets.PresetName2(preset), Grid = "6x4", Widgets = WatchWidgets.Seed(w, preset) }
            : WatchPages.Make(preset, grid);
        if (preset == "empty") page.Name = null;
        (w.Pages ??= new List<WatchPage>()).Add(page);
        ShowPage(WatchPages.Count(w) - 1);
        ui.Changed = true;
    }

    // the page shown goes (page 1 too, when there is another: the next one becomes page 1); only by a second click
    bool RemovePage(Ui ui)
    {
        var w = C.Wrist;
        int n = WatchPages.Count(w), p = EditPage;
        if (n <= 1) return false;
        if (Clock.Now - _wremoveArmed > 4) { _wremoveArmed = Clock.Now; return false; }
        _wremoveArmed = -10;
        if (p == 0)
        {
            var next = w.Pages[0];
            w.Widgets = next.Widgets;
            w.Grid = next.Grid == "6x4" ? null : next.Grid;
            w.Name = next.Name;
            w.Pages.RemoveAt(0);
        }
        else w.Pages.RemoveAt(p - 1);
        if (w.Pages.Count == 0) w.Pages = null;
        w.Page = Math.Min(p, WatchPages.Count(w) - 1);
        _wselW = null;
        _wcell = (-1, -1);
        _wmsg = null;
        ui.Changed = true;
        return true;
    }

    bool RemoveArmed => Clock.Now - _wremoveArmed <= 4;

    // The page on the other grid: every widget where it was (scaled), in the nearest size there, else where there is room; what does
    // not fit is left out and said. Switching straight back gives the page as it was.
    void ChangeGrid(Ui ui, string grid)
    {
        int p = EditPage;
        string from = WatchPages.GridId(C.Wrist, p);
        if (from == grid) return;
        var list = WatchLayout();
        int dropped = 0;
        var next = _gridUndo.page == p && _gridUndo.grid == grid && _gridUndo.list != null ? _gridUndo.list
            : Relay(list, GridSpec.Of(from), GridSpec.Of(grid), out dropped);
        _gridUndo = (p, from, list);
        SetPage(p, next, grid);
        _wselW = null;
        _wcell = (-1, -1);
        _wmsg = dropped > 0 ? $"{dropped} widget{(dropped == 1 ? "" : "s")} did not fit on the {grid.Replace("x", " × ")} grid (switch back to keep {(dropped == 1 ? "it" : "them")})." : null;
        ui.Changed = true;
    }

    static List<WatchWidget> Relay(List<WatchWidget> list, GridSpec from, GridSpec to, out int dropped)
    {
        var res = new List<WatchWidget>();
        dropped = 0;
        foreach (var w in list.OrderBy(w => w.Y).ThenBy(w => w.X))
        {
            var k = WatchWidgets.Find(w.Type);
            if (k == null) { dropped++; continue; }
            int px = (int)MathF.Floor(w.X * (float)to.Cols / from.Cols), py = (int)MathF.Floor(w.Y * (float)to.Rows / from.Rows);
            int ww = Math.Max(1, (int)MathF.Round(w.W * (float)to.Cols / from.Cols)), hh = Math.Max(1, (int)MathF.Round(w.H * (float)to.Rows / from.Rows));
            var s = WatchWidgets.SnapSize(k, ww, hh, false, to);
            var n = new WatchWidget { Type = w.Type, X = px, Y = py, W = s.W, H = s.H, Style = w.Style, Color = w.Color, Color2 = w.Color2, Items = w.Items };
            if (WatchWidgets.Free(res, null, px, py, s.W, s.H, to)) { res.Add(n); continue; }
            var a = WatchWidgets.Add(res, k, Math.Min(px, to.Cols - 1), Math.Min(py, to.Rows - 1), false, to);
            if (a == null) { dropped++; continue; }
            (a.Style, a.Color, a.Color2, a.Items) = (w.Style, w.Color, w.Color2, w.Items);
        }
        return res;
    }

    WatchWidget SelWidget(List<WatchWidget> list)
    {
        if (_wselW != null && !list.Contains(_wselW)) _wselW = null;
        return _wselW;
    }

    static string KindName(WatchWidget w) => WatchWidgets.Find(w.Type)?.Name ?? "Unknown widget";

    // switching to the custom face the first time starts from the face the user had
    void ChooseFace(string face)
    {
        var w = C.Wrist;
        if (face == "custom" && w.Widgets == null) w.Widgets = WatchWidgets.Seed(w, w.Face ?? "full");
        w.Face = face;
    }

    // classic: the 95 editor (only the sizes the 95 face has; the modern one offers the Bento S and L too)
    void AddWidget(Ui ui, List<WatchWidget> list, WatchWidgets.Kind k, bool classic)
    {
        var n = WatchWidgets.Add(list, k, _wcell.x, _wcell.y, classic, EditGridSpec);
        if (n == null) { _wmsg = "No room left: remove or shrink a widget first."; return; }
        _wselW = n;
        _wcell = (-1, -1);
        _wmsg = null;
        ui.Changed = true;
    }

    void StepSize(Ui ui, List<WatchWidget> list, WatchWidget w, int dir)
    {
        if (WatchWidgets.Step(list, w, dir, false, EditGridSpec) is { } s) { w.W = s.W; w.H = s.H; ui.Changed = true; _wmsg = null; }
    }

    void RemoveSelected(Ui ui, List<WatchWidget> list)
    {
        var sel = SelWidget(list);
        if (sel == null) return;
        list.Remove(sel);
        _wcell = (sel.X, sel.Y);
        _wselW = null;
        _wmsg = null;
        ui.Changed = true;
    }

    void ClearWatch(Ui ui, List<WatchWidget> list)
    {
        list.Clear();
        _wselW = null;
        _wcell = (-1, -1);
        _wmsg = null;
        ui.Changed = true;
    }

    // the page shown starts over: from one of the faces of before pages (on the 6 x 4 grid), or a page preset on its own grid
    void StartOver(Ui ui, string preset)
    {
        int p = EditPage;
        bool old = Array.IndexOf(WatchWidgets.Presets, preset) >= 0;
        string grid = old ? "6x4" : WatchPages.GridId(C.Wrist, p);
        SetPage(p, old ? WatchWidgets.Seed(C.Wrist, preset) : WatchWidgets.PagePreset(preset, GridSpec.Of(grid)), grid);
        _wselW = null;
        _wcell = (-1, -1);
        _wmsg = null;
        ui.Changed = true;
    }

    // The preview: the live watch image is already drawn into 'prev'. This adds the grid, the selection and
    // the drag, and changes the layout. classic = the 95 canvas (1:1), else the modern texture (scaled).
    void EditGrid(Ui ui, RectangleF prev, bool classic, List<WatchWidget> list)
    {
        var p = ui.P;
        float sx = prev.Width / (classic ? WristPanel.TW95 : WristPanel.TW), sy = prev.Height / (classic ? WristPanel.TH95 : WristPanel.TH);
        RectangleF View(RectangleF r) => new(prev.X + r.X * sx, prev.Y + r.Y * sy, r.Width * sx, r.Height * sy);
        var m = ui.Mouse;
        var grid = EditGridSpec;
        var (cx, cy) = WatchWidgets.CellAt((m.X - prev.X) / sx, (m.Y - prev.Y) / sy, classic, grid);
        float hs = classic ? 5 : 14;
        RectangleF Handle(WatchWidget w)
        {
            var r = View(WatchWidgets.Rect(w, classic, grid));
            return classic ? new RectangleF(r.Right - 4, r.Bottom - 4, hs, hs) : new RectangleF(r.Right - hs / 2 - 2, r.Bottom - hs / 2 - 2, hs, hs);
        }
        if (_wcell.x >= 0 && WatchWidgets.At(list, _wcell.x, _wcell.y) != null) _wcell = (-1, -1);     // something took the marked cell

        var sel = SelWidget(list);
        if (ui.Hover(RectangleF.Inflate(prev, 8, 8)) && ui.Pressed)
        {
            _wmsg = null;
            if (sel != null && WatchWidgets.Find(sel.Type) != null &&
                RectangleF.Inflate(Handle(sel), classic ? 3 : 10, classic ? 3 : 10).Contains(m.X, m.Y))
            {
                // the size follows how far the corner is dragged (a plain click changes nothing)
                _wdrag = sel;
                _wresize = true;
                _wgrabX = cx; _wgrabY = cy; _wgrabW = sel.W; _wgrabH = sel.H;
            }
            else if (prev.Contains(m.X, m.Y))
            {
                var hit = WatchWidgets.At(list, cx, cy);
                _wselW = hit;
                _wcell = hit == null ? (cx, cy) : (-1, -1);
                if (hit != null)
                {
                    _wdrag = hit;
                    _wresize = false;
                    _wgrabX = cx - hit.X;
                    _wgrabY = cy - hit.Y;
                }
            }
        }

        // the empty cells (the one picked for the next widget marked)
        for (int y = 0; y < grid.Rows; y++)
            for (int x = 0; x < grid.Cols; x++)
            {
                if (WatchWidgets.At(list, x, y) != null) continue;
                var cr = View(WatchWidgets.CellRect(x, y, 1, 1, classic, grid));
                bool picked = _wcell == (x, y);
                if (classic)
                {
                    if (picked) p.FillChecker(cr, 0xFF000080, Theme.W95Teal);
                    p.Stroke(new RectangleF(cr.X + 0.5f, cr.Y + 0.5f, cr.Width - 1, cr.Height - 1), 0xFF004040, 1);
                }
                else
                {
                    if (picked) p.Fill(cr, Theme.WithAlpha(Theme.Accent, 0.22f), Bento.CardRadius * sx);
                    p.Stroke(RectangleF.Inflate(cr, -1, -1), picked ? Theme.Accent : 0x38FFFFFF, picked ? 2 : 1.2f, Bento.CardRadius * sx);
                }
            }

        // a move / resize in progress: where it would land (red when it doesn't fit)
        if (_wdrag != null)
        {
            var k = WatchWidgets.Find(_wdrag.Type);
            int nx = _wdrag.X, ny = _wdrag.Y, nw = _wdrag.W, nh = _wdrag.H;
            if (_wresize && k != null) (nw, nh) = cx == _wgrabX && cy == _wgrabY ? (_wgrabW, _wgrabH)
                : WatchWidgets.SnapSize(k, _wgrabW + cx - _wgrabX, _wgrabH + cy - _wgrabY, classic, grid);
            else if (!_wresize)
            {
                nx = Math.Clamp(cx - _wgrabX, 0, Math.Max(0, grid.Cols - nw));
                ny = Math.Clamp(cy - _wgrabY, 0, Math.Max(0, grid.Rows - nh));
            }
            bool changed = nx != _wdrag.X || ny != _wdrag.Y || nw != _wdrag.W || nh != _wdrag.H;
            bool ok = WatchWidgets.Free(list, _wdrag, nx, ny, nw, nh, grid);
            if (changed)
            {
                var gr = View(WatchWidgets.CellRect(nx, ny, nw, nh, classic, grid));
                if (classic) p.Stroke(new RectangleF(gr.X + 1, gr.Y + 1, gr.Width - 2, gr.Height - 2), ok ? 0xFF000000 : 0xFFFF0000, 2);
                else
                {
                    p.Fill(gr, ok ? Theme.WithAlpha(Theme.Accent, 0.3f) : 0x55F87171, Bento.CardRadius * sx);
                    p.Stroke(RectangleF.Inflate(gr, -1, -1), ok ? Theme.Accent : Theme.Bad, 2, Bento.CardRadius * sx);
                }
            }
            if (!ui.Down)       // let go (also when it happened outside the preview)
            {
                if (changed && ok)
                {
                    _wdrag.X = nx; _wdrag.Y = ny; _wdrag.W = nw; _wdrag.H = nh;
                    ui.Changed = true;
                }
                else if (changed) _wmsg = "It doesn't fit there.";
                _wdrag = null;
            }
        }

        // the selection: outlined, with a corner handle for resizing
        sel = SelWidget(list);
        if (sel != null)
        {
            var sr = View(WatchWidgets.Rect(sel, classic, grid));
            if (classic)
            {
                p.Stroke(new RectangleF(sr.X - 0.5f, sr.Y - 0.5f, sr.Width + 1, sr.Height + 1), 0xFF000000, 1);
                p.Fill(Handle(sel), 0xFF000000);
            }
            else
            {
                p.Stroke(RectangleF.Inflate(sr, 1, 1), Theme.Accent, 3, (Bento.CardRadius + 1) * sx);
                p.Fill(Handle(sel), Theme.Accent, 4);
            }
        }
    }

    // ================= Modern =================

    void DrawWristEditor(Ui ui)
    {
        var p = ui.P;
        var w = C.Wrist;
        var list = WatchLayout();
        PageStrip(ui, 222, 76, 486, 32);
        var prev = EditorPrev;
        p.Fill(RectangleF.Inflate(prev, 10, 10), Theme.Panel, 18);
        p.Image(_h.Wrist.Surf.Bitmap, prev);
        if (_h.Wrist.Animating) ui.Animating = true;          // the preview springs / rolls: keep drawing
        EditGrid(ui, prev, false, list);

        float rx = 724, rw = 260;
        var sel = SelWidget(list);
        // under the watch: a button group's buttons when one is selected, else the palette (click one to put it in the marked cell or
        // the first free spot); what went wrong (no room, it does not fit) over it
        if (sel?.Type == "buttons") GroupPicker(ui, sel, 222, PaletteY - 20);
        else
        {
            if (_wmsg != null) ui.Label(new RectangleF(222, PaletteY - 20, 486, 18), _wmsg, Theme.Warn, 13);
            else ui.Label(new RectangleF(222, PaletteY - 20, 486, 18), _wcell.x >= 0 ? "Add a widget to the marked cell" : "Add a widget");
            for (int i = 0; i < WatchWidgets.Palette.Length; i++)
            {
                var k = WatchWidgets.Palette[i];
                var r = new RectangleF(222 + i % 6 * 81, PaletteY + i / 6 * PalettePitch, 77, PalettePitch - 3);
                // the icon only when it fits with the name and a margin on both sides
                string icon = 11 + 8 + p.MeasureWidth(k.Name, 11, FontWeight.SemiBold) <= r.Width - 20 ? k.Icon : null;
                if (ui.Button(Ui.Id("wpal", i), r, k.Name, icon, false, true, 11)) AddWidget(ui, list, k, false);
            }
        }

        if (_wadd) NewPagePanel(ui, rx, rw);
        else if (sel == null)
        {
            // the page shown: its grid, remove it; the background (all pages); start this page over
            int n = WatchPages.Count(w), ep = EditPage;
            ui.Section(new RectangleF(rx, 84, rw, 22), n > 1 ? $"Page {ep + 1} of {n}" : "This page");
            int gi = EditGridSpec.Is43 ? 1 : 0;
            if (ui.Segmented("we.grid", new RectangleF(rx, 108, rw, 34), new[] { "6 × 4 grid", "4 × 3 grid" }, ref gi))
                ChangeGrid(ui, gi == 1 ? "4x3" : "6x4");
            if (ui.Button("we.rmpage", new RectangleF(rx, 150, rw, 34), RemoveArmed ? "Click again to remove this page" : "Remove this page",
                    Theme.IcoDelete, RemoveArmed, n > 1, 13))
                RemovePage(ui);

            ui.Section(new RectangleF(rx, 196, rw, 22), "Background");
            int bg = w.Background == "cards" ? 1 : 0;
            if (ui.Segmented("we.bg", new RectangleF(rx, 220, rw, 34), new[] { "Watch panel", "Cards only" }, ref bg))
                w.Background = bg == 1 ? "cards" : "panel";

            ui.Section(new RectangleF(rx, 266, rw, 22), "Start this page over from");
            var presets = StartPresets();
            for (int i = 0; i < presets.Length; i++)
                if (ui.Button(Ui.Id("wpre", i), new RectangleF(rx + i % 2 * 134, 290 + i / 2 * 38, 126, 34), WatchWidgets.PresetName2(presets[i]), null, false, true, 14))
                    StartOver(ui, presets[i]);
            int ei = presets.Length;
            if (ui.Button("we.empty", new RectangleF(rx + ei % 2 * 134, 290 + ei / 2 * 38, 126, 34), "Empty", null, false, list.Count > 0, 14)) ClearWatch(ui, list);
            ui.Label(new RectangleF(rx, 486, rw, 80), "Click a widget to select it, drag it to move it, drag its corner to resize it. Click an empty cell " +
                     "to choose where the next one goes. On the watch, swipe sideways to change the page.", Theme.Dim, 13, FontWeight.Normal, true);
        }
        else
        {
            // its name and size, smaller / larger / remove in a row, then its look (the background and "start over" are back when
            // nothing is selected)
            p.Text(KindName(sel), new RectangleF(rx, 106, rw - 64, 28), Theme.Text, 20, FontWeight.SemiBold);
            p.Text($"{sel.W} × {sel.H}", new RectangleF(rx + rw - 64, 106, 64, 28), Theme.Dim, 14, FontWeight.Normal, TextAlignment.Trailing);
            float bw = (rw - 16) / 3;
            if (ui.Button("we.smaller", new RectangleF(rx, 138, bw, 34), "Smaller", null, false, WatchWidgets.Step(list, sel, -1) != null, 13))
                StepSize(ui, list, sel, -1);
            if (ui.Button("we.larger", new RectangleF(rx + bw + 8, 138, bw, 34), "Larger", null, false, WatchWidgets.Step(list, sel, +1) != null, 13))
                StepSize(ui, list, sel, +1);
            if (ui.Button("we.remove", new RectangleF(rx + 2 * (bw + 8), 138, bw, 34), "Remove", Theme.IcoDelete, false, true, 13)) RemoveSelected(ui, list);
            if (sel != null && list.Contains(sel))
            {
                // a clock: its designs first, then its look - both as one row of chips, so it all fits above Done
                bool clock = WatchWidgets.VariantsOf(sel.Type) != null;
                if (clock) ClockDesigns(ui, sel, rx, 180, rw);
                WidgetLook(ui, list, sel, rx, clock ? 250 : 182, rw, clock);
            }
        }
        if (ui.Button("we.done", new RectangleF(rx, 574, rw, 44), "Done", Theme.IcoCheck, true, true, 15)) { EditingWatch = false; _wadd = false; }
    }

    // the presets "start over" offers: the faces of before pages (6 x 4), then the page presets (on the page's own grid)
    static readonly string[] _startPresets = WatchWidgets.Presets.Concat(new[] { "everyday", "vrchat", "performance" }).ToArray();
    static string[] StartPresets() => _startPresets;

    // The pages as tabs over the watch (the one edited is the one the watch shows), "Add page" at the end
    void PageStrip(Ui ui, float x, float y, float w, float h)
    {
        var p = ui.P;
        var wc = C.Wrist;
        int n = WatchPages.Count(wc), cur = EditPage;
        const float addW = 118, gap = 6;
        float tw = MathF.Min(150, (w - addW - gap * n) / n);
        for (int i = 0; i < n; i++)
        {
            var r = new RectangleF(x + i * (tw + gap), y, tw, h);
            string name = WatchPages.Name(wc, i);
            string label = Cut(p, name, tw - 16, 13);
            if (ui.Button(Ui.Id("wpg", i), r, label, null, i == cur, true, 13) && i != cur) ShowPage(i);
        }
        if (ui.Button("we.addpage", new RectangleF(x + w - addW, y, addW, h), "Add page", Theme.IcoAdd, _wadd, n < 8, 13))
        {
            _wadd = !_wadd;
            _wselW = null;
        }
    }

    static string Cut(Painter p, string s, float maxW, float size)
    {
        if (p.MeasureWidth(s, size, FontWeight.SemiBold) <= maxW) return s;
        int n = s.Length;
        while (n > 1 && p.MeasureWidth(s[..n] + "…", size, FontWeight.SemiBold) > maxW) n--;
        return s[..n].TrimEnd() + "…";
    }

    // The new page's choices (in place of the selection): what it starts from, its grid; Add page / Cancel
    void NewPagePanel(Ui ui, float rx, float rw)
    {
        ui.Section(new RectangleF(rx, 84, rw, 22), "New page");
        ui.Label(new RectangleF(rx, 106, rw, 20), "Start from", Theme.Dim, 13);
        for (int i = 0; i < WatchWidgets.PagePresets.Length; i++)
            if (ui.Button(Ui.Id("wnp", i), new RectangleF(rx + i % 2 * 134, 130 + i / 2 * 40, 126, 34), WatchWidgets.PagePresetNames[i], null, i == _waddPreset, true, 14))
                _waddPreset = i;
        ui.Section(new RectangleF(rx, 216, rw, 22), "Grid");
        ui.Segmented("we.npgrid", new RectangleF(rx, 240, rw, 34), new[] { "6 × 4 (small cards)", "4 × 3 (big cards)" }, ref _waddGrid);
        if (ui.Button("we.npadd", new RectangleF(rx, 292, rw, 40), "Add page", Theme.IcoAdd, true, true, 15))
            AddPage(ui, WatchWidgets.PagePresets[Math.Clamp(_waddPreset, 0, WatchWidgets.PagePresets.Length - 1)], _waddGrid == 1 ? "4x3" : "6x4");
        if (ui.Button("we.npcancel", new RectangleF(rx, 340, rw, 34), "Cancel", null, false, true, 14)) _wadd = false;
    }

    // A button group's buttons: every button kind as a chip, the ones in the group lit (2 to 6 of them)
    void GroupPicker(Ui ui, WatchWidget sel, float x, float y)
    {
        var items = sel.Items ??= new List<string>(WatchWidgets.GroupDefault);
        items.RemoveAll(s => Array.IndexOf(WatchWidgets.GroupKinds, s) < 0);
        ui.Label(new RectangleF(x, y, 486, 18), $"Buttons in this group: {items.Count} (2 to 6)", Theme.Dim, 13);
        for (int i = 0; i < WatchWidgets.GroupKinds.Length; i++)
        {
            string id = WatchWidgets.GroupKinds[i];
            var k = WatchWidgets.Find(id);
            bool on = items.Contains(id);
            var r = new RectangleF(x + i % 4 * 122, y + 22 + i / 4 * 38, 116, 34);
            if (ui.Button(Ui.Id("wgk", i), r, k.Name, k.Icon, on, on ? items.Count > 2 : items.Count < 6, 12))
            {
                if (on) items.Remove(id); else items.Add(id);
                ui.Changed = true;
            }
        }
    }

    // The selected widget's own look (the modern watch only; the 95 face stays as 1995 drew it): its style as chips, each with a
    // small picture of it in the widget's colour; its colour (Auto: the accent, or purple / blue for the CPU and memory); a two-tone's
    // second colour; and the same look on every widget of the face. Every target 34 px or more (the settings also show in VR).
    // compact: one row of chips without names (the style's name in the section's title: the one under the pointer, else the one
    // it has) - a clock's look, under its designs
    void WidgetLook(Ui ui, List<WatchWidget> list, WatchWidget sel, float x, float y, float w, bool compact = false)
    {
        var p = ui.P;
        int style = Bento.StyleIndex(sel.Style);
        uint auto = WristPanel.NaturalColor(sel.Type), c = sel.Color ?? auto;
        if (compact)
        {
            string title = null;
            float cw1 = (w - 6 * 3.3f) / 7;
            for (int i = 0; i < Bento.Styles.Length; i++)
            {
                var r = new RectangleF(x + i * (cw1 + 3.3f), y + 22, cw1, 40);
                if (ui.Card(Ui.Id("wsty", i), r, i == style, out bool hover) && i != style)
                {
                    sel.Style = Bento.Styles[i];
                    style = i;
                    ui.Changed = true;
                }
                if (hover) title = Bento.StyleNames[i];
                var look = Bento.Make(i, c, i == 3 ? sel.Color2 ?? 0 : 0, sel.Color.HasValue, false, false);
                Bento.Sample(p, RectangleF.Inflate(r, -5, -7), look, 4);
            }
            ui.Section(new RectangleF(x, y, w, 22), "Style");
            p.Text(title ?? Bento.StyleNames[style], new RectangleF(x, y, w, 22), Theme.Text, 12, FontWeight.SemiBold, TextAlignment.Trailing);
            y += 22 + 40 + 8;
        }
        else
        {
            ui.Section(new RectangleF(x, y, w, 22), "Style");
            y += 24;
            const float cw = 59.5f, ch = 46, gap = 7;
            for (int i = 0; i < Bento.Styles.Length; i++)
            {
                var r = new RectangleF(x + i % 4 * (cw + gap), y + i / 4 * (ch + 6), cw, ch);
                if (ui.Card(Ui.Id("wsty", i), r, i == style, out _) && i != style)
                {
                    sel.Style = Bento.Styles[i];
                    style = i;
                    ui.Changed = true;
                }
                var look = Bento.Make(i, c, i == 3 ? sel.Color2 ?? 0 : 0, sel.Color.HasValue, false, false);
                Bento.Sample(p, new RectangleF(r.X + 7, r.Y + 6, r.Width - 14, 20), look, 5);
                p.Text(Bento.StyleNames[i], new RectangleF(r.X, r.Y + 27, r.Width, 17), i == style ? Theme.Text : Theme.Dim, 11.5f, FontWeight.SemiBold,
                    TextAlignment.Center);
            }
            y += 2 * ch + 6 + 10;
        }
        ui.Section(new RectangleF(x, y, w, 22), "Colour");
        y += 24;
        if (Swatches(ui, "wcol", x, y, sel.Color, auto, out var picked)) { sel.Color = picked; c = picked ?? auto; }
        y += 80;
        if (style == 3)
        {
            ui.Section(new RectangleF(x, y, w, 22), "Second colour");
            y += 24;
            if (Swatches(ui, "wcol2", x, y, sel.Color2, Bento.Second(c), out var picked2)) sel.Color2 = picked2;
            y += 80;
        }
        if (ui.Button("we.lookall", new RectangleF(x, y, w, 34), "Use on all widgets", null, false, list.Count > 1, 13))
        {
            foreach (var o in list) { o.Style = sel.Style; o.Color = sel.Color; o.Color2 = sel.Color2; }
            ui.Changed = true;
        }
        LookBottom = y + 34;
    }

    public float LookBottom;            // --snapshot: where the selected widget's look ends (Done is at 574)

    // A clock's designs (WatchWidget.Variant), each a chip with a small picture of it in the widget's look - only the ones its size
    // takes (a design it has that does not fit draws its size's default, which is the one marked). The name in the section's title.
    public static RectangleF ClockChip(int i, int n, float x = 724, float y = 202, float w = 260)
    {
        float cw = (w - 4 * (n - 1)) / n;
        return new RectangleF(x + i * (cw + 4), y, cw, 40);
    }

    void ClockDesigns(Ui ui, WatchWidget sel, float x, float y, float w)
    {
        var p = ui.P;
        var all = WatchWidgets.VariantsOf(sel.Type);
        var offered = all.Where(v => v.Fits(sel.W, sel.H)).ToArray();
        string cur = WatchWidgets.VariantOf(sel), title = null;
        int style = Bento.StyleIndex(sel.Style);
        var look = Bento.Make(style, sel.Color ?? WristPanel.NaturalColor(sel.Type), style == 3 ? sel.Color2 ?? 0 : 0, sel.Color.HasValue, false, false);
        for (int i = 0; i < offered.Length; i++)
        {
            var v = offered[i];
            var r = ClockChip(i, offered.Length, x, y + 22, w);
            if (ui.Card(Ui.Id("wvar", i), r, v.Id == cur, out bool hover) && v.Id != cur)
            {
                sel.Variant = WatchWidgets.StoreVariant(sel, v.Id);
                cur = v.Id;
                ui.Changed = true;
            }
            if (hover) title = v.Name;
            ClockArt.Preview(p, RectangleF.Inflate(r, -5, -5), sel.Type, v.Id, look, look.Accent);
        }
        ui.Section(new RectangleF(x, y, w, 22), "Clock");
        p.Text(title ?? Array.Find(all, v => v.Id == cur)?.Name, new RectangleF(x, y, w, 22), Theme.Text, 12, FontWeight.SemiBold, TextAlignment.Trailing);
    }

    // Auto, then the palette: 7 a row, 34 px each
    static bool Swatches(Ui ui, string id, float x, float y, uint? value, uint auto, out uint? picked)
    {
        picked = value;
        bool changed = false;
        for (int i = 0; i <= Bento.Palette.Length; i++)
        {
            uint col = i == 0 ? auto : Bento.Palette[i - 1].c;
            bool on = i == 0 ? value == null : value is uint v && (v & 0xFFFFFF) == (col & 0xFFFFFF);
            if (ui.Swatch(Ui.Id(id, i), x + 17 + i % 7 * 37.6f, y + 17 + i / 7 * 38, 14, col, on, i == 0 ? "A" : null))
            {
                picked = i == 0 ? null : col;
                changed = true;
            }
        }
        return changed;
    }

    // ================= Windows 95 =================

    void Wrist95Editor(Ui ui)
    {
        if (SkinWatchEditor(ui)) return;          // a style's own editor (ScreenHooks.cs)
        var p = ui.P;
        var w = C.Wrist;
        var list = WatchLayout();
        ui.Group95(new RectangleF(16, 56, 350, 262), "Watch layout");
        var desk = new RectangleF(23, 74, 336, 236);
        p.Fill(desk, Theme.W95Face);
        p.Bevel(desk, Painter.Edge.Sunken);
        var prev = new RectangleF(31, 82, WristPanel.TW95, WristPanel.TH95);
        if (_h.Wrist.Canvas95 != null) p.ImageNearest(_h.Wrist.Canvas95.Bitmap, prev);
        EditGrid(ui, prev, true, list);

        // the pages: the one edited (the watch shows it), a new one from "From", remove it (a second click confirms)
        int n = WatchPages.Count(w), ep = EditPage;
        var pageNames = new string[n];
        for (int i = 0; i < n; i++) pageNames[i] = $"{i + 1}  {WatchPages.Name(w, i)}";
        Lbl(ui, 16, 327, "Pa&ge:");
        bool before = ui.Changed;
        int pi = ep;
        if (ui.Combo95("we95.page", new RectangleF(54, 327, 128, RowH), pageNames, ref pi) && pi != ep) ShowPage(pi);
        ui.Changed = before;                    // looking at another page is not a change
        var presets = StartPresets();
        string from = presets[Math.Clamp(_wpreset, 0, presets.Length - 1)];
        if (ui.Button95("we95.newpage", new RectangleF(188, 326, 88, 23), "&New page", n < 8)) AddPage(ui, from, "6x4");
        if (ui.Button95("we95.rmpage", new RectangleF(280, 326, 86, 23), RemoveArmed ? "Sure?" : "Remove page", n > 1)) RemovePage(ui);

        ui.Group95(new RectangleF(16, 362, 350, 68), "Start over");
        Lbl(ui, 28, 382, "&From:");
        before = ui.Changed;
        var presetNames = presets.Select(WatchWidgets.PresetName2).ToArray();
        ui.Combo95("we95.preset", new RectangleF(84, 382, 144, RowH), presetNames, ref _wpreset);
        ui.Changed = before;                    // choosing in the list is not a change yet
        if (ui.Button95("we95.reset", new RectangleF(236, 380, 75, 23), "&Reset")) StartOver(ui, from);
        if (ui.Button95("we95.empty", new RectangleF(236, 405, 75, 22), "&Empty", list.Count > 0)) ClearWatch(ui, list);
        Lbl(ui, 28, 406, "&Grid:");
        int gi = EditGridSpec.Is43 ? 1 : 0, gi0 = gi;
        if (ui.Combo95("we95.grid", new RectangleF(84, 406, 144, RowH), new[] { "6 x 4", "4 x 3" }, ref gi) && gi != gi0) ChangeGrid(ui, gi == 1 ? "4x3" : "6x4");

        ui.Group95(new RectangleF(376, 56, 358, 218), "Widgets");
        int hit = ui.List95("we95.pal", new RectangleF(388, 76, 214, 188), WatchWidgets.Palette.Length, _wpal, (i, r, on) =>
        {
            var k = WatchWidgets.Palette[i];
            W95Icons.Draw(p, k.Icon95, r.X + 1, r.Y);
            ui.Text95(k.Name, new RectangleF(r.X + 20, r.Y, r.Width - 22, r.Height), on ? 0xFFFFFFFF : 0xFF000000);
        });
        if (hit >= 0) _wpal = hit;
        if (ui.Button95("we95.add", new RectangleF(610, 76, 112, 23), _wcell.x >= 0 ? "&Add here" : "&Add", true, true))
            AddWidget(ui, list, WatchWidgets.Palette[Math.Clamp(_wpal, 0, WatchWidgets.Palette.Length - 1)], true);
        if (_wmsg != null) ui.Text95(_wmsg, new RectangleF(610, 106, 112, 60), 0xFF800000, wrap: true);

        ui.Group95(new RectangleF(376, 280, 358, 102), "Selected widget");
        var sel = SelWidget(list);
        if (sel == null) ui.Text95("(none)", new RectangleF(388, 300, 300, 13), 0xFF808080);
        else
        {
            var k = WatchWidgets.Find(sel.Type);
            W95Icons.Draw(p, k?.Icon95 ?? W95Icons.Id.Window, 388, 298);
            ui.Text95(KindName(sel), new RectangleF(410, 299, 300, 15), 0xFF000000, true);
            if (k != null)
            {
                // the 95 face's sizes (a 4 x 3 page: its own); a size only the modern face has selects none (the box shows empty) until
                // one is picked
                var grid = EditGridSpec;
                var ks = grid.Is43 ? WatchWidgets.Sizes43(k) : k.Sizes;
                var sizes = ks.Select(s => $"{s.W} x {s.H}").ToArray();
                int si = Array.FindIndex(ks, s => s.W == sel.W && s.H == sel.H);
                int si2 = si;
                bool before2 = ui.Changed;
                if (ComboRow(ui, "we95.size", 388, 322, 468, 100, "&Size:", sizes, ref si2) && si2 != si)
                {
                    var s = ks[si2];
                    if (WatchWidgets.Free(list, sel, sel.X, sel.Y, s.W, s.H, grid)) { sel.W = s.W; sel.H = s.H; _wmsg = null; }
                    else { _wmsg = "That size doesn't fit there."; ui.Changed = before2; }
                }
                if (sel.Type == "buttons")
                {
                    // its buttons: a check box each (2 to 6)
                    var items = sel.Items ??= new List<string>(WatchWidgets.GroupDefault);
                    for (int i = 0; i < WatchWidgets.GroupKinds.Length; i++)
                    {
                        string id = WatchWidgets.GroupKinds[i];
                        bool on = items.Contains(id), v = on;
                        if (ui.Check95(Ui.Id("we95.gk", i), 388 + i % 4 * 84, 348 + i / 4 * 16, WatchWidgets.Find(id).Title95, ref v,
                                on ? items.Count > 2 : items.Count < 6) && v != on)
                        {
                            if (v) items.Add(id); else items.Remove(id);
                        }
                    }
                }
            }
            if (ui.Button95("we95.rm", new RectangleF(647, 321, 75, 23), "Re&move")) RemoveSelected(ui, list);
        }
        int bg = w.Background == "cards" ? 1 : 0;
        if (ComboRow(ui, "we95.bg", 376, 392, 468, 130, "&Background:", new[] { "Desktop", "Windows only" }, ref bg))
            w.Background = bg == 1 ? "cards" : "panel";
        if (ui.Button95("we95.done", new RectangleF(659, 405, 75, 23), "&Done", true, true)) EditingWatch = false;
    }
}
