using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using Valve.VR;
using Vortice.DirectWrite;

namespace Orbit;

public sealed class Notification
{
    public string Title, Content, Source;
    public float Timeout = 3, Volume = 0.7f;
    public uint[] Icon;                         // premultiplied BGRA, IconSize x IconSize
    // display state (main thread)
    public double Shown = -1;
    public float Vis;
    public bool Leaving;
    public Vortice.Direct2D1.ID2D1Bitmap Bitmap;
}

// Receives notifications in XSOverlay's UDP format (port 42069, JSON with messageType 1, title,
// content, timeout, volume, useBase64Icon/icon, sourceApp) - what VRCX and other tools send when
// their "XSOverlay notifications" option is on. Only from this PC (bound to 127.0.0.1).
public sealed class NotificationListener : IDisposable
{
    public const int Port = 42069, IconSize = 96;
    public readonly ConcurrentQueue<Notification> Incoming = new();
    public string Status { get; private set; } = "off";
    UdpClient _udp;
    Thread _t;

    double _retryAt;
    bool _warned;

    // called every frame: a busy port (XSOverlay owns it) is retried every 10 s, not every frame
    public void SetEnabled(bool on)
    {
        if (on == (_udp != null)) return;
        if (!on) { Stop(); Status = "off"; _retryAt = 0; _warned = false; return; }
        double now = Clock.Now;
        if (now < _retryAt) return;
        UdpClient u = null;
        try
        {
            u = new UdpClient(AddressFamily.InterNetwork);
            u.Client.Bind(new IPEndPoint(IPAddress.Loopback, Port));
            _udp = u;
            _warned = false;
            _t = new Thread(Run) { IsBackground = true, Name = "notifications" };
            _t.Start();
            Status = $"listening on UDP {Port}";
            Log.Info("notifications: " + Status);
        }
        catch (SocketException e)
        {
            u?.Dispose();
            _udp = null;
            _retryAt = now + 10;
            Status = $"UDP {Port} is busy (XSOverlay running?)";
            if (!_warned) { _warned = true; Log.Warn("notifications: " + e.Message); }
        }
    }

    void Run()
    {
        var from = new IPEndPoint(IPAddress.Any, 0);
        var udp = _udp;
        while (udp != null)
        {
            byte[] data;
            try { data = udp.Receive(ref from); }
            catch { break; }
            try
            {
                var n = Parse(data);
                if (n != null) Incoming.Enqueue(n);
            }
            catch (Exception e) { Log.Warn("notification parse: " + e.Message); }
        }
    }

    static readonly Regex Tags = new("<[^>]+>");

    public static Notification Parse(byte[] data)
    {
        using var doc = JsonDocument.Parse(data);
        var r = doc.RootElement;
        string str(string k) => r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        float num(string k, float d) => r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? (float)v.GetDouble() : d;
        bool flag(string k) => r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True;
        int type = r.TryGetProperty("messageType", out var mt) && mt.ValueKind == JsonValueKind.Number ? mt.GetInt32() : 1;
        if (type != 1) return null;
        var n = new Notification
        {
            Title = Tags.Replace(str("title") ?? "", ""),
            Content = Tags.Replace(str("content") ?? "", ""),
            Source = str("sourceApp"),
            Timeout = Math.Clamp(num("timeout", 3), 1.5f, 15f),
            Volume = Math.Clamp(num("volume", 0.7f), 0, 1),
        };
        string icon = str("icon");
        if (flag("useBase64Icon") && !string.IsNullOrEmpty(icon))
        {
            try
            {
                using var ms = new MemoryStream(Convert.FromBase64String(icon));
                using var img = Image.FromStream(ms);
                n.Icon = MediaInfo.CoverFit(img, IconSize);
            }
            catch { n.Icon = null; }
        }
        if (string.IsNullOrWhiteSpace(n.Title) && string.IsNullOrWhiteSpace(n.Content)) return null;
        return n;
    }

    void Stop()
    {
        var u = _udp;
        _udp = null;
        try { u?.Close(); } catch { }
    }

    public void Dispose() => Stop();
}

// Shows notifications as cards in the lower part of the view (head-locked), up to three stacked.
public sealed class NotificationPanel : IDisposable
{
    public const int TW = 960, TH = 640;
    const float CardH = 190, CardGap = 14;
    readonly Painter _p;
    readonly ConfigStore _cfg;
    readonly Surface _surf;
    readonly List<Notification> _cards = new();
    readonly KeySounds _sound = new();
    VrOverlay _ov;
    bool _dirty;
    const float Width = 0.42f, Distance = 0.85f;
    float _placedAngle = float.NaN;

