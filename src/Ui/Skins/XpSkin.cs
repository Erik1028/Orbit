using System;
using System.Drawing;
using Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace Orbit;

// Windows XP Luna (Blue), at the metrics of its day on the shared 95 canvases: blue title bars with rounded top corners and the
// Trebuchet MS bold title, Tahoma 8 pt, rounded buttons that light up orange under the pointer (blue rim on the default one),
// the cream dialog face #ECE9D8, the white tab pages with the orange top on the selected tab, green ticks and dots, the trackbar
// thumb with its green tip, green-block progress bars, the pale blue scroll bars and drop-down buttons, #7F9DB9 edit borders.
// Text: aliased below 17 px (Tahoma's own hinting, as XP's standard smoothing left 8 to 12 pt), grey-smoothed above it.
// The pictures (icons, glyphs) are in Xp/XpArt.cs, the screens XP lays out its own way in Xp/*.Xp.cs.
public sealed partial class XpSkin : Win95Skin
{
    public override RetroStyle Style => RetroStyle.XP;
    public override string Id => "xp";
    public override string Tag => "xp";
    public override string Name => "Windows XP";

    // ---- the palette: Luna Blue's system colours ----
    public override uint Face => 0xFFECE9D8;
    public override uint Light => 0xFFF1EFE2;
    public override uint Hilight => 0xFFFFFFFF;
    public override uint Shadow => 0xFFACA899;
    public override uint DarkShadow => 0xFF716F64;
    public override uint Window => 0xFFFFFFFF;
    public override uint WindowText => 0xFF000000;
    public override uint ButtonText => 0xFF000000;
    public override uint GrayText => 0xFFACA899;
    public override uint DimText => 0xFF4B4B4B;
    public override uint Highlight => 0xFF316AC5;
    public override uint HighlightText => 0xFFFFFFFF;
    public override uint ActiveCaption => 0xFF0054E3;
    public override uint CaptionText => 0xFFFFFFFF;
    public override uint InactiveCaption => 0xFF7A96DF;
    public override uint InactiveCaptionText => 0xFFD8E4F8;
    public override uint Desktop => DesktopBlue;
    public override uint Laser => 0xFF3D95FF;            // GradientActiveCaption: the bright end of the title bar

    // ---- the colours of the themed parts (uxtheme's Luna bitmaps, measured) ----
    public const uint DesktopBlue = 0xFF3A72C4;          // behind the watch's windows (the Bliss sky's blue)
    public const uint ButtonBorder = 0xFF003C74;         // push buttons
    public const uint EditBorder = 0xFF7F9DB9;           // edits, lists, combo boxes, the sunken wells
    public const uint CheckBorder = 0xFF1C5180;          // check boxes, option buttons
    public const uint TabBorder = 0xFF919B9C;
    public const uint GroupBorder = 0xFFD0D0BF;
    public const uint GroupTitle = 0xFF0046D5;
    public const uint Tick = 0xFF21A121;                 // the green check mark and option dot
    public const uint PageTop = 0xFFFCFCFE, PageBottom = 0xFFF4F3EE;      // a tab page: white fading to cream
    public const uint Navy = 0xFF0A246A;                 // XP's dark blue: big numbers
    public const uint Orange1 = 0xFFE68B2C, Orange2 = 0xFFFFC83C;         // the hot / selected tab's top edge

    // what is behind a window's rounded top corners on the canvas being drawn: 0 = nothing (the corner pixels are cleared
    // to transparent), else that colour (a window over the watch's desktop). Each screen's hook sets it before its first window
    // (Xp/*.Xp.cs); a caption uses it up: a window drawn after it on the same canvas (a message box, What's New over the sheet)
    // sits on a window, its corners on the dialog face.
    public static uint CornerBack;
    // the same for the custom face's widget windows (their own desktop, or nothing on the cards background)
    public static uint WidgetBack = DesktopBlue;

    public override uint MapColor(uint c) => c;
    public override uint TextColor(uint c) => c;

    // ---- type: Tahoma 8 pt; titles in Trebuchet MS bold ----
    // "tahoma" in lower case: Tahoma is one of the modern style's font choices, which Painter scales to Segoe UI's width - XP draws
    // it at its own size (DirectWrite finds the family by any case).
    public override string Font => "tahoma";
    public const string TitleFont = "trebuchet ms";
    public const float SmoothFrom = 17;                  // 13 pt and up: grey-smoothed

    public override void MapFont(ref string family, ref float size, ref FontWeight weight)
    {
        if (family == "Tahoma" || family == "Microsoft Sans Serif" || family == "MS Sans Serif") family = "tahoma";
        else if (family == "Trebuchet MS") family = TitleFont;
    }

    public override PixelFont PixelFont(string family, float size, FontWeight weight) => null;
    public override bool SmoothText => false;
    public override float DropRowH => 13;

    // ---- text ----

    public override void Text(Painter p, string s, RectangleF r, uint c, bool bold, TextAlignment al, bool enabled, bool wrap, float size)
    {
        if (string.IsNullOrEmpty(s)) return;
        var w = bold ? FontWeight.Bold : FontWeight.Normal;
        var v = wrap ? ParagraphAlignment.Near : ParagraphAlignment.Center;
        if (!enabled) { DisabledText(p, s, r, size, w, al, v); return; }
        bool smooth = size >= SmoothFrom && p.PixelMode;
        if (smooth) BeginSmooth(p);
        p.Text(s, r, TextColor(c), size, w, al, v, Font, wrap);
        if (smooth) EndSmooth(p);
    }

    // any text in Tahoma at size (the panels' own big numbers too): grey-smoothed from 17 px
    public void TextAt(Painter p, string s, RectangleF r, uint c, float size, FontWeight w, TextAlignment al = TextAlignment.Leading,
        ParagraphAlignment v = ParagraphAlignment.Center, string family = null)
    {
        if (string.IsNullOrEmpty(s)) return;
        bool smooth = size >= SmoothFrom && p.PixelMode;
        if (smooth) BeginSmooth(p);
        p.Text(s, r, c, size, w, al, v, family ?? Font);
        if (smooth) EndSmooth(p);
    }

    static IDWriteRenderingParams _smooth;
    static IDWriteFactory _smoothFor;
    IDWriteRenderingParams _saved;

