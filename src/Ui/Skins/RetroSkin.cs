using System;
using System.Collections.Generic;
using System.Drawing;
using Vortice.DirectWrite;

namespace Orbit;

// Look > Style: Modern, or one of the retro styles. Every retro style is drawn by the Windows 95 code paths (Theme.Classic is true
// for all of them: the same small pixel canvases, the same layouts, the same hit areas); what each style changes goes through its
// skin - the palette, the font, the frames and every control - and, where a style lays a whole screen out its own way, through
// the screen hooks (ScreenHooks.cs).
public enum RetroStyle { None, Win95, XP, Aero, Mac, Terminal }

// what a caption box does
public enum CaptionKind { Close, Help, Min, Max }

// A retro style's look. Win95Skin is the Windows 95 look exactly as it was; the other styles derive from it and override what
// their era drew differently. Coordinates are pixels of the small canvas (Painter.BeginPixel); the canvas is shown 2x.
//
// The rules for a skin:
// - it only draws: the hit areas, the metrics the controls react to and the layouts stay in Ui / the panels (the same for
//   every style, so a laser hits the same places);
// - everything a control looks like comes in as its state (enabled, hover, down, default, latched, open, selected);
// - colours: the palette below (the Theme.W95* names read it, so the shared screens follow it), MapColor (every brush) and
//   TextColor (every retro text);
// - text: Text / TextWidth / Label (access keys, "&File"), in the skin's Font; MapFont and PixelFont change how pixel-mode text
//   is drawn (a bitmap font of its own: PixelFont.cs).
public abstract class RetroSkin
{
    // ---- who it is ----
    public abstract RetroStyle Style { get; }
    public abstract string Id { get; }          // AppearanceConfig.Style: classic | xp | aero | mac | terminal
    public abstract string Tag { get; }         // --snapshot's file suffix: 95 | xp | aero | mac | term
    public abstract string Name { get; }        // the Look pickers' name

    // ---- the palette (Windows' system colours of the era) ----
    public abstract uint Face { get; }                  // COLOR_3DFACE: dialogs, buttons, the window face
    public abstract uint Light { get; }                 // COLOR_3DLIGHT
    public abstract uint Hilight { get; }               // COLOR_3DHILIGHT: the lit edge
    public abstract uint Shadow { get; }                // COLOR_3DSHADOW
    public abstract uint DarkShadow { get; }            // COLOR_3DDKSHADOW: the outer dark edge, the default button's frame
    public abstract uint Window { get; }                // COLOR_WINDOW: text boxes, lists, wells
    public abstract uint WindowText { get; }            // text on the face and in windows
    public abstract uint ButtonText { get; }            // a button's text and glyphs
    public abstract uint GrayText { get; }              // disabled / secondary text
    public abstract uint DimText { get; }               // the modern style's Dim text drawn in this style
    public abstract uint Highlight { get; }             // a selection
    public abstract uint HighlightText { get; }
    public abstract uint ActiveCaption { get; }
    public abstract uint CaptionText { get; }
    public abstract uint InactiveCaption { get; }
    public abstract uint InactiveCaptionText { get; }
    public abstract uint Desktop { get; }               // behind the windows (the watch's desktop, the settings' backdrop)
    public abstract uint Laser { get; }                 // the laser's "same as the style" colour

    // every brush colour while this style is on (after the modern text greys became WindowText / DimText / GrayText); its own
    // drawing goes through it too. Win95: as it is.
    public abstract uint MapColor(uint c);
    // every text drawn by Text / Label (black text, grey text, white text on a selection...)
    public abstract uint TextColor(uint c);

    // ---- type ----
    public abstract string Font { get; }                // the family of every retro text (Theme.RetroFont)
    // pixel-mode text in any family (the retro one, the icon font): change the family, size or weight it is drawn in
    public abstract void MapFont(ref string family, ref float size, ref FontWeight weight);
    // a bitmap font for this text (after MapFont), or null for DirectWrite's aliased glyphs
    public abstract PixelFont PixelFont(string family, float size, FontWeight weight);
    // pixel-mode text grey-scale smoothed (Windows 7) instead of aliased (95 / XP)
    public abstract bool SmoothText { get; }

    // ---- metrics the shared controls follow (hit areas included) ----
    public abstract float DropRowH { get; }             // a drop-down list's row

