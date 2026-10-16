using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Numerics;
using Vortice.DirectWrite;

namespace Orbit;

// The clocks' designs (WatchWidget.Variant, WatchWidgets.TimeVariants / AnalogVariants) on the modern custom face. Each draws
// with the widget's look (Bento.Use's tokens), so the seven looks and a picked colour work on all of them. Standard (TimeOrbit)
// and Classic (AnalogOrbit) are the designs the watch had; the 1x1 analog clock's Squircle (AnalogS) can now be picked at any
// size. The 95 face ignores all of this.
public sealed partial class WristPanel
{
    // a time widget in a design of its own; false: Standard (TimeOrbit draws it as it always did)
    bool TimeVariant(RectangleF r, WatchWidget w, DateTime now, PanelData d, int id)
    {
        string v = WatchWidgets.VariantOf(w);
        if (v is null or "standard") return false;
        bool h24 = _cfg.Data.Appearance.Clock24h;
        string ampm = h24 ? null : now.Hour < 12 ? "AM" : "PM";
        Words(now);
        switch (v)
        {
            case "flip": TimeFlip(r, w, now, d, id, h24, ampm); return true;
            case "outline": TimeOutline(r, w, now, d, h24, ampm); return true;
            case "stacked": TimeStacked(r, w, now, d, id, h24, ampm); return true;
            case "segment": TimeSegment(r, w, now, d, h24, ampm, id); return true;
            case "words": TimeWords(r, w, now, d, id); return true;
            default: return false;
        }
    }

    // The bold digits' top and their top-to-bottom span (in em), and where their middle is: Segoe UI's constants for the default
    // font (it draws as it always did), a chosen font's own measured (Look > Font: its scale and figures included); h: the top for
    // what lines up with it
    (float top, float span, float mid, float h) Ink()
    {
        if (!Theme.OwnFont) return (DigitTop, DigitSpan, DigitH / 2, DigitH);
        var (a, b) = _p.DigitInk(FontWeight.Bold, Theme.Display);
        return (a, a + b, (a - b) / 2, a);
    }

    // drawn shapes (not text) start at least this far under a header look's band (4 px clear of its edge, plus 'more'), whatever a
    // chosen font made the date (the default font's layout already is: it stays exactly as it was)
    static readonly WatchWidgets.Kind TimeKind = WatchWidgets.Find("time");
    float BelowBand(RectangleF r, WatchWidget w, float y, float more = 0) =>
        !Theme.OwnFont ? y : MathF.Max(y, r.Y + BandHeight(TimeKind, w, r) + 4 + more);

    // the clock's header on a card wider than a 1x1 (TimeOrbit's): the date, the time in VR at its right; returns where the space
    // under it starts (10 px under the date's descenders)
    float ClockHead(RectangleF r, WatchWidget w, PanelData d)
    {
        float pad = PadX(r), iw = r.Width - 2 * pad;
        float ds = w.H >= 2 ? MathF.Round(Math.Clamp(r.Height * 0.085f, SmallText, 24)) : SmallText;
        var (db, _) = _p.TextLine(ds, FontWeight.SemiBold);
        float dBase = r.Y + 9 + db;
        DateHeader(r.X + pad, dBase, iw, ds, VrNote(d));
        return dBase + ds * 0.24f + 10;
    }

    // a 1x1's caption row (TimeS's): the weekday and the day, AM / PM at its right; returns where the space under it starts (clear
    // of a header's band)
    float ClockCaption(RectangleF r, string ampm)
    {
        float pad = PadX(r), iw = r.Width - 2 * pad;
        float aw = ampm == null ? 0 : _p.MeasureTracked(ampm, CapSize, FontWeight.SemiBold, CapTrack);
        Caption(r, aw > 0 ? iw - aw - 8 : iw, _capDayDate);
        if (aw > 0) CapText(ampm, r.Right - pad - aw, r.Y + CapBase, aw + 2, Bento.Cap);
        return r.Y + 38;
    }

    // ---- Flip: the hours and the minutes each on a card split across the middle; a new minute folds down over the old one ----
    const float FlipTime = 0.32f;

    void TimeFlip(RectangleF r, WatchWidget w, DateTime now, PanelData d, int id, bool h24, string ampm)
    {
        var culture = CultureInfo.CurrentCulture;
        string hh = now.ToString(h24 ? "HH" : "%h", culture), mm = now.ToString("mm", culture);
        bool small = w.W == 1 && w.H == 1, tall = w.H >= 2;
        float pad = PadX(r);
        float top = small ? ClockCaption(r, ampm) : BelowBand(r, w, ClockHead(r, w, d) + (tall ? 2 : 0));
        var area = RectangleF.FromLTRB(r.X + pad, top, r.Right - pad, r.Bottom - (tall ? 14 : 10));
        // AM / PM beside the minutes (a 1x1 has it in its caption row)
        string side = small ? null : ampm;
        float ss = side == null ? 0 : tall ? MathF.Max(SmallText, MathF.Round(area.Height * 0.09f)) : CapSize;
        float sw = side == null ? 0 : _p.MeasureWidth(side, ss, FontWeight.SemiBold) + 8;
        // a card: the digits and a fifth of their size each side, at least about as tall as it is wide; the digits 60 % of it at most
        float k = _p.MeasureWidth("00", 100, FontWeight.Bold, Theme.Display) / 100;
        float fill0 = tall ? 0.6f : 0.68f;             // how much of a card's height its digits may take
        var ink = Ink();
        float dh = !Theme.OwnFont ? DigitH : ink.span;      // the digits' height, in em
        float size = MathF.Min(area.Height * fill0 / dh, (area.Width - sw) / (2 * k + 0.9f));
        size = MathF.Max(14, MathF.Floor(size * 2) / 2);
        float cw = (k + 0.4f) * size, gap = MathF.Max(4, size * 0.1f);
        float ch = MathF.Min(area.Height, MathF.Max(size * dh / fill0, cw * 0.95f));
        float x0 = MathF.Round(area.X + (area.Width - (2 * cw + gap + sw)) / 2), y0 = MathF.Round(area.Y + (area.Height - ch) / 2);
        // the cards: darker than the widget's (white digits), a touch darker on a light one (dark digits); glass: a dark veil
        uint mid = Theme.Mix(Bento.Cur.Fill0, Bento.Cur.Fill1, 0.5f);
        bool seeThrough = (mid >> 24) < 0xF0;
        uint fill = seeThrough ? 0x52000000 : Bento.Lum(Bento.Text) > 0.4f ? Theme.Mix(mid, 0xFF000000, 0.42f) : Bento.Tile;
        uint split = seeThrough ? 0x8C000000 : mid;
        FlipCard(new RectangleF(x0, y0, cw, ch), hh, size, ink.mid, Ui.Id("wflh", id), fill, split);
        FlipCard(new RectangleF(x0 + cw + gap, y0, cw, ch), mm, size, ink.mid, Ui.Id("wflm", id), fill, split);
        if (side != null) TextAt(side, x0 + 2 * cw + gap + 8, y0 + ch - MathF.Max(6, ch * 0.1f), sw, Sub, ss, FontWeight.SemiBold);
    }