    // XP's standard font smoothing for big type: grey antialiasing (the pixel canvas draws aliased text otherwise)
    void BeginSmooth(Painter p)
    {
        var dc = p.Gpu.D2D;
        if (_smooth == null || _smoothFor != p.Gpu.DWrite)
        {
            _smooth = p.Gpu.DWrite.CreateCustomRenderingParams(1.8f, 0.6f, 0f, PixelGeometry.Flat, RenderingMode.NaturalSymmetric);
            _smoothFor = p.Gpu.DWrite;
        }
        _saved = dc.TextRenderingParams;
        dc.TextAntialiasMode = Vortice.Direct2D1.TextAntialiasMode.Grayscale;
        dc.TextRenderingParams = _smooth;
    }

    void EndSmooth(Painter p)
    {
        var dc = p.Gpu.D2D;
        dc.TextAntialiasMode = Vortice.Direct2D1.TextAntialiasMode.Aliased;
        dc.TextRenderingParams = _saved;
        _saved?.Dispose();
        _saved = null;
    }

    // XP hides the access keys' underlines until Alt is pressed: a label is its text
    public override void Label(Painter p, string s, RectangleF r, bool enabled, TextAlignment al, uint c, bool bold)
    {
        var (t, _) = Ui.Amp(s);
        if (string.IsNullOrEmpty(t)) return;
        Text(p, t, r, c, bold, al, enabled, false, Ui.F95);
    }

    // greyed-out text: flat GrayText, no etching
    public override void DisabledText(Painter p, string s, RectangleF r, float size, FontWeight w, TextAlignment h, ParagraphAlignment v)
    {
        bool smooth = size >= SmoothFrom && p.PixelMode;
        if (smooth) BeginSmooth(p);
        p.Text(s, r, GrayText, size, w, h, v);
        if (smooth) EndSmooth(p);
    }

    // ---- little drawing helpers ----

    static void H(Painter p, float x, float y, float w, uint c) { if (w > 0) p.Fill(new RectangleF(x, y, w, 1), c); }
    static void V(Painter p, float x, float y, float h, uint c) { if (h > 0) p.Fill(new RectangleF(x, y, 1, h), c); }
    static void Dot(Painter p, float x, float y, uint c) => p.Fill(new RectangleF(x, y, 1, 1), c);
    static uint Mix(uint a, uint b, float t) => Theme.Mix(a, b, t);

    // a colour along stops (t 0..1)
    static uint Along(float t, float[] at, uint[] cols)
    {
        if (t <= at[0]) return cols[0];
        for (int i = 1; i < at.Length; i++)
            if (t <= at[i]) return Mix(cols[i - 1], cols[i], (t - at[i - 1]) / Math.Max(1e-4f, at[i] - at[i - 1]));
        return cols[^1];
    }

    // a 1 px outline with the corner pixels left out (radius 1) or rounded off (radius 2: the next ones blended)
    static void Outline(Painter p, RectangleF r, uint c, int radius = 1, uint inner = 0)
    {
        float x = r.X, y = r.Y, w = r.Width, h = r.Height;
        H(p, x + radius, y, w - 2 * radius, c);
        H(p, x + radius, y + h - 1, w - 2 * radius, c);
        V(p, x, y + radius, h - 2 * radius, c);
        V(p, x + w - 1, y + radius, h - 2 * radius, c);
        if (radius >= 2)
        {
            // the round: one pixel in, both ways
            Dot(p, x + 1, y + 1, c); Dot(p, x + w - 2, y + 1, c); Dot(p, x + 1, y + h - 2, c); Dot(p, x + w - 2, y + h - 2, c);
        }
        else if (inner != 0)
        {
            // radius 1 with the corner pixels a blend (a softened corner)
            uint b = Mix(c, inner, 0.55f);
            Dot(p, x, y, b); Dot(p, x + w - 1, y, b); Dot(p, x, y + h - 1, b); Dot(p, x + w - 1, y + h - 1, b);
        }
    }

    // a top-to-bottom fill through stops, as whole-pixel rows
    static void Rows(Painter p, RectangleF r, float[] at, uint[] cols)
    {
        int n = (int)MathF.Round(r.Height);
        for (int i = 0; i < n; i++)
            p.Fill(new RectangleF(r.X, r.Y + i, r.Width, 1), Along(n == 1 ? 0 : i / (float)(n - 1), at, cols));
    }

    // clears (or paints CornerBack into) the pixels outside a window's rounded top corners
    static readonly int[] CornerCut = { 4, 2, 1, 1 };

    static void CutCorners(Painter p, float left, float right, float top, uint back)
    {
        if (back != 0)
        {
            for (int i = 0; i < CornerCut.Length; i++)
            {
                H(p, left, top + i, CornerCut[i], back);
                H(p, right - CornerCut[i], top + i, CornerCut[i], back);
            }
            return;
        }
        var dc = p.Gpu.D2D;
        dc.PrimitiveBlend = PrimitiveBlend.Copy;
        for (int i = 0; i < CornerCut.Length; i++)
        {
            p.Fill(new RectangleF(left, top + i, CornerCut[i], 1), 0x00000000);
            p.Fill(new RectangleF(right - CornerCut[i], top + i, CornerCut[i], 1), 0x00000000);
        }
        dc.PrimitiveBlend = PrimitiveBlend.SourceOver;
    }

    // ---- frames ----

    // XP: Window = the blue Luna frame (three rings), Page = a tab page (white to cream, the grey-blue edge), Sunken = the 1 px
    // #7F9DB9 edge of a themed list / edit / well, Raised and Pressed = thin one-pixel edges
    public override void Bevel(Painter p, RectangleF r, Painter.Edge e)
    {
        switch (e)
        {
            case Painter.Edge.Window:
                Frame(p, r, 0xFF0831D9, 0xFF00138C, 1);
                Frame(p, RectangleF.Inflate(r, -1, -1), 0xFF166AEE, 0xFF001EA0, 1);
                Frame(p, RectangleF.Inflate(r, -2, -2), 0xFF0855DD, 0xFF003BDA, 1);
                return;
            case Painter.Edge.Page:
                {
                    var inner = RectangleF.Inflate(r, -1, -1);
                    RowsGradient(p, inner, PageTop, PageBottom);
                    Outline(p, r, TabBorder, 0);
                    H(p, r.X + 1, r.Bottom, r.Width, 0xFFD0CEBF);           // its soft shadow on the dialog
                    V(p, r.Right, r.Y + 1, r.Height, 0xFFD0CEBF);
                    return;
                }
            case Painter.Edge.Sunken:
                Outline(p, r, EditBorder, 0);
                return;
            case Painter.Edge.Raised:
                Frame(p, r, Hilight, Shadow, 1);
                return;
            case Painter.Edge.Pressed:
                Frame(p, r, Shadow, Hilight, 1);
                return;
            default:
                base.Bevel(p, r, e);
                return;
        }
    }

