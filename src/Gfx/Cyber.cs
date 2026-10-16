using System;
using System.Drawing;
using System.Numerics;
using Vortice.DirectWrite;

namespace Orbit;

// Look > Style "Cyber": a sci-fi HUD, Erik's own concept (scratchpad cyber_1 / cyber_2, 10-16). A sibling of Modern, not of the
// retro styles: Theme.Classic stays false, so every panel keeps its modern layout and behaviour; Theme.Cyber swaps the token layer -
// near-black surfaces, an olive frame round the watch page, cards with cut corners and a thin dark-red outline, YELLOW TAGS as
// captions (black monospace capitals, a slanted right end), values in Bahnschrift's condensed bold, notes in a monospace grey,
// segmented bars (cyan / yellow, red when low), cyan for what is on or live. The watch's widgets draw their Cyber designs in
// WristCyber.cs; the shared shapes (Painter.Fill / Stroke with a radius) cut their corners instead of rounding them.
// Its own colours and fonts: the Look tab's accent, panel colour and Look > Font do not apply (the panel opacity does).
static class CyberHud
{
    public const string Id = "cyber", Name = "Cyber";

    // ---- the palette (sampled from the concept pictures) ----
    public const uint Page = 0xFF0B0A0C;        // the watch page, the settings' ground
    public const uint Card = 0xFF141216;        // a card
    public const uint Card2 = 0xFF1B191D;       // a raised surface: a field, a key, a tile that is off
    public const uint Card3 = 0xFF27242A;       // hover, the selected row
    public const uint Edge = 0xFF5C3033;        // a card's thin dark-red outline
    public const uint Line = 0xFF3B2D30;        // a divider, a field's outline
    public const uint Frame = 0xFF8C843C;       // the olive frame round the watch page
    public const uint Yellow = 0xFFF2E34B;      // caption tags, the time card, bars
    public const uint Cyan = 0xFF7DE8EE;        // what is on or live, accents
    public const uint CyanDim = 0xFF3D8288;     // a button's outline
    public const uint Red = 0xFFEC5E52;         // a warning (a low battery, a dropped frame)
    public const uint White = 0xFFEFECE2;       // values, titles
    public const uint Grey = 0xFF9C978E;        // notes, units, names
    public const uint Faint = 0xFF56514D;       // "nothing there"
    public const uint Track = 0xFF2B2726;       // a bar's empty segments
    public const uint Bars = 0xFFB0ADA6;        // the frame graph's bars
    public const uint Ink = 0xFF0B0A0C;         // dark text on yellow / cyan
    public const uint DangerFill = 0xFF2A1215, DangerEdge = 0xFFB0403A;   // a muted volume / microphone

    // ---- type: Bahnschrift's condensed widths for values and UI text, a monospace for tags, notes and units ----
    // The two Bahnschrift names are resolved to a real family and width (Painter.Format): DirectWrite lists the variable font's
    // widths either as families of their own or as stretches of "Bahnschrift" - whichever this PC has.
    public const string Display = "Bahnschrift Condensed";
    public const string Text = "Bahnschrift SemiCondensed";
    public static string Mono { get; private set; } = "Cascadia Mono";
    static string _display = "Bahnschrift", _text = "Bahnschrift";
    static FontStretch _displayStretch = FontStretch.Condensed, _textStretch = FontStretch.SemiCondensed;

    public static void ResolveFonts(Func<string, bool> has)
    {
        if (has(Display)) (_display, _displayStretch) = (Display, FontStretch.Normal);
        else if (!has("Bahnschrift")) (_display, _displayStretch) = ("Segoe UI", FontStretch.Normal);
        if (has(Text)) (_text, _textStretch) = (Text, FontStretch.Normal);
        else if (!has("Bahnschrift")) (_text, _textStretch) = ("Segoe UI", FontStretch.Normal);
        if (!has(Mono)) Mono = "Consolas";
    }

    // a Cyber family name -> the family DirectWrite knows and its width (false: not one of them)
    public static bool Resolve(string family, out string real, out FontStretch stretch)
    {
        if (ReferenceEquals(family, Display) || family == Display) { (real, stretch) = (_display, _displayStretch); return true; }
        if (ReferenceEquals(family, Text) || family == Text) { (real, stretch) = (_text, _textStretch); return true; }
        (real, stretch) = (family, FontStretch.Normal);
        return false;
    }

