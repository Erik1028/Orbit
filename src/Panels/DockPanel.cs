using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using Valve.VR;
using Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace Orbit;

// The faces of the dock's six keys as bitmaps, shared by the dock panel and the watch widget (one Painter,
// one device). Rebuilt only for the keys that changed.
public sealed class HexpadFaces : IDisposable
{
    public readonly ID2D1Bitmap[] Bitmaps = new ID2D1Bitmap[HexpadLink.Keys];
    readonly int[] _ver = new int[HexpadLink.Keys];
    public bool Any { get; private set; }

    public void Update(Painter p, HexpadLink.Snapshot s)
    {
        Any = false;
        for (int i = 0; i < HexpadLink.Keys; i++)
        {
            if (s.FaceVersion[i] != _ver[i])
            {
                _ver[i] = s.FaceVersion[i];
                Bitmaps[i]?.Dispose();
                Bitmaps[i] = null;
                var px = s.Faces[i];
                if (px != null && px.Length == s.FaceW * s.FaceH) Bitmaps[i] = p.CreateBitmap(px, s.FaceW, s.FaceH);
            }
            Any |= Bitmaps[i] != null;
        }
    }

    public void Dispose()
    {
        for (int i = 0; i < Bitmaps.Length; i++) { Bitmaps[i]?.Dispose(); Bitmaps[i] = null; }
    }
}

// The stream dock in VR: Hexpad's AKP03 laid out like the real one - six keys (what they show right now),
// a round button under each column, the big knob top right and the two small ones under it. The laser presses
// keys and buttons (press and release, so Hexpad's hold and double tap work), pushes knobs, and turns them
// (the stick over a knob, or the knob's left / right side). What could end the VR session (lock, sleep,
// empty the bin) asks first. Grab it once onto the real dock on your desk; it stays there.
public sealed class DockPanel : IPointerTarget, IDisposable
{
    public const int TW = 800, TH = 480, W95 = TW / 2, H95 = TH / 2;

    readonly Painter _p;
    readonly Ui _ui;
    readonly ConfigStore _cfg;
    readonly HexpadLink _link;
    readonly HexpadFaces _faces;
    readonly PointerState _ptr = new();
    public readonly Surface Surf;
    Surface _canvas95;
    public VrOverlay Ov { get; private set; }
    VrSession _vr;
    float _vis;
    bool _dirty = true;
    double _nextDraw;
    int _seen = -1;
    float _twoStartWidth;
    HexpadLink.Snapshot _s = new();

    // input
    Hand? _owner;
    string _hover, _pressed, _confirm, _scrollId;
    float _scrollAcc;
    double _repeatAt, _wokeAt = -10;
    readonly Dictionary<string, RectangleF> _hit = new();     // control -> rect in texture pixels
    readonly Dictionary<string, float> _spin = new();         // knob -> turned angle (it spins as you turn it)

    DockConfig C => _cfg.Data.Dock;
    public bool Shown => C.Enabled && C.Visible;

    public DockPanel(Gpu g, Painter p, ConfigStore cfg, HexpadLink link, HexpadFaces faces)
    {
        _p = p; _cfg = cfg; _link = link; _faces = faces;
        _ui = new Ui(p);
        Surf = new Surface(g, TW, TH);
    }

    public void CreateOverlay(VrSession vr)
    {
        _vr = vr;
        Ov = new VrOverlay("erik.orbit.dock", "Orbit Hexpad");
        Ov.SetSortOrder(135);
        Ov.SetFlag(VROverlayFlags.IsPremultiplied, true);
        Ov.SetMouseScale(TW, TH);
        _vis = 0;
        _dirty = true;
    }

    public void DestroyOverlay()
    {
        ReleasePress();
        _ptr.Leave();
        Ov?.Dispose();
        Ov = null;
        _vr = null;
        _vis = 0;
    }

    public void Toggle()
    {
        if (C.Visible) { C.Visible = false; _confirm = null; _cfg.MarkDirty(); return; }
        Open();
    }

