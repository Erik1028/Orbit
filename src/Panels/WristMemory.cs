using System;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using Vortice.DirectWrite;

namespace Orbit;

// On this day ("memory"; the photos: Sys/PhotoMemories.cs): a VRChat photo of this day a year ago (or two, or more) filling its card,
// "1 year ago" over it (the nearest day's: "1 year ago · Oct 16"). Every style draws it with the latest photo's views (WristPhoto.cs and
// the skins' WristPhoto.*.cs: the card, the HUD frame, Imaging, the Picture and Fax Viewer, the Slide Show gadget and Photo Viewer, the
// Scrapbook, the characters) on its own set of photos - the opened view's photo big with its date (and year), world and people, the
// stick or the arrows stepping through that day's photos and the other years, Open in VR Gallery. Here: its set, its words, the 1x1's
// years badge.
public sealed partial class WristPanel
{
    public const string MemoryKindIcon = "";         // Segoe's history (a clock turning back): the widget in the editor's palette

    readonly PhotoSet _memory = new()
    {
        Memory = true, Cap = new[] { "On this day", "This day" }, CapHud = new[] { "On this day", "Archive" }, None = "No photos from this day",
        Hint = "Photos from this day in earlier years show up here.", Tag = "On this day", TitleXp = "On This Day", TitleMac = "On This Day",
    };

    // --snapshot's hooks (On this day's set)
    public string MemoryWidgetShown => _memory.WidgetShown;
    public string MemoryOpenShown => _memory.OpenShown;
    public bool MemoryEmptyShown => _memory.EmptyShown;
    public int MemoryOpenIndex => _memory.Index;
    public string MemoryAgoShown => _memory.New.Ago;

    // "October 16, 2025" / "2025. október 16.": the culture's long date without its weekday (the pattern made once per culture)
    static CultureInfo _mdCulture;
    static string _mdPattern, _mdShort;

    static void MemoryPatterns(CultureInfo c)
    {
        if (ReferenceEquals(c, _mdCulture)) return;
        _mdCulture = c;
        _mdPattern = Regex.Replace(c.DateTimeFormat.LongDatePattern, @"[\s,]*dddd[\s,]*", " ").Trim(' ', ',');
        _mdShort = c.DateTimeFormat.MonthDayPattern.Replace("MMMM", "MMM");
    }

    static string MemoryDate(DateTime t, CultureInfo c)
    {
        MemoryPatterns(c);
        return Capital(t.ToString(_mdPattern, c));
    }

    // "1 year ago", "3 years ago" (short: "1 yr ago"); a photo of the nearest day says which ("1 year ago · Oct 16")
    static string MemoryAgo(PhotoWatcher.Photo p, CultureInfo c, bool brief)
    {
        string s = p.Years == 1 ? Lang.T(brief ? "1 yr ago" : "1 year ago") : Lang.F(brief ? "{0} yrs ago" : "{0} years ago", p.Years);
        if (brief || !p.OtherDay) return s;
        MemoryPatterns(c);
        return s + " · " + Capital(p.Taken.ToString(_mdShort, c));
    }

    // a 1x1's badge: a dark pill at the foot of the photo, how many years ago (its short form where the whole does not fit)
    void MemoryBadge(RectangleF r, PhotoWords w)
    {
        float pad = 8, room = r.Width - 2 * pad - 16;
        string s = _p.MeasureWidth(w.Ago, CapSize, FontWeight.SemiBold) <= room ? w.Ago : PhotoCut(w.AgoShort, room, CapSize);
        float tw = _p.MeasureWidth(s, CapSize, FontWeight.SemiBold);
        var pill = new RectangleF(r.X + pad, r.Bottom - pad - 24, tw + 16, 24);
        _p.Fill(pill, 0xB0000000, 12);
        TextAt(s, pill.X + 8, pill.Bottom - 7, tw + 4, 0xFFFFFFFF, CapSize, FontWeight.SemiBold);
    }

    // Cyber's: a dark strip at the foot of the frame, the years in cyan monospace
    void CyMemoryBadge(RectangleF f, PhotoWords w)
    {
        string s = Upper(w.AgoShort);
        float tw = CyMonoW(s);
        if (tw > f.Width - 12) return;
        var strip = new RectangleF(f.X, f.Bottom - 22, f.Width, 22);
        _p.Fill(strip, 0xD90B0A0C);
        _p.Fill(new RectangleF(strip.X, strip.Y, strip.Width, 1), CyberHud.Edge);
        CyMono(s, f.X + 6, f.Bottom - 7, tw + 2, CyC);
    }
}
