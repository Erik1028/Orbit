# What's new in OrbitOverlay

This file is built into the app: after an update, the "What's new" window shows every section newer than the
version you came from. Format: "## <version> | <title>", then "- " points (an indented line continues a point).

## 1.13.1 | Cyber in every style list
- The Style list in the Windows 7, Classic Mac and Terminal settings now offers Cyber too (it had six styles
  there).

## 1.13.0 | Seven styles, all of them moving
- Settings > Look > Style has five new styles besides Modern and Windows 95: Windows XP, Windows 7, Classic Mac, a
  green Terminal and Cyber. Each one draws the watch, the settings, the keyboard, notifications, the screen bar and
  the Room and Hexpad views the way that system really looked.
- Windows XP: Luna windows, Tahoma, green progress blocks; opened widgets become Date and Time Properties, Task
  Manager and Windows Media Player; notifications are tray balloons.
- Windows 7: glass windows, the Control Panel look, the On-Screen Keyboard and the Windows 7 Minesweeper.
- Classic Mac: platinum windows with Chicago and Geneva type; the room is a Finder list, the screen bar a Control Strip.
- Terminal: green phosphor with scanlines, a BIOS-style setup screen and Unix broadcast messages.
- Cyber: a sci-fi HUD with yellow tags, cut corners, segmented bars and a glitching time card.
- Every style has its own animations: Cyber boots its HUD, Terminal switches on like a CRT, the Mac zooms its
  windows, XP and Windows 7 fade and glow, Modern springs and slides. Windows 95 stays as still as it was.
- Settings > Look > Motion: Full, Reduced (short transitions only) or Off (nothing moves).
- A new OrbitOverlay icon on the desktop, the taskbar and the tray.
- Fixed: the microphone widget no longer gives up after an audio error.

## 1.12.3 | Smooth Hexpad on the watch
- Hexpad's keys and knobs on the watch now move as smoothly as Hexpad sends them, instead of a few times a second.

## 1.12.2 | Room and Hexpad open on the watch
- The VRChat room widget opens right on the watch: tap it and it grows into the list of who is in the world, your
  friends, how long you have been there and who left lately (the stick scrolls). No separate window any more.
- The Hexpad widget opens on the watch too: tap it and it grows into the whole dock - keys to press, knobs to drag
  and the page you are on.

## 1.12.1 | Volume in VR with Virtual Desktop
- The watch's volume now works with Virtual Desktop's speakers. That device ignores Windows' own volume (the Windows
  slider does nothing there either), so the watch sets the volume of every app playing on it instead - one level for
  all of them, and an app that starts playing later gets it too.
- The Microphone widget works with Virtual Desktop's microphone.

## 1.12.0 | Watch pages, new widgets and clock designs
- Your custom watch face can have several pages. Swipe sideways with the laser or tap the dots at the bottom (in the
  Windows 95 style: the page buttons along the bottom).
- Two new pages, VRChat and Performance, come after your own face, which stays exactly as it is. A copy of your
  settings is saved first. Add, remove and edit pages in the watch editor; a page can also use a 4 x 3 grid with
  bigger cards.
- New widgets: Microphone (tap to mute), Notifications (the last ones you got), Button group (several buttons in
  one card) and Minesweeper - a small tile that grows into the game when you tap it. In the Windows 95 style it is
  the real 1995 Minesweeper.
- Clock designs: the time can be Flip, Outline, Stacked, Segment or a Words clock, and the analog clock Squircle,
  Classic, Minimal, Roman or a Swiss station clock. New 1 x 1 analog clock, like the macOS widget clock.
- Time, date, calendar and time in VR are redesigned, with new sizes (time 1 x 1 and 2 x 2, date 1 x 1), a week strip
  and a month grid. Seconds tick exactly on the second.
- Now playing shows how far the song is and takes its colours from the album art; the frame graph shows the last
  30 seconds with the 1% low.
- Settings > Look > Font: pick the typeface of the modern style.
- The bar under the monitors stays under the middle one, and it has a keyboard button.
- Fixed: a window can be dragged from one monitor to the next again; moving the settings window from VR no longer
  freezes everything; VRChat gets the controllers back sooner after you switch the overlay off.
- The installer download is about a third smaller (49 MB instead of 76 MB).

## 1.11.0 | A look for every widget
- Each widget on the watch can have its own design: Tint, Solid, Two-tone, Header, Outline or Glass, or the normal
  card.
- Pick a colour for each widget: Auto or one of 13 colours. Two-tone also takes a second colour, or makes a matching
  one for you.
- In the watch layout editor, select a widget to see the Style and Colour choices with small previews. "Use on all
  widgets" copies the look to the whole face.
- Text stays readable on every colour: the watch picks light or dark text for you. Warnings show as a small badge, so
  they never vanish on a red or amber card, and switched-on buttons always look different from switched-off ones.
- Cards have a soft light along the top for a bit more depth. A widget's own colour also carries into its opened view.
- Night mode dims the new colours too. The Windows 95 style is unchanged.

## 1.10.1 | Fix: starting after the update to 1.10.0
- The first start after updating to 1.10.0 stopped with an error while switching your colours to Bento. Fixed;
  your settings were not changed by it.
- This "What's new" also lists what 1.10.0 brought, since that version never got to show it.

