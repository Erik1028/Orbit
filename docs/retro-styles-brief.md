# Retro styles foundation brief

commit: e8883c2 on branch retro-base (parent a5265e8), in worktree C:/Users/Erik/Desktop/project/Orbit-retro. Nothing was committed to main (it is still at a5265e8) and nothing was pushed. dist and the user config were not touched. Message: "Retro styles foundation: skins for Windows 95, XP, 7, Classic Mac, Terminal", ending with the Co-Authored-By line. 31 files changed: 1581 lines added, 476 removed. The new folder is src/Ui/Skins (RetroSkin.cs, Win95Skin.cs, XpSkin.cs, AeroSkin.cs, MacSkin.cs, TerminalSkin.cs, PixelFont.cs, ScreenHooks.cs). All files use CRLF line endings.

## design
BRIEF FOR THE FOUR STYLE AGENTS (start from commit e8883c2, branch retro-base)

0. SETUP
Create your own worktree:
  git -C C:/Users/Erik/Desktop/project/Orbit worktree add C:/Users/Erik/Desktop/project/Orbit-<style> -b retro-<style> e8883c2
Build:
  dotnet build Orbit.csproj -c Release -nologo -v q
Exe: bin/Release/net8.0-windows10.0.22621.0/win-x64/OrbitOverlay.exe
Copy the exe folder to a scratch directory before running it, so you can keep building while it runs.

1. THE STYLE MODEL
- AppearanceConfig.Style values: "modern" | "classic" (Windows 95) | "xp" | "aero" (Windows 7) | "mac" | "terminal". Any unknown value means Modern.
- enum RetroStyle { None, Win95, XP, Aero, Mac, Terminal } lives in src/Ui/Skins/RetroSkin.cs.
- Theme.Retro is the current style.
- Theme.Classic == (Retro != None). It is TRUE FOR EVERY RETRO STYLE, so all styles share the 95 code paths:
  - the 2x pixel canvases (watch TW95 x TH95 = 320x220; keyboard 740x338; settings 750x480; dock and room panels 400x240; theater bar _w95 x 34);
  - the layouts, the hit areas and the 2x pointer mapping.
- Theme.RetroSkin is the current skin. It is never null: Win95Skin while Modern is on.
- The registry is static class Skins:
  - Skins.Win95, Xp, Aero, Mac, Terminal;
  - Skins.All, in the order 95, xp, aero, mac, term;
  - StyleIds and StyleNames (used by both pickers);
  - Parse(string), For(RetroStyle), IndexOf(string).
- Theme.Apply for a retro style sets: Bg = Panel = Panel2 = Face, Panel3 = Light, Border = Shadow, OverlayBg = Face, Accent = Highlight, OnAccent = HighlightText, Laser = skin.Laser (unless the user picked a laser colour).
- These are now properties that read the skin's palette: Theme.W95Face, W95Light, W95Hi, W95Shadow, W95Dark, W95Navy (= Highlight), W95Teal (= Desktop), W95Window and ClassicLaser.
- Theme.RetroFont = skin.Font. Theme.Font and Theme.Display return RetroFont in every retro style.
- Theme.Skin(c) runs for every Painter brush colour. In a retro style it maps the modern Text, Dim and Faint greys to WindowText, DimText and GrayText, then passes the result through skin.MapColor.
- The pickers:
  - The modern Look tab now has a Style field. Its list opens over the page (SettingsView.DrawStyleList), and each style gets a little window chip drawn in its own palette.
  - The 95 sheet's Look tab has a Style box with all six.
  - A pick applies live through _h.AppearanceChanged().

2. THE SKIN INTERFACE: abstract class RetroSkin (src/Ui/Skins/RetroSkin.cs)
Win95Skin (src/Ui/Skins/Win95Skin.cs) implements every member with exactly the 1.12.3 pixels. It reads its own palette properties, so anything you do not override is 1995 drawn in your palette.

Identity:
- Style, Id (the config string), Tag ("95", "xp", "aero", "mac", "term"; used as the file suffix), Name (shown in the pickers).

Palette (uint ARGB):
- Face, Light, Hilight, Shadow, DarkShadow, Window, WindowText, ButtonText, GrayText, DimText, Highlight, HighlightText, ActiveCaption, CaptionText, InactiveCaption, InactiveCaptionText, Desktop, Laser.

Colour hooks:
- MapColor(uint) is applied to EVERY solid brush while your style is on, including your own drawing and Fill / Pixels / Text.
  - It is NOT applied to bitmaps (W95Icons, album art, Minesweeper, ImageNearest) or to FillChecker.
  - Leave it as identity unless you want a global remap. Terminal can use it to turn everything into green phosphor by luminance.