    public override void FillDesktop(Painter p, RectangleF r) => p.Fill(r, DesktopBlue);

    // ---- the title bar ----

    // the Luna caption, top to bottom over 21 rows (the frame's top 3 and the 18 of the bar): a bright line under the edge,
    // darkening to the caption blue, a lighter band low down, a dark bottom line
    static readonly uint[] CapRows =
    {
        0xFF0A58E0, 0xFF3D95FF, 0xFF2B88FF, 0xFF1878FA, 0xFF0A68F2, 0xFF045DEA, 0xFF0157E5, 0xFF0054E3, 0xFF0054E3, 0xFF0055E4,
        0xFF0056E6, 0xFF0058E9, 0xFF005AEC, 0xFF005DEF, 0xFF0060F2, 0xFF0063F5, 0xFF0065F7, 0xFF0064F6, 0xFF005EF0, 0xFF0050DF,
        0xFF003DC8,
    };
    static uint[] _capInactive;
    static uint[] CapInactive
    {
        get
        {
            if (_capInactive != null) return _capInactive;
            var a = new uint[CapRows.Length];
            for (int i = 0; i < a.Length; i++) a[i] = Mix(CapRows[i], 0xFFB4C9F2, 0.52f);
            return _capInactive = a;
        }
    }

    // the bar over the window's top: x0..x1, from the window's top edge y, rows tall (21 on a window, less on a widget)
    static void CaptionBar(Painter p, float x0, float x1, float y, int rows, bool active, bool round, uint back = 0)
    {
        var cols = active ? CapRows : CapInactive;
        int n = cols.Length;
        for (int i = 0; i < rows; i++)
        {
            // short bars squeeze the middle rows (the edges stay)
            int k = rows >= n ? Math.Min(i, n - 1) : i < 4 ? i : i >= rows - 3 ? n - (rows - i) : 4 + (int)((i - 4) * (n - 7f) / Math.Max(1, rows - 7));
            H(p, x0, y + i, x1 - x0, cols[k]);
        }
        // the frame's sides go on down the bar: dark outside, light inside
        uint edgeL = active ? 0xFF0831D9 : 0xFF7E97D8, edgeR = active ? 0xFF001EA0 : 0xFF6F86C8;
        V(p, x0, y + 3, rows - 3, edgeL);
        V(p, x1 - 1, y + 3, rows - 3, edgeR);
        if (!round) return;
        CutCorners(p, x0, x1, y, back);
        // the rounded rim: a darker blue along the curve
        uint rim = active ? 0xFF1E5BD8 : 0xFF8FA6DE;
        Dot(p, x0 + 4, y, rim); Dot(p, x0 + 2, y + 1, rim); Dot(p, x0 + 3, y + 1, rim); Dot(p, x0 + 1, y + 2, rim); Dot(p, x0 + 1, y + 3, rim);
        Dot(p, x0, y + 4, rim);
        Dot(p, x1 - 5, y, rim); Dot(p, x1 - 3, y + 1, rim); Dot(p, x1 - 4, y + 1, rim); Dot(p, x1 - 2, y + 2, rim); Dot(p, x1 - 2, y + 3, rim);
        Dot(p, x1 - 1, y + 4, rim);
    }

    // the title: Trebuchet MS bold 10 pt, white with its dark shadow (Tahoma bold when it would not fit)
    void Title(Painter p, string title, RectangleF r, bool active, float room)
    {
        if (string.IsNullOrEmpty(title)) return;
        string fam = TitleFont;
        float size = 13;
        if (p.MeasureWidth(title, size, FontWeight.Bold, fam) > room) { fam = Font; size = Ui.F95; }
        title = FitIn(p, title, room, size, fam);
        var tr = new RectangleF(r.X, r.Y, room + 4, r.Height);
        if (active) p.Text(title, new RectangleF(tr.X + 1, tr.Y + 1, tr.Width, tr.Height), 0xFF0A1883, size, FontWeight.Bold,
            TextAlignment.Leading, ParagraphAlignment.Center, fam);
        p.Text(title, tr, active ? CaptionText : InactiveCaptionText, size, FontWeight.Bold, TextAlignment.Leading, ParagraphAlignment.Center, fam);
    }

    static string FitIn(Painter p, string s, float w, float size, string fam)
    {
        if (p.MeasureWidth(s, size, FontWeight.Bold, fam) <= w) return s;
        int n = s.Length;
        while (n > 1 && p.MeasureWidth(s[..n] + "...", size, FontWeight.Bold, fam) > w) n--;
        if (n > 1 && char.IsHighSurrogate(s[n - 1])) n--;
        return s[..n].TrimEnd() + "...";
    }

    public override void Caption(Painter p, RectangleF r, string title, W95Icons.Id? icon, bool active)
    {
        float x0 = r.X - 3, x1 = r.Right + 3, y = r.Y - 3;
        CaptionBar(p, x0, x1, y, 21, active, true, CornerBack);
        CornerBack = Face;
        float tx = r.X + 3;
        if (icon is { } ic) { W95Icons.Draw(p, ic, r.X + 2, r.Y + 1); tx = r.X + 22; }
        Title(p, title, new RectangleF(tx, r.Y, r.Right - tx, r.Height), active, r.Right - tx - 40);
    }