    public void Open()
    {
        C.Visible = true;
        _cfg.MarkDirty();
        _dirty = true;
        if (C.Place.M == null) BringHere();             // else where it was put last (over the real dock)
    }

    // a desk console in front of you: 0.45 m ahead, 0.3 m below the eyes, facing up at you
    public void BringHere()
    {
        if (_vr == null || !_vr.HmdValid) return;
        var local = Matrix4x4.CreateRotationX(-35 * MathF.PI / 180) * Matrix4x4.CreateTranslation(0, -0.30f, -0.45f);
        C.Place.Attach = "world";
        C.Place.M = MathUtil.ToArray(local * MathUtil.GazeFrame(_vr.Hmd));
        _cfg.MarkDirty();
    }

    public void Update(VrSession vr, double now, float dt, HexpadLink.Snapshot s)
    {
        if (Ov == null) return;
        float target = Shown ? 1 : 0;
        _vis = Math.Clamp(_vis + Math.Sign(target - _vis) * dt / 0.18f, 0, 1);
        if (_vis <= 0 || C.Place.M == null || !Placement.Apply(C.Place, vr, Ov))
        {
            if (_vis <= 0) { ReleasePress(); _ptr.Leave(); _hover = null; }
            Ov.Hide();
            return;
        }
        float e = 1 - MathF.Pow(1 - _vis, 3);
        Ov.SetAlpha(e);
        Ov.SetWidth(Math.Clamp(C.Width, 0.15f, 0.9f) * (0.92f + 0.08f * e));
        if (s.Version != _seen) { _seen = s.Version; _s = s; _dirty = true; }
        // a knob side held down turns it again and again, like a 1995 spin button
        if (_pressed != null && _pressed.Contains(':') && now >= _repeatAt)
        {
            Turn(_pressed[..4], _pressed.EndsWith(":l") ? -1 : 1);
            _repeatAt = now + 0.12;
        }
        bool flashing = now - _s.EchoAt < 0.4;
        if (_dirty || flashing || now >= _nextDraw || _ui.Animating)
        {
            Draw(now);
            Ov.SetTexture(Surf.Tex);
            _dirty = _ui.Interacting || _ui.Changed;
            _nextDraw = now + 1;
        }
        Ov.Show();
    }

    // ---------------- input ----------------

    bool Live => _s.State == HexpadLink.LinkState.Ok;
    bool CanPress => Live && C.Input && _confirm == null;

    string HitTest(Vector2 px)
    {
        foreach (var (id, r) in _hit)
            if (r.Contains(px.X, px.Y)) return id;
        return null;
    }

    static string Base(string id) => id != null && id.Contains(':') ? id[..id.IndexOf(':')] : id;

    void Turn(string enc, int dir)
    {
        _link.Turn(enc, dir);
        _spin[enc] = (_spin.TryGetValue(enc, out var a) ? a : 0) + dir * 24;
        _dirty = true;
    }

    void Press(string id)
    {
        if (id.Contains(':'))
        {
            Turn(Base(id), id.EndsWith(":l") ? -1 : 1);
            _pressed = id;
            _repeatAt = Clock.Now + 0.45;
            return;
        }
        if (_s.Controls.TryGetValue(id, out var c) && c.Danger)
        {
            _confirm = id;               // nothing is sent until it is confirmed
            return;
        }
        _link.Input(id, true);
        _pressed = id;
    }

    void ReleasePress()
    {
        if (_pressed != null && !_pressed.Contains(':')) _link.Input(_pressed, false);
        _pressed = null;
    }

    public void PointerMove(Hand h, Vector2 px)
    {
        _owner ??= h;
        if (_owner != h) return;
        _ptr.Move(px.X, px.Y);
        string hov = CanPress ? HitTest(px) : null;
        if (hov != _hover) { _hover = hov; _dirty = true; }
        if (_ui.NeedsRedrawForMove(_ptr, Theme.Classic ? 2 : 1)) _dirty = true;
        // the dock's idle screen swallows the first press: wake it as soon as the laser arrives
        if (_s.Ambient && C.Input && Clock.Now - _wokeAt > 2)
        {
            _wokeAt = Clock.Now;
            _link.Wake();
        }
    }

