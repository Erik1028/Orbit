using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Numerics;
using Vortice.DirectWrite;

namespace Orbit;

// The notifications widget opened (Erik 10-16: it could jump out into a big widget too): a tap on its card grows it into the watch's
// opened view - every notification the app keeps (NotificationHistory: the last 20 shown, newest first, nothing written to disk),
// each with where it came from, its title and text (two lines at most, cut), when it came and how long ago; the stick scrolls the
// list; Clear empties Orbit's own list (the widget's too), nothing else. Its X (or a tap beside it; the retro styles' OK) closes it.
// Modern: a Bento card per notification - the app as a chip in its colour. Cyber: plates with cut corners under a yellow INCOMING
// tag, a number and a red chip each, the times in monospace; a new one scans in (Full). The retro styles: a 95 window (the skin
// draws it) with a list box of two-line rows under column headers (From, Message, Received), how many at the bottom, Clear beside
// OK; the Terminal its log (WristPanel.Term.cs).
public sealed partial class WristPanel
{
    // a row of the opened view: its app (null: none said), title, text, when
    readonly record struct NoteRow(string Chip, string Title, string Text, DateTime At);

    readonly List<NoteRow> _noteOpen = new();
    NotificationHistory _noteOpenOf;
    int _noteOpenVersion = -1;
    float _notesScroll, _notesMax;               // modern / Cyber: px scrolled from the list's top, how far it goes
    int _notesTop;                               // the Terminal's log: the first entry shown
    bool _notesEmptyDrawn;
    string _notesFirst;                          // the first row drawn (--snapshot: newest first)
    const string NotesList95 = "wn95list";
    const string BellGlyph = "";           // the widget's own (Segoe's ringer)
    const string ClearGlyph = "";
    const string NotesHint = "Notifications from VRCX and other tools appear here.";

    // the rows, made again only when the list changed (a new one, Clear) - every one the app keeps
    List<NoteRow> NotesOpen(PanelData d)
    {
        var h = d.Notes;
        if (ReferenceEquals(h, _noteOpenOf) && (h?.Version ?? -1) == _noteOpenVersion) return _noteOpen;
        _noteOpenOf = h;
        _noteOpenVersion = h?.Version ?? -1;
        _noteOpen.Clear();
        if (h == null) return _noteOpen;
        foreach (var it in h.Items)
        {
            string t = Tidy(it.Title), c = Tidy(it.Content), s = Tidy(it.Source);
            if (t.Length == 0 && c.Length == 0) continue;
            _noteOpen.Add(new NoteRow(s.Length == 0 ? null : s, t, c, it.At));
        }
        return _noteOpen;
    }

    static string Tidy(string s) => string.IsNullOrWhiteSpace(s) ? "" : s.Replace('\r', ' ').Replace('\n', ' ').Trim();

    // opened: from the top of the list
    void NotesFromTop()
    {
        _notesScroll = 0;
        _notesTop = 0;
        _ui.ListScrollTo95(NotesList95, 0);
        _cyNoteSeen = -10;
    }

    // Clear: Orbit's own list (the widget's too); the notifications in the view stay where they were shown
    void NotesClear(PanelData d)
    {
        d.Notes?.Clear();
        NotesFromTop();
        _dirty = true;
    }

    // "14:20 · 3 m": when it came, how long ago
    string NoteWhen(DateTime at, DateTime now) => ShortClock(at) + " · " + Age(at, now);

    // how many, said as the 95 windows' status does ("8 notifications")
    static string NotesCount(int n) => n == 1 ? Lang.T("1 notification") : Lang.F("{0} notifications", n);

    // the title over the text, each cut to w; one of them alone takes both lines (two: the second line is its too; a text alone in
    // the text's weight, wt)
    (string a, string b, bool two, FontWeight wt) NoteLines(NoteRow e, float w, float ts, float cs)
    {
        if (e.Title.Length > 0 && e.Text.Length > 0)
            return (Ellipsize(e.Title, w, ts, FontWeight.SemiBold), Ellipsize(e.Text, w, cs, FontWeight.Normal), false, FontWeight.SemiBold);
        var wt = e.Title.Length > 0 ? FontWeight.SemiBold : FontWeight.Normal;
        var (a, b) = TwoLines(e.Title.Length > 0 ? e.Title : e.Text, w, ts, wt);
        return (a, b, true, wt);
    }

