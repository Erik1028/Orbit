using System;
using System.Drawing;

namespace Orbit;

// The Bento look of the modern watch (the custom face): rounded cards on the watch page, one grey for captions and
// small text, colours by role. Modern only: Theme.Apply derives the surfaces in its modern branch (the Windows 95 skin
// returns before it), and only the watch's widgets and their opened views draw with these - the settings, keyboard, dock
// and the other panels keep Theme's tokens.
// A widget can have a look of its own (WatchWidget.Style / Color / Color2): Make works it out, Use switches the tokens below
// to it while that widget draws, Reset puts the default back. The default look IS what Derive makes, so an unstyled widget
// draws exactly as it did.
static class Bento
{
    // ---- shapes (watch texture px, 640 x 440) ----
    public const float PageRadius = 26;          // the card radius plus the 10 px gap: concentric corners
    public const float CardRadius = 16;          // every card, whatever its size; buttons are their card
    public const float PageEdge = 2;             // the page's outline: the watch edge still shows against a dark world
    public const float EdgeWidth = 1.25f;        // a card's hairline, in both backgrounds (cards would merge in the headset)

    // How far a card is lifted from the page: a mix of the panel colour toward a cool tint of it. The one knob for how
    // well the cards stand apart in the headset (night mode halves the difference); Tile, Track and the hover follow it.
    public static float Lift = 0.15f;

    // ---- surfaces, derived from the Look tab's panel colour (Derive) ----
    public static uint Page;                     // the panel colour itself
    public static uint Tint;                     // the mix target: the panel's own hue, lighter (never drawn)
    public static uint Card;                     // a widget's card
    public static uint CardTop;                  // the card's top light: a touch lighter at the top, Card from half-way down
    public static uint DangerFill, DangerEdge;   // a card whose state needs action (the volume, muted)

    public const uint PageLine = 0x1FFFFFFF;     // the page's outline (12 % white)

    // ---- text: one white, one grey (captions, units, notes, artist), a faint one for "nothing there"; dark on a light fill ----
    public const uint White = 0xFFF2F3F7, Grey = 0xFF9EA4B6, Dim = 0xFF5D6378, Dark = 0xFF0B1220;

    // ---- status: only when a value needs attention, and then its text too (not only its bar) ----
    public const uint Green = 0xFF3DDC84, Warn = 0xFFF7C948, Bad = 0xFFFF5A5A;

    // ---- data colours: what a value is, not how it is ----
    public const uint DataPurple = 0xFF8B7CF8;   // CPU
    public const uint DataBlue = 0xFF5AA9FF;     // RAM, VRAM

    // ---- what the widget being drawn reads: its look's while it draws (Use), the default look's otherwise ----
    public static uint Text = White, Sub = Grey, Faint = Dim;
    public static uint Cap = Grey, CapText = White;   // the caption row's grey and white (a header's band has its own)
    public static uint CapDot;                   // a dot in the caption row (a stopwatch that runs)
    public static uint Tile;                     // a button that is off, round buttons
    public static uint TileHover;                // a tile under the laser
    public static uint Track;                    // the empty part of a bar
    public static uint Edge = 0x10FFFFFF;        // a card's hairline (6 % white)
    public static uint Accent;                   // lines and marks: bars, the seconds line, the timer, the today mark
    public static uint AccentFill;               // the accent as a fill with text on it (a touch darker when the accent is mid-light)
    public static uint OnAccent;                 // icons and labels on AccentFill
    public static uint Data;                     // a reading's colour (0: by role - purple CPU, blue memory, the accent else)
    public static uint Ok = Green;               // a fine level (a battery's bar); the text colour on a coloured card
    public static uint Halo;                     // the card's colour behind a satellite or a knob (a gap around it)
    public static bool Badge;                    // a coloured card: a level shows as a dot (values keep the text colour)
    public static bool CapBadge;                 // the same in the caption row only (a header's band)
    public static uint BadgeRing;                // the ring around that dot
    public static uint WarnInk = Warn, BadInk = Bad;   // Warn / Bad as text and bars on this card (lifted where it is lighter)
    public static Look Cur, Default;             // the whole look (its card and its button tiles), and the default one