    // Head-locked: the first card's centre sits 'angle' degrees below the centre of view, the panel
    // turned to face the eyes. Further cards stack below it.
    void Place(float angleDeg)
    {
        if (angleDeg == _placedAngle) return;
        _placedAngle = angleDeg;
        float a = angleDeg * MathF.PI / 180;
        float metersPerPx = Width / TW;
        float firstCardFromCentre = (TH / 2f - CardH / 2f) * metersPerPx;    // first card is drawn at the top
        // where the first card should be, then the panel centre is that much further "down" the panel
        var cardPos = new Vector3(0, -Distance * MathF.Sin(a), -Distance * MathF.Cos(a));
        var rot = Matrix4x4.CreateRotationX(-a);                  // +Z (the panel's face) points back at the eyes
        var down = Vector3.TransformNormal(-Vector3.UnitY, rot);
        var centre = cardPos + down * firstCardFromCentre;
        _ov.SetRelative(0, rot * Matrix4x4.CreateTranslation(centre));
    }

    public NotificationPanel(Gpu g, Painter p, ConfigStore cfg)
    {
        _p = p; _cfg = cfg;
        _surf = new Surface(g, TW, TH);
    }

    public void CreateOverlay()
    {
        _ov = new VrOverlay("erik.orbit.notifications", "Orbit Notifications");
        _ov.SetFlag(VROverlayFlags.IsPremultiplied, true);
        _ov.SetSortOrder(180);
        _ov.SetWidth(Width);
        _placedAngle = float.NaN;
        _sound.Warm();
    }

    public void DestroyOverlay()
    {
        _ov?.Dispose(); _ov = null;
        foreach (var c in _cards) c.Bitmap?.Dispose();
        _cards.Clear();
    }

    public void Update(NotificationListener src, double now, float dt)
    {
        while (src.Incoming.TryDequeue(out var n))
        {
            if (_ov == null) continue;
            n.Shown = now;
            _cards.Insert(0, n);
            if (_cfg.Data.General.NotificationSound) _sound.PlayChime(n.Volume * 0.6f);
            _dirty = true;
        }
        if (_ov == null) return;
        // more than three: the oldest leaves early
        int alive = 0;
        foreach (var c in _cards) if (!c.Leaving && ++alive > 3) c.Leaving = true;
        for (int i = _cards.Count - 1; i >= 0; i--)
        {
            var c = _cards[i];
            if (!c.Leaving && now - c.Shown > c.Timeout) c.Leaving = true;
            float target = c.Leaving ? 0 : 1;
            float nv = Math.Clamp(c.Vis + Math.Sign(target - c.Vis) * dt / (c.Leaving ? 0.3f : 0.22f), 0, 1);
            if (nv != c.Vis) { c.Vis = nv; _dirty = true; }
            if (c.Leaving && c.Vis <= 0) { c.Bitmap?.Dispose(); _cards.RemoveAt(i); _dirty = true; }
        }
        if (_cards.Count == 0) { _ov.Hide(); return; }
        Place(Math.Clamp(_cfg.Data.General.NotificationAngle, 0f, 40f));
        if (_dirty)
        {
            Draw();
            _ov.SetTexture(_surf.Tex);
            _dirty = false;
        }
        _ov.Show();
    }

    public void Draw()
    {
        _p.Begin(_surf.Bitmap, 0x00000000);
        float y = 0;
        foreach (var c in _cards)
        {
            float e = 1 - MathF.Pow(1 - c.Vis, 3);
            var r = new RectangleF(20 + (1 - e) * 40, y, TW - 40, CardH);
            uint A(uint col) => Theme.WithAlpha(col, e);
            _p.Fill(r, A(Theme.WithAlpha(Theme.Bg, 0.96f)), 24);
            _p.Stroke(r, A(Theme.WithAlpha(Theme.Accent, 0.6f)), 2, 24);
            var icon = new RectangleF(r.X + 24, r.Y + 24, 96, 96);
            if (c.Icon != null)
            {
                c.Bitmap ??= _p.CreateBitmap(c.Icon, NotificationListener.IconSize, NotificationListener.IconSize);
                _p.Image(c.Bitmap, icon, e);
            }
            else
            {
                _p.Fill(icon, A(Theme.Panel3), 20);
                _p.Icon("", icon, A(Theme.Accent), 40);    // bell
            }
            float tx = icon.Right + 24, tw = r.Right - tx - 24;
            if (!string.IsNullOrEmpty(c.Source))
                _p.Text(c.Source, new RectangleF(tx, r.Y + 18, tw, 26), A(Theme.Dim), 18);
            _p.Text(c.Title, new RectangleF(tx, r.Y + 42, tw, 44), A(Theme.Text), 30, FontWeight.SemiBold);
            _p.Text(c.Content, new RectangleF(tx, r.Y + 90, tw, 84), A(Theme.Text), 23, FontWeight.Normal,
                TextAlignment.Leading, ParagraphAlignment.Near, null, true);
            y += CardH + CardGap;
            if (y > TH - CardH) break;
        }
        _p.End();
    }

    // snapshot tool: draw a sample card
    public void AddForPreview(Notification n) { n.Vis = 1; n.Shown = 0; _cards.Add(n); }
    public Surface Surface => _surf;

    public void Dispose() { DestroyOverlay(); _surf.Dispose(); _sound.Dispose(); }
}
