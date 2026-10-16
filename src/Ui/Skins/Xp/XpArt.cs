namespace Orbit;

// Windows XP's little pictures, pixel by pixel ('k' = a pixel; the maps with more letters say which shade).
public sealed partial class XpSkin
{
    // the caption boxes' white glyphs (on a 16 x 16 box)
    static readonly string[] XpCloseGlyph =
    {
        "kk....kk",
        "kkk..kkk",
        ".kkkkkk.",
        "..kkkk..",
        "..kkkk..",
        ".kkkkkk.",
        "kkk..kkk",
        "kk....kk",
    };
    static readonly string[] XpMinGlyph = { "kkkkkk", "kkkkkk", "kkkkkk" };
    static readonly string[] XpMaxGlyph =
    {
        "kkkkkkkkkk",
        "kkkkkkkkkk",
        "kkkkkkkkkk",
        "k........k",
        "k........k",
        "k........k",
        "k........k",
        "k........k",
        "k........k",
        "kkkkkkkkkk",
    };
    static readonly string[] XpHelpGlyph =
    {
        ".kkkk.",
        "kk..kk",
        "kk..kk",
        "...kk.",
        "..kk..",
        "..kk..",
        "......",
        "..kk..",
        "..kk..",
    };

    // the scroll bar's and drop-down's chevrons (#4D6185)
    static readonly string[] ChevronDown =
    {
        "k.....k",
        "kk...kk",
        ".kk.kk.",
        "..kkk..",
        "...k...",
    };
    static readonly string[] ChevronUp =
    {
        "...k...",
        "..kkk..",
        ".kk.kk.",
        "kk...kk",
        "k.....k",
    };

    // the option button: o = the border, a = its soft edge; inside, r = where the hot ring goes
    static readonly string[] RadioMap =
    {
        "...aooooa....",
        "..oo.....oo..",
        ".o.........o.",
        "ao.........oa",
        "o...........o",
        "o...........o",
        "o...........o",
        "o...........o",
        "o...........o",
        "ao.........oa",
        ".o.........o.",
        "..oo.....oo..",
        "...aooooa....",
    };
    static readonly string[] RadioRing =
    {
        ".............",
        "....rrrrr....",
        "..rrrrrrrrr..",
        "..rr.....rr..",
        ".rr.......rr.",
        ".rr.......rr.",
        ".rr.......rr.",
        ".rr.......rr.",
        ".rr.......rr.",
        "..rr.....rr..",
        "..rrrrrrrrr..",
        "....rrrrr....",
        ".............",
    };
    static readonly string[] RadioDotMap =
    {
        ".mmm.",
        "mGGmm",
        "mGmmd",
        "mmmdd",
        ".mdd.",
    };

    // the trackbar thumb pointing down at its ticks (11 x 21), and the plain one (no ticks): o the edge, w its light, b the body,
    // s the shade, g G the green tip (orange when hot)
    static readonly string[] ThumbDown =
    {
        ".ooooooooo.",
        "owwwwwwwwso",
        "owbbbbbbbso",
        "owbbbbbbbso",
        "owbbbbbbbso",
        "owbbbbbbbso",
        "owbbbbbbbso",
        "owbbbbbbbso",
        "owbbbbbbbso",
        "owbbbbbbbso",
        "owbbbbbbbso",
        "owbbbbbbbso",
        "owbbbbbbbso",
        "oGbbbbbbbgo",
        "oGGbbbbbggo",
        ".oGGbbbggo.",
        "..oGGbggo..",
        "...oGggo...",
        "....ogo....",
        ".....o.....",
        "...........",
    };
    static readonly string[] ThumbBoth =
    {
        ".ooooooooo.",
        "owwwwwwwwso",
        "owbbbbbbbso",
        "owbbbbbbbso",
        "owbbbbbbbso",
        "owbbbbbbbso",
        "owbbbbbbbso",
        "owbbbbbbbso",
        "owbbbbbbbso",
        "owbbbbbbbso",
        "owbbbbbbbso",
        "owbbbbbbbso",
        "owbbbbbbbso",
        "oGGGGGGGGgo",
        "ogggggggggo",
        ".ooooooooo.",
    };
}