    public void PointerLeave(Hand h)
    {
        if (_owner != h) return;
        _owner = null;
        ReleasePress();
        _ptr.Leave();
        _hover = null;
        _dirty = true;
    }

    public void PointerButton(Hand h, PtrButton b, bool down, Vector2 px)
    {
        if (b != PtrButton.Primary) return;
        if (down)
        {
            if (_owner != h && (_ptr.Down || _pressed != null)) return;
            _owner = h;
            _ptr.Move(px.X, px.Y);
            _ptr.Press();
            if (CanPress && HitTest(px) is { } id) Press(id);
        }
        else
        {
            if (_owner != h) return;
            _ptr.Move(px.X, px.Y);
            _ptr.Release();
            ReleasePress();
        }
        _dirty = true;
    }

    // the stick over a knob turns it, one detent per notch
    public void PointerScroll(Hand h, float notches, Vector2 px)
    {
        if (_owner != h || !CanPress) return;
        string id = Base(HitTest(px));
        if (id == null || !id.StartsWith("enc")) return;
        if (_scrollId != id) { _scrollId = id; _scrollAcc = 0; }
        _scrollAcc += notches;
        while (_scrollAcc >= 1) { Turn(id, 1); _scrollAcc -= 1; }
        while (_scrollAcc <= -1) { Turn(id, -1); _scrollAcc += 1; }
    }

    public VrOverlay Overlay => Ov;
    public bool Interactive => Ov != null && Ov.Visible && Shown && _vis > 0.8f;
    public bool AcceptsFrom(Hand h) => true;
    public Vector2 TextureSize => new(TW, TH);
    public bool Grabbable => true;
    public Matrix4x4 WorldTransform =>
        _vr != null && Placement.TryWorld(C.Place, _vr, out var w) ? w : MathUtil.FromArray(C.Place.M);
    public void GrabBegin(bool detach) { ReleasePress(); }
    public void GrabMove(Matrix4x4 world) { if (_vr != null) { Placement.SetWorld(C.Place, _vr, world); _cfg.MarkDirty(); } }
    public void GrabStickX(float v, float dt) { C.Width = Math.Clamp(C.Width * (1 + v * 0.9f * dt), 0.15f, 0.9f); _cfg.MarkDirty(); }
    public void GrabTwoHandBegin() => _twoStartWidth = C.Width;
    public void GrabTwoHandScale(float r) { C.Width = Math.Clamp(_twoStartWidth * r, 0.15f, 0.9f); _cfg.MarkDirty(); }
    public bool GrabSnapPreview => false;
    public void GrabEnd() { }

    // ---------------- drawing ----------------

    // without VR (--docktest): this snapshot, a pointer over a control, a pending confirmation
    public void DrawFor(HexpadLink.Snapshot s, double now, string hover = null, string pressed = null, string confirm = null)
    {
        _s = s; _hover = hover; _pressed = pressed; _confirm = confirm;
        Draw(now);
    }

    public void Draw(double now)
    {
        _hit.Clear();
        if (Theme.Classic) Draw95(now);
        else DrawModern(now);
    }

    string Where()
    {
        if (_s.InFolder)
            return _s.FolderPages > 1 ? $"{_s.FolderName} · {_s.FolderPage + 1}/{_s.FolderPages}" : _s.FolderName;
        return $"{_s.PageName} · {_s.Page + 1}/{_s.Pages}";
    }

    string StateLine() => _s.State switch
    {
        HexpadLink.LinkState.NotRunning => "Hexpad is not running",
        HexpadLink.LinkState.OldHexpad => "Update Hexpad to see the dock here",
        HexpadLink.LinkState.Unplugged => "The dock is unplugged",
        HexpadLink.LinkState.ScreenOff => "The dock's screen is off",
        _ => "Connecting to Hexpad...",
    };

    HexpadLink.Control Ctl(string id) => _s.Controls.TryGetValue(id, out var c) ? c : null;

