using System;
using System.Drawing;
using System.Numerics;
using Vortice.DirectWrite;

namespace Orbit;

// The Hexpad panel in the Cyber style: a HEXPAD tag, where on the dock and the idle state as monospace readouts; the six key faces
// in cut frames with a small K1-K6 code and cyan brackets under the pointer; the round buttons as cut plates; each knob a card with
// the watch's dark-red edge, its value a segmented bar with a notch. Same places, ids and hits as the modern panel.
public sealed partial class DockPanel
{
    void CyberHeader()
    {
        float tw = CyberHud.Tag(_p, Pad + 4, 20, "HEXPAD", size: 16, h: 25);
        float hx = Pad + 4 + tw + 14;
        if (Live || _s.State == HexpadLink.LinkState.ScreenOff)
        {
            string where = Where().ToUpperInvariant();
            float pw = Math.Min(CyberHud.MonoWidth(_p, where, 14, FontWeight.Bold) + 4, 330);
            CyberHud.MonoAt(_p, where, hx, 38, pw, CyberHud.White, 14, FontWeight.Bold);
            hx += pw + 16;
            if (_s.Ambient)
            {
                const string idle = "IDLE · POINT TO WAKE";
                float iw = CyberHud.MonoWidth(_p, idle, 13, FontWeight.Bold) + 18;
                var ir = new RectangleF(hx, 22, iw, 24);
                _p.StrokeCut(RectangleF.Inflate(ir, -0.6f, -0.6f), CyberHud.Cyan, 1.25f, 0, 0, 6, 0);
                _p.Text(idle, ir, CyberHud.Cyan, 13, FontWeight.Bold, TextAlignment.Center, ParagraphAlignment.Center, CyberHud.Mono);
            }
        }
        _p.Fill(new RectangleF(Pad, 58, TW - 2 * Pad, 1), CyberHud.Line);
    }

    static readonly string[] CyKeyCodes = { "K1", "K2", "K3", "K4", "K5", "K6" };

    void CyberKeyMarks(RectangleF rr, bool hov, bool flash, int i)
    {
        if (hov || flash) CyberHud.Brackets(_p, RectangleF.Inflate(rr, 7, 7), flash ? CyberHud.Yellow : CyberHud.Cyan, 14, 0, 2.5f);
        var code = new RectangleF(rr.X, rr.Bottom - 20, 30, 20);
        _p.FillCut(code, Theme.WithAlpha(CyberHud.Page, 0.85f), 0, 0, 5, 0);
        _p.Text(CyKeyCodes[Math.Clamp(i, 0, CyKeyCodes.Length - 1)], code, CyberHud.Grey, 11, FontWeight.Bold, TextAlignment.Center,
            ParagraphAlignment.Center, CyberHud.Mono);
    }

    void CyberRoundButton(RectangleF r, bool down, bool hov, bool flash)
    {
        var b = down ? RectangleF.Inflate(r, -3, -3) : r;
        _p.FillCut(b, down || flash ? CyberHud.Cyan : hov ? CyberHud.Card3 : CyberHud.Card2, 0, 0, 10, 0);
        if (!(down || flash)) _p.StrokeCut(RectangleF.Inflate(b, -0.6f, -0.6f), hov ? CyberHud.Cyan : CyberHud.CyanDim, 1.25f, 0, 0, 9.7f, 0);
        if (hov) CyberHud.Brackets(_p, b, CyberHud.Cyan, 7, 3, 2);
        _p.Fill(new RectangleF(b.X + b.Width / 2 - 6, b.Y + b.Height / 2 - 1, 12, 2), down || flash ? CyberHud.Ink : CyberHud.Cyan);
    }

    // a knob's value: segments, a notch (Hexpad has not said it yet: the empty segments)
    void CyberAbsTrack(string id, RectangleF tr, float cy, float hl, bool dragging, double now)
    {
        var bar = new RectangleF(tr.X, tr.Y + 1, tr.Width, 12);
        if (AbsValue(id, now) is not float v)
        {
            CyberHud.Segments(_p, bar, 0, 0, CyberHud.Track, 0, 4);
            return;
        }
        bool muted = _s.Values.TryGetValue(id, out var x) && x.Muted && !dragging && !_holdUntil.ContainsKey(id);
        CyberHud.Segments(_p, bar, v, muted ? CyberHud.Faint : CyberHud.Cyan, CyberHud.Track, 0, 4);
        CyberThumb(tr.X + tr.Width * v, cy, hl, dragging);
    }

    static readonly Vector2[] _cyN = new Vector2[3];

    void CyberThumb(float x, float y, float hl, bool held)
    {
        if (hl > 0.01f) CyberFx.Corners(_p, new RectangleF(x - 14, y - 18, 28, 36), Theme.WithAlpha(CyberHud.Cyan, hl), 6, 2, 1 - 2 * hl);
        _p.Fill(new RectangleF(x - 2.5f, y - 13, 5, 26), held ? CyberHud.Cyan : CyberHud.White);
        _cyN[0] = new(x - 7, y - 19); _cyN[1] = new(x + 7, y - 19); _cyN[2] = new(x, y - 12);
        _p.Polygon(_cyN, 3, CyberHud.Yellow);
    }

    // the "it asks first" card: dark, its edge red, a hazard strip along its bottom, a CONFIRM tag on its top edge
    void CyberConfirmCard(RectangleF card)
    {
        CyberHud.CardShape(_p, card, CyberHud.Card, CyberHud.DangerEdge, 1.5f, 8, 16);
        CyberHud.Tag(_p, card.X + 24, card.Y - 10, "CONFIRM", CyberHud.Red, CyberHud.Ink, 13, 20);
        var hz = new RectangleF(card.X + 24, card.Bottom - 12, 120, 5);
        _p.Fill(hz, CyberHud.Yellow);
        CyberHud.Hazard(_p, hz, CyberHud.Ink, 9);
    }
}