    // one card: its digits centred on the split; a change folds down - the old top half falls onto the split (squashed), then the
    // new bottom half opens from it - in FlipTime, the only time it asks for frames (_rolling)
    void FlipCard(RectangleF c, string s, float size, float inkMid, string key, uint fill, uint split)
    {
        if (!_rolls.TryGetValue(key, out var ro)) _rolls[key] = ro = new Roll { From = s, To = s };
        else if (ro.To != s) { ro.From = ro.To; ro.To = s; ro.Start = Clock.Now; }
        float rad = MathF.Max(5, MathF.Min(c.Width, c.Height) * 0.1f), mid = MathF.Round(c.Y + c.Height / 2);
        float by = mid + size * inkMid;
        void Digits(string str) => TextAt(str, c.X, by, c.Width, Bento.Text, size, FontWeight.Bold, TextAlignment.Center, Theme.Display);
        _p.Fill(c, fill, rad);
        float t = (float)((Clock.Now - ro.Start) / FlipTime);
        if (t >= 1 || ro.From == ro.To || !Motion.Full)          // the fold: Motion full only (like the rolling digits)
            Digits(s);
        else
        {
            _rolling = true;
            t = Math.Clamp(t, 0, 1);
            var topHalf = RectangleF.FromLTRB(c.X - 1, c.Y - 1, c.Right + 1, mid);
            var bottomHalf = RectangleF.FromLTRB(c.X - 1, mid, c.Right + 1, c.Bottom + 1);
            var g = ClockGeo.For(_p);
            // behind the flap: the new top, the old bottom
            _p.PushClip(topHalf); Digits(ro.To); _p.PopClip();
            _p.PushClip(bottomHalf); Digits(ro.From); _p.PopClip();
            bool first = t < 0.5f;
            float fold = MathF.Abs(MathF.Cos(t * MathF.PI));        // 1 -> 0 (the top flap falls), 0 -> 1 (the bottom one opens)
            _p.PushClip(first ? topHalf : bottomHalf);
            g.PushSquash(fold, mid);
            _p.Fill(c, fill, rad);
            Digits(first ? ro.From : ro.To);
            _p.Fill(c, Theme.WithAlpha(0xFF000000, 0.28f * (1 - fold)), rad);      // it darkens as it turns away
            g.PopSquash();
            _p.PopClip();
        }
        float lw = MathF.Max(1.5f, c.Height * 0.022f);
        _p.Fill(new RectangleF(c.X, mid - lw / 2, c.Width, lw), split);
    }

    // ---- Outline: the time as hollow digits (the glyphs' outlines), as big as the card allows; the seconds small beside them ----
    // The static Segoe UI: a variable font's outlines come in its default weight, whatever weight is asked for
    public const string OutlineFont = "Segoe UI";
    public const FontWeight OutlineWeight = FontWeight.Bold;

    void TimeOutline(RectangleF r, WatchWidget w, DateTime now, PanelData d, bool h24, string ampm)
    {
        var g = ClockGeo.For(_p);
        var culture = CultureInfo.CurrentCulture;
        string t = now.ToString(h24 ? "HH:mm" : "h:mm", culture);
        bool small = w.W == 1 && w.H == 1, tall = w.H >= 2;
        float pad = PadX(r), iw = r.Width - 2 * pad, x = r.X + pad;
        float top = small ? ClockCaption(r, ampm) : BelowBand(r, w, ClockHead(r, w, d) + 3, 3);      // geometry (its stroke too): clear of a header band's edge
        float bottom = r.Bottom - (tall ? 16 : 12);
        float w100 = g.OutlineWidth(h24 ? "00:00" : "12:00", 100, OutlineWeight, OutlineFont);      // the widest time decides
        string sec = small ? null : now.ToString("ss", culture);
        float ss = 0, sideW = 0;
        if (sec != null)
        {
            ss = tall ? MathF.Max(SmallText, MathF.Round((bottom - top) * 0.12f)) : SmallText;
            float aw = ampm == null ? 0 : _p.MeasureWidth(ampm, ss, FontWeight.SemiBold);
            sideW = MathF.Max(_p.MeasureWidth("00", ss, FontWeight.SemiBold), aw) + 10;
        }
        float size = MathF.Min(100 * (iw - sideW - 4) / w100, (bottom - top) / DigitSpan);
        size = MathF.Max(16, MathF.Floor(size * 2) / 2);
        float by = top + size * DigitTop + (bottom - top - size * DigitSpan) / 2;
        float sw = Math.Clamp(size * 0.028f, 1.4f, 4.5f);
        float tw = g.Outline(t, x + sw / 2, by, size, OutlineWeight, OutlineFont, Bento.Text, sw);
        if (sec == null) return;
        float sx = x + sw + tw + 8;
        TextAt(sec, sx, by, sideW, Sub, ss, FontWeight.SemiBold);
        if (ampm != null) TextAt(ampm, sx, by - size * DigitH + ss * DigitH, sideW, Sub, ss, FontWeight.SemiBold);
    }