    // "Volume: mute" -> "Mute": a knob's group / caption already says what it is
    static string Short(string summary)
    {
        if (string.IsNullOrEmpty(summary)) return "";
        int i = summary.IndexOf(": ", StringComparison.Ordinal);
        string s = i >= 0 ? summary[(i + 2)..] : summary;
        return s.Length > 0 ? char.ToUpperInvariant(s[0]) + s[1..] : s;
    }
    bool Flash(string id, double now) => now - _s.EchoAt < 0.4 && _s.Echo == id;

    // ---- modern: a dark slab like the device ----
    const float Pad = 20, Tile = 136, Gap = 12, KeysY = 66;
    static float KeyX(int col) => Pad + col * (Tile + Gap);
    static float KeyY(int row) => KeysY + row * (Tile + Gap);
    const float KnobCx = 638;

    void DrawModern(double now)
    {
        _ui.Begin(_ptr, 1);
        _p.Begin(Surf.Bitmap, 0x00000000);
        var all = new RectangleF(0, 0, TW, TH);
        _p.Fill(all, Theme.OverlayBg, 30);
        _p.Stroke(new RectangleF(1, 1, TW - 2, TH - 2), 0x40FFFFFF, 2, 29);
        uint sub = Theme.Mix(Theme.Text, Theme.OverlayBg, 0.42f);

        // header: name, where on the dock, the idle chip; bring here / close on the right
        _p.Text("Hexpad", new RectangleF(Pad + 4, 14, 140, 38), Theme.Text, 22, FontWeight.SemiBold);
        float hx = Pad + 4 + _p.MeasureWidth("Hexpad", 22, FontWeight.SemiBold) + 14;
        if (Live || _s.State == HexpadLink.LinkState.ScreenOff)
        {
            string where = Where();
            float pw = Math.Min(_p.MeasureWidth(where, 15, FontWeight.SemiBold) + 26, 360);
            _p.Fill(new RectangleF(hx, 19, pw, 30), Theme.Panel3, 15);
            _p.Text(where, new RectangleF(hx + 13, 19, pw - 22, 30), sub, 15, FontWeight.SemiBold);
            hx += pw + 10;
            if (_s.Ambient)
            {
                const string idle = "idle · point to wake";
                float iw = _p.MeasureWidth(idle, 14) + 24;
                _p.Fill(new RectangleF(hx, 19, iw, 30), Theme.WithAlpha(Theme.Accent, 0.18f), 15);
                _p.Text(idle, new RectangleF(hx + 12, 19, iw - 18, 30), Theme.Accent, 14);
            }
        }
        if (HeaderButton("dk.x", new RectangleF(TW - Pad - 40, 14, 40, 40), "")) { Toggle(); }
        if (HeaderButton("dk.here", new RectangleF(TW - Pad - 90, 14, 40, 40), Theme.IcoRecenter)) BringHere();

        if (!Live && _s.State != HexpadLink.LinkState.ScreenOff || _s.State == HexpadLink.LinkState.ScreenOff && !_faces.Any)
        {
            _p.Text(StateLine(), new RectangleF(0, 150, TW, 60), Theme.Text, 24, FontWeight.SemiBold, TextAlignment.Center);
            string hint = _s.State switch
            {
                HexpadLink.LinkState.NotRunning => "start it, and the dock shows up here",
                HexpadLink.LinkState.ScreenOff => "hold its middle round button to turn it on",
                _ => "",
            };
            _p.Text(hint, new RectangleF(0, 206, TW, 30), sub, 16, FontWeight.Normal, TextAlignment.Center);
            _p.End();
            _ui.End();
            return;
        }

        // the six keys
        for (int i = 0; i < HexpadLink.Keys; i++)
        {
            string id = $"key{i + 1}";
            var r = new RectangleF(KeyX(i % 3), KeyY(i / 3), Tile, Tile);
            _hit[id] = r;
            bool down = _pressed == id, hov = _hover == id, flash = Flash(id, now);
            var rr = down ? RectangleF.Inflate(r, -5, -5) : r;
            _p.Fill(rr, 0xFF000000, 18);
            var bmp = _faces.Bitmaps[i];
            if (bmp != null && !_s.Dark)
            {
                _p.PushRoundedClip(rr, 18);
                _p.Image(bmp, rr);
                _p.PopRoundedClip();
            }
            if (Ctl(id) is { Danger: true }) _p.Circle(rr.Right - 14, rr.Y + 14, 5, Theme.Bad);
            if (hov || flash) _p.Stroke(RectangleF.Inflate(rr, 3, 3), flash ? Theme.Accent : Theme.WithAlpha(Theme.Accent, 0.85f), 3, 21);
        }

        // a round button under each column
        for (int col = 0; col < 3; col++)
        {
            string id = $"btn{col + 7}";
            float cx = KeyX(col) + Tile / 2, cy = KeyY(2) + 18;
            var r = new RectangleF(cx - 24, cy - 24, 48, 48);
            _hit[id] = RectangleF.Inflate(r, 10, 6);
            bool down = _pressed == id, hov = _hover == id, flash = Flash(id, now);
            _p.Circle(cx, cy, down ? 21 : 24, down || flash ? Theme.Accent : Theme.Panel3);
            if (hov) _p.Stroke(RectangleF.Inflate(r, 3, 3), Theme.Accent, 2.5f, 27);
            string label = Ctl(id)?.Label ?? "";
            _p.Text(label, new RectangleF(cx - Tile / 2, cy + 28, Tile, 22), sub,
                FitSize(label, Tile - 4, 14), FontWeight.SemiBold, TextAlignment.Center);
        }

        // the knobs: the big one top right, the two small ones under it (left = Encoder 0, right = Encoder 2)
        Knob("enc1", KnobCx, 138, 72, now, sub);
        Knob("enc0", KnobCx - 76, 296, 50, now, sub);
        Knob("enc2", KnobCx + 76, 296, 50, now, sub);

        if (_confirm != null) ConfirmModern(sub);
        _p.End();
        _ui.End();
    }

