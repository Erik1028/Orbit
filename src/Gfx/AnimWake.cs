namespace Orbit;

// The panels' side of the styles' animations (Motion.cs). A style drawing something that moves asks for the panel's next frame:
// AnimWake.At(Clock.Now) = the very next one, AnimWake.At(t) = by then (a cursor that blinks at t, a scan band's next step).
// The panel, right after it has drawn, takes the soonest ask (Take) and draws again by that time. When nothing moves nothing
// asks, and every panel keeps its own pace (no frames for a picture that has not changed).
// One panel draws at a time (the main thread), so the asks of a draw belong to the panel that drew.
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