    // ---- Stacked: the hours over the minutes (the minutes in the accent), big; on a wide card side by side, no colon ----
    void TimeStacked(RectangleF r, WatchWidget w, DateTime now, PanelData d, int id, bool h24, string ampm)
    {
        var culture = CultureInfo.CurrentCulture;
        string hh = now.ToString(h24 ? "HH" : "%h", culture), mm = now.ToString("mm", culture);
        bool small = w.W == 1 && w.H == 1, tall = w.H >= 2, stack = r.Width <= r.Height * 1.6f;
        float pad = PadX(r), iw = r.Width - 2 * pad, x = r.X + pad;
        float top = small ? r.Y + 12 : ClockHead(r, w, d);
        if (small && Bento.Cur.Band != 0)
        {
            // a header look's band: the caption row on it (with AM / PM), the digits under it
            top = ClockCaption(r, ampm);
            ampm = null;
        }
        var (dTop, dSpan, _, dH) = Ink();
        float bottom = r.Bottom - (tall ? 16 : 12), ah = bottom - top;
        uint hc = Bento.Text, mc = Bento.Cur.AccentText;
        float k = _p.MeasureWidth("00", 100, FontWeight.Bold, Theme.Display) / 100;
        string sec = small ? null : now.ToString("ss", culture);
        float ss = small ? CapSize : tall ? MathF.Max(SmallText, MathF.Round(ah * 0.11f)) : SmallText;
        float aw = ampm == null ? 0 : small ? _p.MeasureTracked(ampm, CapSize, FontWeight.SemiBold, CapTrack) : _p.MeasureWidth(ampm, ss, FontWeight.SemiBold);
        float sideW = MathF.Max(sec == null ? 0 : _p.MeasureWidth("00", ss, FontWeight.SemiBold), aw);
        if (sideW > 0) sideW += 8;
        void Side(float sx, float digitTop, float secBase)
        {
            if (ampm != null)
            {
                if (small) CapText(ampm, sx, digitTop + CapSize * DigitH, aw, Sub);
                else TextAt(ampm, sx, digitTop + ss * DigitH, sideW, Sub, ss, FontWeight.SemiBold);
            }
            if (sec != null) TextAt(sec, sx, secBase, sideW, Sub, ss, FontWeight.SemiBold);
        }
        if (stack)
        {
            const float Gap = 0.12f;
            float size = MathF.Min(ah / (2 * dSpan + Gap), (iw - sideW) / k);
            size = MathF.Max(16, MathF.Floor(size * 2) / 2);
            float y0 = top + (ah - size * (2 * dSpan + Gap)) / 2;
            float b1 = y0 + size * dTop, b2 = b1 + size * (dSpan + Gap);
            ValueText(x, b1, hh, size, hc, Ui.Id("wsth", id));
            ValueText(x, b2, mm, size, mc, Ui.Id("wstm", id));
            Side(x + k * size + 8, b1 - size * dH, b2);
            return;
        }
        {
            const float Gap = 0.22f;
            float size = MathF.Min(ah / dSpan, (iw - sideW) / (2 * k + Gap));
            size = MathF.Max(16, MathF.Floor(size * 2) / 2);
            float by = top + size * dTop + (ah - size * dSpan) / 2;
            float hw = ValueText(x, by, hh, size, hc, Ui.Id("wsth", id));
            float mx = x + hw + Gap * size;
            float mw = ValueText(mx, by, mm, size, mc, Ui.Id("wstm", id));
            Side(mx + mw + 8, by - size * dH, by);
        }
    }

    // ---- Segment: seven-segment LED digits in the accent, the unlit segments faint; the seconds smaller beside them. A digit that
    // changes cross-fades its segments, the way an LED's light comes and goes (0.16 s; Motion full). ----
    const float SegFadeTime = 0.16f;

    // how far 'key''s change to s has faded in (1: done), and what it was before
    float SegFade(string key, string s, out string from)
    {
        if (!_rolls.TryGetValue(key, out var ro)) _rolls[key] = ro = new Roll { From = s, To = s };
        else if (ro.To != s) { ro.From = ro.To; ro.To = s; ro.Start = Clock.Now; }
        from = ro.From;
        float t = (float)((Clock.Now - ro.Start) / SegFadeTime);
        if (t >= 1 || ro.From == ro.To || ro.From.Length != s.Length || !Motion.Full) return 1;
        _rolling = true;
        return Math.Clamp(t, 0, 1);
    }

    void TimeSegment(RectangleF r, WatchWidget w, DateTime now, PanelData d, bool h24, string ampm, int id)
    {
        var g = ClockGeo.For(_p);
        var culture = CultureInfo.CurrentCulture;
        string t = now.ToString(h24 ? "HH:mm" : "h:mm", culture);
        if (t.Length == 4) t = " " + t;                   // a one-digit hour: its first digit unlit
        bool small = w.W == 1 && w.H == 1, tall = w.H >= 2;
        float pad = PadX(r), iw = r.Width - 2 * pad, x = r.X + pad;
        uint on = Bento.Accent, off = Bento.Track;
        float wBig = ClockArt.SegWidth("00:00", 1);
        if (small)
        {
            // TimeS's: the caption row, the minute so far as a line along the bottom
            float top = ClockCaption(r, ampm);
            var bar = new RectangleF(x, r.Bottom - 16, iw, 4);
            _p.Fill(bar, Bento.Track, 2);
            if (now.Second > 0) _p.Fill(new RectangleF(bar.X, bar.Y, MathF.Max(4, bar.Width * now.Second / 60f), 4), Bento.Accent, 2);
            float avail = bar.Y - 8 - top, h1 = MathF.Floor(MathF.Min(avail, iw / wBig));
            float k1 = SegFade(Ui.Id("wsgt", id), t, out string t1);
            ClockArt.SegText(g, t, t1, k1, x, top + (avail - h1) / 2, h1, on, off);
            return;
        }
        float head = BelowBand(r, w, ClockHead(r, w, d) + 3), bottom = r.Bottom - (tall ? 16 : 12), ah = bottom - head;       // clear of a header band's edge
        string sec = now.ToString("ss", culture);
        const float Sr = 0.45f, Sg = 0.22f;               // the seconds' height, and the gap before them, of the digits' height
        float wSec = ClockArt.SegWidth("00", 1) * Sr;
        float ss = tall ? MathF.Max(SmallText, MathF.Round(ah * 0.1f)) : CapSize;
        float aw = ampm == null ? 0 : tall ? _p.MeasureWidth(ampm, ss, FontWeight.SemiBold) : _p.MeasureTracked(ampm, CapSize, FontWeight.SemiBold, CapTrack);
        float h = MathF.Min(ah, iw / (wBig + Sg + wSec));
        if (aw > wSec * h) h = MathF.Min(h, (iw - aw) / (wBig + Sg));
        h = MathF.Floor(h);
        float y0 = MathF.Round(head + (ah - h) / 2);
        float kt = SegFade(Ui.Id("wsgt", id), t, out string t0);
        float bw = ClockArt.SegText(g, t, t0, kt, x, y0, h, on, off);
        float sx = x + bw + Sg * h, sh = MathF.Round(h * Sr);
        float ks = SegFade(Ui.Id("wsgs", id), sec, out string s0);
        ClockArt.SegText(g, sec, s0, ks, sx, y0 + h - sh, sh, on, off);
        if (ampm == null) return;
        if (tall) TextAt(ampm, sx, y0 + ss * DigitH, aw + 2, Sub, ss, FontWeight.SemiBold);
        else CapText(ampm, sx, y0 + CapSize * DigitH, aw, Sub);
    }