    public static void Derive(uint pc, uint accent)
    {
        pc |= 0xFF000000;
        Page = pc;
        Tint = Theme.Hsv(Theme.Hue(pc), MathF.Min(0.40f, Theme.Sat(pc)), 0.88f);
        Card = Theme.Mix(pc, Tint, Lift);
        CardTop = Theme.Mix(pc, Tint, Lift + 0.035f);
        DangerFill = Theme.Mix(pc, Bad, 0.12f);
        DangerEdge = Theme.Mix(pc, Bad, 0.38f);
        var d = new Look
        {
            Fill0 = CardTop, Fill1 = Card, Angle = 90, Span = 0.5f, Border = 0x10FFFFFF, BorderW = EdgeWidth,
            Text = White, Sub = Grey, Faint = Dim, Cap = Grey, CapText = White, CapDot = accent, Edge = 0x10FFFFFF,
            Tile = Theme.Mix(pc, Tint, Lift + 0.10f), TileHover = Theme.Mix(pc, Tint, Lift + 0.17f), Track = Theme.Mix(pc, Tint, Lift + 0.16f),
            Accent = accent, Ok = Green, Halo = Card, BadgeRing = White, WarnInk = Warn, BadInk = Bad,
        };
        d.AccentFill = Theme.Luma(accent) < 0.55f ? Theme.Mix(accent, 0xFF000000, 0.08f) : accent;
        d.OnAccent = Theme.Luma(d.AccentFill) > 0.55f ? Dark : 0xFFFFFFFF;
        d.Off0 = d.Off1 = d.Tile; d.OffHover = d.TileHover; d.OffText = White; d.OffBorder = d.Edge; d.OffBorderW = EdgeWidth;
        d.On0 = d.On1 = d.AccentFill; d.OnText = d.OnAccent;
        Default = d;
        Use(d);
    }

    // a value's colour: its normal colour, or Warn / Bad when it needs attention (on a coloured card: normal, its dot says it)
    public static uint Level(StatLevel l, uint normal) =>
        l == StatLevel.Ok || Badge ? normal : l == StatLevel.Bad ? BadInk : WarnInk;

    // ================= a widget's own look =================

    public static readonly string[] Styles = { null, "tint", "solid", "twotone", "header", "outline", "glass" };
    public static readonly string[] StyleNames = { "Default", "Tint", "Solid", "Two-tone", "Header", "Outline", "Glass" };

    // the editor's colours (Auto is the accent, or a reading's role colour)
    public static readonly (string name, uint c)[] Palette =
    {
        ("Pink", 0xFFFF5D93), ("Red", 0xFFEF4444), ("Orange", 0xFFF97316), ("Amber", 0xFFF59E0B), ("Green", 0xFF22C55E),
        ("Teal", 0xFF14B8A6), ("Cyan", 0xFF06B6D4), ("Blue", 0xFF3B82F6), ("Indigo", 0xFF6366F1), ("Purple", 0xFFA855F7),
        ("Navy", 0xFF1E3A8A), ("Grey", 0xFF64748B), ("White", 0xFFE5E7EB),
    };

    // a style's index (an unknown one, say from a newer Orbit, is the default card)
    public static int StyleIndex(string s) => s == null ? 0 : Math.Max(0, Array.IndexOf(Styles, s));

    public struct Look
    {
        public int Style;                         // Styles index
        public uint Fill0, Fill1;                 // the card: Fill0 -> Fill1 along Angle, done after Span of it
        public float Angle, Span;
        public uint Sheen;                        // glass: a light over the top (0: none)
        public uint Border; public float BorderW; // the card's edge: a hairline, a tint's coloured one, an outline's 2 px
        public uint Band;                         // header: the band behind the caption (0: none)
        public uint Off0, Off1, OffHover, OffText, OffBorder, On0, On1, OnText;   // a button that is its card
        public float OffBorderW;
        public bool Check;                        // on: a check mark too (solid / two-tone, where off is coloured as well)
        public uint Text, Sub, Faint, Cap, CapText, CapDot, Tile, TileHover, Track, Edge, Accent, AccentFill, OnAccent, Data, Ok, Halo, BadgeRing;
        public uint WarnInk, BadInk;
        public bool Badge, CapBadge;
    }

    public static void Use(in Look l)
    {
        Cur = l;
        Text = l.Text; Sub = l.Sub; Faint = l.Faint; Cap = l.Cap; CapText = l.CapText; CapDot = l.CapDot;
        Tile = l.Tile; TileHover = l.TileHover; Track = l.Track; Edge = l.Edge;
        Accent = l.Accent; AccentFill = l.AccentFill; OnAccent = l.OnAccent; Data = l.Data; Ok = l.Ok; Halo = l.Halo;
        Badge = l.Badge; CapBadge = l.Badge || l.CapBadge; BadgeRing = l.BadgeRing; WarnInk = l.WarnInk; BadInk = l.BadInk;
    }

    public static void Reset() => Use(Default);