    // ================= Modern =================

    const float NoteH = 92, NoteGap = 10, NoteNotch = 34;        // a card, the gap between two, the stick's notch (px)

    // E: the opened card. Its heading (the widget's name) and the X are DrawDetail's: the count after the name, Clear left of the X,
    // the cards under them
    void NotesDetail(RectangleF E, PanelData d)
    {
        if (Theme.Cyber) { CyberNotesDetail(E, d); return; }
        var rows = NotesOpen(d);
        int n = rows.Count;
        var now = DateTime.Now;
        if (n > 0)
        {
            float nx = E.X + 24 + _p.MeasureWidth(Lang.T(WatchWidgets.Find("notifications").Name), 24, FontWeight.SemiBold) + 12;
            TextAt(n.ToString(CultureInfo.CurrentCulture), nx, E.Y + 45, 60, Sub, 24, FontWeight.SemiBold);
        }
        if (Press("wnclr", NotesClearRect(E), ClearGlyph, "Clear", false, n > 0, 18)) NotesClear(d);
        var L = new RectangleF(E.X + 24, E.Y + 74, E.Width - 48, E.Height - 74 - 16);
        _notesEmptyDrawn = n == 0;
        _notesFirst = n > 0 ? rows[0].Title : null;
        if (n == 0) { _notesMax = 0; NotesEmpty(L); return; }
        float pitch = NoteH + NoteGap, content = n * pitch - NoteGap, max = MathF.Max(0, content - L.Height);
        _notesMax = max;
        if (_ui.Wheel != 0 && !_ui.Inert) { _notesScroll -= _ui.Wheel * NoteNotch; _dirty = true; }
        _notesScroll = Math.Clamp(_notesScroll, 0, max);
        float rw = max > 0 ? L.Width - 14 : L.Width;
        _p.PushClip(L);
        for (int i = 0; i < n; i++)
        {
            float y = L.Y - _notesScroll + i * pitch;
            if (y + NoteH < L.Y || y > L.Bottom) continue;
            NoteCard(rows[i], new RectangleF(L.X, y, rw, NoteH), now);
        }
        _p.PopClip();
        if (max > 0)
        {
            // a thin scroll bar on the right (the room's)
            float bh = MathF.Max(24, L.Height * L.Height / content), sy = L.Y + (L.Height - bh) * (_notesScroll / max);
            _p.Fill(new RectangleF(L.Right - 4, sy, 4, bh), Theme.WithAlpha(Bento.Text, 0.3f), 2);
        }
    }

    // Clear: a tile with its label, left of the X (DrawDetail's, 46 px at the top right)
    RectangleF NotesClearRect(RectangleF E)
    {
        float lw = Theme.Cyber ? _p.MeasureWidth(Lang.T("Clear"), 17, FontWeight.SemiBold) : _p.MeasureWidth(Lang.T("Clear"), SmallText, FontWeight.SemiBold);
        float w = MathF.Ceiling(18 + 8 + lw + 34);
        return new RectangleF(E.Right - 22 - 46 - 12 - w, E.Y + 14, w, 46);
    }

