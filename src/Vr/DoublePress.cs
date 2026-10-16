using System;

namespace Orbit;

// The quick gesture (VRChat > Controllers > "B / Y twice"): B or Y pressed twice within 0.35 s toggles the keyboard (or pins the
// watch), the way Erik asked for it - but VRChat uses B / Y too (its menus), so a press only counts when it can never reach the game:
//   - that hand's laser is on one of OrbitOverlay's own panels (the watch, the keyboard, the settings, the dock, the room, the theater
//     bar, What's new) - not a monitor or a mirrored window, where B / Y is the right mouse button (two right clicks there would open
//     and close a context menu), and not nothing (then the press is VRChat's);
//   - Orbit already held that hand's buttons back from the game when the press was read (the action set at the overlay priority:
//     "Keep clicks from the game" on, the beam on the panel since the frame before) - so neither press reaches VRChat;
//   - not while that hand grabs (B / Y + grip takes a monitor out of its row).
// Leaving the panel between the two presses starts over. A long press was not taken: held on nothing it would open VRChat's
// action menu, and on a panel it adds nothing a double press does not do.
public sealed class DoublePress
{
    public const double Window = 0.35;
    readonly double[] _first = { double.NegativeInfinity, double.NegativeInfinity };

    // every frame, per hand: pressed = B / Y went down this frame; eligible = all of the above holds right now
    public bool Update(int hand, bool pressed, bool eligible, double now)
    {
        if (!eligible) { _first[hand] = double.NegativeInfinity; return false; }
        if (!pressed) return false;
        if (now - _first[hand] <= Window)
        {
            _first[hand] = double.NegativeInfinity;
            return true;
        }
        _first[hand] = now;
        return false;
    }

    public void Reset() => _first[0] = _first[1] = double.NegativeInfinity;

    // OrbitOverlay's own panels: not a monitor or a mirrored window (there B / Y is the right mouse button), not nothing
    public static bool OwnPanel(IPointerTarget t) => t != null && t is not DesktopPanel && t is not WindowPanel;

    // a press can count: Keep clicks from the game on, this hand held back from the game when its buttons were read, the beam on one of
    // Orbit's own panels, not grabbing
    public static bool Eligible(bool keepClicks, bool heldBack, IPointerTarget t, bool grab) => keepClicks && heldBack && OwnPanel(t) && !grab;

    // the setting (GeneralConfig.DoublePress): null = keyboard
    public static string Mode(string setting) => setting is "off" or "watch" ? setting : "keyboard";
    public static string Store(string mode) => mode == "keyboard" ? null : mode;
}
