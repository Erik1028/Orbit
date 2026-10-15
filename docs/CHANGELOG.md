# What's new in OrbitOverlay

This file is built into the app: after an update, the "What's new" window shows every section newer than the
version you came from. Format: "## <version> | <title>", then "- " points (an indented line continues a point).

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
