AMDFamily17.bin - a signed PawnIO module (https://github.com/namazso/PawnIO.Modules, release 0.1.6), byte for byte the one
LibreHardwareMonitorLib 0.9.6 embeds (LibreHardwareMonitorLib/Resources/PawnIo/AMDFamily17.bin, git blob df84d8c1), which Hexpad
already uses for its CPU temperature. OrbitOverlay embeds it (Orbit.csproj, "pawnio/AMDFamily17.bin") and hands it to the PawnIO
driver only when PawnIO is installed on the PC (OrbitOverlay never installs it), to read an AMD Ryzen's temperature register
(Sys/Temps.cs). Its source is AMDFamily17.p in the PawnIO.Modules repository; licensed under the GNU LGPL 2.1 (COPYING).
