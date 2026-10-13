using System;
using System.Numerics;

namespace Orbit;

// One Euro filter (Casiez et al.): a low-pass whose cutoff rises with speed.
public sealed class OneEuro
{
    readonly float _minCutoff, _beta, _dCutoff;
    Vector3 _x, _dx;
    bool _init;

    public OneEuro(float minCutoff, float beta, float dCutoff = 1f)
    {
        _minCutoff = minCutoff; _beta = beta; _dCutoff = dCutoff;
    }

    static float Alpha(float dt, float cutoff)
    {
        float tau = 1f / (2 * MathF.PI * cutoff);
        return 1f / (1f + tau / dt);
    }

    public Vector3 Filter(Vector3 x, float dt)
    {
        if (!_init) { _init = true; _x = x; _dx = Vector3.Zero; return x; }
        if (dt <= 1e-5f) return _x;
        var dx = (x - _x) / dt;
        _dx = Vector3.Lerp(_dx, dx, Alpha(dt, _dCutoff));
        float cutoff = _minCutoff + _beta * _dx.Length();
        _x = Vector3.Lerp(_x, x, Alpha(dt, cutoff));
        return _x;
    }

    public void Reset() => _init = false;
}

// Smooths a pointing ray (origin + direction). Level 0 = off, 1 = low, 2 = medium, 3 = high.
//
// Two stages: a light One Euro low-pass (high cutoff, so little lag) takes the edge off the
// tremor, then a dead band ("backlash") holds the direction still until the hand has really
// moved more than a small angle - jitter inside that angle disappears completely, while a real
// movement is followed at once (only offset by the dead-band angle, no time lag).
public sealed class RayFilter
{
    int _level = -1;
    OneEuro _pos, _dir;
    Vector3 _held;
    bool _hasHeld;
    float _band;

    // per level: direction (min cutoff Hz, beta per rad/s), origin (min cutoff Hz, beta per m/s), dead band (deg)
    static readonly (float dc, float db, float pc, float pb, float bandDeg)[] Levels =
    {
        (0, 0, 0, 0, 0),
        (10.0f, 3.0f, 10.0f, 2.0f, 0.06f),
        (7.0f, 3.0f, 8.0f, 1.5f, 0.1f),
        (4.5f, 2.5f, 5.0f, 1.0f, 0.15f),
    };

    public (Vector3 origin, Vector3 dir) Filter(Vector3 origin, Vector3 dir, float dt, int level)
    {
        level = Math.Clamp(level, 0, 3);
        if (level != _level)
        {
            _level = level;
            var l = Levels[level];
            _dir = level == 0 ? null : new OneEuro(l.dc, l.db);
            _pos = level == 0 ? null : new OneEuro(l.pc, l.pb);
            _band = l.bandDeg * MathF.PI / 180;
            _hasHeld = false;
        }
        if (_dir == null) return (origin, dir);
        var d = _dir.Filter(dir, dt);
        d = d.LengthSquared() < 1e-8f ? dir : Vector3.Normalize(d);

        if (!_hasHeld) { _held = d; _hasHeld = true; }
        float ang = MathF.Acos(Math.Clamp(Vector3.Dot(_held, d), -1f, 1f));
        if (ang > _band)
        {
            // drag the held direction along, staying 'band' behind the hand
            float t = (ang - _band) / ang;
            _held = Vector3.Normalize(Slerp(_held, d, t));
        }
        return (_pos.Filter(origin, dt), _held);
    }

    static Vector3 Slerp(Vector3 a, Vector3 b, float t)
    {
        float dot = Math.Clamp(Vector3.Dot(a, b), -1f, 1f);
        float th = MathF.Acos(dot);
        if (th < 1e-5f) return Vector3.Lerp(a, b, t);
        float s = MathF.Sin(th);
        return a * (MathF.Sin((1 - t) * th) / s) + b * (MathF.Sin(t * th) / s);
    }

    public void Reset() { _pos?.Reset(); _dir?.Reset(); _hasHeld = false; }
}
