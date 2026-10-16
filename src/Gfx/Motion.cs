namespace Orbit;

// How much the styles move (Look > Motion). Every style's animations ask this before they start:
// - Full: the style's own animations, all of them (Cyber's boot and glitches, Terminal's CRT, XP's fades...).
// - Reduced: only short ones that help to follow what happened (a card opening, a page sliding); no loops, no flashes,
//   no glitches, no scanning lines.
// - Off: nothing animates - things change at once (VR comfort).
public enum MotionLevel { Full, Reduced, Off }

public static class Motion
{
    public static MotionLevel Level = MotionLevel.Full;

    public static bool Full => Level == MotionLevel.Full;          // decorative, looping or flashing effects
    public static bool Any => Level != MotionLevel.Off;            // short transitions (opening, sliding, fading)

    public static void Apply(AppearanceConfig a) =>
        Level = a.Motion switch { "reduced" => MotionLevel.Reduced, "off" => MotionLevel.Off, _ => MotionLevel.Full };

    public static string Id(MotionLevel l) => l switch { MotionLevel.Reduced => "reduced", MotionLevel.Off => "off", _ => null };
}
