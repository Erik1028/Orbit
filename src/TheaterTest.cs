using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace Orbit;

// --theatertest <dir>: theater mode without VR - a made-up headset and made-up screens through start, glide, grab,
// switch and stop (the saved layout must not change), and pictures of the switcher in both styles.
static class TheaterTest
{
    sealed class FakeScreen : ITheaterScreen
    {
        public string Name;
        public bool IsPlayable = true;
        public Matrix4x4 HomeWorld;
        public TheaterView Theater { get; set; }
        public bool TheaterHidden { get; set; }
        public string TheaterLabel => Name;
        public bool Playable => IsPlayable;
        public bool Home(out Matrix4x4 world, out float width, out float curvature, out float opacity)
        {
            world = HomeWorld; width = 0.6f; curvature = 0.05f; opacity = 0.9f;
            return true;
        }
        public VrOverlay Overlay => null;
        public bool Interactive => false;
        public bool AcceptsFrom(Hand h) => true;
        public Vector2 TextureSize => new(2560, 1440);
        public void PointerMove(Hand h, Vector2 px) { }
        public void PointerLeave(Hand h) { }
        public void PointerButton(Hand h, PtrButton b, bool down, Vector2 px) { }
        public void PointerScroll(Hand h, float notches, Vector2 px) { }
        public bool Grabbable => true;
        public Matrix4x4 WorldTransform => Theater?.World ?? HomeWorld;
        public void GrabBegin(bool detach) { }
        public void GrabMove(Matrix4x4 world) { }
        public void GrabStickX(float v, float dt) { }
        public void GrabTwoHandBegin() { }
        public void GrabTwoHandScale(float ratio) { }
        public bool GrabSnapPreview => false;
        public void GrabEnd() { }
    }

    public static int Run(string dir)
    {
        dir = Path.GetFullPath(dir);
        Directory.CreateDirectory(dir);
        int fails = 0;
        void Ck(string name, bool ok) { Log.Info($"theatertest: {(ok ? "ok  " : "FAIL")} {name}"); if (!ok) fails++; }
        static bool Near(float a, float b, float tol = 0.01f) => MathF.Abs(a - b) <= tol;

        var vr = new VrSession();
        var head = new Vector3(0.3f, 1.6f, -0.2f);
        vr.Device[0] = Matrix4x4.CreateRotationY(0.5f) * Matrix4x4.CreateTranslation(head);     // looking level, turned a little
        vr.DeviceValid[0] = true;
        var fwd = Vector3.Normalize(MathUtil.Forward(vr.Hmd));

        var a = new FakeScreen { Name = "AOC 24G4X", HomeWorld = Matrix4x4.CreateTranslation(-0.7f, 1.2f, -1f) };
        var b = new FakeScreen { Name = "Xiaomi Monitor", HomeWorld = Matrix4x4.CreateTranslation(0, 1.2f, -1.1f) };
        var c = new FakeScreen { Name = "Xiaomi Monitor", HomeWorld = Matrix4x4.CreateTranslation(0.7f, 1.2f, -1f) };
        var live = new List<ITheaterScreen> { a, b, c };
        var cfg = new TheaterConfig();

        using var gpu = new Gpu(-1);
        using var painter = new Painter(gpu);
        using var th = new Theater(gpu, painter);
        void Run(float seconds) { for (float t = 0; t < seconds; t += 1 / 90f) th.Update(vr, 1 / 90f, cfg.Dim, live); }

        th.Start(b, vr, cfg);
        th.Update(vr, 0, cfg.Dim, live);
        Ck("start: the screen plays, the others step aside", b.Theater != null && !b.TheaterHidden && a.TheaterHidden && c.TheaterHidden);
        Ck("start: it begins at its own place", Vector3.Distance(b.Theater.World.Translation, b.HomeWorld.Translation) < 0.01f);
        Run(0.6f);
        var v = b.Theater;
        var want = head + fwd * 2.5f;
        Ck($"glided in: 2.5 m in front ({v.World.Translation})", Vector3.Distance(v.World.Translation, want) < 0.01f);
        Ck($"glided in: 2.8 m wide, bent around you (c {v.Curvature:0.000})", Near(v.Width, 2.8f) && Near(v.Curvature, 2.8f / (2 * MathF.PI * 2.5f), 0.002f) && Near(v.Opacity, 1));
        Ck("it faces you", Vector3.Dot(MathUtil.Back(v.World), -fwd) > 0.999f);

        // grab: pull it back to 4 m and make it bigger - only the override moves
        v.Move(Matrix4x4.CreateTranslation(0, 0, -4) * MathUtil.GazeFrame(vr.Hmd));
        v.ScaleBegin();
        v.Scale(1.5f);
        Run(0.05f);
        Ck($"grab: 4.2 m wide at 4 m, the bend follows (c {v.Curvature:0.000})", Near(v.Width, 4.2f) && Near(v.Curvature, 4.2f / (2 * MathF.PI * 4f), 0.002f));

        // the switcher: another screen takes the same place and size
        th.Switch(c);
        Run(0.05f);
        Ck("switch: the new one plays at the same place", c.Theater == v && b.Theater == null && b.TheaterHidden && !c.TheaterHidden && a.TheaterHidden);
        Ck("switch: same size", Near(c.Theater.Width, 4.2f));
        th.Switch(a);
        th.Switch(a);
        Run(0.05f);
        Ck("switch again (twice is once)", a.Theater == v && c.Theater == null && c.TheaterHidden);

        // stop: glides home, the others come back, size and distance are kept for next time
        th.Stop(vr, cfg);
        th.Update(vr, 0, cfg.Dim, live);
        Ck("stop: the others wait while the screen is on its way home", !th.On && b.TheaterHidden && c.TheaterHidden && !a.TheaterHidden);
        Ck($"stop: keeps {cfg.Width:0.00} m wide at {cfg.Distance:0.00} m", Near(cfg.Width, 4.2f) && Near(cfg.Distance, 4f, 0.02f));
        Run(0.6f);
        Ck("glided home: no override left, everything is back", a.Theater == null && !a.TheaterHidden && !b.TheaterHidden && !c.TheaterHidden);

        // the screen goes away mid-theater (window closed / monitor rebuilt)
        th.Start(b, vr, cfg);
        Run(0.6f);
        Ck("again at the kept size", Near(b.Theater.Width, 4.2f));
        b.IsPlayable = false;
        Run(0.02f);
        Ck("its screen went away: theater ends", !th.On && b.Theater == null && !a.TheaterHidden && !c.TheaterHidden);
        b.IsPlayable = true;
        live.Remove(a);
        th.Start(a, vr, cfg);
        Run(0.02f);
        Ck("a screen that is not there any more", !th.On && a.Theater == null);

        // the switcher, both styles
        var app = new ConfigStore(Path.Combine(dir, "config.json"));
        using var bar = new TheaterBar(gpu, painter);
        var choices = new List<ITheaterScreen> { a, b, c, new FakeScreen { Name = "YouTube - Lofi hip hop radio - beats to relax/study to - Mozilla Firefox" } };
        foreach (var style in new[] { "modern", "classic" })
        {
            app.Data.Appearance.Style = style;
            Theme.Apply(app.Data.Appearance);
            bar.DrawFor(choices, b).SavePng(gpu, Path.Combine(dir, $"bar_{style}.png"));
            bar.DrawFor(new List<ITheaterScreen> { a }, a).SavePng(gpu, Path.Combine(dir, $"bar_one_{style}.png"));
        }
        Log.Info($"theatertest: {(fails == 0 ? "ALL OK" : fails + " FAILED")}");
        return fails == 0 ? 0 : 1;
    }
}