- TextColor(uint) is applied by Win95Skin.Text and Label: black text, grey text, white on a selection, and so on.

Type:
- Font: the family name of every retro text.
- MapFont(ref string family, ref float size, ref FontWeight w): called for all PIXEL-MODE text in any family, including the icon font. It covers Painter.Text, MeasureWidth and MeasureHeight.
- PixelFont(family, size, weight): return a PixelFont (a bitmap font) or null (null = DirectWrite aliased glyphs).
- SmoothText: true means grey-scale anti-aliased text on the pixel canvas (Windows 7). Shapes stay aliased.

Metric:
- DropRowH: a drop-list row, 13 in 95. The combo hit areas and the snapshot clicks follow it.

Text:
- Text(p, s, r, c, bold, al, enabled, wrap, size)
- TextWidth(p, s, bold, size)
- Label(p, s, r, enabled, al, c, bold): s carries access keys like "&Size:". Ui.Amp(s) returns (text, keyIndex).
- DisabledText(p, s, r, size, w, h, v)

Frames:
- Bevel(p, r, Painter.Edge e). Edge is { Raised, Window, Pressed, Sunken, Etched, Page }:
  - Window = a window frame;
  - Page = the settings property-sheet page, drawn like Window in 95.
- EtchedLine(p, x1, x2, y)
- FillDesktop(p, r)
- Caption(p, r, title, W95Icons.Id? icon, active):
  - r is 18 px high and sits 3 px inside the window frame;
  - it is drawn after Bevel(Window), so it may paint over the frame top;
  - the caption boxes are drawn after it.
- CaptionButton(p, r 16x14, CaptionKind {Close, Help, Min, Max}, enabled, down, hover)
- WidgetFrame(p, r, title-or-null) → content rect. This is the custom-face widget's little window; a title is passed when the widget is 2 or more cells high.

Controls:
- ButtonFace(p, r, isDefault, down, latched, hover) → the face rect
- PushButton(p, r, label, icon?, enabled, isDefault, down, latched, hover)
- GlyphButton(p, r, string[] glyph, enabled, down, hover)
- KeyCap(p, r, oneShot, down, latched, hover) → the face rect (one keyboard key; real per-laser hover)
- CheckBox(p, x, y, label, value, enabled, held, hover): the box is 13x13 at x, y; the label starts at x + 19.
- Radio(p, x, y, label, on, enabled, held, hover)
- GroupBox(p, r, title)
- Trackbar(p, r, t, enabled, ticks, hover, held): the channel is (r.X + 6, r.Y + 8, r.Width - 12, 4); the thumb's centre is at round(chX + chW * t).
- Progress(p, r, frac, block): block is the normal colour (0xFF000080 or Highlight) or warning maroon 0xFF800000.
- Tab(p, r, label, selected, hover): tabs are LabelWidth + 14 wide and 18 high; the selected tab is passed 2 px bigger and drawn last.
- ListBox(p, r)
- ListSelection(p, row)
- ScrollBar(p, bar 16 wide, up 16x16, down 16x16, thumb, upHeld, downHeld)
- EditBox(p, r, text, enabled)
- ComboBox(p, r, text, enabled, down, open, hover)
- DropList(p, r, items, hot, rowH)
- StatusPane(p, r, text, dot)
- MenuItem(p, x, y, text)

Icons:
- Icon(p, W95Icons.Id id, x, y, scale): return true when you drew it yourself.

Helpers (not virtual):
- LabelWidth(p, s, bold)
- Fit(p, s, w, size, bold): cuts with "..."
- static RowsGradient(p, r, top, bottom) and ColumnsGradient(p, r, left, right): banded whole-pixel gradients.

Win95Skin protected members you can reuse:
- CheckGlyph, CloseGlyph, HelpGlyph, MinGlyph, MaxGlyph, UpGlyph, DownGlyph;
- static Frame(p, r, tl, br, px);
- ArrowButton(p, r, glyph, held).

Painter extras for skins (Fill and Stroke force square corners in the retro styles, and FillGradient is flat there):
- FillRound(r, c, radius)
- StrokeRound(r, c, w, radius)
- GradientRaw(r, a, b[, c], radius, angleDeg): a real D2D gradient whose colours go through MapColor
- Smooth(bool): anti-aliased shapes on the pixel canvas until Smooth(false)
- FillMask(...): used by PixelFont
- Existing helpers: Pixels(string[] map, x, y, c) where 'k' = ink; FillChecker(r, a, b); ImageNearest; CreateBitmap; PushClip / PopClip; Polygon; Line; Circle; Px.