    float FitSize(string s, float w, float max)
    {
        if (string.IsNullOrEmpty(s)) return max;
        float w100 = _p.MeasureWidth(s, 100, FontWeight.SemiBold);
        return w100 <= 0 ? max : MathF.Max(9, MathF.Min(max, MathF.Floor(100 * w / w100 * 2) / 2));
    }

    bool HeaderButton(string id, RectangleF r, string icon)
    {
        bool click = _ui.Clickable(id, r, out bool hover, out bool held);
        _p.Fill(r, held ? Theme.Accent : hover ? Theme.Mix(Theme.Panel3, 0xFFFFFFFF, 0.12f) : Theme.Panel3, r.Height / 2);
        _p.Icon(icon, r, held ? Theme.OnAccent : Theme.Text, 16);
        return click;
    }

    // a knob: a disc that spins as it turns, its function inside; its left / right third turn it, the middle pushes
    void Knob(string id, float cx, float cy, float rad, double now, uint sub)
    {
        var c = Ctl(id);
        var box = new RectangleF(cx - rad, cy - rad, rad * 2, rad * 2);
        float third = rad * 2 / 3;
        _hit[id + ":l"] = new RectangleF(box.X - 6, box.Y, third + 6, box.Height);
        _hit[id + ":r"] = new RectangleF(box.Right - third, box.Y, third + 6, box.Height);
        _hit[id] = new RectangleF(box.X + third, box.Y, third, box.Height);
        string b = Base(_pressed);
        bool hov = Base(_hover) == id, pushed = _pressed == id, flash = Flash(id, now);
        _p.Circle(cx, cy, rad, pushed ? Theme.Mix(Theme.Panel3, Theme.Accent, 0.35f) : Theme.Panel3);
        _p.Circle(cx, cy, rad - 7, Theme.Mix(Theme.Panel3, 0xFF000000, 0.18f));
        // the notch, turned with every detent
        float a = (_spin.TryGetValue(id, out var deg) ? deg : 0) * MathF.PI / 180;
        float nr = rad - 14;
        _p.Circle(cx + nr * MathF.Sin(a), cy - nr * MathF.Cos(a), MathF.Max(3, rad * 0.07f), flash || b == id ? Theme.Accent : Theme.Text);
        if (hov || flash) _p.Stroke(RectangleF.Inflate(box, 3, 3), Theme.Accent, 2.5f, rad + 3);
        if (hov)
        {
            bool l = _hover == id + ":l", r = _hover == id + ":r";
            _p.Icon(Theme.IcoLeft, new RectangleF(box.X + 4, cy - 14, 24, 28), l ? Theme.Accent : sub, 14);
            _p.Icon(Theme.IcoRight, new RectangleF(box.Right - 28, cy - 14, 24, 28), r ? Theme.Accent : sub, 14);
        }
        string label = c?.Label ?? "";
        _p.Text(label, new RectangleF(cx - rad + 16, cy - 14, rad * 2 - 32, 28), Theme.Text, FitSize(label, rad * 2 - 40, rad > 60 ? 19 : 15),
            FontWeight.SemiBold, TextAlignment.Center);
        string push = string.IsNullOrEmpty(c?.Push) ? "" : "push: " + Short(c.Push).ToLowerInvariant();
        _p.Text(push, new RectangleF(cx - 90, cy + rad + 4, 180, 22), sub, FitSize(push, 176, 13), FontWeight.SemiBold, TextAlignment.Center);
    }