    // a notification as a card: the app as a chip in its colour (the widget's initials have it), when at the right; the title,
    // the text under it
    void NoteCard(NoteRow e, RectangleF r, DateTime now)
    {
        _p.Fill(r, Bento.Tile, 14);
        if (Bento.Edge != 0) _p.Stroke(RectangleF.Inflate(r, -0.6f, -0.6f), Bento.Edge, Bento.EdgeWidth, 13.4f);
        float x = r.X + 16, right = r.Right - 16, w = right - x;
        string when = NoteWhen(e.At, now);
        float ww = _p.MeasureWidth(when, SmallText, FontWeight.SemiBold);
        TextAt(when, right - ww - 2, r.Y + 31, ww + 4, Sub, SmallText, FontWeight.SemiBold);
        if (e.Chip != null)
        {
            float room = w - ww - 20 - 18;
            string s = Upper(e.Chip);
            if (_p.MeasureTracked(s, CapSize, FontWeight.SemiBold, CapTrack) > room) s = Ellipsize(s, room - 4, CapSize, FontWeight.SemiBold);
            float cw = _p.MeasureTracked(s, CapSize, FontWeight.SemiBold, CapTrack) + 18;
            if (room > 30)
            {
                _p.Fill(new RectangleF(x, r.Y + 12, cw, 26), AvatarColor(e.Chip), 13);
                CapText(s, x + 9, r.Y + 30, cw, Bento.Dark);
            }
        }
        var (a, b, two, wt) = NoteLines(e, w, 20, 18);
        TextAt(a, x, r.Y + 62, w + 2, Bento.Text, 20, wt);
        TextAt(b, x, r.Y + 84, w + 2, two ? Bento.Text : Sub, two ? 20 : 18, two ? wt : FontWeight.Normal);
    }

    // nothing yet: the bell, the words, where they come from
    void NotesEmpty(RectangleF L)
    {
        float my = L.Y + (L.Height - 124) / 2;
        _p.Icon(BellGlyph, new RectangleF(L.X, my, L.Width, 52), Bento.Faint, 40);
        _p.Text(Ellipsize("No notifications", L.Width, 22, FontWeight.SemiBold), new RectangleF(L.X, my + 58, L.Width, 34), Bento.Text, 22, FontWeight.SemiBold,
            TextAlignment.Center);
        _p.Text(Ellipsize(NotesHint, L.Width, SmallText, FontWeight.Normal), new RectangleF(L.X, my + 94, L.Width, 28), Sub, SmallText, FontWeight.Normal,
            TextAlignment.Center);
    }

    // ================= Cyber =================

    const float CyPlateH = 84, CyPlateGap = 10;
    const double CyNoteInLen = 0.45;
    double _cyNoteInAt = -10;                    // a new one arrived while the view was open: its plate scans in
    DateTime _cyNoteNewest;
    string _cyNoteNewestTitle;
    double _cyNoteSeen = -10;                    // when the view was last drawn (the frames come 10 a second while it is open)