    // ---- Words: a word clock in English - every word printed faint, the time's bright ("it is twenty to seven"); the minutes past
    // the five as dots under them. The words fill the card under the date, as big as they fit (cached per card size). ----
    static readonly string[] ClockWords =
    {
        "IT", "IS", "HALF", "TEN", "QUARTER", "TWENTY", "FIVE", "TO", "PAST",
        "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE", "TEN", "ELEVEN", "TWELVE", "O’CLOCK",
    };
    const int WHalf = 2, WTen = 3, WQuarter = 4, WTwenty = 5, WFive = 6, WTo = 7, WPast = 8, WHour = 9, WOClock = 21;

    // in Magyar a word clock of its own, read the way a Hungarian says it: "PONT HÁROM", "TÍZ PERC MÚLVA NEGYED NÉGY" (3:05),
    // "ÖT PERC MÚLVA FÉL NÉGY", "HÁROMNEGYED NÉGY", "ÖT PERC MÚLVA NÉGY" (3:55). Only "perc múlva" and no ÓRA: as few letters as
    // the English (the 2x2 tile keeps its words at 14 px). Every sentence reads in the words' order.
    static readonly string[] ClockWordsHu =
    {
        "PONT", "ÖT", "TÍZ", "PERC", "MÚLVA", "NEGYED", "HÁROMNEGYED", "FÉL",
        "EGY", "KETTŐ", "HÁROM", "NÉGY", "ÖT", "HAT", "HÉT", "NYOLC", "KILENC", "TÍZ", "TIZENEGY", "TIZENKETTŐ",
    };
    const int HPont = 0, HFive = 1, HTen = 2, HPerc = 3, HMulva = 4, HNegyed = 5, HHaromnegyed = 6, HFel = 7, HHour = 7;   // + 1..12

    // the word clock's words in the language picked (English: as it always was)
    static string[] ClockWordsNow => Lang.Code == "hu" ? ClockWordsHu : ClockWords;
    static int Mask(DateTime now) => Lang.Code == "hu" ? WordMaskHu(now) : WordMask(now);

    public static int WordMaskHu(DateTime now)
    {
        int m = now.Minute / 5 * 5, h = now.Hour % 12;
        int next = h % 12 + 1, cur = h == 0 ? 12 : h;
        int Bit(int i) => 1 << i;
        int ten = Bit(HTen) | Bit(HPerc) | Bit(HMulva), five = Bit(HFive) | Bit(HPerc) | Bit(HMulva);
        return m switch
        {
            0 => Bit(HPont) | Bit(HHour + cur),
            5 => ten | Bit(HNegyed),
            10 => five | Bit(HNegyed),
            15 => Bit(HNegyed),
            20 => ten | Bit(HFel),
            25 => five | Bit(HFel),
            30 => Bit(HFel),
            35 => ten | Bit(HHaromnegyed),
            40 => five | Bit(HHaromnegyed),
            45 => Bit(HHaromnegyed),
            50 => ten,
            _ => five,
        } | (m == 0 ? 0 : Bit(HHour + next));
    }
    const float WordTrack = 0.06f, WordLine = 1.32f, WordCap = 0.7f;     // tracking, line pitch and cap height, in em

    // the words that tell the time (a bit each, ClockWords' order), to the five minutes before
    public static int WordMask(DateTime now)
    {
        int m = now.Minute / 5 * 5, h = now.Hour % 12;
        if (m >= 35) h = (h + 1) % 12;
        int mask = 1 | 2 | 1 << (WHour + (h == 0 ? 11 : h - 1));
        int Bit(int i) => 1 << i;
        mask |= m switch
        {
            0 => Bit(WOClock),
            5 or 55 => Bit(WFive),
            10 or 50 => Bit(WTen),
            15 or 45 => Bit(WQuarter),
            20 or 40 => Bit(WTwenty),
            25 or 35 => Bit(WTwenty) | Bit(WFive),
            _ => Bit(WHalf),
        };
        if (m != 0) mask |= Bit(m < 35 ? WPast : WTo);
        return mask;
    }

    sealed class WordFit { public float Size; public float[] X, Y, W; }
    readonly Dictionary<(float, float, string), WordFit> _wordFits = new();