    void ConfirmModern(uint sub)
    {
        string label = Ctl(_confirm)?.Label;
        if (string.IsNullOrEmpty(label)) label = _confirm;
        _p.Fill(new RectangleF(0, 0, TW, TH), Theme.WithAlpha(0xFF000000, 0.55f), 30);
        var card = new RectangleF(TW / 2 - 230, TH / 2 - 100, 460, 200);
        _p.Fill(card, Theme.Panel, 22);
        _p.Stroke(RectangleF.Inflate(card, -0.6f, -0.6f), Theme.Border, 1.2f, 22);
        _p.Text($"Run “{label}”?", new RectangleF(card.X + 24, card.Y + 20, card.Width - 48, 40), Theme.Text,
            FitSize($"Run “{label}”?", card.Width - 48, 22), FontWeight.SemiBold);
        _p.Text("It could end your VR session, so it asks first.", new RectangleF(card.X + 24, card.Y + 62, card.Width - 48, 26), sub, 15);
        if (_ui.Button("dk.no", new RectangleF(card.X + 24, card.Bottom - 68, 196, 46), "Cancel")) _confirm = null;
        if (_ui.Button("dk.yes", new RectangleF(card.Right - 220, card.Bottom - 68, 196, 46), "Do it", null, true))
        {
            _link.Input(_confirm, true);
            _link.Input(_confirm, false);
            _confirm = null;
        }
    }

    // ---- Windows 95: a "Hexpad" window, the key faces 1:1 in picture boxes, spin buttons for the knobs ----
    const uint Blk95 = 0xFF000000, Wht95 = 0xFFFFFFFF, Navy95 = 0xFF000080;
    static readonly string[] LeftGlyph = { "...k", "..kk", ".kkk", "kkkk", ".kkk", "..kk", "...k" };
    static readonly string[] RightGlyph = { "k...", "kk..", "kkk.", "kkkk", "kkk.", "kk..", "k..." };