    // the caption boxes: 16 x 16 rounded squares with a white rim, blue (red for close), white glyphs
    public override void CaptionButton(Painter p, RectangleF r, CaptionKind kind, bool enabled, bool down, bool hover)
    {
        var b = new RectangleF(r.X, r.Y - 1, 16, 16);
        bool close = kind == CaptionKind.Close;
        // the hot box lights up quickly (in a few steps: a few gradient brushes, not one per frame)
        float h = XpAeroFx.Step(XpAeroFx.Fade(XpAeroFx.Caption, r, hover && enabled && !down, HotIn, HotOut), 6);
        uint a, z;
        if (close) (a, z) = down ? (0xFFB5391C, 0xFFE07152) : (Mix(0xFFF09C82, 0xFFF8B29C, h), Mix(0xFFD5492A, 0xFFE55B37, h));
        else (a, z) = down ? (0xFF1C47C6, 0xFF4C86F2) : (Mix(0xFF5F9CFF, 0xFF8CBBFF, h), Mix(0xFF1F5EE0, 0xFF3373F2, h));
        if (!enabled) { a = Mix(a, 0xFFC8D4F0, 0.6f); z = Mix(z, 0xFFC8D4F0, 0.6f); }
        p.GradientRaw(RectangleF.Inflate(b, -1, -1), a, z, 0f, 60f);
        Outline(p, b, enabled ? 0xFFFFFFFF : 0xFFE6ECF8, 1);
        // the inner corner pixels darker: the rim's round
        uint glyph = enabled ? 0xFFFFFFFF : 0xFFE0E6F4;
        switch (kind)
        {
            case CaptionKind.Close: p.Pixels(XpCloseGlyph, b.X + 4, b.Y + 4, glyph); break;
            case CaptionKind.Min: p.Pixels(XpMinGlyph, b.X + 4, b.Y + 10, glyph); break;
            case CaptionKind.Max: p.Pixels(XpMaxGlyph, b.X + 3, b.Y + 3, glyph); break;
            default: p.Pixels(XpHelpGlyph, b.X + 5, b.Y + 3, glyph); break;
        }
    }

    // a watch widget's little window: a blue tool-window bar (Tahoma bold), the cream body inside a two-pixel blue frame
    public const float WidgetBar = 14;

    public override RectangleF WidgetFrame(Painter p, RectangleF r, string title)
    {
        p.Fill(r, Face);
        // the frame: outer dark blue, inner bright
        Frame(p, r, 0xFF0831D9, 0xFF001C9C, 1);
        Frame(p, RectangleF.Inflate(r, -1, -1), 0xFF2A6CEB, 0xFF0A45C8, 1);
        if (title == null)
        {
            // no room for a bar (the widget needs its row): the frame, its top caption-bright
            H(p, r.X + 1, r.Y + 1, r.Width - 2, 0xFF3D95FF);
            return RectangleF.Inflate(r, -3, -3);
        }
        int rows = (int)WidgetBar + 1;
        CaptionBar(p, r.X, r.Right, r.Y, rows, true, true, WidgetBack);
        var cap = new RectangleF(r.X + 3, r.Y + 1, r.Width - 6, WidgetBar);
        // a narrow window drops a short name's full stop before it cuts the name ("Reproj." -> "Reproj")
        if (title.EndsWith('.') && TextWidth(p, title, true, Ui.F95) > cap.Width - 1) title = title[..^1];
        string t = Fit(p, title, cap.Width - 1, Ui.F95, true);
        p.Text(t, new RectangleF(cap.X + 1, cap.Y + 1, cap.Width, cap.Height), 0xFF0A1883, Ui.F95, FontWeight.Bold, TextAlignment.Leading,
            ParagraphAlignment.Center, Font);
        p.Text(t, cap, CaptionText, Ui.F95, FontWeight.Bold, TextAlignment.Leading, ParagraphAlignment.Center, Font);
        return new RectangleF(r.X + 3, r.Y + rows + 1, r.Width - 6, r.Height - rows - 4);
    }

    // ---- push buttons ----

    static readonly float[] FaceAt = { 0, 0.14f, 0.86f, 1 };
    static readonly uint[] FaceCols = { 0xFFFFFFFF, 0xFFF7F7F4, 0xFFECEBE6, 0xFFD6D0C5 };
    static readonly float[] DownAt = { 0, 0.08f, 0.94f, 1 };
    static readonly uint[] DownCols = { 0xFFCDCAC3, 0xFFE3E3DB, 0xFFE5E5DE, 0xFFF2F2F1 };
    static readonly uint[] LatchCols = { 0xFFF4F8FF, 0xFFE3ECFB, 0xFFC9DAF6, 0xFFAFC6EE };
    static readonly uint[] ArmedCols = { 0xFFFFF4DA, 0xFFFFE6A8, 0xFFFDCB6A, 0xFFF0AC3A };

    // XP's push button: a 1 px dark blue rounded border, a white-to-cream face, the orange rim under the pointer, the blue one on
    // the default button; pressed: the face goes darker at the top. latched: the toggled-in light blue. Returns the face.
    public override RectangleF ButtonFace(Painter p, RectangleF r, bool isDefault, bool down, bool latched, bool hover) =>
        XpButton(p, r, isDefault, down, latched, hover, true, ButtonBorder, false);

    // the hot state's fade (XP lit it at once; a tenth of a second here, so the rim does not flick on and off under a laser)
    public const float HotIn = 0.10f, HotOut = 0.18f;

    // fx: the hot rim fades (not on the keyboard's keys: that panel draws only when a key changes)
    RectangleF XpButton(Painter p, RectangleF r, bool isDefault, bool down, bool latched, bool hover, bool enabled, uint border, bool armed,
        bool fx = true)
    {
        var inner = RectangleF.Inflate(r, -1, -1);
        float h = !fx ? (hover ? 1 : 0) : XpAeroFx.Fade(XpAeroFx.Hot, r, hover && enabled && !down, HotIn, HotOut);
        if (!enabled)
        {
            p.Fill(inner, 0xFFF5F4EA);
            Outline(p, r, 0xFFC9C7BA, 1, Face);
            return inner;
        }
        if (down) Rows(p, inner, DownAt, DownCols);
        else if (armed) Rows(p, inner, FaceAt, ArmedCols);
        else if (latched) Rows(p, inner, FaceAt, LatchCols);
        else Rows(p, inner, FaceAt, FaceCols);
        Outline(p, r, border, 1, Face);
        if (down) return inner;
        if (h >= 1) HotRim(p, inner, 1);
        else
        {
            // on its way: the default's blue rim (if it is one), the orange over it
            if (isDefault) DefaultRim(p, inner);
            if (h > 0) HotRim(p, inner, h);
        }
        return inner;
    }