PixelFont (src/Ui/Skins/PixelFont.cs):
  new PixelFont(height, ascent, Dictionary<char, string[]>) { Spacing, SpaceWidth, Smear, Fallback }
- Glyph rows use '.' or ' ' for empty and any other character for ink. Rows are top-aligned.
- Bold = smeared 1 px. A missing letter falls back to the other case, then to '?'.
- It handles alignment, wrapping and clipping.
- It is one font per pixel size: map sizes in PixelFont(family, size, weight). 11 = 8 pt body text; the watch uses 13 / 16 / 30–48 for clocks.

3. WHERE THE 95 CODE CALLS THE SKIN
Painter (src/Gfx/Painter.cs):
- Bevel → skin.Bevel
- ButtonFace95 → skin.ButtonFace (the Hexpad buttons in DockPanel and WristDock)
- EtchedLine → skin.EtchedLine
- DisabledText → skin.DisabledText
- Text, MeasureWidth and MeasureHeight in pixel mode → skin.MapFont, then skin.PixelFont
- Brush → Theme.Skin → skin.MapColor
- BeginPixel → skin.SmoothText

Ui95 (src/Ui/Ui95.cs; behaviour and hit areas stay here, drawing goes to the skin):
- Text95 → Text; Width95 → TextWidth; Label95 → Label; LabelWidth95 → TextWidth of the Amp text
- Button95 → PushButton; GlyphButton95 → GlyphButton
- Check95 → CheckBox; Radio95 → Radio; Group95 → GroupBox
- Track95 → Trackbar; Progress95 → Progress; Tabs95 → Tab
- List95 → ListBox, ListSelection and ScrollBar
- Caption95 → Caption; CaptionClose95, CaptionHelp95, CaptionMin95 and CaptionMax95 → CaptionButton
- Edit95 → EditBox; Combo95 → ComboBox; Popups95 → DropList; Pane95 → StatusPane; Menu95 → MenuItem

Other call sites:
- W95Icons.Draw asks skin.Icon first.
- KeyboardPanel95 keys → KeyCap.
- WristCustom Custom95 widget windows → WidgetFrame.
- The custom-face desktop and the 95 Wrist-tab preview desktop → FillDesktop.
- The settings page → Edge.Page.
- KeyboardPanel95 and Notifications text use Theme.RetroFont.

Panel-local raw constants stay raw:
- Blk95, Wht95, Gry95, Navy95, Navy95c, Teal95, Deep95, K95, W95, Grey95, and literals such as 0xFF808080 and 0xFF800000.
- They reach your style only through TextColor (when drawn via Text95 / Label95) and MapColor.

4. SCREEN HOOKS (src/Ui/Skins/ScreenHooks.cs; it only declares them)
How they work:
- Each hook is one partial method per style, with the suffix Xp, Aero, Mac or Term.
- Implement it in a NEW file and set done = true when you drew the screen. If you leave done false, the shared 95 screen is drawn in your skin.
- Unimplemented hooks compile away.
- Your hook has all the class's private members, including the shared 95 pieces you can call: Media95, Dial95, Hand95, BigTime95, Status95, Graph95, Batteries95, Toolbar95, Value95, Time95, Calendar95, Room95Detail, Dock95Detail, Mines95Detail, Fit, and so on.

The hooks:
- SettingsView.Settings{S}(Ui ui, ref bool done)
  - Placed at the top of Draw95. The 750x480 pixel canvas is open.
  - What's New over the sheet is yours.
  - Render95 calls ui.Popups95() afterwards; the dispatcher does MarkDirty.
  - Terminal's TUI goes here.
- SettingsView.WatchEditor{S}(Ui ui, ref bool done): placed at the top of Wrist95Editor.
- WristPanel.Face{S}(PanelData d, ref bool done)
  - Placed at the top of Window95: the fixed faces full / analog / minimal / time. The 320x220 canvas is open.
  - Keep the dismiss logic: _dismissReq = true for _ and X.
- WristPanel.Custom{S}(PanelData d, ref bool done)
  - Replaces ALL of Custom95.
  - You must then call CardTap(id, w, k, r) for every widget, Taskbar95 when there are several pages, and DrawDetail95(d, list).
- WristPanel.Widget{S}(WatchWidget w, WatchWidgets.Kind k, RectangleF r, PanelData d, int id, ref bool done)
  - One widget including its frame; r is its cell area; its tap is already handled.
  - This is the light way to restyle the modular face.
