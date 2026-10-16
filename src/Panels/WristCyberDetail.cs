using System;
using System.Drawing;
using System.Globalization;
using Vortice.DirectWrite;

namespace Orbit;

// The opened views in the Cyber style that are the HUD's own designs, not the Modern layout in Cyber's colours (Erik 10-16: the
// opened clock more cyberpunk). Made of the face's Cyber parts (WristCyber.cs, WristCyberFaces.cs), so they belong to the preset
// faces' and the custom face's family.
//  - The clock (time, date, analog, time in VR): the CLOCK tag with the week and the day of the year beside it; the yellow time card
//    (CyberTime's hero: TIME, the zone, the digits with their split and slice, SEC, the date tag, the hazard stripes) and the HUD
//    dial in its segmented bezel (the hour so far) beside it; under them plates with cut corners - DATE (the date card's lines), DAY
//    (how much of today has gone, a segment an hour - the hours in VR cyan - and what is left of it), IN VR (how long, since when)
//    and TIMER (what is left, its segments) while they are on.
// Motion as on the face: the time card glitches on a new minute (Full); at rest only the seconds it shows draw it again.
public sealed partial class WristPanel
{
    // c: the opened card's inside (DrawDetail's; its tag and X are over it)
    void CyberClockDetail(RectangleF c, PanelData d, DateTime now)
    {
        var E = new RectangleF(c.X - 24, c.Y - 74, c.Width + 48, c.Height + 96);
        var culture = CultureInfo.CurrentCulture;
        // beside the CLOCK tag: the week, the day of the year
        float nx = E.X + 24 + CyberHud.TagWidth(_p, Upper("Clock"), 18) + 14, nr = E.Right - 22 - 46 - 14;
        string wk = Lang.F("Week {0} · day {1} of {2}", ISOWeek.GetWeekOfYear(now), now.DayOfYear, DateTime.IsLeapYear(now.Year) ? 366 : 365).ToUpper(culture);
        if (CyMonoW(wk) <= nr - nx) CyMono(wk, nx, E.Y + 23 + (26 + CyberHud.CapH(_p, CyNote)) / 2, nr - nx, CyG);
        // the time card and the dial
        const float topH = 194;
        var hero = new RectangleF(c.X, c.Y, MathF.Min(350, c.Width - topH - 16), topH);
        CyberTimeCard(hero);
        CyberTime(hero, _open, now, true);
        var box = new RectangleF(hero.Right + MathF.Max(16, (c.Right - hero.Right - topH) / 2), c.Y, topH, topH);
        float ra = topH / 2, bw = MathF.Max(4, MathF.Round(ra * 0.065f)), aw = MathF.Max(2, MathF.Round(ra * 0.03f * 2) / 2);
        CfBezel(box.X + ra, box.Y + ra, ra - 0.5f - bw, ra - 0.5f, now);
        CyberDialFace(box.X + ra, box.Y + ra, (ra - bw - 5 - aw / 2) / 1.07f, now, aw);
        CyberHud.Brackets(_p, RectangleF.Inflate(box, 6, 0), CyY, 12, 0, 2);
        // the plates: DATE, DAY, and IN VR / TIMER while they are on
        bool vr = d.VrSince != null, timing = _timer != TimerState.Idle;
        int n = 2 + (vr ? 1 : 0) + (timing ? 1 : 0), k = 0;
        float py = c.Y + topH + 14, gap = 12, pw = (c.Width - (n - 1) * gap) / n;
        RectangleF Next() => new(MathF.Round(c.X + k++ * (pw + gap)), py, MathF.Round(pw), c.Bottom - py);
        var dp = Next();
        CyPlate(dp, "Date", null, 0);
        CyDateBody(dp.X + 14, dp.Right - 14, dp.Y + 36, dp.Bottom - 14, dp.Width < 160, now);
        CyDayPlate(Next(), d, now, culture);
        if (vr) CyVrPlate(Next(), d.VrSince.Value, culture);
        if (timing) CyTimerPlate(Next());
    }