    // The INCOMING tag is DrawDetail's (it types in as the view opens), how many beside it in cyan, Clear left of the X; a plate per
    // notification (the newest first, its outline cyan): its number in yellow, the app in a red chip, when at the right (the time cyan,
    // how long ago grey), the title in the condensed capitals, the text grey
    void CyberNotesDetail(RectangleF E, PanelData d)
    {
        var rows = NotesOpen(d);
        int n = rows.Count;
        var now = DateTime.Now;
        CyFrame();
        float tagW = CyberHud.TagWidth(_p, Upper("Incoming"), 18);
        var clear = NotesClearRect(E);
        if (n > 0)
        {
            string cnt = Lang.F("{0} NEW", n);
            float room = clear.X - 14 - (E.X + 24 + tagW + 14);
            if (CyMonoW(cnt) <= room) CyMono(cnt, E.X + 24 + tagW + 14, E.Y + 23 + (26 + CyberHud.CapH(_p, CyNote)) / 2, room, CyC);
        }
        if (Press("wnclr", clear, ClearGlyph, "Clear", false, n > 0, 18)) NotesClear(d);
        // a new one while it was in view (drawn a moment ago, not just opened): its plate scans in, the others move down to make room
        bool seen = _cyT - _cyNoteSeen < 1;
        _cyNoteSeen = _cyT;
        if (n > 0 && (rows[0].At != _cyNoteNewest || !string.Equals(rows[0].Title, _cyNoteNewestTitle, StringComparison.Ordinal)))
        {
            if (seen && CyLiveFx && !_closing) _cyNoteInAt = _cyT;
            (_cyNoteNewest, _cyNoteNewestTitle) = (rows[0].At, rows[0].Title);
        }
        var L = new RectangleF(E.X + 24, E.Y + 74, E.Width - 48, E.Height - 74 - 16);
        _notesEmptyDrawn = n == 0;
        _notesFirst = n > 0 ? rows[0].Title : null;
        if (n == 0) { _notesMax = 0; CyNotesEmpty(L); return; }
        float pitch = CyPlateH + CyPlateGap, content = n * pitch - CyPlateGap, max = MathF.Max(0, content - L.Height);
        _notesMax = max;
        if (_ui.Wheel != 0 && !_ui.Inert) { _notesScroll -= _ui.Wheel * NoteNotch; _dirty = true; }
        _notesScroll = Math.Clamp(_notesScroll, 0, max);
        float rw = max > 0 ? L.Width - 14 : L.Width;
        double t = _cyT - _cyNoteInAt;
        float k = t >= 0 && t < CyNoteInLen && CyberFx.Full ? (float)(t / CyNoteInLen) : 1;
        if (k < 1) _cyBusy = true;
        float slide = (1 - CyberFx.EaseOut(k)) * pitch;
        _p.PushClip(L);
        for (int i = 0; i < n; i++)
        {
            float y = L.Y - _notesScroll + i * pitch - (i > 0 ? slide : 0);
            if (y + CyPlateH < L.Y || y > L.Bottom) continue;
            var p = new RectangleF(L.X, y, rw, CyPlateH);
            if (i == 0 && k < 1)
            {
                // the scan: what is above the line is there, the line runs down it with its trail
                float s = CyberFx.EaseInOut(k), sy = p.Y + (p.Height + 4) * s;
                _p.PushClip(RectangleF.FromLTRB(p.X - 2, p.Y - 2, p.Right + 2, sy));
                CyberNotePlate(0, rows[0], p, now);
                _p.PopClip();
                CyberFx.ScanH(_p, p.X, p.Right, sy, CyC, 1);
                continue;
            }
            CyberNotePlate(i, rows[i], p, now);
        }
        _p.PopClip();
        if (max > 0)
        {
            var track = new RectangleF(L.Right - 4, L.Y + 4, 4, L.Height - 8);
            _p.Fill(track, CyberHud.Track);
            float th = MathF.Max(16, track.Height * L.Height / content), ty = track.Y + (track.Height - th) * _notesScroll / max;
            _p.Fill(new RectangleF(track.X, ty, 4, th), CyC);
        }
    }

    void CyberNotePlate(int i, NoteRow e, RectangleF p, DateTime now)
    {
        CyberHud.CardShape(_p, p, CyberHud.Card2, i == 0 ? CyberHud.CyanDim : CyberHud.Edge);
        float x = p.X + 12, right = p.Right - 14, by = p.Y + 26;
        string idx = (i + 1).ToString("00", CultureInfo.InvariantCulture);
        CyMono(idx, x, by, 30, CyY, CyNote, FontWeight.Bold);
        float cx = x + CyMonoW("00", CyNote, FontWeight.Bold) + 10;
        // when: "18:20" in cyan, " · 3M" grey after it, at the right
        string age = " · " + Upper(Age(e.At, now)).Replace(" ", "");
        string time = ShortClock(e.At);
        float aw = CyMonoW(age), tw = CyMonoW(time);
        CyMono(age, right, by, aw + 2, CyG, CyNote, FontWeight.SemiBold, TextAlignment.Trailing);
        CyMono(time, right - aw, by, tw + 2, CyC, CyNote, FontWeight.SemiBold, TextAlignment.Trailing);
        if (e.Chip != null)
        {
            float room = right - aw - tw - 16 - cx;
            string s = Upper(e.Chip);
            if (CyMonoW(s) + 10 > room) s = Ellipsize(s, room - 10, CyNote, FontWeight.SemiBold, CyberHud.Mono);
            float cw = CyMonoW(s) + 10;
            if (room >= 40)
            {
                var cr = new RectangleF(cx, by - 15, cw, 21);
                _p.Stroke(RectangleF.Inflate(cr, -0.6f, -0.6f), CyR, 1.25f);
                CyMono(s, cx + 5, by, cw, CyR);
            }
        }
        float w = p.Right - 14 - x;
        if (e.Title.Length > 0 && e.Text.Length > 0)
        {
            TextAt(Ellipsize(Upper(e.Title), w, 21, FontWeight.Bold, Theme.Display), x, p.Y + 53, w + 2, CyW, 21, FontWeight.Bold, TextAlignment.Leading, Theme.Display);
            TextAt(Ellipsize(e.Text, w, 17, FontWeight.SemiBold, Theme.Display), x, p.Y + 75, w + 2, CyG, 17, FontWeight.SemiBold, TextAlignment.Leading, Theme.Display);
            return;
        }
        // one of them alone: over both lines, white
        var (a, b) = TwoLines(e.Title.Length > 0 ? e.Title : e.Text, w, 19, FontWeight.SemiBold);
        TextAt(a, x, p.Y + 53, w + 2, CyW, 19, FontWeight.SemiBold);
        TextAt(b, x, p.Y + 75, w + 2, CyW, 19, FontWeight.SemiBold);
    }