- WristPanel.Detail{S}(PanelData d, WatchWidgets.Kind k, string kind, RectangleF win, ref bool done)
  - The opened widget window after the zoom.
  - kind is one of media, battery, clock, calendar, timer, stopwatch, perf, system, volume, mines, room, dock.
  - Keep the close behaviour: CloseDetail().
- WristPanel.Taskbar{S}(int count, int cur, ref bool done): the pages taskbar; call GoToPage(i).
- KeyboardPanel.Keyboard{S}(ref bool done)
  - Placed at the top of Draw95, BEFORE the canvas exists. Copy Draw95's canvas, BeginPixel, End and ImageNearest steps.
  - Keys are _keys plus _barNormal or _barChat, at Half(k.R). Hit testing uses k.R, so keys must stay where they are.
- TheaterBar.Theater{S}(ref bool done): the whole bar, before the canvas. Do what Draw95 does and end with Act(...).
- TheaterBar.TheaterWidth{S}(ref int w95, ref bool done): your canvas width, set in Relayout.
- WhatsNewView.WhatsNew{S}(Ui ui, RectangleF r, IReadOnlyList<WhatsNew.Entry> list, string sub, ref bool closed, ref bool done): a static partial method.
- NotificationPanel.Card{S}(Notification c, float y, ref bool done): one card on the 480x320 canvas.
- DockPanel.Dock{S}(double now, ref bool done): the whole Hexpad panel. Fill _hit[id] = Tex(box) as Draw95 does, because DockInput reads it.
- RoomPanel.Room{S}(DateTime now, ref bool done): the whole Room panel.

5. HOW A STYLE AGENT ADDS ITS LOOK
1. Edit ONLY your skin file: src/Ui/Skins/XpSkin.cs, AeroSkin.cs, MacSkin.cs or TerminalSkin.cs. Each is `public sealed partial class <X>Skin : Win95Skin`.
   - Override the palette, Font / MapFont / PixelFont / SmoothText, and any primitive, e.g. `public override void Caption(Painter p, RectangleF r, string title, W95Icons.Id? icon, bool active)`.
   - base.X(...) reuses the 95 body.
2. Put everything else in NEW files under src/Ui/Skins/<Style>/, for example:
   - src/Ui/Skins/Xp/WristPanel.Xp.cs containing `public sealed partial class WristPanel { partial void FaceXp(PanelData d, ref bool done) { ...; done = true; } }`;
   - big data such as a Chicago or VGA glyph table, or icon pixel art, in partial class <X>Skin files there.
   - Prefer C# data. Only if unavoidable, use assets/skins/<style>/; the csproj copies assets/**.
3. Concept crops (Hungarian labels; keep the app text in ENGLISH): scratchpad/retro/concept_xp.png, concept_aero.png, concept_mac.png, concept_terminal.png.
   - Rule: authentic era pixels, fonts, metrics and widgets, not a recolour.
   - Tahoma, Trebuchet MS, Segoe UI and Consolas are installed. Chicago and Geneva are not, so use PixelFont.
4. Keep every hit area and behaviour. Text must fit its boxes. Draw nothing outside a widget's cell; the layout check counts it.

6. MUST NOT TOUCH (so the four branches merge without conflicts)
Do not edit any existing file except your own <Style>Skin.cs. In particular:
- RetroSkin.cs, Win95Skin.cs, PixelFont.cs, ScreenHooks.cs;
- Painter.cs, Ui.cs, Ui95.cs, W95Icons.cs;
- every panel, SettingsView*.cs, Tools*.cs, *Test.cs, Program.cs, Config.cs, Orbit.csproj;
- docs (CHANGELOG, NOTES), the version;
- the other styles' files and folders.

