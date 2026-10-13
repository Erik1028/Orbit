using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Orbit;

// The current Windows mouse cursor (arrow, I-beam, hand, resize...) as a 64x64 texture, so the
// laser can draw it straight onto a monitor panel instead of waiting for it to come back through
// the desktop capture (that round trip is ~30-60 ms).
public sealed class SystemCursor : IDisposable
{
    public const int Size = 64;
    public readonly Surface Surf;
    public Vector2 Hotspot { get; private set; } = new(0, 0);
    public int Version { get; private set; }
    readonly Gpu _g;
    IntPtr _last;
    double _nextCheck;

    [StructLayout(LayoutKind.Sequential)]
    struct CURSORINFO { public int cbSize, flags; public IntPtr hCursor; public Native.POINT pt; }
    [StructLayout(LayoutKind.Sequential)]
    struct ICONINFO { public bool fIcon; public int xHotspot, yHotspot; public IntPtr hbmMask, hbmColor; }
    [StructLayout(LayoutKind.Sequential)]
    struct BITMAPINFOHEADER
    {
        public int biSize, biWidth, biHeight; public short biPlanes, biBitCount;
        public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
    }

    [DllImport("user32.dll")] static extern bool GetCursorInfo(ref CURSORINFO ci);
    [DllImport("user32.dll")] static extern bool GetIconInfo(IntPtr h, out ICONINFO ii);
    [DllImport("user32.dll")] static extern bool DrawIconEx(IntPtr hdc, int x, int y, IntPtr icon, int w, int h, int step, IntPtr brush, int flags);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bi, int usage, out IntPtr bits, IntPtr section, int offset);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);

    public SystemCursor(Gpu g)
    {
        _g = g;
        Surf = new Surface(g, Size, Size);
    }

    // Cheap to call every frame; re-renders only when the cursor shape changes.
    public void Update(double now)
    {
        if (now < _nextCheck) return;
        _nextCheck = now + 0.03;
        var ci = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
        if (!GetCursorInfo(ref ci) || ci.hCursor == IntPtr.Zero || ci.hCursor == _last) return;
        _last = ci.hCursor;
        try { Render(ci.hCursor); Version++; }
        catch (Exception e) { Log.Warn("cursor: " + e.Message); }
    }

    // Drawn twice (on black and on white): the difference gives the alpha, which also handles the
    // old monochrome cursors that GDI draws with XOR masks.
    unsafe void Render(IntPtr cursor)
    {
        var hot = Vector2.Zero;
        if (GetIconInfo(cursor, out var ii))
        {
            hot = new Vector2(ii.xHotspot, ii.yHotspot);
            if (ii.hbmMask != IntPtr.Zero) DeleteObject(ii.hbmMask);
            if (ii.hbmColor != IntPtr.Zero) DeleteObject(ii.hbmColor);
        }
        uint[] black = Draw(cursor, 0x00000000, _black), white = Draw(cursor, 0x00FFFFFF, _white);
        var px = _px;
        for (int i = 0; i < px.Length; i++)
        {
            uint b = black[i], w = white[i];
            int a = 255 - Math.Clamp((int)((w >> 8) & 0xFF) - (int)((b >> 8) & 0xFF), 0, 255);   // green channel
            // premultiplied colour = the image on black
            px[i] = ((uint)a << 24) | (b & 0x00FFFFFF);
        }
        _g.Ctx.UpdateSubresource(px, Surf.Tex, 0, Size * 4, 0, null);
        Hotspot = hot;
    }

    // reused for every cursor shape (they were 3 x 16 KB of garbage per change)
    readonly uint[] _black = new uint[Size * Size], _white = new uint[Size * Size], _px = new uint[Size * Size];

    static unsafe uint[] Draw(IntPtr cursor, uint background, uint[] r)
    {
        var bi = new BITMAPINFOHEADER { biSize = Marshal.SizeOf<BITMAPINFOHEADER>(), biWidth = Size, biHeight = -Size, biPlanes = 1, biBitCount = 32 };
        IntPtr dc = CreateCompatibleDC(IntPtr.Zero);
        IntPtr bmp = CreateDIBSection(dc, ref bi, 0, out IntPtr bits, IntPtr.Zero, 0);
        IntPtr old = SelectObject(dc, bmp);
        try
        {
            uint* p = (uint*)bits;
            for (int i = 0; i < Size * Size; i++) p[i] = background;
            DrawIconEx(dc, 0, 0, cursor, 0, 0, 0, IntPtr.Zero, 3 /* DI_NORMAL */);
            for (int i = 0; i < r.Length; i++) r[i] = p[i];
            return r;
        }
        finally
        {
            SelectObject(dc, old);
            DeleteObject(bmp);
            DeleteDC(dc);
        }
    }

    public void Dispose() => Surf.Dispose();
}