    // the hot rim: two pixels of orange inside the border, light on top, deep at the bottom; a: how far it has faded in
    static void HotRim(Painter p, RectangleF inner, float a)
    {
        uint A(uint c) => a >= 1 ? c : Theme.WithAlpha(c, a);
        if (a >= 1)
        {
            H(p, inner.X, inner.Y, inner.Width, 0xFFFFF0CF);
            H(p, inner.X, inner.Y + 1, inner.Width, 0xFFFDD889);
            V(p, inner.X, inner.Y + 1, inner.Height - 2, 0xFFFDD889);
            V(p, inner.X + 1, inner.Y + 2, inner.Height - 4, 0xFFFBC761);
            V(p, inner.Right - 1, inner.Y + 1, inner.Height - 2, 0xFFFBC761);
            V(p, inner.Right - 2, inner.Y + 2, inner.Height - 4, 0xFFFBC761);
            H(p, inner.X, inner.Bottom - 2, inner.Width, 0xFFF8B330);
            H(p, inner.X, inner.Bottom - 1, inner.Width, 0xFFE5A01A);
            return;
        }
        // translucent: every pixel once (no darker crossings)
        H(p, inner.X, inner.Y, inner.Width, A(0xFFFFF0CF));
        H(p, inner.X, inner.Y + 1, inner.Width, A(0xFFFDD889));
        V(p, inner.X, inner.Y + 2, inner.Height - 4, A(0xFFFDD889));
        V(p, inner.X + 1, inner.Y + 2, inner.Height - 4, A(0xFFFBC761));
        V(p, inner.Right - 1, inner.Y + 2, inner.Height - 4, A(0xFFFBC761));
        V(p, inner.Right - 2, inner.Y + 2, inner.Height - 4, A(0xFFFBC761));
        H(p, inner.X, inner.Bottom - 2, inner.Width, A(0xFFF8B330));
        H(p, inner.X, inner.Bottom - 1, inner.Width, A(0xFFE5A01A));
    }

    // the default button's blue rim
    static void DefaultRim(Painter p, RectangleF inner)
    {
        H(p, inner.X, inner.Y, inner.Width, 0xFFCEE7FF);
        H(p, inner.X, inner.Y + 1, inner.Width, 0xFFBCD4F6);
        V(p, inner.X, inner.Y + 1, inner.Height - 2, 0xFF98B8EA);
        V(p, inner.X + 1, inner.Y + 2, inner.Height - 4, 0xFFBCD4F6);
        V(p, inner.Right - 1, inner.Y + 1, inner.Height - 2, 0xFFBCD4F6);
        V(p, inner.Right - 2, inner.Y + 2, inner.Height - 4, 0xFFBCD4F6);
        H(p, inner.X, inner.Bottom - 2, inner.Width, 0xFF98B8EA);
        H(p, inner.X, inner.Bottom - 1, inner.Width, 0xFF89ADE4);
    }

    public override void PushButton(Painter p, RectangleF r, string label, W95Icons.Id? icon, bool enabled, bool isDefault, bool down,
        bool latched, bool hover)
    {
        var b = XpButton(p, r, isDefault, down, latched, hover && enabled, enabled, ButtonBorder, false);
        uint tc = enabled ? ButtonText : GrayText;
        if (icon is { } ic)
        {
            int n = W95Icons.Size(ic);
            string text = string.IsNullOrEmpty(label) ? null : Ui.Amp(label).text;
            float tw = text == null ? 0 : TextWidth(p, text, false, Ui.F95);
            // a tall button stacks its picture over its label (XP's big toolbar buttons), a low one puts it beside
            if (text != null && b.Height >= n + 14 && tw <= b.Width - 2)
            {
                float y = MathF.Floor(b.Y + (b.Height - n - 13) / 2);
                DrawIcon(p, ic, MathF.Floor(b.X + (b.Width - n) / 2), y, enabled);
                Text(p, text, new RectangleF(b.X, y + n, b.Width, 13), tc, false, TextAlignment.Center, true, false, Ui.F95);
                return;
            }
            float tw2 = text == null ? 0 : tw + 4;
            float x = MathF.Round(b.X + (b.Width - n - tw2) / 2), iy = MathF.Round(b.Y + (b.Height - n) / 2);
            DrawIcon(p, ic, x, iy, enabled);
            if (text != null) Text(p, text, new RectangleF(x + n + 4, b.Y, tw + 2, b.Height), tc, false, TextAlignment.Leading, true, false, Ui.F95);
        }
        else
            Label(p, label, b, true, TextAlignment.Center, tc, false);
    }

    static void DrawIcon(Painter p, W95Icons.Id ic, float x, float y, bool enabled)
    {
        if (enabled) W95Icons.Draw(p, ic, x, y);
        else
        {
            p.PushFade(0.4f);
            W95Icons.Draw(p, ic, x, y);
            p.PopFade();
        }
    }

    public override void GlyphButton(Painter p, RectangleF r, string[] glyph, bool enabled, bool down, bool hover)
    {
        var b = XpButton(p, r, false, down, false, hover && enabled, enabled, ButtonBorder, false);
        float gw = glyph[0].Length, gh = glyph.Length;
        float gx = b.X + MathF.Floor((b.Width - gw) / 2), gy = b.Y + MathF.Floor((b.Height - gh) / 2);
        p.Pixels(glyph, gx, gy, enabled ? 0xFF1F2F5A : GrayText);
    }

    // a key: an XP button with the grey-blue edge of the On-Screen Keyboard's keys; armed modifier orange, locked light blue
    public override RectangleF KeyCap(Painter p, RectangleF r, bool oneShot, bool down, bool latched, bool hover) =>
        XpButton(p, r, false, down, latched && !oneShot, hover, true, 0xFF7A8EA8, oneShot, false);

    // ---- check box, option button ----

    public override void CheckBox(Painter p, float x, float y, string label, bool value, bool enabled, bool held, bool hover)
    {
        var box = new RectangleF(x, y, 13, 13);
        var inner = RectangleF.Inflate(box, -1, -1);
        if (!enabled) p.Fill(inner, 0xFFFFFFFF);
        else if (held) p.GradientRaw(inner, 0xFFB0B0A7, 0xFFE3E1D2, 0f, 45f);
        else p.GradientRaw(inner, 0xFFDCDCD7, 0xFFFFFFFF, 0f, 45f);
        float hot = XpAeroFx.Fade(XpAeroFx.Check, box, enabled && hover && !held, HotIn, HotOut);
        if (hot > 0)
        {
            // the hot box: an orange-yellow ring inside the border
            uint A(uint c) => hot >= 1 ? c : Theme.WithAlpha(c, hot);
            Frame(p, inner, A(0xFFFEDF9C), A(0xFFF8B636), 1);
            Frame(p, RectangleF.Inflate(inner, -1, -1), A(0xFFFFE9B7), A(0xFFFBC968), 1);
        }
        Frame(p, box, enabled ? CheckBorder : 0xFFCAC8BB, enabled ? CheckBorder : 0xFFCAC8BB, 1);
        if (value) p.Pixels(CheckGlyph, x + 3, y + 3, enabled ? Tick : 0xFFCAC8BB);
        Label(p, label, new RectangleF(x + 19, y - 1, 400, 15), enabled, TextAlignment.Leading, WindowText, false);
    }