    // ---- text ----
    public abstract void Text(Painter p, string s, RectangleF r, uint c, bool bold, TextAlignment al, bool enabled, bool wrap, float size);
    public abstract float TextWidth(Painter p, string s, bool bold, float size);
    // a label with an access key: s is "&Size:" (the S underlined), "&&" a plain &
    public abstract void Label(Painter p, string s, RectangleF r, bool enabled, TextAlignment al, uint c, bool bold);
    public abstract void DisabledText(Painter p, string s, RectangleF r, float size, FontWeight w, TextAlignment h, ParagraphAlignment v);

    // ---- frames ----
    public abstract void Bevel(Painter p, RectangleF r, Painter.Edge e);
    public abstract void EtchedLine(Painter p, float x1, float x2, float y);
    public abstract void FillDesktop(Painter p, RectangleF r);
    // a title bar in r (r is 18 px high, 3 px inside the window frame; its caption boxes are drawn after it)
    public abstract void Caption(Painter p, RectangleF r, string title, W95Icons.Id? icon, bool active);
    // a caption box: r is 16 x 14
    public abstract void CaptionButton(Painter p, RectangleF r, CaptionKind kind, bool enabled, bool down, bool hover);
    // a watch widget's little window on the custom face: its frame and (title != null, a widget 2 cells high or more) its title bar;
    // returns the area inside, where the widget draws itself
    public abstract RectangleF WidgetFrame(Painter p, RectangleF r, string title);

    // ---- controls ----
    // a push button's face (isDefault: a dialog's default button; latched: a toggle that is in); returns the face inside its frame
    public abstract RectangleF ButtonFace(Painter p, RectangleF r, bool isDefault, bool down, bool latched, bool hover);
    // a push button with its label ("&OK") and / or icon
    public abstract void PushButton(Painter p, RectangleF r, string label, W95Icons.Id? icon, bool enabled, bool isDefault, bool down,
        bool latched, bool hover);
    // a push button with a little pixel picture ('k' = a pixel), the CD Player's |<<  >  >>|
    public abstract void GlyphButton(Painter p, RectangleF r, string[] glyph, bool enabled, bool down, bool hover);
    // a key of the keyboard (oneShot: a modifier on for the next key, latched: locked); returns the face the legend goes on
    public abstract RectangleF KeyCap(Painter p, RectangleF r, bool oneShot, bool down, bool latched, bool hover);
    // a check box at x, y (the box 13 x 13) with its label right of it
    public abstract void CheckBox(Painter p, float x, float y, string label, bool value, bool enabled, bool held, bool hover);
    public abstract void Radio(Painter p, float x, float y, string label, bool on, bool enabled, bool held, bool hover);
    public abstract void GroupBox(Painter p, RectangleF r, string title);
    // r as Ui.Track95 has it: the channel is (r.X + 6, r.Y + 8, r.Width - 12, 4), the thumb's middle at its t
    public abstract void Trackbar(Painter p, RectangleF r, float t, bool enabled, int ticks, bool hover, bool held);
    // block: the normal colour (0xFF000080 or Highlight) or a warning one (0xFF800000: a low battery)
    public abstract void Progress(Painter p, RectangleF r, float frac, uint block);
    // a property sheet tab (the selected one comes 2 px bigger, drawn last)
    public abstract void Tab(Painter p, RectangleF r, string label, bool selected, bool hover);
    public abstract void ListBox(Painter p, RectangleF r);
    public abstract void ListSelection(Painter p, RectangleF row);
    // the vertical scroll bar (16 wide): its arrows (16 x 16) and thumb
    public abstract void ScrollBar(Painter p, RectangleF bar, RectangleF up, RectangleF down, RectangleF thumb, bool upHeld, bool downHeld);
    public abstract void EditBox(Painter p, RectangleF r, string text, bool enabled);
    public abstract void ComboBox(Painter p, RectangleF r, string text, bool enabled, bool down, bool open, bool hover);
    // the open list of a combo box, rows of rowH
    public abstract void DropList(Painter p, RectangleF r, string[] items, int hot, float rowH);
    public abstract void StatusPane(Painter p, RectangleF r, string text, uint dot);
    public abstract void MenuItem(Painter p, float x, float y, string text);

    // ---- icons: true when the skin drew this one itself (else W95Icons' picture) ----
    public abstract bool Icon(Painter p, W95Icons.Id id, float x, float y, int scale);