## 1.10.0 | A new look: Bento
- The watch is redesigned: dark navy cards, small uppercase captions, big bold numbers with their units, a pink
  accent, and clear colours for state (green ok, yellow look, red act) and for data (purple CPU, blue memory).
- Your colours switch to the new Bento theme once. Your old theme is still one click away on the Look tab.
- Buttons on the watch carry their names, and a switched-on button is filled with the accent.
- Every watch widget is easier to read in VR: nothing smaller than 15 px, and every value on a small card is the
  same size.
- Battery colours are calmer: green above 30%, yellow from 16 to 30%, red at 15% and below.
- The watch draws faster than before the redesign.

## 1.9.4 | Dragging with two lasers on one screen
- While you hold the trigger on a screen, that screen keeps your laser even past its edge, and the cursor stays on
  the edge. Dragging a window to the very top of a monitor now snaps it full screen.
- The other hand can no longer take the mouse in the middle of a drag (it used to jump to the other laser when the
  dragging one went past the edge or held still).

## 1.9.3 | What's new, after every update
- After an update, a "What's new" window lists what changed since your old version: in VR it appears in front of
  you, on the desktop in the settings window. OK closes it in both places.
- Settings > General > What's new opens it again at any time.

## 1.9.2 | The volume widget turns the device you hear in VR
- The volume widget follows the same default device as the Windows volume icon, checks it every second and
  switches at once. In VR that is Virtual Desktop Audio; before, it kept turning the previous device.
- The log names the device it controls, and says so if a device ignores a new level.

## 1.9.1 | Overlays update at your headset's rate
- OrbitOverlay used to update once per game frame, so whenever the game dropped a frame, the cursor, a dragged
  panel, mirrored video and the watch skipped it too. It now runs on its own clock at the headset's refresh rate.
- An update reaches your eyes about half a frame sooner on average.
- No more ~35 ms pause when SteamVR doesn't signal a frame in time.

## 1.9.0 | A bar under your screens
- Point at the lower part of a monitor or window and a bar appears under it: a Theater button, plus Dim, Size
  and Curved for theater mode.
- In theater mode the bar under the big screen switches monitors and changes the same settings live.
- The settings are also under Settings > Layouts > Theater mode, where the bar can be turned off.

## 1.8.2 | Theater mode: smoother switching
- The big screen always goes in front of the middle of your monitors.
- Monitors off to the side fade in and out in place instead of gliding across the room.
- Switching monitors is a real cross-fade, and the monitor you switch to shows what is on it now.

## 1.8.1 | Theater mode fix, smoother animations
- The theater screen appears where your monitors are, not where you looked when you tapped the watch.
- The dim behind the theater screen is a wall behind the screen, so it can never cover it.
- The watch animates at your headset's refresh rate, and draws about a third faster.

## 1.8.0 | Theater mode
- A Theater watch button turns the monitor or window you used last into one big curved screen in front of you.
  The other panels and the keyboard step aside and the world dims behind it.
- Grab it to move it, use the stick or both hands to size it. Tap again and everything goes back.
- Point at the screen for a switcher under it: one button per monitor and window, and Exit.

## 1.7.1 | The cursor no longer jumps to the centre
- With "Keep clicks from the game" on, VRChat re-centred its cursor whenever the laser crossed a panel edge.
  The game now gets its input back once, when you are done.

## 1.7.0 | Orbit is now OrbitOverlay
- Same app, new name. Your install, shortcuts and settings move over by themselves.
- Who is in the room (VRChat): a panel with everyone in your instance, friends and starred people first, and a
  card when one of them comes or goes.
- Status line: your chatbox shows what you are listening to, the time and your time in VR (needs OSC on).
- No stray clicks in the game while a laser is on an OrbitOverlay panel.
- Hexpad stream dock: the knobs are sliders, and the keys are sharper.

## 1.6.0 | Your stream dock in VR
- An Ajazz AKP03 dock run by Hexpad appears as a panel you can place over the real one: keys, buttons and knobs
  exactly as they look now, pressed and turned with the laser.
- A Hexpad watch widget shows the keys live.

## 1.5.2 | Plain lines instead of rings
- Every value in the Modern style is a big number over a plain line in your accent colour.
- A full visual pass over every widget, size and opened view in both styles.

## 1.5.1 | Everything fits
- Nothing sticks out of a widget any more; every widget at every size is now checked automatically.

## 1.5.0 | Your own watch face
- Put widgets anywhere on a 6 x 4 grid: time, date, calendar, analog clock, timer, stopwatch, batteries, now
  playing, volume, performance, frame graph, CPU, memory and every watch button.
- Tap a widget to open it big, with more to see and to control.

## 1.4.0 | Windows 95 style
- An optional Windows 95 style for the settings, the watch, the keyboard and the notifications, drawn at their
  original pixel size and enlarged without smoothing.

## 1.3.3 | Settings stay private
- Opening the settings panel no longer counts as "an overlay is open" for avatar add-ons like DXOverlay.

## 1.3.2 | A watch that stays put
- Wrist > Lock position: the grip no longer moves or resizes the watch by accident.

## 1.3.1 | Says hello after an update
- After an update a card in VR tells you so.

## 1.3.0 | Your desktop in orbit
- The first public release: a watch on your wrist, your monitors and single windows as curved screens with a
  laser mouse, a VR keyboard, layout profiles, night mode, notifications and in-app updates.