    public override void Radio(Painter p, float x, float y, string label, bool on, bool enabled, bool held, bool hover)
    {
        uint border = enabled ? CheckBorder : 0xFFCAC8BB;
        float hot = XpAeroFx.Fade(XpAeroFx.Check, new RectangleF(x, y, 13, 13), enabled && hover && !held, HotIn, HotOut);
        for (int row = 0; row < 13; row++)
        {
            string m = RadioMap[row];
            int first = m.IndexOf('o'), last = m.LastIndexOf('o');
            for (int col = 0; col < 13; col++)
            {
                char ch = m[col];
                if (ch == 'o') { Dot(p, x + col, y + row, border); continue; }
                if (ch == 'a') { Dot(p, x + col, y + row, Mix(border, Face, 0.55f)); continue; }
                if (col <= first || col >= last) continue;
                // the inside: the box's diagonal gradient (the hot ring orange)
                float t = (row + col) / 24f;
                uint c = !enabled ? 0xFFFFFFFF : held ? Mix(0xFFB0B0A7, 0xFFE3E1D2, t) : Mix(0xFFDCDCD7, 0xFFFFFFFF, t);
                if (hot > 0 && RadioRing[row][col] == 'r') c = hot >= 1 ? Mix(0xFFFEDF9C, 0xFFF8B636, t) : Mix(c, Mix(0xFFFEDF9C, 0xFFF8B636, t), hot);
                Dot(p, x + col, y + row, c);
            }
        }
        if (on)
        {
            var dot = enabled ? RadioDot : RadioDotOff;
            for (int row = 0; row < 5; row++)
                for (int col = 0; col < 5; col++)
                {
                    char ch = RadioDotMap[row][col];
                    if (ch == '.') continue;
                    Dot(p, x + 4 + col, y + 4 + row, ch == 'G' ? dot.light : ch == 'd' ? dot.dark : dot.mid);
                }
        }
        Label(p, label, new RectangleF(x + 18, y - 2, 400, 15), enabled, TextAlignment.Leading, WindowText, false);
    }

    static readonly (uint light, uint mid, uint dark) RadioDot = (0xFF7BE07B, 0xFF21A121, 0xFF167A16), RadioDotOff = (0xFFE0DED3, 0xFFCAC8BB, 0xFFB6B4A6);

    public override void GroupBox(Painter p, RectangleF r, string title)
    {
        float y = r.Y + 6;
        var box = new RectangleF(r.X, y, r.Width - 1, r.Bottom - y - 1);
        float tw = string.IsNullOrEmpty(title) ? 0 : LabelWidth(p, title);
        // the rounded border, with a gap in the top edge for the title
        if (tw > 0)
        {
            H(p, box.X + 2, box.Y, 4, GroupBorder);
            H(p, box.X + 8 + tw + 3, box.Y, box.Width - (8 + tw + 3) - 2, GroupBorder);
        }
        else H(p, box.X + 2, box.Y, box.Width - 4, GroupBorder);
        H(p, box.X + 2, box.Bottom - 1, box.Width - 4, GroupBorder);
        V(p, box.X, box.Y + 2, box.Height - 4, GroupBorder);
        V(p, box.Right - 1, box.Y + 2, box.Height - 4, GroupBorder);
        Dot(p, box.X + 1, box.Y + 1, GroupBorder); Dot(p, box.Right - 2, box.Y + 1, GroupBorder);
        Dot(p, box.X + 1, box.Bottom - 2, GroupBorder); Dot(p, box.Right - 2, box.Bottom - 2, GroupBorder);
        if (tw > 0) Label(p, title, new RectangleF(r.X + 8, r.Y, tw + 2, 13), true, TextAlignment.Leading, GroupTitle, false);
    }

    // ---- trackbar: a thin track and the thumb with its green tip (orange under the pointer) ----

    public override void Trackbar(Painter p, RectangleF r, float t, bool enabled, int ticks, bool hover, bool held)
    {
        var ch = new RectangleF(r.X + 6, r.Y + 8, r.Width - 12, 4);
        p.Fill(RectangleF.Inflate(ch, -1, -1), 0xFFF5F4EF);
        H(p, ch.X + 1, ch.Y, ch.Width - 2, 0xFF9D9C99);
        V(p, ch.X, ch.Y + 1, ch.Height - 2, 0xFF9D9C99);
        H(p, ch.X + 1, ch.Bottom - 1, ch.Width - 2, 0xFFDEDBCF);
        V(p, ch.Right - 1, ch.Y + 1, ch.Height - 2, 0xFFDEDBCF);
        for (int i = 0; i < ticks && ticks > 1; i++)
        {
            float tx = MathF.Round(ch.X + (ch.Width - 1) * i / (ticks - 1));
            V(p, tx, r.Y + 24, i == 0 || i == ticks - 1 ? 4 : 3, 0xFFA1A192);
        }
        float x = MathF.Round(ch.X + ch.Width * t) - 5, y = r.Y;
        var map = ticks > 1 ? ThumbDown : ThumbBoth;
        float hot = XpAeroFx.Fade(XpAeroFx.Thumb, r, enabled && (hover || held), HotIn, HotOut);
        for (int row = 0; row < map.Length; row++)
            for (int col = 0; col < map[row].Length; col++)
            {
                char c = map[row][col];
                if (c == '.') continue;
                uint col32 = c switch
                {
                    'o' => enabled ? 0xFF7C8FA6 : 0xFFC9C7BA,
                    'w' => 0xFFFFFFFF,
                    'b' => enabled ? Mix(0xFFFDFDFB, 0xFFE4E3DB, row / (float)map.Length) : 0xFFF4F3EC,
                    's' => enabled ? 0xFFD1CEC3 : 0xFFE6E4DA,
                    'g' => !enabled ? 0xFFDCDACF : hot >= 1 ? 0xFFF8B636 : hot <= 0 ? Tick : Mix(Tick, 0xFFF8B636, hot),
                    'G' => !enabled ? 0xFFE8E6DD : hot >= 1 ? 0xFFFFDC8C : hot <= 0 ? 0xFF7BD77B : Mix(0xFF7BD77B, 0xFFFFDC8C, hot),
                    _ => 0xFF000000,
                };
                Dot(p, x + col, y + row, col32);
            }
    }