    // The look of a style (Styles index) in a colour. c: the widget's colour (its own, else the accent or its role colour);
    // c2: two-tone's second (0: made from c); own: c is the widget's own (glass without one is plain frost); night: the fills
    // dimmer (a bright card glares in a dark room); cards: no page behind the cards (glass lets more of the world through).
    // Every text colour is checked against what it sits on (the whole of a gradient): a fill too close to its text is pushed
    // darker or lighter until it reads.
    public static Look Make(int style, uint c, uint c2, bool own, bool night, bool cards)
    {
        var L = Default;
        L.Style = style;
        c |= 0xFF000000;
        if (c2 != 0) c2 |= 0xFF000000;
        if (style == 0) { Colour(ref L, c, Card, L.Tile); return L; }     // the default card, its accent parts in the colour
        if (night)
        {
            c = Theme.Mix(c, 0xFF000000, 0.4f);
            if (c2 != 0) c2 = Theme.Mix(c2, 0xFF000000, 0.4f);
        }
        switch (style)
        {
            case 1:
                // tint: the card mixed toward the colour, a hairline in it, the accent parts in it
                L.Fill0 = Theme.Mix(CardTop, c, 0.22f);
                L.Fill1 = Theme.Mix(Card, c, 0.22f);
                L.Tile = L.Off0 = L.Off1 = Theme.Mix(Default.Tile, c, 0.25f);
                L.TileHover = L.OffHover = Theme.Mix(Default.TileHover, c, 0.3f);
                L.Track = Theme.Mix(Default.Track, c, 0.25f);
                L.Halo = L.Fill1;
                L.Sub = L.Cap = Lighten(Grey, L.Fill0, 4.5f);
                L.Faint = Theme.Mix(L.Fill1, White, 0.42f);
                L.Border = L.OffBorder = Theme.WithAlpha(Away(c, L.Fill1, 2.4f), 0.65f);
                Colour(ref L, c, L.Fill1, L.Tile);
                L.WarnInk = Lighten(Warn, L.Fill0, 3.6f);
                L.BadInk = Lighten(Bad, L.Fill0, 3.6f);
                L.Badge = Near(c, Warn) || Near(c, Bad);
                break;
            case 2:
                // solid: filled with the colour (a little lighter at the top)
                Fills(ref L, Theme.Mix(c, 0xFFFFFFFF, 0.07f), c, 90, 0.5f);
                break;
            case 3:
                // two-tone: the colour into the second one, corner to corner
                Fills(ref L, c, c2 != 0 ? c2 : Second(c), 45, 1);
                break;
            case 4:
            {
                // header: the default card under a band in the colour (the caption on it); the accent parts in the colour
                uint bt = OnText(c, c);
                uint band = Fit(c, c, bt, 4.8f).f0;
                L.Band = band;
                L.CapBadge = true;                // a level in the caption row: a dot (red text on a red band would vanish)
                Colour(ref L, c, Card, L.Tile);
                L.Cap = Theme.Mix(band, bt, 0.85f);
                L.CapText = L.CapDot = L.BadgeRing = bt;
                break;
            }
            case 5:
                // outline: the default card inside a 2 px border of the colour
                Colour(ref L, c, Card, L.Tile);
                L.Border = L.OffBorder = Away(c, Card, 3f);
                L.BorderW = L.OffBorderW = 2;
                break;
            case 6:
            {
                // glass: lighter and see-through, a sheen over the top, a bright hairline; frosted in the colour when it has one
                uint frost = Theme.Mix(own ? Theme.Mix(Card, c, 0.2f) : Card, 0xFFFFFFFF, 0.16f);
                float a = cards ? 0.55f : 0.72f;
                uint ground = cards ? 0xFF2A2C33 : Page, seen = Theme.Mix(ground, frost, a);   // over the page (cards: a mid-dark world)
                // white text all over it, the sheen included: a light colour's frost is darker
                for (int i = 0; i < 12 && Contrast(White, Theme.Mix(seen, 0xFFFFFFFF, 0.25f)) < 5.5f; i++)
                {
                    frost = Theme.Mix(frost, 0xFF000000, 0.08f);
                    seen = Theme.Mix(ground, frost, a);
                }
                L.Fill0 = L.Fill1 = L.Off0 = L.Off1 = Theme.WithAlpha(frost, a);
                L.OffHover = Theme.WithAlpha(Theme.Mix(frost, 0xFFFFFFFF, 0.1f), a);
                L.Sheen = 0x40FFFFFF;
                L.Border = L.OffBorder = L.Edge = 0x59FFFFFF;
                L.Halo = seen;
                L.Sub = L.Cap = Lighten(Grey, Theme.Mix(seen, 0xFFFFFFFF, 0.25f), 4.5f);    // under the sheen
                L.Faint = Theme.Mix(seen, White, 0.4f);
                L.Track = 0x33FFFFFF;
                L.Tile = 0x29FFFFFF;
                L.TileHover = 0x40FFFFFF;
                Colour(ref L, c, seen, Theme.Mix(seen, 0xFFFFFFFF, 0.16f));
                L.WarnInk = Lighten(Warn, Theme.Mix(seen, 0xFFFFFFFF, 0.25f), 3.6f);
                L.BadInk = Lighten(Bad, Theme.Mix(seen, 0xFFFFFFFF, 0.25f), 3.6f);
                break;
            }
        }
        return L;
    }

