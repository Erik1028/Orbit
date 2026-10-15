using System.Collections.Generic;
using Vortice.Direct2D1;

namespace Orbit;

// The Classic Mac skin's own small icons, where the Mac had its own picture: the compact Macintosh for the monitors, an Apple
// keyboard, the alarm clock, the lavender Mac OS 8 folder, the Trash. The rest stay W95Icons' (their 16 colours were the
// Mac's 16 colours too).
public sealed partial class MacSkin
{
    // k black  w white  s platinum  a light grey  g grey  d dark grey  y yellow  r red  c screen blue
    // l folder lavender  b folder shade  v folder edge  . transparent
    static uint MacPal(char c) => c switch
    {
        'k' => 0xFF000000, 'w' => 0xFFFFFFFF, 's' => 0xFFDDDDDD, 'a' => 0xFFBBBBBB, 'g' => 0xFF999999, 'd' => 0xFF555555,
        'y' => 0xFFFFCC00, 'r' => 0xFFDD0000, 'c' => 0xFFAACCFF, 'l' => 0xFFCCCCFF, 'b' => 0xFF9999DD, 'v' => 0xFF555599,
        _ => 0,
    };

    static string[] MacMap(W95Icons.Id id) => id switch
    {
        W95Icons.Id.Computer or W95Icons.Id.ComputerOff => Compact(id == W95Icons.Id.Computer ? 'c' : 'd'),
        W95Icons.Id.Keyboard => new[]
        {
            "................", "................", "................", "................",
            ".kkkkkkkkkkkkkk.", ".kwwwwwwwwwwwwk.", ".kswsswsswsswsk.", ".kssssssssssssk.", ".kswsswsswsswsk.",
            ".kssssssssssssk.", ".ksswwwwwwwsssk.", ".kggggggggggggk.", ".kkkkkkkkkkkkkk.",
            "................", "................", "................",
        },
        W95Icons.Id.Center => new[]
        {
            "................", ".......k........", ".....kkkkk......", "....k..k..k.....", "...k...k...k....", "..k....k....k...",
            "..k...rrr...k...", ".kkkkkkrkkkkkkk.", "..k...rrr...k...", "..k....k....k...", "...k...k...k....", "....k..k..k.....",
            ".....kkkkk......", ".......k........", "................", "................",
        },
        W95Icons.Id.Moon => new[]
        {
            "................", ".....kkkk.......", "...kkyyk........", "..kyyyk.........", "..kyyk..........", ".kyyyk..........",
            ".kyyyk..........", ".kyyyk..........", ".kyyyyk.........", ".kyyyyyk.....k..", "..kyyyyykkkkyk..", "..kkyyyyyyyyk...",
            "....kkyyyyykk...", "......kkkkk.....", "................", "................",
        },
        W95Icons.Id.Clock => new[]
        {
            "................", "..kk.......kk...", ".kyyk.....kyyk..", ".kyk.kkkkk.kyk..", "..k.kwwwwwk.k...", "...kwwwkwwwk....",
            "..kwwwwkwwwwk...", "..kwwwwkwwwwk...", "..kwwwwkkkwwk...", "..kwwwwwwwwwk...", "...kwwwwwwwk....", "....kkkkkkk.....",
            "...k.......k....", "................", "................", "................",
        },
        W95Icons.Id.Note => new[]
        {
            "................", "................", "......kkkkkkkk..", "......kkkkkkkk..", "......k......k..", "......k......k..",
            "......k......k..", "......k......k..", "......k......k..", "...kkkk...kkkk..", "..kkkkk..kkkkk..", "..kkkk...kkkk...",
            "...kk.....kk....", "................", "................", "................",
        },
        W95Icons.Id.Folder => new[]
        {
            "................", "................", "..vvvvv.........", ".vwllllvvvvvvvv.", ".vllllllllllllv.", ".vwwwwwwwwwwwwv.",
            ".vllllllllllllv.", ".vllllllllllllv.", ".vllllllllllllv.", ".vllllllllllllv.", ".vllllllllllllv.", ".vbbbbbbbbbbbbv.",
            ".vvvvvvvvvvvvvv.", "................", "................", "................",
        },
        W95Icons.Id.Delete => new[]
        {
            "................", "......kkkk......", "..kkkkk..kkkkk..", "..kwwwwwwwwwwk..", "..kkkkkkkkkkkk..", "...kwgwgwgwgk...",
            "...kwgwgwgwgk...", "...kwgwgwgwgk...", "...kwgwgwgwgk...", "...kwgwgwgwgk...", "...kwgwgwgwgk...", "...kwgwgwgwgk...",
            "...kkkkkkkkkk...", "................", "................", "................",
        },
        _ => null,
    };

    // the compact Macintosh: a lit screen ('c') or a dark one ('d')
    static string[] Compact(char screen)
    {
        string S(string row) => row.Replace('c', screen);
        return new[]
        {
            "................", "...kkkkkkkkkk...", "...kwwwwwwwwk...", "...kwkkkkkkwk...", S("...kwkcccckwk..."), S("...kwkcccckwk..."),
            S("...kwkcccckwk..."), "...kwkkkkkkwk...", "...kwwwwwwwwk...", "...kwwwwkkkwk...", "...kwwwwwwwwk...", "...kggggggggk...",
            "...kkkkkkkkkk...", "....kssssssk....", "....kkkkkkkk....", "................",
        };
    }

    static readonly Dictionary<(Painter, W95Icons.Id), ID2D1Bitmap> _icons = new();

    static bool DrawMacIcon(Painter p, W95Icons.Id id, float x, float y, int scale)
    {
        if (!_icons.TryGetValue((p, id), out var bmp))
        {
            var m = MacMap(id);
            if (m != null)
            {
                int h = m.Length, w = m[0].Length;
                var px = new uint[w * h];
                for (int yy = 0; yy < h; yy++)
                    for (int xx = 0; xx < w; xx++) px[yy * w + xx] = MacPal(m[yy][xx]);
                bmp = p.CreateBitmap(px, w, h);
            }
            _icons[(p, id)] = bmp;
        }
        if (bmp == null) return false;
        int n = 16 * scale;
        p.ImageNearest(bmp, new System.Drawing.RectangleF(x, y, n, n));
        return true;
    }
}