    // ---- progress: green blocks in a white rounded trough ----

    static readonly float[] BlockAt = { 0, 0.14f, 0.28f, 0.42f, 0.57f, 0.71f, 0.85f, 1 };
    static readonly uint[] GreenBlock = { 0xFFACEDAD, 0xFF7BE47D, 0xFF4CDA50, 0xFF2ED330, 0xFF42D845, 0xFF76E275, 0xFF8FE791, 0xFFE6FAE6 };
    static readonly uint[] RedBlock = { 0xFFF7B5AE, 0xFFF0877C, 0xFFE95A4F, 0xFFE03C33, 0xFFE4524A, 0xFFEC7C74, 0xFFF0968F, 0xFFFCE8E6 };

    public override void Progress(Painter p, RectangleF r, float frac, uint block)
    {
        p.Fill(RectangleF.Inflate(r, -1, -1), 0xFFFFFFFF);
        Outline(p, r, 0xFF8E8F8F, 1, Face);
        var inner = new RectangleF(r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4);
        if (inner.Height < 1 || inner.Width < 2) return;
        float bw = Math.Clamp(MathF.Round(inner.Height * 0.8f), 3, 8);
        int n = (int)((inner.Width + 2) / (bw + 2));
        int on = (int)MathF.Round(Math.Clamp(frac, 0, 1) * n);
        if (on == 0 && frac >= 0.01f) on = 1;
        var cols = block == 0xFF800000 ? RedBlock : GreenBlock;
        int h = (int)inner.Height;
        for (int row = 0; row < h; row++)
        {
            uint c = Along(h == 1 ? 0.4f : row / (float)(h - 1), BlockAt, cols);
            for (int i = 0; i < on; i++) p.Fill(new RectangleF(inner.X + i * (bw + 2), inner.Y + row, bw, 1), c);
        }
    }

    // ---- property sheet tabs: white-to-cream, the orange top on the selected (and the hot) one ----

    public override void Tab(Painter p, RectangleF r, string label, bool selected, bool hover)
    {
        var face = new RectangleF(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 1);
        if (selected) p.Fill(face, PageTop);
        else Rows(p, face, new[] { 0f, 0.26f, 0.95f, 1f }, new[] { 0xFFFFFFFF, 0xFFFAFAF9, 0xFFF0F0EA, 0xFFECEBE5 });
        // the edges: rounded top corners
        H(p, r.X + 2, r.Y, r.Width - 4, TabBorder);
        Dot(p, r.X + 1, r.Y + 1, TabBorder);
        Dot(p, r.Right - 2, r.Y + 1, TabBorder);
        float sh = r.Height - 2 - (selected ? 1 : 0);
        V(p, r.X, r.Y + 2, sh, TabBorder);
        V(p, r.Right - 1, r.Y + 2, sh, TabBorder);
        // a tab under the pointer gets the orange top too, fading in (the selected one comes 2 px bigger: its own key, so a tab
        // just picked does not fade)
        float o = selected ? 1 : XpAeroFx.Fade(XpAeroFx.Tab, r, hover, HotIn, HotOut);
        if (o > 0 && o < 1)
        {
            uint A(uint c) => Theme.WithAlpha(c, o);
            H(p, r.X + 2, r.Y, r.Width - 4, A(Orange1));
            H(p, r.X + 1, r.Y + 1, r.Width - 2, A(Orange2));
            H(p, r.X + 1, r.Y + 2, r.Width - 2, A(Orange2));
        }
        else if (o >= 1)
        {
            // the orange top: one deep line, two light under it
            H(p, r.X + 2, r.Y, r.Width - 4, Orange1);
            Dot(p, r.X + 1, r.Y + 1, Orange1); Dot(p, r.Right - 2, r.Y + 1, Orange1);
            H(p, r.X + 1, r.Y + 1, r.Width - 2, Orange2);
            H(p, r.X + 1, r.Y + 2, r.Width - 2, Orange2);
            Dot(p, r.X + 1, r.Y + 1, Mix(Orange1, Orange2, 0.5f)); Dot(p, r.Right - 2, r.Y + 1, Mix(Orange1, Orange2, 0.5f));
        }
        Label(p, label, new RectangleF(r.X, r.Y + (selected ? 2 : 2), r.Width, 14), true, TextAlignment.Center, WindowText, selected && false);
    }

    // ---- lists, scroll bars, edits, combo boxes ----

    public override void ListBox(Painter p, RectangleF r)
    {
        p.Fill(r, Window);
        Outline(p, r, EditBorder, 0);
    }

    public override void ListSelection(Painter p, RectangleF row) => p.Fill(row, Highlight);

    // the pale blue Luna button of a scroll bar / drop-down (hot: lighter, pressed: deeper)
    static void BlueButton(Painter p, RectangleF r, bool hot, bool down, bool enabled = true) => BlueButton(p, r, hot ? 1f : 0f, down, enabled);

    // hot 0..1: on its way to lighter (in steps: a few gradient brushes, not one per frame)
    static void BlueButton(Painter p, RectangleF r, float hot, bool down, bool enabled)
    {
        var inner = RectangleF.Inflate(r, -1, -1);
        uint a, z, edge;
        hot = XpAeroFx.Step(hot, 6);
        if (!enabled) (a, z, edge) = (0xFFF4F3EE, 0xFFE8E6DC, 0xFFE0DED3);
        else if (down) (a, z, edge) = (0xFF8FAAF0, 0xFFB7CAF6, 0xFF8BA4E4);
        else if (hot >= 1) (a, z, edge) = (0xFFF2F6FF, 0xFFC5D7FC, 0xFFB4C7F5);
        else if (hot <= 0) (a, z, edge) = (0xFFE3EBFE, 0xFFB9CCF7, 0xFFB0C3F2);
        else (a, z, edge) = (Mix(0xFFE3EBFE, 0xFFF2F6FF, hot), Mix(0xFFB9CCF7, 0xFFC5D7FC, hot), Mix(0xFFB0C3F2, 0xFFB4C7F5, hot));
        p.GradientRaw(inner, a, z, 0f, 45f);
        Outline(p, r, edge, 1, 0xFFF4F3EE);
        if (enabled && !down)
        {
            H(p, inner.X, inner.Y, inner.Width - 1, 0xFFFFFFFF);
            V(p, inner.X, inner.Y, inner.Height - 1, 0xFFFFFFFF);
        }
    }