    // ---- the token layer: Theme's surfaces and Bento's default look ----
    public static void Apply(AppearanceConfig a)
    {
        Theme.Bg = Page;
        Theme.Panel = Card;
        Theme.Panel2 = Card2;
        Theme.Panel3 = Card3;
        Theme.Border = Line;
        Theme.OverlayBg = Theme.WithAlpha(Page, Math.Clamp(a.PanelOpacity, 0.3f, 1f));
        Theme.Accent = Cyan;
        Theme.OnAccent = Ink;
        Theme.Laser = a.LaserColor == 0 ? Cyan : 0xFF000000 | (a.LaserColor & 0xFFFFFF);
        Bento.Page = Page;
        Bento.Tint = Card3;
        Bento.Card = Card;
        Bento.CardTop = Card;
        Bento.DangerFill = DangerFill;
        Bento.DangerEdge = DangerEdge;
        var d = new Bento.Look
        {
            Fill0 = Card, Fill1 = Card, Angle = 90, Span = 1, Border = Edge, BorderW = 1.25f,
            Text = White, Sub = Grey, Faint = Faint, Cap = Grey, CapText = Ink, CapDot = Cyan, Edge = Edge,
            Tile = Card2, TileHover = Card3, Track = Track,
            Accent = Yellow, AccentText = Cyan, AccentFill = Yellow, OnAccent = Ink, Ok = Cyan, Halo = Card, BadgeRing = White,
            WarnInk = Red, BadInk = Red, Cpu = Yellow, Mem = Cyan, GraphBar = Bars, FlagWarn = Red, FlagBad = Red,
            Off0 = Card, Off1 = Card, OffHover = Card3, OffText = White, OffBorder = CyanDim, OffBorderW = 1.25f,
            On0 = Cyan, On1 = Cyan, OnText = Ink,
        };
        Bento.Default = d;
        Bento.Use(d);
    }

    // Every brush in the Cyber style: the modern constants a panel names directly (its text greys, the logo's indigo, the status
    // colours, the watch's role colours) drawn as the Cyber ones; anything else as it is
    public static uint MapColor(uint c)
    {
        uint a = c & 0xFF000000;
        switch (c & 0xFFFFFF)
        {
            case Theme.Text & 0xFFFFFF:
            case Bento.White & 0xFFFFFF: return a | (White & 0xFFFFFF);
            case Theme.Dim & 0xFFFFFF:
            case Bento.Grey & 0xFFFFFF: return a | (Grey & 0xFFFFFF);
            case Theme.Faint & 0xFFFFFF:
            case Bento.Dim & 0xFFFFFF: return a | (Faint & 0xFFFFFF);
            case Theme.Accent2 & 0xFFFFFF:
            case Theme.Warn & 0xFFFFFF:
            case Bento.Warn & 0xFFFFFF:
            case Bento.DataPurple & 0xFFFFFF: return a | (Yellow & 0xFFFFFF);
            case Theme.Good & 0xFFFFFF:
            case Bento.Green & 0xFFFFFF:
            case Bento.DataBlue & 0xFFFFFF: return a | (Cyan & 0xFFFFFF);
            case Theme.Bad & 0xFFFFFF:
            case Bento.Bad & 0xFFFFFF: return a | (Red & 0xFFFFFF);
        }
        return c;
    }

    // ---- the shapes every Cyber surface is made of ----

    // the cuts a rounded corner of this radius becomes: the bottom-right one about the radius, the top-left one half of it
    // (0, 0: too small to cut - a square corner)
    public static (float tl, float br) Cuts(RectangleF r, float radius)
    {
        float br = MathF.Min(radius * 0.7f, MathF.Min(r.Width, r.Height) * 0.35f);
        if (br < 1.5f) return (0, 0);
        br = MathF.Round(br * 2) / 2;
        float tl = MathF.Round(br) / 2;
        return (tl >= 1 ? tl : 0, br);
    }

    // a card: its fill, cut top-left and bottom-right, a thin outline
    public const float CardTL = 5, CardBR = 11;

    public static void CardShape(Painter p, RectangleF r, uint fill, uint edge, float width = 1.25f, float tl = CardTL, float br = CardBR)
    {
        p.FillCut(r, fill, tl, 0, br, 0);
        if (edge != 0) p.StrokeCut(RectangleF.Inflate(r, -width / 2, -width / 2), edge, width, MathF.Max(0, tl - width * 0.2f), 0, MathF.Max(0, br - width * 0.2f), 0);
    }

    // a caption tag: a yellow label, the text in black UPPERCASE monospace bold, its right end slanted. Returns its width.
    public const float TagSize = 14, TagH = 19, TagPad = 6, TagSlant = 7;

    public static float TagWidth(Painter p, string s, float size = TagSize) =>
        p.MeasureWidth(s, size, FontWeight.Bold, Mono) + 2 * TagPad + TagSlant - 2;

    public static float Tag(Painter p, float x, float y, string s, uint fill = Yellow, uint ink = Ink, float size = TagSize, float h = TagH) =>
        Tag(p, x, y, s, 1, 1, fill, ink, size, h);

