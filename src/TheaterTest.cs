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
        public bool HomeInWorld => true;
        public string TheaterState => "fake";
        public float Fade = 1;
        public int Vanished;
        public float TheaterFade => Fade;
        public void TheaterVanish() => Vanished++;
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
        // the middle monitor: up and ahead, tilted back 30 degrees (lying back), 1.2 m from the head
        var bDir = Vector3.Normalize(new Vector3(0.2f, 0.6f, -1f));
        var b = new FakeScreen { Name = "Xiaomi Monitor", HomeWorld = Matrix4x4.CreateRotationX(0.52f) * Matrix4x4.CreateTranslation(head + bDir * 1.2f) };
        var c = new FakeScreen { Name = "Xiaomi Monitor", HomeWorld = Matrix4x4.CreateTranslation(0.7f, 1.2f, -1f) };
        var live = new List<ITheaterScreen> { a, b, c };
        var cfg = new TheaterConfig();

        using var gpu = new Gpu(-1);
        using var painter = new Painter(gpu);
        using var th = new Theater(gpu, painter);
        void Run(float seconds) { for (float t = 0; t < seconds; t += 1 / 90f) th.Update(vr, 1 / 90f, cfg.Dim, live); }

        // started from the watch: looking down at the wrist - the screen must still go where the monitors are.
        // b is the middle monitor (the anchor) and plays: it grows out of its own place
        var level = vr.Device[0];
        vr.Device[0] = Matrix4x4.CreateRotationX(-0.9f) * level;
        th.Start(b, b, vr, cfg);
        vr.Device[0] = level;
        th.Update(vr, 0, cfg.Dim, live);
        Ck("start: the screen plays, the others step aside", b.Theater != null && !b.TheaterHidden && a.TheaterHidden && c.TheaterHidden);
        Ck("start: the middle monitor grows out of its own place", Vector3.Distance(b.Theater.World.Translation, b.HomeWorld.Translation) < 0.01f);
        Run(0.6f);
        var v = b.Theater;
        var want = head + bDir * 2.5f;
        Ck($"in: 2.5 m out where the monitors are, not where you looked ({v.World.Translation})", Vector3.Distance(v.World.Translation, want) < 0.01f);
        Ck($"in: 2.8 m wide, bent around your head (c {v.Curvature:0.000})", Near(v.Width, 2.8f) && Near(v.Curvature, 2.8f / (2 * MathF.PI * 2.5f), 0.002f) && Near(v.Opacity, 1));
        Ck("it faces you", Vector3.Dot(MathUtil.Back(v.World), -bDir) > 0.999f);
        Ck("it keeps the monitor's tilt", Vector3.Dot(MathUtil.Up(v.World), Vector3.Normalize(MathUtil.Up(b.HomeWorld) - Vector3.Dot(MathUtil.Up(b.HomeWorld), bDir) * bDir)) > 0.999f);

        // grab: pull it out to 4 m along the same line and make it bigger - only the override moves
        v.Move(Theater.Where(b, vr, 4));
        v.ScaleBegin();
        v.Scale(1.5f);
        Run(0.05f);
        Ck($"grab: 4.2 m wide at 4 m, the bend follows (c {v.Curvature:0.000})", Near(v.Width, 4.2f) && Near(v.Curvature, 4.2f / (2 * MathF.PI * 4f), 0.002f));

        // the switcher: the new one fades in over the old one, which stays solid underneath until it is covered
        th.Switch(c);
        Run(0.05f);
        Ck("switch: the new one plays at the same place and size", c.Theater == v && Near(c.Theater.Width, 4.2f) && !c.TheaterHidden && c.Vanished == 1);
        Ck("switch: the old one stays under it, solid, at the same place", b.Theater is { Behind: true } bv && Near(bv.Opacity, 1) &&
            Vector3.Distance(bv.World.Translation, v.World.Translation) < 0.001f && Near(bv.Width, 4.2f) && !b.TheaterHidden && a.TheaterHidden);
        Run(0.15f);
        Ck("switch: once covered, the old one goes", b.Theater == null && b.TheaterHidden && b.Vanished == 1);
        th.Switch(a);
        th.Switch(a);
        Run(0.2f);
        Ck("switch again (twice is once)", a.Theater == v && c.Theater == null && c.TheaterHidden);

        // stop with a side monitor playing: it fades out where it plays (no glide across the room), then fades in at home
        int van = a.Vanished;
        th.Stop(vr, cfg);
        th.Update(vr, 0, cfg.Dim, live);
        Ck("stop: the others wait while it goes", !th.On && b.TheaterHidden && c.TheaterHidden && !a.TheaterHidden);
        Ck($"stop: keeps {cfg.Width:0.00} m wide at {cfg.Distance:0.00} m", Near(cfg.Width, 4.2f) && Near(cfg.Distance, 4f, 0.02f));
        Run(0.1f);
        Ck("stop: a side monitor fades out in place", a.Theater != null && Vector3.Distance(a.Theater.World.Translation, v.Place.Translation) < 0.001f && a.Theater.Opacity < 0.9f);
        Run(0.4f);
        Ck("gone: no override left, everything is back, it fades in at home", a.Theater == null && a.Vanished > van && !a.TheaterHidden && !b.TheaterHidden && !c.TheaterHidden);

        // a side monitor starts by fading in where the big screen goes: in front of the middle one, not of itself
        th.Start(a, b, vr, cfg);
        th.Update(vr, 1 / 90f, cfg.Dim, live);
        Ck("side start: in front of the middle monitor", Vector3.Distance(a.Theater.World.Translation, head + bDir * 4f) < 0.02f);
        Ck("side start: from slightly smaller and clear", a.Theater.Width < 4.2f * 0.95f && a.Theater.Opacity < 0.1f);
        Run(0.4f);
        Ck("side start: settled at the kept size", Near(a.Theater.Width, 4.2f) && Near(a.Theater.Opacity, 1));

        // the screen goes away mid-theater (window closed / monitor rebuilt)
        a.IsPlayable = false;
        Run(0.02f);
        Ck("its screen went away: theater ends", !th.On && a.Theater == null && !b.TheaterHidden && !c.TheaterHidden);
        a.IsPlayable = true;
        live.Remove(c);
        th.Start(c, b, vr, cfg);
        Run(0.02f);
        Ck("a screen that is not there any more", !th.On && c.Theater == null);

        // the settings on the bar: size and curve apply to the big screen at once
        live.Add(c);
        th.Start(b, b, vr, cfg);
        Run(0.6f);
        cfg.Width = 3.5f;
        cfg.Curved = false;
        th.Apply(cfg);
        Run(0.02f);
        Ck($"bar: size and flat apply at once (w {b.Theater.Width:0.00}, c {b.Theater.Curvature:0.000})", Near(b.Theater.Width, 3.5f) && b.Theater.Curvature == 0);
        cfg.Curved = true;
        th.Apply(cfg);
        Run(0.02f);
        Ck($"bar: curved again (c {b.Theater.Curvature:0.000})", b.Theater.Curvature > 0.1f);
        th.Stop(vr, cfg);
        Ck("bar: the size set there is kept", Near(cfg.Width, 3.5f));
        Run(0.6f);

        // the bar, both styles: under a screen (Theater button + settings) and in theater mode (switcher + settings)
        var app = new ConfigStore(Path.Combine(dir, "config.json"));
        using var bar = new TheaterBar(gpu, painter, app);
        var choices = new List<ITheaterScreen> { a, b, c, new FakeScreen { Name = "YouTube - Lofi hip hop radio - beats to relax/study to - Mozilla Firefox" } };
        var view = new TheaterView { Size = 2.8f };
        foreach (var style in new[] { "modern", "classic" })
        {
            app.Data.Appearance.Style = style;
            Theme.Apply(app.Data.Appearance);
            bar.DrawFor(choices, b, view).SavePng(gpu, Path.Combine(dir, $"bar_{style}.png"));
            bar.DrawFor(new List<ITheaterScreen> { a }, a, view).SavePng(gpu, Path.Combine(dir, $"bar_one_{style}.png"));
            bar.DrawFor(new List<ITheaterScreen>(), null).SavePng(gpu, Path.Combine(dir, $"bar_screen_{style}.png"));
        }
        Log.Info($"theatertest: {(fails == 0 ? "ALL OK" : fails + " FAILED")}");
        return fails == 0 ? 0 : 1;
    }
}