    // the words laid out in a box: the biggest size whose lines fit, the lines spread to its width (not the last)
    WordFit FitWords(float iw, float ih)
    {
        var words = ClockWordsNow;
        var key = (iw, ih, Theme.Display + "|" + Lang.Code);
        if (_wordFits.TryGetValue(key, out var fit)) return fit;
        if (_wordFits.Count > 16) _wordFits.Clear();
        int n = words.Length;
        var w100 = new float[n];
        for (int i = 0; i < n; i++) w100[i] = _p.MeasureTracked(words[i], 100, FontWeight.SemiBold, WordTrack * 100, Theme.Display);
        float space = 0.34f;
        int Lines(float s)
        {
            int lines = 1;
            float x = 0;
            for (int i = 0; i < n; i++)
            {
                float ww = w100[i] * s / 100;
                if (ww > iw) return int.MaxValue;
                if (x > 0 && x + space * s + ww > iw) { lines++; x = ww; }
                else x += (x > 0 ? space * s : 0) + ww;
            }
            return lines;
        }
        bool Fits(float s) { int l = Lines(s); return l != int.MaxValue && (l - 1) * s * WordLine + s * WordCap <= ih; }
        float lo = 6, hi = 80;
        for (int it = 0; it < 14; it++) { float m = (lo + hi) / 2; if (Fits(m)) lo = m; else hi = m; }
        float size = MathF.Floor(lo * 2) / 2;
        fit = new WordFit { Size = size, X = new float[n], Y = new float[n], W = new float[n] };
        // the lines, then each spread to the width
        var starts = new List<int> { 0 };
        float lx = 0;
        for (int i = 0; i < n; i++)
        {
            float ww = w100[i] * size / 100;
            fit.W[i] = ww;
            if (lx > 0 && lx + space * size + ww > iw) { starts.Add(i); lx = ww; }
            else lx += (lx > 0 ? space * size : 0) + ww;
        }
        int count = starts.Count;
        float blockH = (count - 1) * size * WordLine + size * WordCap, y0 = (ih - blockH) / 2 + size * WordCap;
        for (int l = 0; l < count; l++)
        {
            int a = starts[l], b = l + 1 < count ? starts[l + 1] : n;
            float used = 0;
            for (int i = a; i < b; i++) used += fit.W[i];
            // spread to the width (not the last line, nor so far that the words fall apart: ragged then)
            float gap = b - a > 1 && l < count - 1 ? MathF.Min((iw - used) / (b - a - 1), space * size * 2.4f) : space * size;
            float x = 0;
            for (int i = a; i < b; i++) { fit.X[i] = x; fit.Y[i] = y0 + l * size * WordLine; x += fit.W[i] + gap; }
        }
        _wordFits[key] = fit;
        return fit;
    }

    // --snapshot: the size the words get on a card of this size
    public float WordSize(int w, int h)
    {
        var r = WatchWidgets.CellRect(0, 0, w, h, false);
        var (iw, ih) = WordBox(r, h);
        return FitWords(iw, ih).Size;
    }

    (float iw, float ih) WordBox(RectangleF r, int h)
    {
        float ds = h >= 2 ? MathF.Round(Math.Clamp(r.Height * 0.085f, SmallText, 24)) : SmallText;
        float top = 9 + _p.TextLine(ds, FontWeight.SemiBold).baseline + ds * 0.24f + 10;
        return (r.Width - 2 * PadX(r), r.Height - top - 30);
    }

    // a word clock's words and dots as they were and are now, and when that changed: the ones that change light up / go dark over
    // 0.4 s (Motion: not off)
    readonly Dictionary<int, (int from, int to, int dFrom, int dTo, double start)> _wordFades = new();

    void TimeWords(RectangleF r, WatchWidget w, DateTime now, PanelData d, int id)
    {
        float top = ClockHead(r, w, d) + 4, pad = PadX(r);
        var (iw, ih) = WordBox(r, w.H);
        var fit = FitWords(iw, ih);
        int mask = Mask(now), extra = now.Minute % 5;
        var words = ClockWordsNow;
        if (!_wordFades.TryGetValue(id, out var wf)) wf = (mask, mask, extra, extra, -10);
        else if (wf.to != mask || wf.dTo != extra) wf = (wf.to, mask, wf.dTo, extra, Clock.Now);
        _wordFades[id] = wf;
        float k = Motion.Any ? (float)Math.Clamp((Clock.Now - wf.start) / 0.4, 0, 1) : 1;
        if (k < 1) _rolling = true;
        k = k * k * (3 - 2 * k);
        float s = fit.Size;
        var (b, lh) = _p.TextLine(s, FontWeight.SemiBold, Theme.Display);
        for (int i = 0; i < words.Length && i < fit.W.Length; i++)
        {
            bool lit = ((mask >> i) & 1) != 0, was = ((wf.from >> i) & 1) != 0;
            uint col = lit ? Bento.Text : Bento.Faint;
            if (lit != was && k < 1) col = Theme.Mix(was ? Bento.Text : Bento.Faint, col, k);
            float x = r.X + pad + fit.X[i], by = top + fit.Y[i];
            _p.TrackedText(words[i], new RectangleF(x, by - b, fit.W[i] + 6, lh), col, s, FontWeight.SemiBold,
                s * WordTrack, TextAlignment.Leading, ParagraphAlignment.Near, Theme.Display);
        }
        // the minutes past the five: four dots, that many lit
        float dg = 13, dy = r.Bottom - 15, dx = r.X + r.Width / 2 - 1.5f * dg;
        for (int i = 0; i < 4; i++)
        {
            bool lit = i < extra, was = i < wf.dFrom;
            uint col = lit ? Bento.Text : Bento.Track;
            if (lit != was && k < 1) col = Theme.Mix(was ? Bento.Text : Bento.Track, col, k);
            _p.Circle(dx + i * dg, dy, 3, col);
        }
    }

    // ================= the analog clock's designs =================

    void AnalogClock(RectangleF r, WatchWidget w, DateTime now)
    {
        string v = WatchWidgets.VariantOf(w);
        if (v == "squircle") { AnalogS(r, w, now); return; }
        if (v is not ("minimal" or "roman" or "station")) { AnalogOrbit(r, now, Bento.Accent); return; }
        float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, rad = MathF.Min(r.Width, r.Height) / 2 - 12;
        var (ht, mt, st) = ClockArt.Turns(now);
        switch (v)
        {
            case "minimal":
                ClockArt.Minimal(_p, cx, cy, rad, ht, mt, st, Bento.Text, Bento.Accent, ClockArt.Dial(Bento.Track));
                break;
            case "roman":
                ClockArt.Roman(_p, cx, cy, rad, ht, mt, st, Bento.Text, Sub, Bento.Accent, true);
                break;
            default:
            {
                // the dial is the look's text colour, its marks and hands the card's: black on white on a dark card, and the other
                // way round on a light one; the second hand red (a colour of its own: that colour, dark or light enough to see)
                uint face = Bento.Text | 0xFF000000;
                uint ink = Theme.Mix(Bento.Cur.Fill0, Bento.Cur.Fill1, 0.5f) | 0xFF000000;
                ink = ClockArt.Away(ink, face, 7);
                uint sec = ClockArt.Away(w.Color.HasValue ? w.Color.Value | 0xFF000000 : ClockArt.StationRed, face, 3);
                ClockArt.Station(_p, cx, cy, rad, ht, mt, st, face, ink, sec, rad >= 50);
                break;
            }
        }
    }
}

