using System;
using System.Collections.Generic;
using System.Drawing;
using T = Orbit.TerminalSkin;

namespace Orbit;

// The folder widget on the terminal: a directory. Closed, its box says DIR; inside, "ls -l" - the folder's name with a slash, then a line
// per widget in it (its short name, its value at the right where one is at hand), "+3" for what does not fit; one cell high, the name
// and how many. Opened, the whole tube: "cd" into it and "ls", the [X] key and Esc to go back up, the widgets on the screen under it as
// their own boxes.
public sealed partial class WristPanel
{
    void FolderTileTerm(WatchWidget w, RectangleF r, List<WatchWidget> kids, int n, PanelData d)
    {
        _tCards.Add(r);                     // where the tube shows on a see-through face (CustomTerm)
        var c = Tsk.WidgetFrame(_p, r, "DIR");
        string name = WatchFolders.Name(w);
        if (w.H < 2)
        {
            // the name with its slash, how many under it (wide: the first few names after it)
            float top = MathF.Floor(c.Y + (c.Height - 18) / 2);
            TCaps(TermCut(name, c.Width - 6, true), c.X + 3, top, T.INK);
            string sub = c.Width >= 90 ? TermNames(kids, c.Width - 6) : TermCut(TermFiles(n), c.Width - 6, false);
            TCaps(sub, c.X + 3, top + 11, T.DIM);
            return;
        }
        float y = c.Y + 2;
        TCaps(TermCut(name, c.Width - 6, true), c.X + 3, y, T.HOT);
        y += 12;
        if (n == 0) { TCaps("(EMPTY)", c.X + 3, y, T.DIM); return; }
        const float Row = 11;
        int lines = Math.Max(1, (int)((c.Bottom - y + 4) / Row)), shown = n <= lines ? n : lines - 1;
        int j = 0;
        foreach (var ch in kids)
        {
            var k = ch == null ? null : WatchWidgets.Find(ch.Type);
            if (k == null) continue;
            if (j >= shown) break;
            float ly = y + j * Row;
            string v = FolderValue(ch, d);
            float vw = v == null ? 0 : T.W(v) + 4;
            if (vw > c.Width * 0.5f) { v = null; vw = 0; }
            TCaps("-", c.X + 3, ly, T.DIM);
            TCaps(TermCut(TTitle(k.Id), c.Width - 18 - vw, false), c.X + 12, ly, T.INK);
            if (v != null) TCapsR(v, c.Right - 3, ly, T.MID);
            j++;
        }
        if (n > lines) TCaps(Plus(n - shown), c.X + 12, y + j * Row, T.DIM);
    }

    // the whole tube: the session's lines over the widgets - cd into the folder, ls - with the [X] key at the right and Esc (up)
    RectangleF FolderChromeTerm(string name, int n)
    {
        var full = new RectangleF(0, 0, TW95, TH95);
        _tCards.Add(full);
        _p.Fill(full, T.BG);
        _p.Bevel(full, Painter.Edge.Window);
        if (TKey("wf95x", new RectangleF(TW95 - 6 - 24, 6, 24, 13), "[X]")) CloseDetail();
        if (TKey("wf95up", new RectangleF(TW95 - 6 - 24 - 4 - 46, 6, 46, 13), "ESC UP")) CloseDetail();
        TCaps("$ CD", 6, 9, T.MID);
        TCaps(TermCut(name, TW95 - 112 - 36, true), 6 + T.W("$ CD "), 9, T.INK);
        TCaps("$ LS -L", 6, 22, T.MID);
        TCaps(TermFiles(n), 6 + T.W("$ LS -L "), 22, T.DIM);
        float head = FolderHead95;
        T.Dashes(_p, 4, TW95 - 4, head - 6, T.FAINT);
        return RectangleF.FromLTRB(4, head, TW95 - 4, TH95 - 4);
    }

    // "VRCHAT/" (dir: with its slash) or a short name, cut to w in the terminal's font - kept: a folder's name is the user's
    string TermCut(string s, float w, bool dir)
    {
        int how = dir ? 40 : 41;
        if (CutKept(s, w, 7, how, out var c)) return c;
        return CutKeep(s, w, 7, how, TFit(dir ? T.Up(Lang.T(s)) + "/" : T.Up(Lang.T(s)), w));
    }

    // "5 FILES"
    string TermFiles(int n)
    {
        FolderWords();
        if (n >= _fFiles.Length) return Lang.F("{0} FILES", n);
        return _fFiles[n] ??= n == 1 ? Lang.T("1 FILE") : Lang.F("{0} FILES", n);
    }
    readonly string[] _fFiles = new string[24];

    // the first names in it on one line ("TIME BATT FPS"), made when what is in it changes
    readonly Dictionary<WatchWidget, (int ver, float w, string s)> _fNames = new();
    string TermNames(List<WatchWidget> kids, float w)
    {
        int ver = Lang.Version * 4096 + kids.Count;
        foreach (var c in kids) ver = ver * 31 + (c?.Type?.GetHashCode() ?? 0);
        var key = kids.Count > 0 ? kids[0] : null;
        if (key != null && _fNames.TryGetValue(key, out var e) && e.ver == ver && e.w == w) return e.s;
        var sb = new System.Text.StringBuilder();
        foreach (var c in kids)
        {
            var k = c == null ? null : WatchWidgets.Find(c.Type);
            if (k == null) continue;
            string t = Lang.T(TTitle(k.Id));
            if (T.W(sb + (sb.Length > 0 ? " " : "") + t) > w) break;
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(t);
        }
        string s = sb.ToString();
        if (key != null)
        {
            if (_fNames.Count > 32) _fNames.Clear();
            _fNames[key] = (ver, w, s);
        }
        return s;
    }
}
