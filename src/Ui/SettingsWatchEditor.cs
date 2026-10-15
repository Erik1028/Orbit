using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Vortice.DirectWrite;

namespace Orbit;

// The watch layout editor (Wrist tab, custom face): the live watch with its 6 x 4 grid. Click a widget to
// select it, drag it to move it, drag its corner to resize it; a palette adds new ones. Both styles.
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

    List<WatchWidget> WatchLayout()
    {
        var w = C.Wrist;
        w.Widgets ??= WatchWidgets.Seed(w, "full");
        return w.Widgets;
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

    void AddWidget(Ui ui, List<WatchWidget> list, WatchWidgets.Kind k)
    {
        var n = WatchWidgets.Add(list, k, _wcell.x, _wcell.y);
        if (n == null) { _wmsg = "No room left: remove or shrink a widget first."; return; }
        _wselW = n;
        _wcell = (-1, -1);
        _wmsg = null;
        ui.Changed = true;
    }

    void StepSize(Ui ui, List<WatchWidget> list, WatchWidget w, int dir)
    {
        if (WatchWidgets.Step(list, w, dir) is { } s) { w.W = s.W; w.H = s.H; ui.Changed = true; _wmsg = null; }
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

    void StartOver(Ui ui, string preset)
    {
        C.Wrist.Widgets = WatchWidgets.Seed(C.Wrist, preset);
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
        var (cx, cy) = WatchWidgets.CellAt((m.X - prev.X) / sx, (m.Y - prev.Y) / sy, classic);
        float hs = classic ? 5 : 14;
        RectangleF Handle(WatchWidget w)
        {
            var r = View(WatchWidgets.Rect(w, classic));
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
        for (int y = 0; y < WatchWidgets.Rows; y++)
            for (int x = 0; x < WatchWidgets.Cols; x++)
            {
                if (WatchWidgets.At(list, x, y) != null) continue;
                var cr = View(WatchWidgets.CellRect(x, y, 1, 1, classic));
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
                : WatchWidgets.SnapSize(k, _wgrabW + cx - _wgrabX, _wgrabH + cy - _wgrabY);
            else if (!_wresize)
            {
                nx = Math.Clamp(cx - _wgrabX, 0, WatchWidgets.Cols - nw);
                ny = Math.Clamp(cy - _wgrabY, 0, WatchWidgets.Rows - nh);
            }
            bool changed = nx != _wdrag.X || ny != _wdrag.Y || nw != _wdrag.W || nh != _wdrag.H;
            bool ok = WatchWidgets.Free(list, _wdrag, nx, ny, nw, nh);
            if (changed)
            {
                var gr = View(WatchWidgets.CellRect(nx, ny, nw, nh, classic));
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
            var sr = View(WatchWidgets.Rect(sel, classic));
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
        var prev = new RectangleF(222, 84, 480, 330);
        p.Fill(RectangleF.Inflate(prev, 10, 10), Theme.Panel, 18);
        p.Image(_h.Wrist.Surf.Bitmap, prev);
        if (_h.Wrist.Animating) ui.Animating = true;          // the preview springs / rolls: keep drawing
        EditGrid(ui, prev, false, list);

        // the palette: click one to put it in the marked cell or the first free spot
        // what went wrong (no room, it does not fit) where it happened: over the palette, under the watch
        if (_wmsg != null) ui.Label(new RectangleF(222, 428, 480, 20), _wmsg, Theme.Warn, 13);
        else ui.Label(new RectangleF(222, 428, 480, 20), _wcell.x >= 0 ? "Add a widget to the marked cell" : "Add a widget");
        for (int i = 0; i < WatchWidgets.Palette.Length; i++)
        {
            var k = WatchWidgets.Palette[i];
            var r = new RectangleF(222 + i % 6 * 81, 450 + i / 6 * 34, 77, 30);
            // the icon only when it fits with the name and a margin on both sides
            string icon = 11 + 8 + p.MeasureWidth(k.Name, 11, FontWeight.SemiBold) <= r.Width - 20 ? k.Icon : null;
            if (ui.Button(Ui.Id("wpal", i), r, k.Name, icon, false, true, 11)) AddWidget(ui, list, k);
        }

        float rx = 724, rw = 260;
        var sel = SelWidget(list);
        ui.Section(new RectangleF(rx, 84, rw, 22), "Selected");
        if (sel == null)
        {
            ui.Label(new RectangleF(rx, 108, rw, 90), "Click a widget on the watch to select it. Drag it to move it, drag its corner to resize it. " +
                     "Click an empty cell to choose where the next one goes.", Theme.Dim, 13, FontWeight.Normal, true);

            ui.Section(new RectangleF(rx, 274, rw, 22), "Background");
            int bg = w.Background == "cards" ? 1 : 0;
            if (ui.Segmented("we.bg", new RectangleF(rx, 298, rw, 36), new[] { "Watch panel", "Cards only" }, ref bg))
                w.Background = bg == 1 ? "cards" : "panel";

            ui.Section(new RectangleF(rx, 346, rw, 22), "Start over from");
            for (int i = 0; i < WatchWidgets.Presets.Length; i++)
                if (ui.Button(Ui.Id("wpre", i), new RectangleF(rx + i % 2 * 134, 370 + i / 2 * 40, 126, 34), WatchWidgets.PresetNames[i], null, false, true, 14))
                    StartOver(ui, WatchWidgets.Presets[i]);
            if (ui.Button("we.empty", new RectangleF(rx + 134, 450, 126, 34), "Empty", null, false, list.Count > 0, 14)) ClearWatch(ui, list);
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
            if (sel != null && list.Contains(sel)) WidgetLook(ui, list, sel, rx, 182, rw);
        }
        if (ui.Button("we.done", new RectangleF(rx, 574, rw, 44), "Done", Theme.IcoCheck, true, true, 15)) EditingWatch = false;
    }

    // The selected widget's own look (the modern watch only; the 95 face stays as 1995 drew it): its style as chips, each with a
    // small picture of it in the widget's colour; its colour (Auto: the accent, or purple / blue for the CPU and memory); a two-tone's
    // second colour; and the same look on every widget of the face. Every target 34 px or more (the settings also show in VR).
    void WidgetLook(Ui ui, List<WatchWidget> list, WatchWidget sel, float x, float y, float w)
    {
        var p = ui.P;
        int style = Bento.StyleIndex(sel.Style);
        uint auto = WristPanel.NaturalColor(sel.Type), c = sel.Color ?? auto;
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
        ui.Text95("Click a widget to select it, drag it to move it, drag its black corner to resize it. Click an empty cell " +
                  "to choose where the next one goes.", new RectangleF(16, 324, 350, 40), 0xFF000000, wrap: true);

        ui.Group95(new RectangleF(16, 362, 350, 68), "Start over");
        Lbl(ui, 28, 382, "&From:");
        bool before = ui.Changed;
        ui.Combo95("we95.preset", new RectangleF(108, 382, 120, RowH), WatchWidgets.PresetNames, ref _wpreset);
        ui.Changed = before;                    // choosing in the list is not a change yet
        if (ui.Button95("we95.reset", new RectangleF(236, 380, 75, 23), "&Reset"))
            StartOver(ui, WatchWidgets.Presets[Math.Clamp(_wpreset, 0, WatchWidgets.Presets.Length - 1)]);
        if (ui.Button95("we95.empty", new RectangleF(236, 405, 75, 22), "&Empty", list.Count > 0)) ClearWatch(ui, list);

        ui.Group95(new RectangleF(376, 56, 358, 218), "Widgets");
        int hit = ui.List95("we95.pal", new RectangleF(388, 76, 214, 188), WatchWidgets.Palette.Length, _wpal, (i, r, on) =>
        {
            var k = WatchWidgets.Palette[i];
            W95Icons.Draw(p, k.Icon95, r.X + 1, r.Y);
            ui.Text95(k.Name, new RectangleF(r.X + 20, r.Y, r.Width - 22, r.Height), on ? 0xFFFFFFFF : 0xFF000000);
        });
        if (hit >= 0) _wpal = hit;
        if (ui.Button95("we95.add", new RectangleF(610, 76, 112, 23), _wcell.x >= 0 ? "&Add here" : "&Add", true, true))
            AddWidget(ui, list, WatchWidgets.Palette[Math.Clamp(_wpal, 0, WatchWidgets.Palette.Length - 1)]);
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
                var sizes = k.Sizes.Select(s => $"{s.W} x {s.H}").ToArray();
                int si = Array.FindIndex(k.Sizes, s => s.W == sel.W && s.H == sel.H);
                int si2 = si;
                bool before2 = ui.Changed;
                if (ComboRow(ui, "we95.size", 388, 322, 468, 100, "&Size:", sizes, ref si2) && si2 != si)
                {
                    var s = k.Sizes[si2];
                    if (WatchWidgets.Free(list, sel, sel.X, sel.Y, s.W, s.H)) { sel.W = s.W; sel.H = s.H; _wmsg = null; }
                    else { _wmsg = "That size doesn't fit there."; ui.Changed = before2; }
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
