using System;
using System.Collections.Generic;
using System.Linq;

namespace Orbit;

// The folder widget (Erik, 10-17: "ennyi dologhoz kell egy mappa widget is"): a "folder" on a page holds widgets of any kind - the
// kinds made after it too, nothing here lists them - on its own grid, GridSpec.Folder: 6 x 3 cells of the page's own size under the
// row its name takes when it is open. Its name (WatchWidget.Name) and its widgets (Children) are written only once set, so a config
// without folders saves exactly as before. No folder in a folder. Drawn by WristFolder*.cs, edited in SettingsView.Folder.cs.
public static class WatchFolders
{
    public const string Icon = "\uE8B7";              // Segoe's folder: the palette's, the closed folder's mark
    public const int MaxName = 40;
    static readonly List<WatchWidget> _none = new();

    public static bool Is(WatchWidget w) => w?.Type == "folder";

    // its widgets as they are drawn (none: an empty list, never null - do not add to it)
    public static List<WatchWidget> Kids(WatchWidget f) => f?.Children ?? _none;

    // its name as the screens write it: its own, else "Folder" in the language picked
    public static string Name(WatchWidget f) => string.IsNullOrWhiteSpace(f?.Name) ? Lang.T("Folder") : f.Name;

    // a name as it is kept: no control characters, at most MaxName long, empty = none (null: "Folder")
    public static string CleanName(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var sb = new System.Text.StringBuilder();
        foreach (char c in s.Trim()) if (!char.IsControl(c)) sb.Append(c);
        string n = sb.ToString().Trim();
        if (n.Length > MaxName) n = n[..MaxName].TrimEnd();
        if (n.Length > 0 && char.IsHighSurrogate(n[^1])) n = n[..^1];
        return n.Length == 0 ? null : n;
    }

    // is this kind among these widgets (a folder's: one level, folders hold no folders)
    public static bool Has(List<WatchWidget> list, string type)
    {
        foreach (var w in list) if (w?.Type == type) return true;
        return false;
    }

    // The folder's rules on its widgets (a hand-edited config, an imported page): none of them a folder, each inside its grid in a size it
    // offers, no overlap (WatchWidgets.Clean on the folder's grid - a kind this version does not know is kept, as on a page)
    public static List<WatchWidget> CleanChildren(List<WatchWidget> kids)
    {
        var l = new List<WatchWidget>(kids.Count);
        foreach (var c in kids) if (c != null && c.Type != "folder") l.Add(c);
        return WatchWidgets.Clean(l, GridSpec.Folder);
    }

    // the folder on this page that holds w (null: none - w is on the page itself, or nowhere)
    public static WatchWidget Holding(List<WatchWidget> page, WatchWidget w)
    {
        if (page == null || w == null) return null;
        foreach (var f in page)
            if (f?.Children != null && f.Children.Contains(w)) return f;
        return null;
    }

    // Puts w into the folder: its own size where it fits first (else the nearest, then the others from the smallest), the first free
    // spot. False (nothing changed) when there is no room in it.
    public static bool MoveIn(List<WatchWidget> page, WatchWidget folder, WatchWidget w, bool classic)
    {
        if (!Is(folder) || w == null || w == folder || Is(w)) return false;
        var kids = folder.Children ?? new List<WatchWidget>();
        if (!Place(kids, w, GridSpec.Folder, classic, -1, -1)) return false;
        folder.Children = kids;
        page.Remove(w);
        return true;
    }

    // Takes w out of the folder onto the page (its grid): the size the page offers nearest to its own, at (prefX, prefY) when it fits
    // there, else the first free spot. False (nothing changed) when the page has no room for it.
    public static bool MoveOut(List<WatchWidget> page, GridSpec grid, WatchWidget folder, WatchWidget w, bool classic, int prefX = -1, int prefY = -1)
    {
        if (folder?.Children == null || !folder.Children.Contains(w)) return false;
        if (!Place(page, w, grid, classic, prefX, prefY)) return false;
        folder.Children.Remove(w);
        return true;
    }

    // A folder removed from its page: keep = its widgets go onto the page where they fit (the first where the folder was), else they
    // go with it. Returns how many of them did not fit (and are gone).
    public static int Remove(List<WatchWidget> page, GridSpec grid, WatchWidget folder, bool keep, bool classic)
    {
        if (!page.Remove(folder) || !keep) return 0;
        int lost = 0;
        bool first = true;
        foreach (var c in Kids(folder).OrderBy(c => c.Y).ThenBy(c => c.X).ToList())
        {
            if (!Place(page, c, grid, classic, first ? folder.X : -1, first ? folder.Y : -1)) lost++;
            first = false;
        }
        folder.Children = null;
        return lost;
    }

    // w added to list (a page's, a folder's) where it fits on that grid; its look, design and buttons go with it
    static bool Place(List<WatchWidget> list, WatchWidget w, GridSpec grid, bool classic, int prefX, int prefY)
    {
        var sizes = new List<(int W, int H)>();
        var k = WatchWidgets.Find(w.Type);
        if (k == null) sizes.Add((w.W, w.H));              // a kind this version does not know: as it is
        else
        {
            var offered = WatchWidgets.SizesOn(k, grid, classic);
            if (Array.IndexOf(offered, (w.W, w.H)) >= 0) sizes.Add((w.W, w.H));
            var near = WatchWidgets.SnapSize(k, w.W, w.H, classic, grid);
            if (!sizes.Contains(near)) sizes.Add(near);
            foreach (var s in offered.OrderBy(s => s.W * s.H).ThenBy(s => s.W)) if (!sizes.Contains(s)) sizes.Add(s);
        }
        if (prefX >= 0)
            foreach (var (sw, sh) in sizes)
                if (WatchWidgets.Free(list, w, prefX, prefY, sw, sh, grid)) { Put(list, w, prefX, prefY, sw, sh); return true; }
        foreach (var (sw, sh) in sizes)
            for (int y = 0; y < grid.Rows; y++)
                for (int x = 0; x < grid.Cols; x++)
                    if (WatchWidgets.Free(list, w, x, y, sw, sh, grid)) { Put(list, w, x, y, sw, sh); return true; }
        return false;
    }

    static void Put(List<WatchWidget> list, WatchWidget w, int x, int y, int sw, int sh)
    {
        (w.X, w.Y, w.W, w.H) = (x, y, sw, sh);
        if (!list.Contains(w)) list.Add(w);
    }
}