    // The tag drawn in (CyberFx): its body wiped in from the left (wipe 0..1), then its letters typed one by one (type 0..1) with a
    // block cursor after the last one. 1, 1: the tag as it is.
    public static float Tag(Painter p, float x, float y, string s, float wipe, float type, uint fill = Yellow, uint ink = Ink, float size = TagSize,
        float h = TagH)
    {
        float tw = p.MeasureWidth(s, size, FontWeight.Bold, Mono), w = tw + 2 * TagPad + TagSlant - 2, sl = TagSlant * h / TagH;
        if (wipe <= 0) return w;
        bool wiping = wipe < 1;
        if (wiping) p.PushClip(new RectangleF(x - 1, y - 1, w * wipe + 1, h + 2));
        _tagPts[0] = new Vector2(x, y); _tagPts[1] = new Vector2(x + w, y); _tagPts[2] = new Vector2(x + w - sl, y + h); _tagPts[3] = new Vector2(x, y + h);
        p.Polygon(_tagPts, 4, fill);
        float by = y + (h + CapH(p, size, FontWeight.Bold)) / 2;
        if (type >= 1) MonoAt(p, s, x + TagPad, by, tw + 4, ink, size, FontWeight.Bold);
        else if (type > 0 && s.Length > 0)
        {
            // monospace: every letter as wide as the next - the first n of them, and the cursor where the next one goes
            float cw = tw / s.Length;
            int n = Math.Clamp((int)(type * s.Length), 0, s.Length);
            if (n > 0)
            {
                p.PushClip(new RectangleF(x, y, TagPad + n * cw, h));
                MonoAt(p, s, x + TagPad, by, tw + 4, ink, size, FontWeight.Bold);
                p.PopClip();
            }
            if (n < s.Length) { float ch = CapH(p, size, FontWeight.Bold); p.Fill(new RectangleF(x + TagPad + n * cw + 0.5f, by - ch, MathF.Max(2, cw - 1.5f), ch), ink); }
        }
        if (wiping) p.PopClip();
        return w;
    }

    static readonly Vector2[] _tagPts = new Vector2[4];

    // how tall a monospace capital / digit is at this size (its ink above the baseline)
    public static float CapH(Painter p, float size, FontWeight w = FontWeight.SemiBold, string family = null) =>
        p.DigitInk(w, family ?? Mono).above * size;

    // monospace text with its baseline at 'by'
    public static void MonoAt(Painter p, string s, float x, float by, float w, uint c, float size = TagSize, FontWeight wt = FontWeight.SemiBold,
        TextAlignment al = TextAlignment.Leading)
    {
        if (string.IsNullOrEmpty(s)) return;
        var (b, h) = p.TextLine(size, wt, Mono);
        p.Text(s, new RectangleF(x, by - b, w, h), c, size, wt, al, ParagraphAlignment.Near, Mono);
    }

    public static float MonoWidth(Painter p, string s, float size = TagSize, FontWeight wt = FontWeight.SemiBold) =>
        p.MeasureWidth(s, size, wt, Mono);

    // A segmented bar: blocks leaning right with gaps between, 'frac' of them lit in 'on', the rest in 'off'. n = 0: as many as
    // suit its length (each a little over twice as long as the bar is tall)
    public static void Segments(Painter p, RectangleF bar, float frac, uint on, uint off = Track, int n = 0, float gap = 0)
    {
        float h = bar.Height;
        if (gap <= 0) gap = MathF.Max(2, MathF.Round(h * 0.5f));
        float lean = MathF.Min(h * 0.45f, 4);
        if (n <= 0) n = Math.Max(3, (int)MathF.Round((bar.Width + gap) / (h * 2.6f + gap)));
        float bw = (bar.Width - lean - (n - 1) * gap) / n;
        if (bw < 1) { p.Fill(bar, off); return; }
        int lit = frac <= 0 ? 0 : Math.Clamp((int)MathF.Ceiling(Math.Clamp(frac, 0, 1) * n - 0.02f), 1, n);
        if (_quads.Length < 4 * n) _quads = new Vector2[4 * n];
        void Run(int from, int to, uint c)
        {
            int k = 0;
            for (int i = from; i < to; i++)
            {
                float x = bar.X + i * (bw + gap);
                _quads[k++] = new Vector2(x + lean, bar.Y);
                _quads[k++] = new Vector2(x + lean + bw, bar.Y);
                _quads[k++] = new Vector2(x + bw, bar.Bottom);
                _quads[k++] = new Vector2(x, bar.Bottom);
            }
            p.Quads(_quads, to - from, c);
        }
        if (lit > 0) Run(0, lit, on);
        if (lit < n && off != 0) Run(lit, n, off);
    }