    void Draw95(double now)
    {
        _canvas95 ??= new Surface(_p.Gpu, W95, H95);
        _ui.Begin(_ptr, 2);
        _ui.Canvas95 = new SizeF(W95, H95);
        _p.BeginPixel(_canvas95.Bitmap, 0x00000000);
        var win = new RectangleF(0, 0, W95, H95);
        _p.Fill(win, Theme.W95Face);
        _p.Bevel(win, Painter.Edge.Window);
        string title = string.IsNullOrEmpty(_s.Profile) ? "Hexpad" : $"Hexpad - {_s.Profile}";
        _ui.Caption95(new RectangleF(3, 3, W95 - 6, 18), title);
        if (_ui.CaptionClose95("dk95.x", W95 - 5 - 16, 5, true, 6)) Toggle();

        bool show = Live || _s.State == HexpadLink.LinkState.ScreenOff && _faces.Any;
        if (!show)
        {
            // nothing to show: the classic message in one white box where the keys would be
            var msg = new RectangleF(8, 26, 228, 150);
            _p.Fill(msg, Wht95);
            _p.Bevel(msg, Painter.Edge.Sunken);
            W95Icons.Draw(_p, W95Icons.Id.Info32, msg.X + 14, msg.Y + 16);
            _ui.Text95(StateLine() + ".", new RectangleF(msg.X + 54, msg.Y + 18, msg.Width - 64, msg.Height - 30), Blk95, false,
                TextAlignment.Leading, true, true);
        }
        // the six keys: 68 x 68 picture boxes, the dock's own pixels
        for (int i = 0; i < HexpadLink.Keys && show; i++)
        {
            string id = $"key{i + 1}";
            var box = new RectangleF(8 + i % 3 * 78, 26 + i / 3 * 78, 72, 72);
            _hit[id] = Tex(box);
            bool down = _pressed == id;
            _p.Fill(box, show ? Blk95 : Wht95);
            var bmp = _faces.Bitmaps[i];
            if (show && bmp != null && !_s.Dark)
                _p.ImageNearest(bmp, new RectangleF(box.X + 2 + (down ? 1 : 0), box.Y + 2 + (down ? 1 : 0), 68, 68));
            _p.Bevel(box, down ? Painter.Edge.Pressed : Painter.Edge.Sunken);
            if (show && (_hover == id || Flash(id, now))) Dotted(RectangleF.Inflate(box, -4, -4), Flash(id, now) ? 0xFFFFFF00 : Wht95);
        }
        // the round buttons under the columns
        for (int col = 0; col < 3; col++)
        {
            string id = $"btn{col + 7}";
            var r = new RectangleF(8 + col * 78, 182, 72, 22);
            _hit[id] = Tex(r);
            var inner = _p.ButtonFace95(r, false, _pressed == id || Flash(id, now), false);
            string label = Ctl(id)?.Label ?? "";
            _ui.Text95(Fit95(label, inner.Width - 4), inner, Blk95, false, TextAlignment.Center, show);
        }
        // the knobs as spin groups: the big one top right, the small ones under it
        KnobGroup95("enc1", new RectangleF(246, 24, 146, 52), show, now);
        KnobGroup95("enc0", new RectangleF(246, 82, 146, 48), show, now);
        KnobGroup95("enc2", new RectangleF(246, 136, 146, 48), show, now);
        // status bar
        string pages = _s.InFolder ? (_s.FolderPages > 1 ? $"Folder {_s.FolderPage + 1} of {_s.FolderPages}" : "Folder")
            : $"Page {_s.Page + 1} of {_s.Pages}";
        string name = _s.InFolder ? _s.FolderName : _s.PageName;
        string state = Live ? (_s.Ambient ? "Idle" : "Connected") : StateLine();
        if (show)
        {
            _ui.Pane95(new RectangleF(4, 214, 96, 20), pages);
            _ui.Pane95(new RectangleF(102, 214, 140, 20), Fit95(name, 130));
            _ui.Pane95(new RectangleF(244, 214, W95 - 248, 20), Fit95(state, W95 - 262), Live ? 0xFF008000 : 0xFF808080);
        }
        else _ui.Pane95(new RectangleF(4, 214, W95 - 8, 20), Fit95(StateLine(), W95 - 20));
        if (_confirm != null) Confirm95();
        _p.End();
        _p.Begin(Surf.Bitmap, 0x00000000);
        _p.ImageNearest(_canvas95.Bitmap, new RectangleF(0, 0, TW, TH));
        _p.End();
        _ui.End();
    }

    static RectangleF Tex(RectangleF r) => new(r.X * 2, r.Y * 2, r.Width * 2, r.Height * 2);