    // the colour as the accent: lines and marks light enough to see on the card, a fill that does not look like an off tile,
    // white or dark text on that fill (the default look's rule, the fill nudged when it still reads badly)
    static void Colour(ref Look L, uint c, uint card, uint tile)
    {
        uint line = Away(c, card, 3f);
        L.Accent = L.Data = L.CapDot = line;
        uint fill = Theme.Luma(c) < 0.55f ? Theme.Mix(c, 0xFF000000, 0.08f) : c;
        fill = Away(fill, tile, 1.9f);
        uint on = Theme.Luma(fill) > 0.55f ? Dark : 0xFFFFFFFF;
        fill = Fit(fill, fill, on, 3.2f).f0;
        L.AccentFill = L.On0 = L.On1 = fill;
        L.OnAccent = L.OnText = on;
    }

    // a card filled with colour (solid, two-tone): the text that reads best on all of it, the fill pushed darker / lighter
    // until it reads well; the rest from those two - the accent parts in the text colour, a level as a dot
    static void Fills(ref Look L, uint f0, uint f1, float angle, float span)
    {
        uint t = OnText(f0, f1);
        (f0, f1) = Fit(f0, f1, t, 4.8f);
        uint mid = Theme.Mix(f0, f1, 0.5f), worst = Contrast(t, f0) < Contrast(t, f1) ? f0 : f1;
        L.Fill0 = f0; L.Fill1 = f1; L.Angle = angle; L.Span = span;
        L.Border = L.Edge = 0;
        L.Text = L.CapText = L.CapDot = L.Accent = L.Data = L.Ok = L.BadgeRing = t;
        L.Sub = L.Cap = Theme.Mix(worst, t, 0.8f);
        L.Faint = Theme.Mix(mid, t, 0.5f);
        L.Track = Theme.Mix(mid, t, 0.24f);
        L.Tile = Theme.Mix(mid, t, 0.16f);
        L.TileHover = Theme.Mix(mid, t, 0.26f);
        L.AccentFill = t;                          // a white disc with the card's colour as its glyph (dark on a light card)
        L.OnAccent = mid;
        L.Halo = mid;
        L.Badge = true;
        // buttons: on = the fill (lifted when it is too dark to tell from off) with a check, off = a dark version of it
        L.Off0 = Theme.Mix(Card, f0, 0.3f);
        L.Off1 = Theme.Mix(Card, f1, 0.3f);
        L.OffHover = Theme.Mix(Card, mid, 0.4f);
        L.OffText = White;
        L.OffBorder = 0x10FFFFFF;
        uint offMid = Theme.Mix(L.Off0, L.Off1, 0.5f);
        for (int i = 0; i < 12 && Contrast(Theme.Mix(f0, f1, 0.5f), offMid) < 1.9f; i++)
        {
            uint n0 = Theme.Mix(f0, 0xFFFFFFFF, 0.06f), n1 = Theme.Mix(f1, 0xFFFFFFFF, 0.06f);
            if (MinContrast(t, n0, n1) < 4.5f) break;
            (f0, f1) = (n0, n1);
        }
        L.On0 = f0; L.On1 = f1; L.OnText = t;
        L.Check = true;
    }

    // two-tone's second colour when none is picked: the hue turned 40 degrees (cool colours on toward violet, warm ones toward
    // red) and a little darker; a grey just darker
    public static uint Second(uint c)
    {
        c |= 0xFF000000;
        float s = Theme.Sat(c);
        if (s < 0.12f) return Theme.Mix(c, 0xFF000000, 0.35f);
        float h = Theme.Hue(c), v = MathF.Max((c >> 16) & 0xFF, MathF.Max((c >> 8) & 0xFF, c & 0xFF)) / 255f;
        return Theme.Hsv(h + (h >= 90 && h < 300 ? 40 : -40), s, v * 0.85f);
    }