// The clocks' drawings that the watch and the editor's chips share (the chips draw them small, in the widget's look), and the
// seven-segment digits.
static class ClockArt
{
    public const uint StationRed = 0xFFE3251B;

    // the hands' turns (0..1 from 12) at a moment
    public static (float h, float m, float s) Turns(DateTime now)
    {
        float s = now.Second, m = now.Minute + s / 60f, h = now.Hour % 12 + m / 60f;
        return (h / 12, m / 60, s / 60);
    }

    // c moved toward black or white (whichever is away from the ground) until it has 'want' contrast against it
    public static uint Away(uint c, uint ground, float want)
    {
        uint to = Bento.Lum(ground) < 0.18f ? 0xFFFFFFFF : 0xFF000000;
        for (int i = 0; i < 20 && Bento.Contrast(c, ground) < want; i++) c = Theme.Mix(c, to, 0.08f);
        return c;
    }

    static Vector2 At(float cx, float cy, float turns, float len)
    {
        float a = turns * MathF.Tau;
        return new(cx + len * MathF.Sin(a), cy - len * MathF.Cos(a));
    }

    static void Hand(Painter p, float cx, float cy, float turns, float from, float to, uint c, float width)
    {
        var a = At(cx, cy, turns, from);
        var b = At(cx, cy, turns, to);
        p.RoundLine(a.X, a.Y, b.X, b.Y, c, width);
    }

    // Minimal: a plain round dial (a shade of the card), four bars at 12, 3, 6 and 9, thin hands, a thin second hand in the accent
    public static void Minimal(Painter p, float cx, float cy, float rad, float ht, float mt, float st, uint ink, uint sec, uint dial, bool seconds = true)
    {
        if (dial != 0) p.Circle(cx, cy, rad, dial);
        float bw = MathF.Max(2.5f, rad * 0.05f);
        for (int i = 0; i < 4; i++) Hand(p, cx, cy, i / 4f, rad * 0.74f, rad * 0.9f, ink, bw);
        Hand(p, cx, cy, ht, 0, rad * 0.5f, ink, MathF.Max(2.5f, rad * 0.042f));
        Hand(p, cx, cy, mt, 0, rad * 0.8f, ink, MathF.Max(1.8f, rad * 0.026f));
        if (seconds) Hand(p, cx, cy, st, -rad * 0.14f, rad * 0.88f, sec, MathF.Max(1, rad * 0.012f));
        p.Circle(cx, cy, MathF.Max(2.5f, rad * 0.04f), seconds ? sec : ink);
    }

    // the minimal dial's disc: the empty bar's colour, half of it (glass: its own see-through white)
    public static uint Dial(uint track) => (track >> 24) < 0xF0 ? track : Theme.WithAlpha(track, 0.55f);

    static readonly string[] RomanNumerals = { "XII", "III", "VI", "IX" };
    public const string Serif = "Georgia";

    // Roman: a minute track (the hours longer, in the text colour), XII / III / VI / IX in a serif, slender hands, a thin second
    // hand with a counterweight in the accent
    public static void Roman(Painter p, float cx, float cy, float rad, float ht, float mt, float st, uint ink, uint sub, uint sec, bool numerals)
    {
        p.Ring(cx, cy, rad * 0.995f, sub, MathF.Max(1, rad * 0.008f));
        for (int i = 0; i < 60; i++)
        {
            bool hour = i % 5 == 0;
            Hand(p, cx, cy, i / 60f, hour ? rad * 0.86f : rad * 0.92f, rad * 0.97f, hour ? ink : sub, hour ? MathF.Max(1.8f, rad * 0.026f) : MathF.Max(0.9f, rad * 0.01f));
        }
        if (numerals)
        {
            // the serif at its own size: Georgia is also a Look > Font choice, which the painter draws smaller (no wider than Segoe UI)
            float ns0 = MathF.Max(14, MathF.Round(rad * 0.2f)), ns = ns0 / p.FontScale(Serif);
            var (b, lh) = p.TextLine(ns, FontWeight.Normal, Serif);
            for (int i = 0; i < 4; i++)
            {
                var c = At(cx, cy, i / 4f, rad * 0.66f);
                float by = c.Y + ns0 * 0.34f;
                p.Text(RomanNumerals[i], new RectangleF(c.X - ns * 1.5f, by - b, ns * 3, lh), ink, ns, FontWeight.Normal, TextAlignment.Center,
                    ParagraphAlignment.Near, Serif);
            }
        }
        Hand(p, cx, cy, ht, -rad * 0.08f, rad * 0.48f, ink, MathF.Max(3, rad * 0.055f));
        Hand(p, cx, cy, mt, -rad * 0.08f, rad * 0.76f, ink, MathF.Max(2, rad * 0.034f));
        p.Circle(cx, cy, MathF.Max(3, rad * 0.055f), ink);
        Hand(p, cx, cy, st, -rad * 0.2f, rad * 0.84f, sec, MathF.Max(1, rad * 0.012f));
        var cw = At(cx, cy, st, -rad * 0.16f);
        p.Circle(cw.X, cw.Y, MathF.Max(1.6f, rad * 0.03f), sec);
        p.Circle(cx, cy, MathF.Max(1.6f, rad * 0.025f), sec);
    }