    string Fit95(string s, float w)
    {
        if (string.IsNullOrEmpty(s) || _ui.Width95(s) <= w) return s;
        int n = s.Length;
        while (n > 1 && _ui.Width95(s[..n] + "...") > w) n--;
        return s[..n].TrimEnd() + "...";
    }

    void Dotted(RectangleF r, uint c)
    {
        for (float x = r.X; x < r.Right; x += 2) { _p.Fill(new RectangleF(x, r.Y, 1, 1), c); _p.Fill(new RectangleF(x, r.Bottom - 1, 1, 1), c); }
        for (float y = r.Y; y < r.Bottom; y += 2) { _p.Fill(new RectangleF(r.X, y, 1, 1), c); _p.Fill(new RectangleF(r.Right - 1, y, 1, 1), c); }
    }

    void KnobGroup95(string id, RectangleF g, bool show, double now)
    {
        var c = Ctl(id);
        string title = string.IsNullOrEmpty(c?.Label) ? "Knob" : c.Label;
        _ui.Group95(g, title);
        float y = g.Y + (g.Height - 22) / 2 + 4;
        float x0 = g.X + (g.Width - (26 + 4 + 64 + 4 + 26)) / 2;
        var l = new RectangleF(x0, y, 26, 22);
        var push = new RectangleF(x0 + 30, y, 64, 22);
        var r = new RectangleF(x0 + 98, y, 26, 22);
        _hit[id + ":l"] = Tex(l);
        _hit[id] = Tex(push);
        _hit[id + ":r"] = Tex(r);
        Spin95(l, LeftGlyph, _pressed == id + ":l", show);
        var inner = _p.ButtonFace95(push, false, _pressed == id || Flash(id, now), false);
        string pl = string.IsNullOrEmpty(c?.Push) ? "Push" : Short(c.Push);
        _ui.Text95(Fit95(pl, inner.Width - 4), inner, Blk95, false, TextAlignment.Center, show);
        Spin95(r, RightGlyph, _pressed == id + ":r", show);
    }

    void Spin95(RectangleF r, string[] glyph, bool down, bool enabled)
    {
        var b = _p.ButtonFace95(r, false, down, false);
        float o = down ? 1 : 0;
        float gx = b.X + MathF.Floor((b.Width - glyph[0].Length) / 2) + o, gy = b.Y + MathF.Floor((b.Height - glyph.Length) / 2) + o;
        if (!enabled) _p.Pixels(glyph, gx + 1, gy + 1, Wht95);
        _p.Pixels(glyph, gx, gy, enabled ? Blk95 : 0xFF808080);
    }

    void Confirm95()
    {
        string label = Ctl(_confirm)?.Label;
        if (string.IsNullOrEmpty(label)) label = _confirm;
        var box = new RectangleF(W95 / 2 - 130, H95 / 2 - 56, 260, 112);
        _p.Fill(box, Theme.W95Face);
        _p.Bevel(box, Painter.Edge.Window);
        _ui.Caption95(new RectangleF(box.X + 3, box.Y + 3, box.Width - 6, 18), "Hexpad");
        W95Icons.Draw(_p, W95Icons.Id.Info32, box.X + 12, box.Y + 32);
        _ui.Text95(Fit95($"Run \"{label}\"?", box.Width - 64), new RectangleF(box.X + 52, box.Y + 30, box.Width - 60, 16), Blk95, true);
        _ui.Text95("It could end your VR session.", new RectangleF(box.X + 52, box.Y + 48, box.Width - 60, 16));
        if (_ui.Button95("dk95.yes", new RectangleF(box.X + box.Width / 2 - 80, box.Bottom - 32, 75, 23), "&Yes", true, true))
        {
            _link.Input(_confirm, true);
            _link.Input(_confirm, false);
            _confirm = null;
        }
        if (_ui.Button95("dk95.no", new RectangleF(box.X + box.Width / 2 + 5, box.Bottom - 32, 75, 23), "&No")) _confirm = null;
    }

    public void Dispose()
    {
        DestroyOverlay();
        Surf.Dispose();
        _canvas95?.Dispose();
    }
}