    static Vector2[] _quads = new Vector2[64];

    // L-shaped brackets in two opposite corners of r (top-right and bottom-left), 'arm' long, inset by 'inset'
    public static void Brackets(Painter p, RectangleF r, uint c, float arm = 9, float inset = 5, float w = 2, bool topRight = true, bool bottomLeft = true)
    {
        if (topRight)
        {
            float x = r.Right - inset, y = r.Y + inset;
            p.Fill(new RectangleF(x - arm, y, arm, w), c);
            p.Fill(new RectangleF(x - w, y, w, arm), c);
        }
        if (bottomLeft)
        {
            float x = r.X + inset, y = r.Bottom - inset;
            p.Fill(new RectangleF(x, y - w, arm, w), c);
            p.Fill(new RectangleF(x, y - arm, w, arm), c);
        }
    }

    // hazard stripes: black bars leaning right across r (the time card's bottom edge), clipped to it; 'phase' px further right
    // (CyberFx: they crawl - 0..pitch, the same picture again at pitch)
    public static void Hazard(Painter p, RectangleF r, uint ink = Ink, float pitch = 0, float phase = 0)
    {
        if (pitch <= 0) pitch = MathF.Max(8, r.Height * 1.25f);
        float w = pitch * 0.5f, h = r.Height;
        phase -= MathF.Floor(phase / pitch) * pitch;
        int n = (int)((r.Width + h + pitch) / pitch) + 2;
        if (_quads.Length < 4 * n) _quads = new Vector2[4 * n];
        int k = 0;
        for (int i = 0; i < n; i++)
        {
            float x = r.X - h - pitch + phase + i * pitch;
            _quads[k++] = new Vector2(x + h, r.Y);
            _quads[k++] = new Vector2(x + h + w, r.Y);
            _quads[k++] = new Vector2(x + w, r.Bottom);
            _quads[k++] = new Vector2(x, r.Bottom);
        }
        p.PushClip(r);
        p.Quads(_quads, n, ink);
        p.PopClip();
    }

    // a whole panel (the watch page, the keyboard, the Room and Hexpad panels): near black, the olive frame, all four corners cut
    public static void PanelFrame(Painter p, RectangleF all, uint fill, float cut = 18)
    {
        p.FillCut(all, fill, cut, cut, cut, cut);
        p.StrokeCut(RectangleF.Inflate(all, -1.5f, -1.5f), Frame, 2, cut - 1, cut - 1, cut - 1, cut - 1);
    }

    // a console (the settings window, the keyboard, the Room and Hexpad panels, What's new): the panel frame with its four cuts in
    // yellow - the HUD's own, the watch keeps PanelFrame
    public static void ConsoleFrame(Painter p, RectangleF all, uint fill, float cut = 18)
    {
        p.FillCut(all, fill, cut, cut, cut, cut);
        var f = RectangleF.Inflate(all, -1.5f, -1.5f);
        p.StrokeCut(f, Frame, 2, cut - 1, cut - 1, cut - 1, cut - 1);
        float c = cut - 1, x0 = f.X + 1, y0 = f.Y + 1, x1 = f.Right - 1, y1 = f.Bottom - 1;
        p.Line(x0, y0 + c, x0 + c, y0, Yellow, 3);
        p.Line(x1 - c, y0, x1, y0 + c, Yellow, 3);
        p.Line(x1, y1 - c, x1 - c, y1, Yellow, 3);
        p.Line(x0 + c, y1, x0, y1 - c, Yellow, 3);
    }

    // a little window in the style picker: the page, a card with its tag, a cyan line
    public static void Chip(Painter p, RectangleF r)
    {
        p.FillCut(r, Page, 0, 0, 4, 0);
        p.StrokeCut(RectangleF.Inflate(r, -0.5f, -0.5f), Frame, 1, 0, 0, 3.5f, 0);
        p.Polygon(new[] { new Vector2(r.X + 3, r.Y + 3), new Vector2(r.X + 15, r.Y + 3), new Vector2(r.X + 13, r.Y + 8), new Vector2(r.X + 3, r.Y + 8) }, Yellow);
        p.Fill(new RectangleF(r.X + 4, r.Y + 12, r.Width - 10, 3), Cyan);
    }
}

// The Look pickers' list (the modern Style field and the 95 sheet's Style box): Modern, the retro styles, then Cyber
static class StyleMenu
{
    public static readonly string[] Ids = [.. Skins.StyleIds, CyberHud.Id];
    public static readonly string[] Names = [.. Skins.StyleNames, CyberHud.Name];

    // a config value's index (unknown: Modern)
    public static int IndexOf(string style) => Math.Max(0, Array.IndexOf(Ids, style));
}