    // nothing yet: an empty plate in brackets, NO NOTIFICATIONS and where they come from in monospace
    void CyNotesEmpty(RectangleF L)
    {
        var p = new RectangleF(L.X, L.Y + (L.Height - 120) / 2, L.Width, 120);
        CyberHud.CardShape(_p, p, CyberHud.Card2, CyberHud.Line);
        CyberHud.Brackets(_p, p, CyY, 12, 7, 2);
        string a = Upper("No notifications"), b = Upper(NotesHint);
        CyMono(Ellipsize(a, p.Width - 40, 18, FontWeight.Bold, CyberHud.Mono), p.X + 20, p.Y + 56, p.Width - 38, CyG, 18, FontWeight.Bold, TextAlignment.Center);
        CyMono(Ellipsize(b, p.Width - 40, CyNote, FontWeight.SemiBold, CyberHud.Mono), p.X + 20, p.Y + 84, p.Width - 38, CyberHud.Faint, CyNote, FontWeight.SemiBold,
            TextAlignment.Center);
    }

    // ================= Windows 95 (every retro style but the Terminal: the skin draws the controls) =================

    const float NoteRow95 = 28, From95 = 66, When95 = 52;

    // An inbox: column headers (From, Message, Received), the notifications in a list box - two lines each: the app (its icon
    // before it), the title over the text, the time over how long ago - its scroll bar (the stick scrolls it); under it how many,
    // Clear beside OK. c: the window's inside (OK is the window's own, at the bottom right under it)
    void Notes95Detail(RectangleF c, PanelData d)
    {
        var rows = NotesOpen(d);
        int n = rows.Count;
        var now = DateTime.Now;
        bool icons = Icons95;
        var box = new RectangleF(c.X, c.Y + 16, c.Width, c.Height - 16);
        // the headers (the last one over the scroll bar too, the room view's)
        float wx0 = c.Right - 18 - When95;
        Head95(new RectangleF(c.X, c.Y, From95, 16), "From");
        Head95(new RectangleF(c.X + From95, c.Y, wx0 - c.X - From95, 16), "Message");
        Head95(new RectangleF(wx0, c.Y, c.Right - wx0, 16), "Received");
        _notesEmptyDrawn = n == 0;
        _notesFirst = n > 0 ? rows[0].Title : null;
        _ui.List95(NotesList95, box, n, -1, (i, r, on) =>
        {
            var e = rows[i];
            float fx = r.X + (icons ? 19 : 2), fw = c.X + From95 - 2 - fx;
            if (icons) W95Icons.Draw(_p, W95Icons.Id.Chat, r.X + 1, r.Y + 2);
            _ui.Text95(Fit(e.Chip ?? Lang.T("Notification"), fw), new RectangleF(fx, r.Y + 1, fw, 13), Blk95);
            float mx = c.X + From95 + 3, mw = wx0 - mx - 3;
            if (e.Title.Length > 0 && e.Text.Length > 0)
            {
                _ui.Text95(Fit(e.Title, mw, Ui.F95, true), new RectangleF(mx, r.Y + 1, mw, 13), Blk95, true);
                _ui.Text95(Fit(e.Text, mw), new RectangleF(mx, r.Y + 14, mw, 13), Blk95);
            }
            else
            {
                bool bold = e.Title.Length > 0;          // a title alone keeps its bold
                var (a, b) = Wrap95(e.Title.Length > 0 ? e.Title : e.Text, mw, bold);
                _ui.Text95(a, new RectangleF(mx, r.Y + 1, mw, 13), Blk95, bold);
                _ui.Text95(b, new RectangleF(mx, r.Y + 14, mw, 13), Blk95, bold);
            }
            // the last column is partly under the scroll bar when there is one: what is left of it
            float wx = wx0 + 3, ww = r.Right - wx - 1;
            _ui.Text95(Fit(ShortClock(e.At), ww), new RectangleF(wx, r.Y + 1, ww, 13), Blk95);
            _ui.Text95(Fit(Age(e.At, now), ww), new RectangleF(wx, r.Y + 14, ww, 13), Gry95);
        }, NoteRow95);
        if (n == 0)
        {
            _ui.Text95(Fit("No notifications", box.Width - 16), new RectangleF(box.X + 8, box.Y + 14, box.Width - 16, 13), Blk95, false, TextAlignment.Center);
            _ui.Text95(NotesHint, new RectangleF(box.X + 12, box.Y + 34, box.Width - 24, box.Height - 40), Gry95, false, TextAlignment.Center, true, true);
        }
        // the button row (the window's OK at its right): how many, Clear
        var clr = new RectangleF(c.Right - 75 - 6 - 75, c.Bottom + 5, 75, 23);
        if (n > 0) _ui.Text95(Fit(NotesCount(n), clr.X - c.X - 6), new RectangleF(c.X, c.Bottom + 10, clr.X - c.X - 6, 13), Blk95);
        if (_ui.Button95("wn95clr", clr, "&Clear", n > 0)) NotesClear(d);
    }