    // ---- motion: a style's own animations (Look > Motion). The defaults are 1995's: Win95Skin does not override them ----

    // a pixel canvas starts drawing (Painter.BeginPixel): an animating skin keeps its state per canvas
    public virtual void BeginCanvas(Painter p, object canvas) { }
    // when the canvas drawn last wants its next frame (Clock.Now seconds; infinity: not until something changes) - the panels read
    // it right after they drew
    public virtual double NextFrame => double.PositiveInfinity;
    // the watch's opened widget window zooming between its tile and the window: how long (0: at once), and how it is drawn -
    // ZoomFrame at each step (false: 95's grey outline), or LiveZoom: the window itself all the way, opening and closing (its
    // skin's detail hook fades / scales it)
    public virtual double ZoomSeconds => 0.22;
    public virtual bool ZoomFrame(Painter p, RectangleF r, float t) => false;
    public virtual bool LiveZoom => false;
    // a list box at row 'top': the (fractional) row it is drawn from - a skin may glide there (smooth scrolling)
    public virtual float ListScroll(Painter p, RectangleF box, int top) => top;
    // something busy in r (checking for updates; frac < 0: no idea how far, else how far): true when the skin drew its marquee /
    // progress bar there (95: the words say it)
    public virtual bool BusyBar(Painter p, RectangleF r, float frac) => false;

    // ---- helpers for every skin ----

    public float LabelWidth(Painter p, string s, bool bold = false) => TextWidth(p, Ui.Amp(s).text, bold, Ui.F95);

    // s cut with "..." to w (the panels' Fit)
    public string Fit(Painter p, string s, float w, float size = Ui.F95, bool bold = false)
    {
        if (string.IsNullOrEmpty(s) || TextWidth(p, s, bold, size) <= w) return s;
        int n = s.Length;
        while (n > 1 && TextWidth(p, s[..n] + "...", bold, size) > w) n--;
        if (n > 1 && char.IsHighSurrogate(s[n - 1])) n--;
        return s[..n].TrimEnd() + "...";
    }

    // a top-to-bottom gradient as whole-pixel rows (the banding of the era's gradients; no smoothing in pixel mode)
    public static void RowsGradient(Painter p, RectangleF r, uint top, uint bottom)
    {
        int n = (int)MathF.Round(r.Height);
        for (int i = 0; i < n; i++)
            p.Fill(new RectangleF(r.X, r.Y + i, r.Width, 1), Theme.Mix(top, bottom, n == 1 ? 0 : i / (float)(n - 1)));
    }

    // left to right, as whole-pixel columns
    public static void ColumnsGradient(Painter p, RectangleF r, uint left, uint right)
    {
        int n = (int)MathF.Round(r.Width);
        for (int i = 0; i < n; i++)
            p.Fill(new RectangleF(r.X + i, r.Y, 1, r.Height), Theme.Mix(left, right, n == 1 ? 0 : i / (float)(n - 1)));
    }
}

// the styles, and their names in the config and the pickers
public static class Skins
{
    public static readonly RetroSkin Win95 = new Win95Skin(), Xp = new XpSkin(), Aero = new AeroSkin(), Mac = new MacSkin(), Terminal = new TerminalSkin();
    public static readonly RetroSkin[] All = { Win95, Xp, Aero, Mac, Terminal };

    // the Look pickers, Modern first
    public static readonly string[] StyleIds = { "modern", "classic", "xp", "aero", "mac", "terminal" };
    public static readonly string[] StyleNames = { "Modern", "Windows 95", "Windows XP", "Windows 7", "Classic Mac", "Terminal" };

    public static RetroSkin For(RetroStyle s) => s switch
    {
        RetroStyle.XP => Xp, RetroStyle.Aero => Aero, RetroStyle.Mac => Mac, RetroStyle.Terminal => Terminal, _ => Win95,
    };

    // AppearanceConfig.Style -> the style ("modern" and anything unknown: None)
    public static RetroStyle Parse(string style)
    {
        foreach (var s in All) if (s.Id == style) return s.Style;
        return RetroStyle.None;
    }

    // the pickers' index of a config value (unknown: Modern)
    public static int IndexOf(string style) => Math.Max(0, Array.IndexOf(StyleIds, style));
}
