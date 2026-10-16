using System;
using System.Collections.Generic;
using System.Globalization;

namespace Orbit;

// The VRChat chatbox as a status line over your head: what you are listening to (and, if you like, the time and how long
// you have been in VR), sent over OSC (/chatbox/input) when it changes and again every 15 s so it stays up. Off by
// default; switched from the watch or the settings. It keeps out of the way of the keyboard's chatbox mode: nothing is
// sent while you type there, nor for a while after you sent a line of your own. Switched off, it clears the chatbox once.
public sealed class StatusLine
{
    public const int Max = 144;                          // what VRChat's chatbox takes
    const double MinGap = 3, KeepAlive = 15;

    readonly OscSender _osc;
    string _last;
    double _sentAt = -100, _pauseUntil;
    bool _shown;

    public bool Typing;                                  // the keyboard's chat line has text
    public string Preview { get; private set; } = "";    // what it would send now (for the settings)

    public StatusLine(OscSender osc) { _osc = osc; }

    // after a line typed on the keyboard: leave it readable for a while
    public void Pause(double now, double seconds) => _pauseUntil = Math.Max(_pauseUntil, now + seconds);

    public static string Compose(ChatboxConfig c, (bool has, string title, string artist, bool playing) media, DateTime now,
        DateTime? vrSince, bool h24)
    {
        var parts = new List<string>(3);
        if (c.ShowMedia && media.has && media.playing && !string.IsNullOrWhiteSpace(media.title))
            parts.Add("\U0001F3B5 " + media.title.Trim() + (string.IsNullOrWhiteSpace(media.artist) ? "" : " – " + media.artist.Trim()));
        if (c.ShowTime)
            parts.Add("\U0001F552 " + (h24 ? now.ToString("HH:mm", CultureInfo.InvariantCulture)
                : now.ToString("h:mm", CultureInfo.InvariantCulture) + Lang.T(now.Hour < 12 ? " AM" : " PM")));
        if (c.ShowVrTime && vrSince is { } since)
        {
            var span = now - since;
            parts.Add("⏱ " + Lang.F("{0} in VR", span.TotalHours >= 1 ? $"{(int)span.TotalHours}:{span.Minutes:00}" : Lang.F("{0} min", Math.Max(0, (int)span.TotalMinutes))));
        }
        string s = string.Join("  ·  ", parts);
        if (s.Length > Max)
        {
            int cut = Max - 1;
            if (cut > 0 && char.IsHighSurrogate(s[cut - 1])) cut--;
            s = s[..cut] + "…";
        }
        return s;
    }

    public void Update(double now, bool on, int port, string text)
    {
        Preview = text ?? "";
        if (!on)
        {
            if (_shown) { Send(port, "", now); _shown = false; }
            _last = null;
            return;
        }
        if (Typing || now < _pauseUntil) return;
        text ??= "";
        bool changed = text != _last;
        if (changed ? now - _sentAt < MinGap : now - _sentAt < KeepAlive) return;      // VRChat ignores a chatbox flood
        if (text.Length == 0 && (_last ?? "").Length == 0) return;                      // nothing to show, already clear
        Send(port, text, now);
        _shown = text.Length > 0;
    }

    void Send(int port, string text, double now)
    {
        _osc.SendMessage(port, "/chatbox/input", text, true, false);                  // post now, no notification sound
        _last = text;
        _sentAt = now;
    }

    // switched off / disconnected: take the line down at once
    public void Clear(int port, double now)
    {
        if (_shown) Send(port, "", now);
        _shown = false;
        _last = null;
    }
}