    // Station: a Swiss railway clock - the dial in 'face', bold bars and wide hands in 'ink', the red second hand with its disc
    public static void Station(Painter p, float cx, float cy, float rad, float ht, float mt, float st, uint face, uint ink, uint sec, bool minuteBars)
    {
        p.Circle(cx, cy, rad, face);
        for (int i = 0; i < 60; i++)
        {
            bool hour = i % 5 == 0;
            if (!hour && !minuteBars) continue;
            var a = At(cx, cy, i / 60f, hour ? rad * 0.66f : rad * 0.83f);
            var b = At(cx, cy, i / 60f, rad * 0.93f);
            p.Line(a.X, a.Y, b.X, b.Y, ink, hour ? MathF.Max(2.5f, rad * 0.075f) : MathF.Max(1, rad * 0.024f));
        }
        Taper(p, cx, cy, ht, -rad * 0.2f, rad * 0.62f, rad * 0.11f, rad * 0.08f, ink);
        Taper(p, cx, cy, mt, -rad * 0.2f, rad * 0.9f, rad * 0.085f, rad * 0.058f, ink);
        var tip = At(cx, cy, st, rad * 0.6f);
        Hand(p, cx, cy, st, -rad * 0.3f, rad * 0.6f, sec, MathF.Max(1.2f, rad * 0.024f));
        p.Circle(tip.X, tip.Y, MathF.Max(3, rad * 0.095f), sec);
        p.Circle(cx, cy, MathF.Max(1.8f, rad * 0.035f), sec);
    }

    // a hand that narrows from its tail to its tip (flat ends)
    static void Taper(Painter p, float cx, float cy, float turns, float from, float to, float w0, float w1, uint c)
    {
        float a = turns * MathF.Tau, dx = MathF.Sin(a), dy = -MathF.Cos(a), nx = -dy, ny = dx;
        p.Polygon(new[]
        {
            new Vector2(cx + dx * from + nx * w0 / 2, cy + dy * from + ny * w0 / 2), new Vector2(cx + dx * to + nx * w1 / 2, cy + dy * to + ny * w1 / 2),
            new Vector2(cx + dx * to - nx * w1 / 2, cy + dy * to - ny * w1 / 2), new Vector2(cx + dx * from - nx * w0 / 2, cy + dy * from - ny * w0 / 2),
        }, c);
    }

    // ---- seven segments ----
    // a top, b top right, c bottom right, d bottom, e bottom left, f top left, g middle: a bit each, for 0..9
    static readonly byte[] SegMask = { 0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F };
    const float SegW = 0.5f, SegSp = 0.13f, SegSlant = 0.08f, SegT = 0.095f;     // of the digits' height

    // how wide 'text' is in segments 'h' tall (digits, spaces = an unlit digit, anything else a colon)
    public static float SegWidth(string text, float h)
    {
        float w = 0;
        foreach (char ch in text) w += (char.IsDigit(ch) || ch == ' ' ? SegW : SegT * 1.4f) * h + SegSp * h;
        return w - SegSp * h + SegSlant * h;
    }

    // 'text' as an LED clock's digits from (x, y), 'h' tall and leaning a little: lit segments in 'on', the others in 'off' (0: left
    // out). Returns the width.
    public static float SegText(ClockGeo g, string text, float x, float y, float h, uint on, uint off) => SegText(g, text, null, 1, x, y, h, on, off);

    static readonly float[] ColonAt = { 0.31f, 0.69f };

    static int SegBits(char ch) => char.IsDigit(ch) ? SegMask[ch - '0'] : 0;

    // the same, changing from 'from' (as long, or null): a segment that lights up / goes dark is k of the way there
    public static float SegText(ClockGeo g, string text, string from, float k, float x, float y, float h, uint on, uint off)
    {
        float t = MathF.Max(1.5f, SegT * h), dw = SegW * h, e = t / 2 + MathF.Max(0.7f, t * 0.16f);
        float x0 = x;
        if (from == null || from.Length != text.Length) k = 1;
        float X(float px, float py) => px + (y + h - py) * SegSlant;
        void Seg(float ax, float ay, float bx, float by, int m, int m0, int bit)
        {
            bool lit = (m & bit) != 0, was = (m0 & bit) != 0;
            uint c = lit ? on : off;
            if (lit != was && k < 1)
                c = off == 0 ? Theme.WithAlpha(on, lit ? k : 1 - k) : lit ? Theme.Mix(off, on, k) : Theme.Mix(on, off, k);
            if (c != 0) g.Pointed(X(ax, ay), ay, X(bx, by), by, c, t);
        }
        for (int ci = 0; ci < text.Length; ci++)
        {
            char ch = text[ci];
            if (char.IsDigit(ch) || ch == ' ')
            {
                int m = SegBits(ch), m0 = k < 1 ? SegBits(from[ci]) : m;
                float xl = x + t / 2, xr = x + dw - t / 2, yt = y + t / 2, ym = y + h / 2, yb = y + h - t / 2;
                Seg(xl + e, yt, xr - e, yt, m, m0, 1);
                Seg(xr, yt + e, xr, ym - e, m, m0, 2);
                Seg(xr, ym + e, xr, yb - e, m, m0, 4);
                Seg(xl + e, yb, xr - e, yb, m, m0, 8);
                Seg(xl, ym + e, xl, yb - e, m, m0, 16);
                Seg(xl, yt + e, xl, ym - e, m, m0, 32);
                Seg(xl + e, ym, xr - e, ym, m, m0, 64);
                x += dw + SegSp * h;
            }
            else
            {
                float cx = x + t * 0.7f, d = t * 0.22f;
                foreach (float f in ColonAt)
                {
                    float cy = y + h * f;
                    g.Pointed(X(cx - d, cy), cy, X(cx + d, cy), cy, on, t);
                }
                x += SegT * 1.4f * h + SegSp * h;
            }
        }
        return x - x0 - SegSp * h + SegSlant * h;
    }

