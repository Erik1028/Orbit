namespace Orbit;

// The panels' side of the animations (Motion.cs) - the one rule for "draw me again by then". Something drawing that moves asks for
// the panel's next frame: AnimWake.At(Clock.Now) = the very next one, AnimWake.At(t) = by then (a cursor that blinks at t, a scan
// band's next step). Who asks: the Mac's and the Terminal's motion, XP's and 7's (XpAeroFx.Want), Cyber's scheduled effects
// (WristPanel.CyWant), the modern watch's charging bolt. The panel, right after it has drawn, takes the soonest ask (Take) and draws
// again by that time; what needs every frame says so through its panel's Animating instead. When nothing moves nothing asks, and
// every panel keeps its own pace (no frames for a picture that has not changed).
// One panel draws at a time (the main thread), so the asks of a draw belong to the panel that drew. A panel that draws another's
// picture first (the settings' watch and keyboard previews) drops those asks (a Take before its own drawing): that panel draws
// its own frames.
public static class AnimWake
{
    static double _at = double.PositiveInfinity;

    public static void At(double t)
    {
        if (t < _at) _at = t;
    }

    // the soonest ask since the last Take (+inf: none), cleared for the next panel
    public static double Take()
    {
        double t = _at;
        _at = double.PositiveInfinity;
        return t;
    }
}