    // a text over two lines of the 95 font: as many words as fit on the first, the rest cut on the second
    (string, string) Wrap95(string s, float w, bool bold)
    {
        s = Lang.T(s);
        if (_ui.Width95(s, bold) <= w) return (s, "");
        int cut = -1;
        for (int i = s.IndexOf(' '); i > 0; i = s.IndexOf(' ', i + 1))
        {
            if (_ui.Width95(s[..i], bold) > w) break;
            cut = i;
        }
        if (cut < 0) return (Fit(s, w, Ui.F95, bold), "");
        return (s[..cut], Fit(s[(cut + 1)..].TrimStart(), w, Ui.F95, bold));
    }

    // ---- --snapshot's hooks ----

    // modern / Cyber: px scrolled (and how far it goes); 95: the list's first row; the Terminal: its first entry
    public float NotesScroll => !Theme.Classic ? _notesScroll : Theme.Retro == RetroStyle.Terminal ? _notesTop : _ui.ListTop95(NotesList95);
    public float NotesScrollMax => _notesMax;
    public bool NotesEmptyShown => _notesEmptyDrawn;
    public string NotesFirstShown => _notesFirst;

    // where a control of the watch is in a frame drawn now (texture px: the 95 canvas is shown 2x), found by its id; null: not drawn
    public Vector2? ControlPoint(PanelData d, string id)
    {
        RectangleF? at = null;
        _ui.HitSpy = (k, r) => { if (k == id) at = r; };
        try { Draw(d); }
        finally { _ui.HitSpy = null; }
        if (at is not { } a) return null;
        float s = Theme.Classic ? 2 : 1;
        return new Vector2((a.X + a.Width / 2) * s, (a.Y + a.Height / 2) * s);
    }
}