    // ---- the editor's chips: a small picture of a design on the widget's look (10:09, a watch advert's time) ----
    public static void Preview(Painter p, RectangleF r, string kind, string id, in Bento.Look L, uint secColour)
    {
        float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
        uint mid = Theme.Mix(L.Fill0, L.Fill1, 0.5f);
        if (id == "squircle")
        {
            bool apple = L.Style == 0;
            p.FillSquircle(r, apple ? 0xFF262626 : L.Fill0, apple ? 0xFF101010 : L.Fill1, apple ? 90 : L.Angle, apple ? 1 : L.Span);
            uint ink = apple ? 0xFFF8F8F8 : L.Text;
            float s = r.Width / 2;
            for (int i = 0; i < 12; i++) Hand(p, cx, cy, i / 12f, s * 0.68f, s * 0.86f, ink, 1.2f);
            Hand(p, cx, cy, 10.15f / 12, 0, s * 0.42f, ink, 2.4f);
            Hand(p, cx, cy, 9 / 60f, 0, s * 0.72f, ink, 1.8f);
            Hand(p, cx, cy, 0.5f, -s * 0.1f, s * 0.75f, apple ? 0xFFFF8C32 : secColour, 1);
            return;
        }
        p.FillGradient(r, L.Fill0, L.Fill1, 6f, L.Angle, L.Span);       // 6f: an int would pick the three-stop overload
        if (L.Border != 0 && L.BorderW > 0) p.Stroke(RectangleF.Inflate(r, -0.5f, -0.5f), L.Border, 1, 5.5f);
        float rad = MathF.Min(r.Width, r.Height) / 2 - 3;
        const float Ht = 10.15f / 12, Mt = 9 / 60f, St = 0.5f;
        switch (id)
        {
            case "classic":
                p.Ring(cx, cy, rad * 0.94f, L.Track, 1.2f);
                for (int i = 0; i < 12; i++)
                {
                    var a = At(cx, cy, i / 12f, rad * 0.74f);
                    p.Circle(a.X, a.Y, i % 3 == 0 ? 1.4f : 0.9f, i % 3 == 0 ? L.Text : L.Sub);
                }
                Hand(p, cx, cy, Ht, 0, rad * 0.46f, L.Text, 2.4f);
                Hand(p, cx, cy, Mt, 0, rad * 0.66f, L.Text, 1.6f);
                var sat = At(cx, cy, St, rad * 0.94f);
                p.Circle(sat.X, sat.Y, 2, secColour);
                return;
            case "minimal": Minimal(p, cx, cy, rad, Ht, Mt, St, L.Text, secColour, Dial(L.Track)); return;
            case "roman": Roman(p, cx, cy, rad, Ht, Mt, St, L.Text, L.Sub, secColour, false); return;
            case "station":
                Station(p, cx, cy, rad, Ht, Mt, St, L.Text | 0xFF000000, Away(mid | 0xFF000000, L.Text | 0xFF000000, 7), Away(StationRed, L.Text | 0xFF000000, 3), false);
                return;
        }
        // the time's designs
        var g = ClockGeo.For(p);
        float pad = 4, iw = r.Width - 2 * pad, x = r.X + pad;
        switch (id)
        {
            case "flip":
            {
                bool light = Bento.Lum(L.Text) > 0.4f;
                uint fill = (mid >> 24) < 0xF0 ? 0x52000000 : light ? Theme.Mix(mid, 0xFF000000, 0.42f) : L.Tile;
                float cw = (iw - 2) / 2, ch = MathF.Min(r.Height - 10, cw * 1.05f), y = cy - ch / 2;
                for (int i = 0; i < 2; i++)
                {
                    var c = new RectangleF(x + i * (cw + 2), y, cw, ch);
                    p.Fill(c, fill, 2.5f);
                    p.Text(i == 0 ? "10" : "09", c, L.Text, 8.5f, FontWeight.Bold, TextAlignment.Center, ParagraphAlignment.Center, Theme.Display);
                    p.Fill(new RectangleF(c.X, cy - 0.5f, c.Width, 1), (mid >> 24) < 0xF0 ? 0x8C000000 : mid);
                }
                return;
            }
            case "outline":
            {
                const string F = WristPanel.OutlineFont;
                const FontWeight Wt = WristPanel.OutlineWeight;
                float size = 100 * iw / g.OutlineWidth("10", 100, Wt, F);
                size = MathF.Min(size, (r.Height - 8) / 0.74f);
                float w = g.OutlineWidth("10", size, Wt, F);
                g.Outline("10", cx - w / 2, cy + size * 0.36f, size, Wt, F, L.Text, 1);
                return;
            }
            case "stacked":
                p.Text("10", new RectangleF(x, cy - 13, iw, 13), L.Text, 11.5f, FontWeight.Bold, TextAlignment.Center, ParagraphAlignment.Center, Theme.Display);
                p.Text("09", new RectangleF(x, cy, iw, 13), L.AccentText, 11.5f, FontWeight.Bold, TextAlignment.Center, ParagraphAlignment.Center, Theme.Display);
                return;
            case "segment":
            {
                float h = MathF.Min(r.Height - 14, iw / SegWidth("00:00", 1));
                float w = SegWidth("10:09", h);
                SegText(g, "10:09", cx - w / 2, cy - h / 2, h, L.Accent, L.Track);
                return;
            }
            case "words":
            {
                // rows of words, two of them lit
                float[][] rows = { new[] { 0.2f, 0.18f, 0.42f }, new[] { 0.5f, 0.38f }, new[] { 0.28f, 0.3f, 0.3f }, new[] { 0.45f, 0.4f } };
                bool[][] lit = { new[] { true, true, false }, new[] { false, true }, new[] { false, false, true }, new[] { false, false } };
                float y = cy - 11;
                for (int row = 0; row < rows.Length; row++, y += 6)
                {
                    float xx = x;
                    for (int i = 0; i < rows[row].Length; i++)
                    {
                        float ww = rows[row][i] * iw;
                        p.Fill(new RectangleF(xx, y, ww - 2, 2.6f), lit[row][i] ? L.Text : Theme.Mix(mid | 0xFF000000, L.Text, 0.28f), 1.3f);
                        xx += ww + 1;
                    }
                }
                return;
            }
            default:
                p.Text("10:09", RectangleF.Inflate(r, -2, 0), L.Text, MathF.Min(11.5f, r.Width * 0.3f), FontWeight.Bold, TextAlignment.Center,
                    ParagraphAlignment.Center, Theme.Display);
                return;
        }
    }
}
