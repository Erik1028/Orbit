using System;

namespace Orbit;

// The Bento look of the modern watch (the custom face): rounded cards on the watch page, one grey for captions and
// small text, colours by role. Modern only: Theme.Apply derives the surfaces in its modern branch (the Windows 95 skin
// returns before it), and only the watch's widgets and their opened views draw with these - the settings, keyboard, dock
// and the other panels keep Theme's tokens.
static class Bento
{
    // ---- shapes (watch texture px, 640 x 440) ----
    public const float PageRadius = 26;          // the card radius plus the 10 px gap: concentric corners
    public const float CardRadius = 16;          // every card, whatever its size; buttons are their card
    public const float PageEdge = 2;             // the page's outline: the watch edge still shows against a dark world
    public const float EdgeWidth = 1.25f;        // a card's hairline, in both backgrounds (cards would merge in the headset)

    // How far a card is lifted from the page: a mix of the panel colour toward a cool tint of it. The one knob for how
    // well the cards stand apart in the headset (night mode halves the difference); Tile, Track and the hover follow it.
    public static float Lift = 0.15f;

    // ---- surfaces, derived from the Look tab's panel colour (Derive) ----
    public static uint Tint;                     // the mix target: the panel's own hue, lighter (never drawn)
    public static uint Card;                     // a widget's card
    public static uint Tile;                     // a button that is off, round buttons
    public static uint TileHover;                // a tile under the laser
    public static uint Track;                    // the empty part of a bar
    public static uint AccentFill;               // the accent as a fill with text on it (a touch darker when the accent is mid-light)
    public static uint OnAccent;                 // icons and labels on AccentFill
    public static uint DangerFill, DangerEdge;   // a card whose state needs action (the volume, muted)

    public const uint Edge = 0x10FFFFFF;         // a card's hairline (6 % white)
    public const uint PageLine = 0x1FFFFFFF;     // the page's outline (12 % white)

    // ---- text: one white, one grey (captions, units, notes, artist), a faint one for "nothing there" ----
    public const uint Text = 0xFFF2F3F7;
    public const uint Sub = 0xFF9EA4B6;
    public const uint Faint = 0xFF5D6378;

    // ---- status: only when a value needs attention, and then its text too (not only its bar) ----
    public const uint Ok = 0xFF3DDC84, Warn = 0xFFF7C948, Bad = 0xFFFF5A5A;

    // ---- data colours: what a value is, not how it is ----
    public const uint DataPurple = 0xFF8B7CF8;   // CPU
    public const uint DataBlue = 0xFF5AA9FF;     // RAM, VRAM

    public static void Derive(uint pc, uint accent)
    {
        pc |= 0xFF000000;
        Tint = Theme.Hsv(Theme.Hue(pc), MathF.Min(0.40f, Theme.Sat(pc)), 0.88f);
        Card = Theme.Mix(pc, Tint, Lift);
        Tile = Theme.Mix(pc, Tint, Lift + 0.10f);
        TileHover = Theme.Mix(pc, Tint, Lift + 0.17f);
        Track = Theme.Mix(pc, Tint, Lift + 0.16f);
        AccentFill = Theme.Luma(accent) < 0.55f ? Theme.Mix(accent, 0xFF000000, 0.08f) : accent;
        OnAccent = Theme.Luma(AccentFill) > 0.55f ? 0xFF0B1220 : 0xFFFFFFFF;
        DangerFill = Theme.Mix(pc, Bad, 0.12f);
        DangerEdge = Theme.Mix(pc, Bad, 0.38f);
    }

    // a value's colour: its normal colour, or Warn / Bad when it needs attention
    public static uint Level(StatLevel l, uint normal) => l switch { StatLevel.Bad => Bad, StatLevel.Warn => Warn, _ => normal };
}

// how a reading is: fine, worth a look, or needs action (thresholds in WristPanel.LevelOf, shared by both styles)
public enum StatLevel { Ok, Warn, Bad }