Also:
- Never change Win95Skin or the 95 output: *_95.png must stay identical.
- Never change Ui metrics or hit areas: the snapshot's want checks click fixed coordinates (DropRowH is the only metric they follow).
- Do not add hooks. If one is missing, use the broader hook (Custom instead of Widget; Settings for a single tab) and report the need.
- General rules: never commit to main; never push; never touch dist or %LOCALAPPDATA%/Orbit/config.json; no --selftest, --scene or --docktest (docktest opens Hexpad's pipe); never open AjazzDock_ipc_v1.
- File handling: keep CRLF. The Write tool mangles C# \u escapes, so use Edit, or Python with newline=''.

7. SNAPSHOT NAMES PER STYLE (suffix = Tag: _95 unchanged, _xp, _aero, _mac, _term)
Settings:
- settings0..7_<t>, where the tabs are 0 Desktops, 1 Windows, 2 Layouts, 3 Wrist, 4 Keyboard, 5 Look, 6 VRChat, 7 General
- settings_combo_<t>, settings_panel_<t>, settings_whatsnew_<t>, whatsnew_panel_<t>
- settings_wrist_custom_<t>, settings_wrist_editor_<t>, settings_wrist_editor_<t>_extra
Watch:
- wrist_{full,analog,minimal,time}_<t>
- wrist_custom{0,1,2}_{panel,cards}_<t>
- wrist_pages{1,2,3}_<t>
- wrist_open_{media,battery,time,calendar,timer,stopwatch,fps,cpu,volume}_<t>
- wrist_open_room_*_<t>, wrist_open_dock_*_<t>
- mines_*_<t> (mines_95_vs_real only for 95)
Other panels:
- keyboard_<t>, notifications_<t>
- theater_bar_<t>, theater_bar_screen_<t>
- room_panel_<t>, dock_panel_<t>
QA pictures:
- qa/<tag>_<widget>_<w>x<h>.png, qa/<tag>43_*.png, qa/<tag>_open_*.png
Log lines:
- 'layout <tag>: N problem(s)'
- the want lines '<tag> drop-down: …', '<tag> editor: …', 'room view_<t>', 'dock view_<t>', 'opened widget_<t>', 'mines_<t>'
Still 95-only: PagesRun's pages_*_95 and settings_wrist_pages*_95.

8. FAST ITERATION (one style)
  ORBIT_SNAP_FONTS=0 OrbitOverlay.exe --snapshot <dir> --only-style xp > out.txt 2>&1
- The environment variable ORBIT_SNAP_STYLE=xp does the same. Accepted values: 95, xp, aero, mac, term (or the config ids).
- It takes about 12 s and writes only that style's pictures plus its layout check and want lines.
- Redirect stdout to your own file; the shared orbit-tool.log gets clobbered.
- Then Read the PNG you are working on. The canvases are 2x, so crop or zoom with Pillow for pixel detail.
- Run the full snapshot (with ORBIT_SNAP_FONTS=0, about 2.2 min) once or twice at the end. Check that 'layout <tag>: 0 problem(s)' and every 'want' line match, and that *_95.png is unchanged.

## checks
Build: dotnet build Orbit.csproj -c Release reports 0 errors and 0 warnings.

Baseline: I built a5265e8 unchanged and ran it twice: --snapshot with ORBIT_SNAP_FONTS=0, about 1.5 min per run. The second run marks the live areas (clock, CPU, window list).

Final full snapshot (e8883c2, about 2.2 min), stdout written to a file:
- All 78 'want' and 'layout' lines from the baseline appear unchanged, including 'layout modern: 0 problem(s)', 'layout styles: 0 problem(s) in 2074 faces, 11341 texts checked' and 'layout 95: 0 problem(s)'.
- New lines that all pass:
  - 'layout xp/aero/mac/term: 0 problem(s)' (these styles still draw Windows 95; once an agent restyles them they may report problems until it finishes);
  - per-style drop-down, editor, room view, dock view, opened widget and mines want lines;
  - 'look: Windows XP picked from the Style list applies at once True (want True); Modern again from the sheet's Style box True (want True)'.
- After this run I limited the winmine pixel-for-pixel check (mines_95_vs_real) to Windows 95, so the other styles may redraw Minesweeper. It now runs only for 95, where it passed.

*_95.png against main:
- Identical files include settings0, 2, 4, 5, 6, 7 _95, settings_combo_95, settings_panel_95, settings_whatsnew_95, whatsnew_panel_95, keyboard_95 and notifications_95.
- The other differing *_95 files I reviewed side by side with diff masks. Every difference is a live area: clock digits and hands, time in VR, the CPU/RAM graph, the window list on settings1_95, and the Minesweeper zoom outline caught mid-animation.
- Modern pictures differ only in the same live areas, plus the intended change: the Look tab's Style field (settings5.png, settings_alt.png).
- New pictures: settings_look_styles.png, and theater_bar, room_panel, dock_panel and wrist_pages for each style.

Skin hooks: with temporary overrides, now reverted, I confirmed that:
- a changed XP Face, Font (Tahoma) and ActiveCaption reach every screen;
- a Terminal PixelFont draws all pixel text, MapColor recolours every brush, and bitmaps stay as they are.

Fast mode: --only-style 95 and --only-style xp each take about 12 s, and 'layout xp: 0 problem(s)' was reported.

Not run, per the rules: --selftest, --scene, --docktest, --theatertest (it creates a VrSession), and the 9-minute font pass.