    // ---- contrast (WCAG): text on a fill ----

    static float Lin(uint c, int s)
    {
        float v = ((c >> s) & 0xFF) / 255f;
        return v <= 0.04045f ? v / 12.92f : MathF.Pow((v + 0.055f) / 1.055f, 2.4f);
    }

    public static float Lum(uint c) => 0.2126f * Lin(c, 16) + 0.7152f * Lin(c, 8) + 0.0722f * Lin(c, 0);

    public static float Contrast(uint a, uint b)
    {
        float x = Lum(a), y = Lum(b);
        return (MathF.Max(x, y) + 0.05f) / (MathF.Min(x, y) + 0.05f);
    }

    // the worst contrast of text t over a gradient f0 -> f1
    static float MinContrast(uint t, uint f0, uint f1) =>
        MathF.Min(Contrast(t, Theme.Mix(f0, f1, 0.5f)), MathF.Min(Contrast(t, f0), Contrast(t, f1)));

    // white or dark text, whichever reads better on all of f0 -> f1
    static uint OnText(uint f0, uint f1) => MinContrast(White, f0, f1) >= MinContrast(Dark, f0, f1) ? White : Dark;

    // the fill pushed away from its text (darker under white, lighter under dark) until it has 'want' contrast everywhere
    static (uint f0, uint f1) Fit(uint f0, uint f1, uint t, float want)
    {
        uint to = Lum(t) > 0.18f ? 0xFF000000 : 0xFFFFFFFF;
        for (int i = 0; i < 16 && MinContrast(t, f0, f1) < want; i++)
        {
            f0 = Theme.Mix(f0, to, 0.07f);
            f1 = Theme.Mix(f1, to, 0.07f);
        }
        return (f0, f1);
    }

    // c, lifted (or darkened, on a light ground) just enough to have 'want' contrast against 'ground'
    static uint Away(uint c, uint ground, float want)
    {
        uint to = Lum(ground) < 0.18f ? 0xFFFFFFFF : 0xFF000000;
        for (int i = 0; i < 16 && Contrast(c, ground) < want; i++) c = Theme.Mix(c, to, 0.08f);
        return c;
    }

    // light text (a grey, a status colour) on a dark card, lifted toward white until it has 'want' contrast
    static uint Lighten(uint c, uint ground, float want)
    {
        for (int i = 0; i < 16 && Contrast(c, ground) < want; i++) c = Theme.Mix(c, 0xFFFFFFFF, 0.08f);
        return c;
    }

    // a saturated colour of about the same hue as a status colour (a red card and Bad): a level there shows as a dot
    static bool Near(uint c, uint status)
    {
        if (Theme.Sat(c) < 0.35f) return false;
        float d = MathF.Abs(Theme.Hue(c) - Theme.Hue(status)) % 360;
        return MathF.Min(d, 360 - d) < 35;
    }

    // a small picture of a look (the editor's style chips): its card, a caption, a value and a bar
    public static void Sample(Painter p, RectangleF r, in Look L, float radius)
    {
        p.FillGradient(r, L.Fill0, L.Fill1, radius, L.Angle, L.Span);
        if (L.Sheen != 0) p.FillGradient(r, L.Sheen, L.Sheen & 0x00FFFFFF, radius, 90, 0.5f);
        float bh = MathF.Round(r.Height * 0.34f);
        if (L.Band != 0)
        {
            p.PushClip(new RectangleF(r.X - 1, r.Y - 1, r.Width + 2, bh + 1));
            p.Fill(r, L.Band, radius);
            p.PopClip();
        }
        if (L.Border != 0 && L.BorderW > 0)
        {
            float bw = MathF.Max(1, L.BorderW * 0.75f), i = bw * 0.48f;
            p.Stroke(RectangleF.Inflate(r, -i, -i), L.Border, bw, radius - i);
        }
        float x = r.X + 6, w = r.Width - 12;
        p.Fill(new RectangleF(x, r.Y + bh / 2 - 1.5f, w * 0.45f, 3), L.Cap, 1.5f);
        p.Fill(new RectangleF(x, r.Y + r.Height * 0.5f - 1, w * 0.62f, 5), L.Text, 2.5f);
        p.Fill(new RectangleF(x, r.Bottom - 7, w, 3), L.Track, 1.5f);
        p.Fill(new RectangleF(x, r.Bottom - 7, w * 0.6f, 3), L.Accent, 1.5f);
    }
}

// how a reading is: fine, worth a look, or needs action (thresholds in WristPanel.LevelOf, shared by both styles)
public enum StatLevel { Ok, Warn, Bad }