    // a plate under the clock: the raised fill with its corners cut, its tag at the top-left, a note at the top-right (monospace;
    // shortNote: its shorter form, where the whole does not fit)
    void CyPlate(RectangleF p, string tag, string note, uint noteC, string shortNote = null)
    {
        CyberHud.CardShape(_p, p, CyberHud.Card2, CyberHud.Edge);
        float tw = CyberHud.Tag(_p, p.X + CyTagX, p.Y + CyTagY, Upper(tag));
        if (note == null) return;
        float left = p.X + CyTagX + tw + 10, right = p.Right - 12;
        if (right - left < 30) return;
        string s = CyMonoW(note) <= right - left ? note : shortNote != null && CyMonoW(shortNote) <= right - left ? shortNote
            : Ellipsize(note, right - left, CyNote, FontWeight.SemiBold, CyberHud.Mono);
        CyMono(s, right, p.Y + CyBase, right - left, noteC, CyNote, FontWeight.SemiBold, TextAlignment.Trailing);
    }

    // DAY: how much of today has gone, big; a segment an hour under it (two a segment on a narrow plate) - yellow, the hours in VR
    // cyan; what is left of it as the note
    void CyDayPlate(RectangleF p, PanelData d, DateTime now, CultureInfo culture)
    {
        var left = TimeSpan.FromDays(1) - now.TimeOfDay;
        CyPlate(p, "Day", Lang.F("{0} left", $"{(int)left.TotalHours}:{left.Minutes:00}").ToUpper(culture), CyG);
        float f = (float)(now.TimeOfDay.TotalHours / 24);
        float x = p.X + 14, w = p.Width - 28;
        string pct = Pct((int)(f * 100));
        CyValue(x, p.Bottom - 30, pct, CyFit(pct, w, 34), CyW);
        var bar = new RectangleF(x, p.Bottom - 18, w, 6);
        int segs = p.Width >= 170 ? 24 : 12;
        if (d.VrSince is { } since && since < now)
        {
            float s0 = since.Date == now.Date ? (float)(since.TimeOfDay.TotalHours / 24) : 0;
            CyberHud.Segments(_p, bar, f, CyC, CyberHud.Track, segs, 2);
            if (s0 > 0) CyberHud.Segments(_p, bar, s0, CyY, 0, segs, 2);
        }
        else CyberHud.Segments(_p, bar, f, CyY, CyberHud.Track, segs, 2);
    }

    // IN VR: how long (to the second: the seconds the view draws anyway), since when as the note
    void CyVrPlate(RectangleF p, DateTime since, CultureInfo culture)
    {
        string at = ShortClock(since);
        CyPlate(p, "In VR", Lang.F("since {0}", at).ToUpper(culture), CyG, at.ToUpper(culture));
        var span = Now - since;
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        string v = $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}";
        CyValue(p.X + 14, p.Bottom - 30, v, CyFit(v, p.Width - 28, 34), CyC);
    }

    // TIMER: what is left (DONE in red once it is up), its segments; running / paused as the note
    void CyTimerPlate(RectangleF p)
    {
        double left = TimerLeft(Clock.Now);
        bool done = _timer == TimerState.Done;
        string note = _timer switch { TimerState.Running => "running", TimerState.Paused => "paused", _ => "time is up" };
        CyPlate(p, "Timer", Upper(note), done ? CyR : _timer == TimerState.Running ? CyC : CyG);
        string v = done ? Upper("Done") : Clock2(left);
        uint c = done ? CyWarn() : _timer == TimerState.Paused ? CyG : CyY;
        CyValue(p.X + 14, p.Bottom - 30, v, CyFit(v, p.Width - 28, 34), c);
        float frac = done ? 0 : (float)(left / Math.Max(1, Math.Max(_timerTotal, left)));
        CyberHud.Segments(_p, new RectangleF(p.X + 14, p.Bottom - 18, p.Width - 28, 6), frac, c, CyberHud.Track, 10);
    }

    // --snapshot: the timer as the TIMER plate shows it - started for that many minutes (0: back to idle)
    public void TimerForTest(int minutes)
    {
        if (minutes > 0) TimerPreset(minutes);
        else TimerReset();
    }
}