    public override void ScrollBar(Painter p, RectangleF bar, RectangleF up, RectangleF down, RectangleF thumb, bool upHeld, bool downHeld)
    {
        p.Fill(bar, 0xFFF7F6F1);
        V(p, bar.X, bar.Y, bar.Height, 0xFFEEEDE5);
        V(p, bar.Right - 1, bar.Y, bar.Height, 0xFFEEEDE5);
        BlueButton(p, up, false, upHeld);
        BlueButton(p, down, false, downHeld);
        p.Pixels(ChevronUp, up.X + 4, up.Y + 5, upHeld ? 0xFFFFFFFF : 0xFF4D6185);
        p.Pixels(ChevronDown, down.X + 4, down.Y + 6, downHeld ? 0xFFFFFFFF : 0xFF4D6185);
        // the thumb, with its grip of four ridges
        p.GradientRaw(RectangleF.Inflate(thumb, -1, -1), 0xFFD7E3FD, 0xFFB7CBF7, 0f, 0f);
        Outline(p, thumb, 0xFFAEC2F1, 1, 0xFFF4F3EE);
        H(p, thumb.X + 1, thumb.Y + 1, thumb.Width - 3, 0xFFFFFFFF);
        V(p, thumb.X + 1, thumb.Y + 1, thumb.Height - 3, 0xFFFFFFFF);
        if (thumb.Height >= 14)
        {
            float gy = MathF.Floor(thumb.Y + (thumb.Height - 8) / 2);
            for (int i = 0; i < 4; i++)
            {
                H(p, thumb.X + 5, gy + i * 2, 6, 0xFFEEF4FE);
                H(p, thumb.X + 5, gy + i * 2 + 1, 6, 0xFF8CB0F8);
            }
        }
    }

    // read-only here: Orbit types with its own keyboard, not into its settings
    public override void EditBox(Painter p, RectangleF r, string text, bool enabled)
    {
        p.Fill(r, enabled ? Window : 0xFFF5F4EA);
        Outline(p, r, enabled ? EditBorder : 0xFFC9C7BA, 0);
        Text(p, text, new RectangleF(r.X + 4, r.Y + 2, r.Width - 6, r.Height - 4), enabled ? WindowText : GrayText, false,
            TextAlignment.Leading, true, false, Ui.F95);
    }

    public override void ComboBox(Painter p, RectangleF r, string text, bool enabled, bool down, bool open, bool hover)
    {
        p.Fill(r, enabled ? Window : 0xFFF5F4EA);
        Outline(p, r, enabled ? EditBorder : 0xFFC9C7BA, 0);
        var b = new RectangleF(r.Right - 18, r.Y + 1, 17, r.Height - 2);
        BlueButton(p, b, XpAeroFx.Fade(XpAeroFx.Hot, b, hover && enabled && !down && !open, HotIn, HotOut), down || open && enabled, enabled);
        if (open) _openCombo = r;
        float gy = MathF.Floor(b.Y + (b.Height - 5) / 2);
        p.Pixels(ChevronDown, b.X + 5, gy, !enabled ? 0xFFC9C7BA : down || open ? 0xFFFFFFFF : 0xFF4D6185);
        var tr = new RectangleF(r.X + 3, r.Y + 3, b.X - r.X - 5, r.Height - 6);
        if (open) p.Fill(tr, Highlight);
        Text(p, text, new RectangleF(tr.X + 2, tr.Y, tr.Width - 2, tr.Height), open ? HighlightText : enabled ? WindowText : GrayText, false,
            TextAlignment.Leading, true, false, Ui.F95);
    }

    // the combo box whose list is open (drawn just before its list): which way the list slides out
    RectangleF _openCombo;

    // XP's "Slide open combo boxes": the list slides out of its box (down, or up when it opens above it) in 165 ms, comctl32's
    // own time for it
    public override void DropList(Painter p, RectangleF r, string[] items, int hot, float rowH)
    {
        float e = XpAeroFx.Opening(XpAeroFx.Drop, r, 0.165f);
        if (e < 1)
        {
            bool up = r.Bottom <= _openCombo.Y + 1;
            float dy = MathF.Round((1 - e) * r.Height) * (up ? 1 : -1);
            p.PushClip(r);
            DropListAt(p, new RectangleF(r.X, r.Y + dy, r.Width, r.Height), items, hot, rowH);
            p.PopClip();
            return;
        }
        DropListAt(p, r, items, hot, rowH);
    }

    void DropListAt(Painter p, RectangleF r, string[] items, int hot, float rowH)
    {
        p.Fill(r, Window);

        Outline(p, r, 0xFF000000, 0);
        for (int i = 0; i < items.Length; i++)
        {
            var row = new RectangleF(r.X + 1, r.Y + 1 + i * rowH, r.Width - 2, rowH);
            bool on = i == hot;
            if (on) p.Fill(row, Highlight);
            Text(p, items[i], new RectangleF(row.X + 2, row.Y, row.Width - 4, rowH), on ? HighlightText : WindowText, false,
                TextAlignment.Leading, true, false, Ui.F95);
        }
    }

    // XP's status bar: no sunken panes, an etched divider after each one
    public override void StatusPane(Painter p, RectangleF r, string text, uint dot)
    {
        V(p, r.Right, r.Y + 2, r.Height - 4, Shadow);
        V(p, r.Right + 1, r.Y + 2, r.Height - 4, Hilight);
        float x = r.X + 3;
        if (dot != 0)
        {
            var d = new RectangleF(x, MathF.Floor(r.Y + (r.Height - 7) / 2), 7, 7);
            p.Fill(RectangleF.Inflate(d, -1, 0), dot);
            p.Fill(RectangleF.Inflate(d, 0, -1), dot);
            x += 11;
        }
        Text(p, text, new RectangleF(x, r.Y, r.Right - x - 2, r.Height), WindowText, false, TextAlignment.Leading, true, false, Ui.F95);
    }

    // a menu bar's item: XP hides the access keys until Alt
    public override void MenuItem(Painter p, float x, float y, string text) =>
        Text(p, text, new RectangleF(x, y, TextWidth(p, text, false, Ui.F95) + 2, 15), WindowText, false, TextAlignment.Leading, true, false, Ui.F95);
}
