// The try-it watch: a browser copy of OrbitOverlay's custom watch face as the app draws it today (1.12) - the 640 x 440
// texture, the 6 x 4 grid, the Bento cards with each widget's own look and colour, three pages to swipe between.
// It is drawn as SVG from the app's own rules (src/Gfx/Bento.cs for the colours, src/Panels/WristCustom.cs, WristPages.cs,
// WristClocks.cs, WristMines.cs, WristRoom.cs, WristDock.cs for the widgets), so it looks like the app's own pictures.
// Page 1 is a busy face of its own, pages 2 and 3 the VRChat and Performance pages 1.12 adds. The clock is live; what the app
// gets from SteamVR, VRChat and Windows (batteries, frame times, the room, the song) are the app's own demo values.
// In the site's 1990s mode it draws the app's Windows 95 face instead (a 320 x 220 canvas, doubled, like the app).
(function () {
  'use strict';
  var root = document.getElementById('watch-demo');
  if (!root || !document.createElementNS) return;
  var html = document.documentElement;
  var HU = (html.lang || '').slice(0, 2) === 'hu';
  var TEST = window.ORBIT_DEMO_TEST || null;           // the review pictures: a fixed time and page, no motion
  var reduce = !!TEST || !!(window.matchMedia && matchMedia('(prefers-reduced-motion: reduce)').matches);
  var NS = 'http://www.w3.org/2000/svg';
  var TW = 640, TH = 440;
  function nowDate() { return TEST && TEST.now ? new Date(TEST.now.getTime() + (Date.now() - T0)) : new Date(); }
  var T0 = Date.now();

  // ================= colours: Theme and Bento, as the app works them out =================
  function clamp(v, a, b) { return v < a ? a : v > b ? b : v; }
  function ch(c, s) { return (c >>> s) & 255; }
  function argb(a, r, g, b) { return ((a << 24) | (r << 16) | (g << 8) | b) >>> 0; }
  function mix(a, b, t) {
    function m(s) { return Math.trunc(clamp(ch(a, s) * (1 - t) + ch(b, s) * t, 0, 255)); }
    return argb(m(24), m(16), m(8), m(0));
  }
  function alpha(c, a) { return argb(Math.trunc(clamp(a * (c >>> 24), 0, 255)), ch(c, 16), ch(c, 8), ch(c, 0)); }
  function hsv(h, s, v) {
    h = ((h % 360) + 360) % 360 / 60;
    var c = v * s, x = c * (1 - Math.abs(h % 2 - 1)), m = v - c, r, g, b;
    if (h < 1) { r = c; g = x; b = 0; } else if (h < 2) { r = x; g = c; b = 0; } else if (h < 3) { r = 0; g = c; b = x; }
    else if (h < 4) { r = 0; g = x; b = c; } else if (h < 5) { r = x; g = 0; b = c; } else { r = c; g = 0; b = x; }
    function B(f) { return Math.trunc(clamp((f + m) * 255 + 0.5, 0, 255)); }
    return argb(255, B(r), B(g), B(b));
  }
  function rgb01(c) { return [ch(c, 16) / 255, ch(c, 8) / 255, ch(c, 0) / 255]; }
  function hue(c) {
    var p = rgb01(c), r = p[0], g = p[1], b = p[2], max = Math.max(r, g, b), min = Math.min(r, g, b), d = max - min;
    if (d < 1e-4) return 0;
    var h = max === r ? ((g - b) / d) % 6 : max === g ? (b - r) / d + 2 : (r - g) / d + 4;
    return (h * 60 + 360) % 360;
  }
  function sat(c) { var p = rgb01(c), max = Math.max(p[0], p[1], p[2]), min = Math.min(p[0], p[1], p[2]); return max < 1e-4 ? 0 : (max - min) / max; }
  function luma(c) { return (0.2126 * ch(c, 16) + 0.7152 * ch(c, 8) + 0.0722 * ch(c, 0)) / 255; }
  function lin(c, s) { var v = ch(c, s) / 255; return v <= 0.04045 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); }
  function lum(c) { return 0.2126 * lin(c, 16) + 0.7152 * lin(c, 8) + 0.0722 * lin(c, 0); }
  function contrast(a, b) { var x = lum(a), y = lum(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); }
  function minContrast(t, f0, f1) { return Math.min(contrast(t, mix(f0, f1, 0.5)), contrast(t, f0), contrast(t, f1)); }

  var WHITE = 0xFFF2F3F7, GREY = 0xFF9EA4B6, DIM = 0xFF5D6378, DARK = 0xFF0B1220, GREEN = 0xFF3DDC84, WARN = 0xFFF7C948,
    BAD = 0xFFFF5A5A, PURPLE = 0xFF8B7CF8, BLUE = 0xFF5AA9FF, BLACK = 0xFF000000, PURE = 0xFFFFFFFF;
  var AVATARS = [0xFFF9A8D4, 0xFFFDE68A, 0xFF86EFAC, 0xFF93C5FD, 0xFFC4B5FD, 0xFFFDBA74, 0xFF67E8F9, 0xFFFCA5A5];
  var STYLES = [null, 'tint', 'solid', 'twotone', 'header', 'outline', 'glass'];

  function onText(f0, f1) { return minContrast(WHITE, f0, f1) >= minContrast(DARK, f0, f1) ? WHITE : DARK; }
  function fitFill(f0, f1, t, want) {
    var to = lum(t) > 0.18 ? BLACK : PURE;
    for (var i = 0; i < 16 && minContrast(t, f0, f1) < want; i++) { f0 = mix(f0, to, 0.07); f1 = mix(f1, to, 0.07); }
    return [f0, f1];
  }
  function away(c, ground, want) {
    var to = lum(ground) < 0.18 ? PURE : BLACK;
    for (var i = 0; i < 16 && contrast(c, ground) < want; i++) c = mix(c, to, 0.08);
    return c;
  }
  function lighten(c, ground, want) { for (var i = 0; i < 16 && contrast(c, ground) < want; i++) c = mix(c, PURE, 0.08); return c; }
  function near(c, st) {
    if (sat(c) < 0.35) return false;
    var d = Math.abs(hue(c) - hue(st)) % 360;
    return Math.min(d, 360 - d) < 35;
  }
  function second(c) {
    c = (c | 0xFF000000) >>> 0;
    var s = sat(c);
    if (s < 0.12) return mix(c, BLACK, 0.35);
    var h = hue(c), v = Math.max(ch(c, 16), ch(c, 8), ch(c, 0)) / 255;
    return hsv(h + (h >= 90 && h < 300 ? 40 : -40), s, v * 0.85);
  }
  function copy(o) { var r = {}; for (var k in o) r[k] = o[k]; return r; }

  // the look's surfaces from the Look tab's panel colour and accent (Bento.Derive); D = the default card's look
  var K = {};
  function derive(pc, accent) {
    pc = (pc | 0xFF000000) >>> 0;
    K.accent = accent;
    var tint = hsv(hue(pc), Math.min(0.4, sat(pc)), 0.88);
    K.card = mix(pc, tint, 0.15);
    K.cardTop = mix(pc, tint, 0.185);
    K.dangerFill = mix(pc, BAD, 0.12);
    K.dangerEdge = mix(pc, BAD, 0.38);
    var d = {
      style: 0, fill0: K.cardTop, fill1: K.card, angle: 90, span: 0.5, sheen: 0, border: 0x10FFFFFF, borderW: 1.25, band: 0,
      text: WHITE, sub: GREY, faint: DIM, cap: GREY, capText: WHITE, capDot: accent, edge: 0x10FFFFFF,
      tile: mix(pc, tint, 0.25), tileHover: mix(pc, tint, 0.32), track: mix(pc, tint, 0.31),
      accent: accent, accentText: accent, ok: GREEN, halo: K.card, badgeRing: WHITE, warnInk: WARN, badInk: BAD,
      cpu: PURPLE, mem: BLUE, flagWarn: WARN, flagBad: BAD, data: 0, graphBar: 0, badge: false, capBadge: false, check: false
    };
    d.accentFill = luma(accent) < 0.55 ? mix(accent, BLACK, 0.08) : accent;
    d.onAccent = luma(d.accentFill) > 0.55 ? DARK : PURE;
    d.off0 = d.off1 = d.tile; d.offHover = d.tileHover; d.offText = WHITE; d.offBorder = d.edge; d.offBorderW = 1.25;
    d.on0 = d.on1 = d.accentFill; d.onText = d.onAccent;
    K.D = d;
    looks = {};
  }
  function colour(L, c, card, tile, own) {
    var line = away(c, card, 3);
    L.accent = L.capDot = line;
    L.data = own ? line : 0;
    L.accentText = away(c, card, 4.5);
    var fill = luma(c) < 0.55 ? mix(c, BLACK, 0.08) : c;
    fill = away(fill, tile, 1.9);
    var on = luma(fill) > 0.55 ? DARK : PURE;
    fill = fitFill(fill, fill, on, 3.2)[0];
    L.accentFill = L.on0 = L.on1 = fill;
    L.onAccent = L.onText = on;
  }
  function flags(L, c) { L.flagWarn = near(c, WARN) ? L.text : L.warnInk; L.flagBad = near(c, BAD) ? L.text : L.badInk; }
  function fills(L, f0, f1, angle, span) {
    var t = onText(f0, f1), ff = fitFill(f0, f1, t, 4.8);
    f0 = ff[0]; f1 = ff[1];
    var mid = mix(f0, f1, 0.5), worst = contrast(t, f0) < contrast(t, f1) ? f0 : f1;
    L.fill0 = f0; L.fill1 = f1; L.angle = angle; L.span = span;
    L.border = L.edge = 0;
    L.text = L.capText = L.capDot = L.accent = L.accentText = L.data = L.ok = L.badgeRing = t;
    L.sub = L.cap = mix(worst, t, 0.8);
    L.faint = mix(mid, t, 0.5);
    L.track = mix(mid, t, 0.24);
    L.tile = mix(mid, t, 0.16);
    L.tileHover = mix(mid, t, 0.26);
    L.accentFill = t; L.onAccent = mid; L.halo = mid; L.badge = true;
    L.graphBar = L.faint; L.flagWarn = L.flagBad = t;
    L.off0 = mix(K.card, f0, 0.3); L.off1 = mix(K.card, f1, 0.3); L.offHover = mix(K.card, mid, 0.4);
    L.offText = WHITE; L.offBorder = 0x10FFFFFF;
    var offMid = mix(L.off0, L.off1, 0.5);
    for (var i = 0; i < 12 && contrast(mix(f0, f1, 0.5), offMid) < 1.9; i++) {
      var n0 = mix(f0, PURE, 0.06), n1 = mix(f1, PURE, 0.06);
      if (minContrast(t, n0, n1) < 4.5) break;
      f0 = n0; f1 = n1;
    }
    L.on0 = f0; L.on1 = f1; L.onText = t; L.check = true;
  }
  // a widget's own look: style (STYLES index) in colour c (two-tone's second c2), night = the fills dimmer
  function make(style, c, c2, own, night) {
    var L = copy(K.D);
    L.style = style;
    c = (c | 0xFF000000) >>> 0;
    if (c2) c2 = (c2 | 0xFF000000) >>> 0;
    if (style === 0) {
      colour(L, c, K.card, L.tile, own);
      if (own) L.border = L.offBorder = alpha(away(c, K.card, 2.4), 0.5);
      flags(L, c);
      return L;
    }
    if (night) { c = mix(c, BLACK, 0.4); if (c2) c2 = mix(c2, BLACK, 0.4); }
    if (style === 1) {
      L.fill0 = mix(K.cardTop, c, 0.22); L.fill1 = mix(K.card, c, 0.22);
      L.tile = L.off0 = L.off1 = mix(K.D.tile, c, 0.25);
      L.tileHover = L.offHover = mix(K.D.tileHover, c, 0.3);
      L.track = mix(K.D.track, c, 0.25);
      L.halo = L.fill1;
      L.sub = L.cap = lighten(GREY, L.fill0, 4.5);
      L.faint = mix(L.fill1, WHITE, 0.42);
      L.border = L.offBorder = alpha(away(c, L.fill1, 2.4), 0.65);
      colour(L, c, L.fill1, L.tile, own);
      L.accentText = away(c, L.fill0, 4.5);
      L.cpu = away(PURPLE, L.fill0, 3); L.mem = away(BLUE, L.fill0, 3);
      L.warnInk = lighten(WARN, L.fill0, 3.6); L.badInk = lighten(BAD, L.fill0, 3.6);
      L.badge = near(c, WARN) || near(c, BAD);
      flags(L, c);
    } else if (style === 2) fills(L, mix(c, PURE, 0.07), c, 90, 0.5);
    else if (style === 3) fills(L, c, c2 || second(c), 45, 1);
    else if (style === 4) {
      var bt = onText(c, c), band = fitFill(c, c, bt, 4.8)[0];
      L.band = band; L.capBadge = true;
      colour(L, c, K.card, L.tile, own);
      flags(L, c);
      L.cap = mix(band, bt, 0.85);
      L.capText = L.capDot = L.badgeRing = bt;
    } else if (style === 5) {
      colour(L, c, K.card, L.tile, own);
      flags(L, c);
      L.border = L.offBorder = away(c, K.card, 3);
      L.borderW = L.offBorderW = 2;
    }
    return L;
  }
  var looks = {};
  function lookOf(w) {
    if (!w.style && w.color == null) return K.D;
    var style = Math.max(0, STYLES.indexOf(w.style || null)), own = w.color != null;
    var c = own ? w.color : w.type === 'cpu' ? PURPLE : w.type === 'ram' || w.type === 'vram' ? BLUE : K.accent;
    var c2 = style === 3 ? w.color2 || 0 : 0, night = S.night && style > 0;
    var key = [style, c, c2, own, night].join();
    return looks[key] || (looks[key] = make(style, c, c2, own, night));
  }

  // ================= text =================
  var FT = "'Segoe UI Variable Text','Segoe UI',system-ui,-apple-system,'Helvetica Neue',Arial,sans-serif";
  var FD = "'Segoe UI Variable Display','Segoe UI',system-ui,-apple-system,'Helvetica Neue',Arial,sans-serif";
  var mctx = document.createElement('canvas').getContext('2d');
  var widths = {}, mids = {};
  function mw(s, size, wt, disp) {
    if (!s) return 0;
    var k = s + '|' + size + '|' + wt + '|' + (disp ? 1 : 0), v = widths[k];
    if (v === undefined) { mctx.font = wt + ' ' + size + 'px ' + (disp ? FD : FT); v = widths[k] = mctx.measureText(s).width; }
    return v;
  }
  function mtr(s, size, wt, tr) { return s ? mw(s, size, wt) + tr * (s.length - 1) : 0; }
  // a line centred in a box: DirectWrite puts the baseline at the box's middle + (ascent - descent) / 2
  function midK(disp) {
    var k = disp ? 'd' : 't';
    if (mids[k] === undefined) {
      mctx.font = '600 100px ' + (disp ? FD : FT);
      var m = mctx.measureText('0');
      mids[k] = m.fontBoundingBoxAscent ? (m.fontBoundingBoxAscent - m.fontBoundingBoxDescent) / 200 : 0.414;
    }
    return mids[k];
  }
  function fitSize(s, maxW, max, wt, disp) {
    if (!s) return max;
    var w100 = mw(s, 100, wt, disp), size = w100 <= 0 ? max : Math.max(8, Math.min(max, 100 * maxW / w100));
    return Math.floor(size * 2) / 2;
  }
  function ell(s, maxW, size, wt, disp) {
    if (!s || mw(s, size, wt, disp) <= maxW) return s;
    var lo = 0, hi = s.length - 1;
    while (lo < hi) {
      var mid = (lo + hi + 1) >> 1;
      if (mw(s.slice(0, mid).replace(/\s+$/, '') + '…', size, wt, disp) <= maxW) lo = mid; else hi = mid - 1;
    }
    return s.slice(0, lo).replace(/\s+$/, '') + '…';
  }
  function twoLines(s, maxW, size, wt) {
    if (mw(s, size, wt) <= maxW) return [s, ''];
    var cut = -1;
    for (var i = s.indexOf(' '); i > 0; i = s.indexOf(' ', i + 1)) { if (mw(s.slice(0, i), size, wt) > maxW) break; cut = i; }
    if (cut < 0) return [ell(s, maxW, size, wt), ''];
    return [s.slice(0, cut), ell(s.slice(cut + 1).replace(/^\s+/, ''), maxW, size, wt)];
  }

  // ================= the SVG being written =================
  var o = [], gid = 0;
  function n2(v) { return Math.round(v * 100) / 100; }
  function hex(c) { return '#' + ((c & 0xFFFFFF) | 0x1000000).toString(16).slice(1); }
  function esc(s) { return String(s).replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; }); }
  // a paint: a colour (0xAARRGGBB) or a gradient's url
  function pa(attr, p) {
    if (typeof p === 'string') return ' ' + attr + '="' + p + '"';
    var a = p >>> 24;
    return ' ' + attr + '="' + hex(p) + '"' + (a < 255 ? ' ' + attr + '-opacity="' + n2(a / 255) + '"' : '');
  }
  function rect(x, y, w, h, p, rad) {
    if (w <= 0 || h <= 0) return;
    o.push('<rect x="' + n2(x) + '" y="' + n2(y) + '" width="' + n2(w) + '" height="' + n2(h) + '"' + (rad ? ' rx="' + n2(Math.min(rad, w / 2, h / 2)) + '"' : '') + pa('fill', p) + '/>');
  }
  function strokeRect(x, y, w, h, c, sw, rad) {
    o.push('<rect x="' + n2(x) + '" y="' + n2(y) + '" width="' + n2(w) + '" height="' + n2(h) + '" rx="' + n2(Math.max(0, rad)) + '" fill="none"' + pa('stroke', c) + ' stroke-width="' + sw + '"/>');
  }
  function circle(cx, cy, r, p) { o.push('<circle cx="' + n2(cx) + '" cy="' + n2(cy) + '" r="' + n2(r) + '"' + pa('fill', p) + '/>'); }
  function ring(cx, cy, r, c, sw) { o.push('<circle cx="' + n2(cx) + '" cy="' + n2(cy) + '" r="' + n2(r) + '" fill="none"' + pa('stroke', c) + ' stroke-width="' + n2(sw) + '"/>'); }
  function line(x1, y1, x2, y2, c, sw, round) {
    o.push('<line x1="' + n2(x1) + '" y1="' + n2(y1) + '" x2="' + n2(x2) + '" y2="' + n2(y2) + '"' + pa('stroke', c) + ' stroke-width="' + n2(sw) + '"' + (round ? ' stroke-linecap="round"' : '') + '/>');
  }
  function path(d, p, extra) { o.push('<path d="' + d + '"' + pa('fill', p) + (extra || '') + '/>'); }
  // text with its baseline at by; anchor: start / middle / end; disp: the display face (values); tr: letter spacing
  function text(s, x, by, c, size, wt, disp, anchor, tr) {
    if (s === '' || s == null) return;
    o.push('<text x="' + n2(x) + '" y="' + n2(by) + '" font-size="' + size + '" font-weight="' + wt + '" class="' + (disp ? 'd' : 't') + '"' +
      (anchor && anchor !== 'start' ? ' text-anchor="' + anchor + '"' : '') + (tr ? ' letter-spacing="' + tr + '"' : '') + pa('fill', c) + '>' + esc(s) + '</text>');
  }
  // text centred on a box's middle (the app's ParagraphAlignment.Center)
  function textMid(s, x, cy, c, size, wt, disp, anchor) { text(s, x, cy + midK(disp) * size, c, size, wt, disp, anchor); }
  // a linear gradient laid over r the way the app's Painter.Place lays it (angle in degrees, 90 = down; span: where it ends)
  function grad(r, c0, c1, angle, span, c2) {
    if (c0 === c1 && (c2 === undefined || c2 === c1)) return c0;
    var t = angle * Math.PI / 180, dx = Math.cos(t), dy = Math.sin(t), len = Math.abs(r.w * dx) + Math.abs(r.h * dy);
    var sx = r.x + r.w / 2 - dx * len / 2, sy = r.y + r.h / 2 - dy * len / 2, k = len * Math.max(0.01, span === undefined ? 1 : span);
    var id = 'wg' + (++gid);
    function stop(off, c) { return '<stop offset="' + off + '" stop-color="' + hex(c) + '"' + ((c >>> 24) < 255 ? ' stop-opacity="' + n2((c >>> 24) / 255) + '"' : '') + '/>'; }
    o.push('<linearGradient id="' + id + '" gradientUnits="userSpaceOnUse" x1="' + n2(sx) + '" y1="' + n2(sy) + '" x2="' + n2(sx + dx * k) + '" y2="' + n2(sy + dy * k) + '">' +
      stop(0, c0) + (c2 === undefined ? stop(1, c1) : stop(0.5, c1) + stop(1, c2)) + '</linearGradient>');
    return 'url(#' + id + ')';
  }

  // ---- icons: drawn after Segoe Fluent Icons' outlines, on a 24 px grid ----
  var IC = {
    keyboard: '<rect x="0.6" y="4.1" width="22.8" height="15.8" rx="2.6"/><path d="M4.9 8.7h.01M8.4 8.7h.01M12 8.7h.01M15.6 8.7h.01M19.1 8.7h.01M4.9 12.1h.01M8.4 12.1h.01M12 12.1h.01M15.6 12.1h.01M19.1 12.1h.01" stroke-width="2.1"/><path d="M7.4 15.6h9.2"/>',
    monitor: '<rect x="1.5" y="3" width="21" height="14.25" rx="2.25"/><path d="M7.75 21.25h8.5M12 17.25v4"/>',
    hide: '<path d="M.9 12.7C3 7.5 7.1 4.6 12 4.6s9 2.9 11.1 8.1"/><path d="M9 9.4a4.1 4.1 0 0 0 5.7 5.7"/><path d="M1.6 1.6l20.8 20.8"/><path d="M.9 12.7c1.4 3.2 3.4 5.3 5.9 6.5M17.9 18.1c-1.8 1.1-3.8 1.7-5.9 1.7"/>',
    recenter: '<path d="M21.6 12a9.6 9.6 0 1 1-2.8-6.8"/><path d="M19.6 1.4v4.6H15" stroke-linejoin="round"/>',
    layouts: '<rect x="2.75" y="2.75" width="7.75" height="7.75" rx="2"/><rect x="13.5" y="2.75" width="7.75" height="7.75" rx="2"/><rect x="2.75" y="13.5" width="7.75" height="7.75" rx="2"/><rect x="13.5" y="13.5" width="7.75" height="7.75" rx="2"/>',
    night: '<path d="M14.6 1.5a10.9 10.9 0 1 1-13.1 13.1A12.4 12.4 0 0 0 14.6 1.5z" stroke-linejoin="round"/>',
    settings: '<path d="M9.93 3.65L10.45 1.11L13.55 1.11L14.07 3.65L16.44 4.63L18.60 3.20L20.80 5.40L19.37 7.56L20.35 9.93L22.89 10.45L22.89 13.55L20.35 14.07L19.37 16.44L20.80 18.60L18.60 20.80L16.44 19.37L14.07 20.35L13.55 22.89L10.45 22.89L9.93 20.35L7.56 19.37L5.40 20.80L3.20 18.60L4.63 16.44L3.65 14.07L1.11 13.55L1.11 10.45L3.65 9.93L4.63 7.56L3.20 5.40L5.40 3.20L7.56 4.63Z" stroke-linejoin="round"/><circle cx="12" cy="12" r="3.6"/>',
    status: '<path d="M12 1.8A10.2 10.2 0 0 0 3.1 17L1.8 22.2l5.2-1.3A10.2 10.2 0 1 0 12 1.8z" stroke-linejoin="round"/><path d="M7.8 10h8.4M7.8 14.1h5.2"/>',
    theater: '<rect x="2.25" y="2.25" width="19.5" height="19.5" rx="3"/><path d="M7.25 2.25v19.5M16.75 2.25v19.5M2.25 7.1h5M2.25 12h5M2.25 16.9h5M16.75 7.1h5M16.75 12h5M16.75 16.9h5"/>',
    note: '<path d="M8.5 18.5V4.6l13-2.6v14"/><circle cx="5.2" cy="18.6" r="3.3"/><circle cx="18.2" cy="16" r="3.3"/><path d="M8.5 8.5l13-2.6"/>',
    mic: '<rect x="7.9" y="1.4" width="8.2" height="13.2" rx="4.1"/><path d="M4.4 10.9a7.6 7.6 0 0 0 15.2 0M12 18.5v4.2"/>',
    micoff: '<path d="M7.9 7.6V5.5a4.1 4.1 0 0 1 7.7-2M16.1 10.1v.4a4.1 4.1 0 0 1-6.3 3.5M4.4 10.9a7.6 7.6 0 0 0 12.3 6M19.6 10.9a7.5 7.5 0 0 1-.7 3.2M12 18.5v4.2M2 2l20 20"/>',
    minus: '<path d="M2.7 12h18.6"/>',
    plus: '<path d="M12 2.7v18.6M2.7 12h18.6"/>',
    close: '<path d="M3.2 3.2l17.6 17.6M20.8 3.2L3.2 20.8"/>',
    check: '<path d="M4.5 12.5l5 5L19.5 6.8" stroke-width="2.8"/>',
    bolt: '<path d="M14.6 1.2 4.4 13.7h7.3L9.4 22.8 19.6 9.8h-7.3z" stroke-linejoin="round"/>',
    flag: '<path d="M4.5 22V2.5M4.5 3h14.5l-3 4.75 3 4.75H4.5" stroke-linejoin="round"/>',
    speaker: '<path d="M3 8.75h3.75L12 3.9v16.2l-5.25-4.85H3z" stroke-linejoin="round"/><path d="M15.6 8.4a5 5 0 0 1 0 7.2M18.6 5.4a9.2 9.2 0 0 1 0 13.2"/>',
    prev: '<path d="M4.5 3.5v17"/><path d="M20.5 4.4v15.2a1 1 0 0 1-1.6.8L8.3 12.8a1 1 0 0 1 0-1.6l10.6-7.6a1 1 0 0 1 1.6.8z" stroke-linejoin="round"/>',
    next: '<path d="M19.5 3.5v17"/><path d="M3.5 4.4v15.2a1 1 0 0 0 1.6.8l10.6-7.6a1 1 0 0 0 0-1.6L5.1 3.6a1 1 0 0 0-1.6.8z" stroke-linejoin="round"/>',
    pause: '<rect x="4.6" y="2.4" width="5.4" height="19.2" rx="1.8" stroke="none" fill="currentColor"/><rect x="14" y="2.4" width="5.4" height="19.2" rx="1.8" stroke="none" fill="currentColor"/>',
    play: '<path d="M6.2 3.6v16.8c0 1.2 1.3 1.9 2.3 1.2l12.6-8.4a1.4 1.4 0 0 0 0-2.4L8.5 2.4c-1-.7-2.3 0-2.3 1.2z" stroke="none" fill="currentColor"/>'
  };
  // an icon centred on (cx, cy), size px tall like the app's glyphs
  function icon(name, cx, cy, size, c) {
    var k = size / 24;
    o.push('<g transform="translate(' + n2(cx - size / 2) + ' ' + n2(cy - size / 2) + ') scale(' + n2(k) + ')" fill="none" stroke="' + hex(c) + '" color="' + hex(c) + '"' +
      ((c >>> 24) < 255 ? ' opacity="' + n2((c >>> 24) / 255) + '"' : '') + ' stroke-width="1.5" stroke-linecap="round">' + IC[name] + '</g>');
  }

  // ================= the demo data (the app's own made-up values: Tools.cs FaceShot / PagesData) =================
  var S = {
    page: 0, night: true, desktops: false, keyboard: false, status: false, theater: false, layout: 0,
    vol: 0.5, muted: false, mic: false, micPeak: 0.2, playing: true, pos: 102, dur: 243, open: null,
    vrSince: null, bats: [['HMD', 0.71, false], ['L', 0.82, false], ['R', 0.18, true], ['Trk×6', 0.44, false]],
    fps: 86, low: 73, reproj: 4, gpu: 9.3, hz: 90, vram: 9.4, vramTotal: 16, ram: 15.7, ramTotal: 32, cpu: [], frames: [], frameAt: 0
  };
  var LAYOUTS = ['Desk', 'Bed', 'Couch'];
  var WORLD = 'The Black Cat – Midnight Rooftop Lounge (Extended)';
  var PEOPLE = ['Nova', 'Mira', 'Pixel Fox', 'Tobi', 'Moss', 'Juniper', 'Static', 'Kite', 'Orbit Fan 2000', 'Lumen', 'Quill', 'Sable'];
  var FRIENDS = { Mira: 1, Tobi: 1, Kite: 1 };
  var NOTES = [['Tobi joined', 'Tobi joined', 20], ['Mira is here', 'Mira is here', 130], ['Alex: 2 new messages', 'Discord', 185]];
  var KEYCOL = [0xFF2D6CDF, 0xFFE0473C, 0xFF3CB371, 0xFFE8A33D, 0xFF8E5BD8, 0xFF2BB5B8];

  // the look of the face: Forest panel, a green accent - the app's own face from the reference pictures
  var ACCENT = 0xFF4ADE80, PANEL = 0xFF10201B;
  var C = { green: 0xFF22C55E, blue: 0xFF3B82F6, red: 0xFFEF4444, amber: 0xFFF59E0B, orange: 0xFFF97316, grey: 0xFF64748B,
    purple: 0xFFA855F7, white: 0xFFE5E7EB, cyan: 0xFF06B6D4, navy: 0xFF1E3A8A };
  function W(type, x, y, w, h, style, color, color2, extra) {
    var r = { type: type, x: x, y: y, w: w, h: h, style: style || null, color: color == null ? null : color, color2: color2 || null };
    if (extra) for (var k in extra) r[k] = extra[k];
    return r;
  }
  var PAGES = [
    { name: 'Page 1', list: [
      W('time', 0, 0, 2, 2, 'twotone', C.green, C.blue, { variant: 'stacked' }), W('analog', 2, 0, 1, 1, 'tint'),
      W('dock', 3, 0, 1, 1, 'solid', C.purple, C.blue), W('volume', 4, 0, 2, 1, 'solid', C.red, C.red),
      W('battery', 2, 1, 2, 1, 'solid', C.green, C.blue), W('room', 4, 1, 2, 1, 'solid', C.white, C.white),
      W('media', 0, 2, 4, 1, 'twotone', C.red, C.blue), W('vram', 4, 2, 1, 1, 'solid', C.red, C.red), W('ram', 5, 2, 1, 1, 'solid', C.green, C.blue),
      W('buttons', 0, 3, 2, 1, 'tint', null, null, { items: ['keyboard', 'desktops', 'recenter'] }), W('session', 2, 3, 1, 1, 'twotone', C.green, C.blue),
      W('layout', 3, 3, 1, 1, 'solid', C.amber, C.blue), W('night', 4, 3, 1, 1, 'solid', C.orange, C.blue), W('settings', 5, 3, 1, 1, 'twotone', C.grey, C.blue)] },
    { name: 'VRChat', list: [
      W('time', 0, 0, 2, 2, 'twotone', C.red, C.cyan, { variant: 'stacked' }), W('minesweeper', 2, 0, 1, 1), W('room', 3, 0, 3, 1, 'twotone', C.green),
      W('analog', 2, 1, 1, 1, 'tint'), W('mic', 3, 1, 1, 1, 'tint', C.orange), W('battery', 4, 1, 2, 1, 'solid', C.green),
      W('notifications', 0, 2, 3, 1, 'solid', C.blue), W('media', 3, 2, 3, 1),
      W('buttons', 0, 3, 6, 1, 'tint', C.cyan, C.green, { items: ['keyboard', 'desktops', 'status', 'theater', 'night', 'settings'] })] },
    { name: 'Performance', list: [
      W('graph', 0, 0, 4, 2, 'twotone', C.green, C.navy), W('time', 4, 0, 2, 1, 'twotone', C.green, C.navy),
      W('cpu', 4, 1, 1, 1, 'twotone', C.green, C.navy), W('vram', 5, 1, 1, 1, 'twotone', C.green, C.navy),
      W('battery', 0, 2, 2, 1, 'twotone', C.green, C.navy), W('fps', 2, 2, 1, 1, 'twotone', C.green, C.navy),
      W('gpu', 3, 2, 1, 1, 'twotone', C.green, C.navy), W('reproj', 4, 2, 1, 1, 'twotone', C.green, C.navy),
      W('ram', 5, 2, 1, 1, 'twotone', C.green, C.navy),
      W('buttons', 0, 3, 6, 1, 'twotone', C.green, C.navy, { items: ['desktops', 'theater', 'night'] })] }
  ];

  // ---- dates and numbers as the app writes them (Windows' language: English here, Hungarian on the Hungarian page) ----
  var MON = HU ? ['január', 'február', 'március', 'április', 'május', 'június', 'július', 'augusztus', 'szeptember', 'október', 'november', 'december']
    : ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
  var MON3 = HU ? ['jan.', 'febr.', 'márc.', 'ápr.', 'máj.', 'jún.', 'júl.', 'aug.', 'szept.', 'okt.', 'nov.', 'dec.']
    : ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  var DAY = HU ? ['vasárnap', 'hétfő', 'kedd', 'szerda', 'csütörtök', 'péntek', 'szombat'] : ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
  var DAY3 = HU ? ['V', 'H', 'K', 'Sze', 'Cs', 'P', 'Szo'] : ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
  function capital(s) { return s.charAt(0).toUpperCase() + s.slice(1); }
  function dateForms(d) {
    var m = d.getMonth(), day = d.getDate(), wd = d.getDay();
    if (HU) return [capital(MON[m] + ' ' + day + '., ' + DAY[wd]), capital(MON3[m] + ' ' + day + '., ' + DAY3[wd]), capital(MON3[m] + ' ' + day + '.')];
    return [MON[m] + ' ' + day + ', ' + DAY[wd], MON3[m] + ' ' + day + ', ' + DAY3[wd], MON3[m] + ' ' + day];
  }
  function dec(v) { var s = v.toFixed(1); return HU ? s.replace('.', ',') : s; }
  function two(v) { return (v < 10 ? '0' : '') + v; }
  function hhmm(d) { return two(d.getHours()) + ':' + two(d.getMinutes()); }
  function vrMinutes(now) { return Math.max(0, Math.floor((now - S.vrSince) / 60000)); }
  function clock2(s) { s = Math.max(0, Math.ceil(s)); return Math.floor(s / 60) + ':' + two(s % 60); }

  // ================= geometry =================
  function rte(v) { var f = Math.floor(v), d = v - f; return d > 0.5 ? f + 1 : d < 0.5 ? f : (f % 2 === 0 ? f : f + 1); }   // MathF.Round
  function cellRect(x, y, w, h) {
    var cw = (TW - 20 - 50) / 6, chh = (TH - 20 - 30) / 4;
    var x0 = rte(10 + x * (cw + 10)), y0 = rte(10 + y * (chh + 10)), x1 = rte(10 + (x + w) * (cw + 10) - 10), y1 = rte(10 + (y + h) * (chh + 10) - 10);
    return { x: x0, y: y0, w: x1 - x0, h: y1 - y0 };
  }
  function R(x, y, w, h) { return { x: x, y: y, w: w, h: h }; }
  function inflate(r, dx, dy) { return R(r.x - dx, r.y - dy, r.w + 2 * dx, r.h + 2 * dy); }

  // ================= drawing the cards =================
  var SMALL = 17, CAPS = 15, CAPBASE = 25, SBASE = 64, MBASE = 66, HERO = 30, NOTEBASE = 85, DIGH = 0.7, DIGTOP = 0.72, DIGSPAN = 0.735, RAD = 16;
  var L = null, halo = null, hits = null, hov = null, held = null;
  function padX(r) { return r.w < 120 ? 11 : 14; }
  function capB() { return L.badge || L.capBadge; }
  function level(lv, normal) { return lv === 0 ? normal : L.badge ? L.text : lv === 2 ? L.badInk : L.warnInk; }
  function hit(id, r, act, round) { hits.push({ id: id, x: r.x, y: r.y, w: r.w, h: r.h, act: act, round: round }); }
  function isHov(id) { return hov === id; }
  function isHeld(id) { return held === id; }

  function caption(x, r, maxW, forms, badge, color) {
    var dot = badge && capB();
    if (dot) maxW -= 17;
    for (var i = 0; i < forms.length; i++) {
      var s = forms[i].toUpperCase(), w = mtr(s, CAPS, 600, 1);
      if (w > maxW) continue;
      text(s, x, r.y + CAPBASE, color || L.cap, CAPS, 600, false, 'start', 1);
      if (!dot) return w;
      statusDot(x + w + 10, r.y + CAPBASE - 5, badge);
      return w + 17;
    }
    if (dot) statusDot(x + 7, r.y + CAPBASE - 5, badge);
    return dot ? 14 : 0;
  }
  function captionW(forms, maxW) {
    for (var i = 0; i < forms.length; i++) { var w = mtr(forms[i].toUpperCase(), CAPS, 600, 1); if (w <= maxW) return w; }
    return -1;
  }
  function capText(s, x, by, c, end) { text(s, end ? x - mtr(s, CAPS, 600, 1) : x, by, c, CAPS, 600, false, 'start', 1); }
  function statusDot(cx, cy, lv) {
    var c = lv === 2 ? BAD : WARN;
    circle(cx, cy, 7, L.badgeRing);
    circle(cx, cy, 5.8, DARK);
    rect(cx - 1.25, cy - 4.5, 2.5, 5.5, c, 1.25);
    circle(cx, cy + 3.3, 1.4, c);
  }
  function captionRow(r, x, right, note, noteColor, forms, badge) {
    var iw = right - x, nw = note ? mw(note, SMALL, 600) : 0;
    var cw = caption(x, r, nw > 0 ? Math.max(iw * 0.4, iw - nw - 10) : iw, forms, badge);
    if (nw <= 0) return;
    var room = iw - (cw > 0 ? cw + 10 : 0);
    if (room < 36) return;
    text(ell(note, room, SMALL, 600), right, r.y + CAPBASE, noteColor, SMALL, 600, false, 'end');
  }
  function valueText(x, by, v, vs, vc) { text(v, x, by, vc, vs, 700, true); return mw(v, vs, 700, true); }
  function valueUnit(x, by, maxW, v, unit, size, min, vc) {
    if (!v) return 0;
    var uw = unit ? mw(unit, SMALL, 600) + 4 : 0;
    var vs = fitSize(v, maxW - uw, size, 700, true);
    if (vs < min && uw > 0) { uw = 0; vs = fitSize(v, maxW, size, 700, true); }
    vs = Math.max(min, vs);
    var vw = valueText(x, by, v, vs, vc);
    if (uw > 0) text(unit, x + vw + 4, by, L.sub, SMALL, 600);
    return vw + uw;
  }
  function heroFit(v, unit, iw) {
    var vs = Math.max(20, fitSize(v, iw, HERO, 700, true));
    if (!unit) return [vs, 0];
    var vw = mw(v, vs, 700, true) + 4;
    if (vw + mw(unit, SMALL, 600) <= iw) return [vs, SMALL];
    if (vw + mw(unit, CAPS, 600) <= iw) return [vs, CAPS];
    return [vs, 0];
  }
  function meter(x, y, w, h, frac, tint) {
    rect(x, y, w, h, L.track, h / 2);
    if (frac < 0 || frac < 0.002) return;
    rect(x, y, Math.max(h, w * Math.min(frac, 1)), h, tint, h / 2);
  }
  function small(r, cap, v, unit, frac, bar, vc, showMeter, unitUp, lv) {
    var pad = padX(r), iw = r.w - 2 * pad, x = r.x + pad, by = r.y + SBASE;
    var hf = heroFit(v, unitUp ? null : unit, iw), vs = hf[0], us = hf[1];
    var uw = us === 0 && unit ? mtr(unit, CAPS, 600, 1) : 0, dw = lv && capB() ? 17 : 0;
    if (uw > 0 && captionW(cap, iw - uw - 6 - dw) >= 0) {
      caption(r.x + pad, r, iw - uw - 6, cap, lv);
      capText(unit, r.x + r.w - pad - uw, r.y + CAPBASE, L.cap);
    } else caption(r.x + pad, r, iw, cap, lv);
    if (v) {
      var vw = valueText(x, by, v, vs, vc);
      if (us > 0) text(unit, x + vw + 4, by, L.sub, us, 600);
    }
    if (showMeter) meter(x, r.y + r.h - 16, iw, 4, frac, bar);
  }
  function note(x, by, iw, forms) {
    var s = forms[0], size = fitSize(s, iw, 15, 600);
    for (var i = 1; i < forms.length && size < 14; i++) { s = forms[i]; size = fitSize(s, iw, 15, 600); }
    size = Math.max(14, size);
    text(ell(s, iw, size, 600), x, by, size < 15 ? mix(L.sub, L.text, 0.3) : L.sub, size, 600);
  }
  function roleColor(id) { return L.data ? L.data : id === 'cpu' ? L.cpu : id === 'ram' || id === 'vram' ? L.mem : L.accent; }

  // a round or square button inside a widget (Press): Tile, the accent when on
  function press(id, r, iconName, label, on, iconSize, circleBtn, act, fill) {
    if (act) hit(id, r, act, circleBtn);
    var s = isHeld(id) ? 0.9 : 1;
    var b = R(r.x + r.w * (1 - s) / 2, r.y + r.h * (1 - s) / 2, r.w * s, r.h * s);
    var rad = circleBtn ? Math.min(b.w, b.h) / 2 : Math.min(RAD * s, Math.min(b.w, b.h) / 2);
    var bg = on ? (fill || L.accentFill) : isHov(id) ? L.tileHover : L.tile;
    if (circleBtn) circle(b.x + b.w / 2, b.y + b.h / 2, rad, bg); else rect(b.x, b.y, b.w, b.h, bg, rad);
    if (!circleBtn && !on && L.edge) strokeRect(b.x + 0.6, b.y + 0.6, b.w - 1.2, b.h - 1.2, L.edge, 1.25, rad);
    var fg = on ? (fill ? (luma(fill) > 0.55 ? DARK : PURE) : L.onAccent) : L.text;
    var ls = Math.min(18, Math.max(SMALL, r.h * 0.3));
    if (label && iconName) {
      var tw = mw(label, ls, 600), total = iconSize + 8 + tw, x = b.x + (b.w - total) / 2;
      icon(iconName, x + iconSize / 2, b.y + b.h / 2, iconSize, fg);
      textMid(label, x + iconSize + 8, b.y + b.h / 2, fg, ls, 600);
    } else if (iconName) icon(iconName, b.x + b.w / 2, b.y + b.h / 2, Math.round(iconSize * s), fg);
    else if (label) textMid(ell(label, b.w - 12, ls, 600), b.x + b.w / 2, b.y + b.h / 2, fg, ls, 600, false, 'middle');
  }

  // the card behind a widget in its look (CardBack); the cards float on their own (the "Cards only" background): a little
  // of the world shows through them
  function cardBack(r, w, danger) {
    halo = null;
    if (danger) {
      rect(r.x, r.y, r.w, r.h, K.dangerFill, RAD);
      strokeRect(r.x + 0.6, r.y + 0.6, r.w - 1.2, r.h - 1.2, K.dangerEdge, 1.25, RAD - 0.6);
      return;
    }
    var f0 = alpha(L.fill0, 0.96), f1 = alpha(L.fill1, 0.96);
    if (w.type === 'analog' && w.w === 1 && w.h === 1) { analogFace(r, w, f0, f1); return; }
    var g = grad(r, f0, f1, L.angle, L.span);
    rect(r.x, r.y, r.w, r.h, g, RAD);
    halo = g;
    if (L.band) {
      var id = 'wc' + (++gid);
      o.push('<clipPath id="' + id + '"><rect x="' + (r.x - 1) + '" y="' + (r.y - 1) + '" width="' + (r.w + 2) + '" height="' + (bandH(w, r) + 1) + '"/></clipPath>');
      o.push('<g clip-path="url(#' + id + ')">'); rect(r.x, r.y, r.w, r.h, L.band, RAD); o.push('</g>');
    }
    if (L.border && L.borderW > 0) {
      var i = L.borderW * 0.48;
      strokeRect(r.x + i, r.y + i, r.w - 2 * i, r.h - 2 * i, L.border, L.borderW, RAD - i);
    }
  }
  function bandH(w, r) {
    if (/^(analog|media|buttons)$/.test(w.type) || w.type === 'dock' || (/^(volume|stopwatch|minesweeper)$/.test(w.type) && w.w >= 2)) return 6;
    if (w.type === 'time' && w.h >= 2) return Math.max(34, Math.ceil(9 + 1.0791 * Math.round(clamp(r.h * 0.085, SMALL, 24)) + Math.round(clamp(r.h * 0.085, SMALL, 24)) * 0.24 + 6));
    return 34;
  }
  function haloDisc(cx, cy, rad) { if (halo) circle(cx, cy, rad, halo); }

  // ---- the clocks ----
  function vrNote(now) { var m = vrMinutes(now); return 'VR ' + Math.floor(m / 60) + ':' + two(m % 60); }
  function clockHead(r, w, now) {
    var pad = padX(r), iw = r.w - 2 * pad, ds = w.h >= 2 ? Math.round(clamp(r.h * 0.085, SMALL, 24)) : SMALL;
    var dBase = r.y + 9 + 1.0791 * ds;
    dateHeader(r.x + pad, dBase, iw, ds, vrNote(now), now);
    return dBase + ds * 0.24 + 10;
  }
  function dateHeader(x, by, iw, size, meta, now) {
    var forms = dateForms(now), mwid = meta ? mw(meta, size, 600) : 0, date = null, i;
    if (mwid > 0) {
      for (i = 0; i < forms.length; i++) if (mw(forms[i], size, 600) <= iw - mwid - 12) { date = forms[i]; break; }
      if (date === null) mwid = 0;
    }
    if (date === null) {
      for (i = 0; i < forms.length; i++) if (mw(forms[i], size, 600) <= iw) { date = forms[i]; break; }
      if (date === null) date = ell(forms[forms.length - 1], iw, size, 600);
    }
    text(date, x, by, L.cap, size, 600);
    if (mwid > 0) text(meta, x + iw, by, L.cap, size, 600, false, 'end');
  }
  function timeWidget(r, w, now) {
    var pad = padX(r), iw = r.w - 2 * pad, x = r.x + pad, hh = two(now.getHours()), mm = two(now.getMinutes()), ss = two(now.getSeconds());
    var top = clockHead(r, w, now);
    if (w.variant === 'stacked') {
      var tall = w.h >= 2, stack = r.w <= r.h * 1.6, bottom = r.y + r.h - (tall ? 16 : 12), ah = bottom - top;
      var k = mw('00', 100, 700, true) / 100, sz = tall ? Math.max(SMALL, Math.round(ah * 0.11)) : SMALL;
      var sideW = mw('00', sz, 600) + 8, size;
      if (stack) {
        size = Math.max(16, Math.floor(Math.min(ah / (2 * DIGSPAN + 0.12), (iw - sideW) / k) * 2) / 2);
        var y0 = top + (ah - size * (2 * DIGSPAN + 0.12)) / 2, b1 = y0 + size * DIGTOP, b2 = b1 + size * (DIGSPAN + 0.12);
        valueText(x, b1, hh, size, L.text);
        valueText(x, b2, mm, size, L.accentText);
        text(ss, x + k * size + 8, b2, L.sub, sz, 600);
        return;
      }
      size = Math.max(16, Math.floor(Math.min(ah / DIGSPAN, (iw - sideW) / (2 * k + 0.22)) * 2) / 2);
      var by = top + size * DIGTOP + (ah - size * DIGSPAN) / 2, hw = valueText(x, by, hh, size, L.text), mx = x + hw + 0.22 * size;
      var mww = valueText(mx, by, mm, size, L.accentText);
      text(ss, mx + mww + 8, by, L.sub, sz, 600);
      return;
    }
    // the standard design, one row (TimeM): 44 px, the seconds small on its baseline
    var bottomM = r.y + r.h - 14, sw = mw('00', SMALL, 600) + 6;
    var vs = Math.max(24, fitSize('00:00', iw - sw, 44, 700, true));
    var byM = top + vs * DIGTOP + (bottomM - top - vs * DIGSPAN) / 2;
    var tw = valueText(x, byM, hh + ':' + mm, vs, L.text);
    text(ss, x + tw + 6, byM, L.sub, SMALL, 600);
  }
  // the 1x1 analog clock: a squircle face, ticks reaching into its corners (AnalogFace + AnalogS)
  function squircle(r, n) {
    var a = r.w / 2, b = r.h / 2, cx = r.x + a, cy = r.y + b, e = 2 / n, N = 160, d = '';
    for (var i = 0; i < N; i++) {
      var t = i * Math.PI * 2 / N, c = Math.cos(t), s = Math.sin(t);
      d += (i ? 'L' : 'M') + n2(cx + a * Math.sign(c) * Math.pow(Math.abs(c), e)) + ' ' + n2(cy + b * Math.sign(s) * Math.pow(Math.abs(s), e));
    }
    return d + 'Z';
  }
  function analogFace(r, w, f0, f1) {
    var apple = !w.style;
    if (apple) {
      path(squircle(r, 4), grad(r, alpha(0xFF262626, 0.96), alpha(0xFF101010, 0.96), 90, 1));
      path(squircle(inflate(r, -0.6, -0.6), 4), 'none', ' stroke="#ffffff" stroke-opacity="0.25" stroke-width="1.2"');
    } else {
      path(squircle(r, 4), grad(r, f0, f1, L.angle, L.span));
      if (L.border && L.borderW > 0) { var i = L.borderW / 2; path(squircle(inflate(r, -i, -i), 4), 'none', pa('stroke', L.border) + ' stroke-width="' + L.borderW + '"'); }
    }
    var cx = r.x + r.w / 2, cy = r.y + r.h / 2, s = Math.min(r.w, r.h) / 2, ia = r.w / 2 - 0.1 * s, ib = r.h / 2 - 0.1 * s;
    var hw = Math.max(1.9, s * 0.04), mwd = Math.max(1, s * 0.02), hc = apple ? 0xFFF8F8F8 : L.text, mc = apple ? 0xFF7A7A7A : L.sub, d1 = '', d2 = '';
    for (var k = 0; k < 60; k++) {
      var t = k / 60 * Math.PI * 2, dx = Math.sin(t), dy = -Math.cos(t);
      var ro = 1 / Math.pow(Math.pow(Math.abs(dx) / ia, 4.7) + Math.pow(Math.abs(dy) / ib, 4.7), 1 / 4.7);
      var hour = k % 5 === 0, ri = hour ? s * 0.71 : ro - s * 0.14, seg = 'M' + n2(cx + dx * ri) + ' ' + n2(cy + dy * ri) + 'L' + n2(cx + dx * ro) + ' ' + n2(cy + dy * ro);
      if (hour) d1 += seg; else d2 += seg;
    }
    path(d2, 'none', pa('stroke', mc) + ' stroke-width="' + n2(mwd) + '"');
    path(d1, 'none', pa('stroke', hc) + ' stroke-width="' + n2(hw) + '"');
  }
  function analogS(r, w, now) {
    var apple = !w.style, face = apple ? mix(0xFF262626, 0xFF101010, 0.5) : mix(L.fill0, L.fill1, 0.5), ink = apple ? 0xFFF8F8F8 : L.text;
    var sec = w.color != null ? L.accent : apple || contrast(0xFFFF8C32, (face | 0xFF000000) >>> 0) >= 3 ? 0xFFFF8C32 : L.accent;
    var cx = r.x + r.w / 2, cy = r.y + r.h / 2, s = Math.min(r.w, r.h) / 2;
    var ns = Math.max(14, Math.round(s * 0.283 * 2) / 2), ox = r.w / 2 * 0.575, oy = r.h / 2 * 0.553, nb = ns * DIGH / 2;
    text('12', cx, cy - oy + nb, ink, ns, 600, true, 'middle');
    text('3', cx + ox, cy + nb, ink, ns, 600, true, 'middle');
    text('6', cx, cy + oy + nb, ink, ns, 600, true, 'middle');
    text('9', cx - ox, cy + nb, ink, ns, 600, true, 'middle');
    var m = now.getMinutes() + now.getSeconds() / 60, h = now.getHours() % 12 + m / 60;
    var neck = Math.max(1.5, s * 0.034), bar = Math.max(3.2, s * 0.066), gap = 1.4;
    function hand(turns, from, to) {
      var a = turns * Math.PI * 2, dx = Math.sin(a), dy = -Math.cos(a), b0 = s * from + bar / 2, b1 = s * to - bar / 2;
      line(cx, cy, cx + dx * b0, cy + dy * b0, face, neck + gap, true);
      line(cx + dx * b0, cy + dy * b0, cx + dx * b1, cy + dy * b1, face, bar + gap, true);
      line(cx, cy, cx + dx * b0, cy + dy * b0, ink, neck, true);
      line(cx + dx * b0, cy + dy * b0, cx + dx * b1, cy + dy * b1, ink, bar, true);
    }
    hand(h / 12, 0.12, 0.475);
    hand(m / 60, 0.135, 0.805);
    var sa = now.getSeconds() / 60 * Math.PI * 2, sx = Math.sin(sa), sy = -Math.cos(sa);
    line(cx - sx * s * 0.095, cy - sy * s * 0.095, cx + sx * s * 0.8, cy + sy * s * 0.8, sec, Math.max(1.2, s * 0.022), true);
    circle(cx, cy, Math.max(3.2, s * 0.067), ink);
    circle(cx, cy, Math.max(2, s * 0.042), sec);
    circle(cx, cy, Math.max(0.85, s * 0.018), face);
  }
  function session(r, now) {
    var m = vrMinutes(now), v = m >= 60 ? Math.floor(m / 60) + ':' + two(m % 60) : String(m), unit = m >= 60 ? null : 'min';
    var pad = padX(r), iw = r.w - 2 * pad, x = r.x + pad;
    small(r, ['In VR'], v, unit, 0, 0, L.text, false);
    note(x, r.y + NOTEBASE, iw, ['since ' + hhmm(S.vrSince)]);
  }

  // ---- what is going on ----
  function shownPct(p) { return Math.round(p * 100); }
  function batLevel(shown) { return shown <= 15 ? 2 : shown <= 30 ? 1 : 0; }
  function deviceName(x, by, name, charging, maxW) {
    var x0 = x;
    if (charging) { icon('bolt', x + 6, by - 7.5, 15, L.text); x += 14; }
    var s = ell(name, maxW - (x - x0), CAPS, 600);
    text(s, x, by, L.sub, CAPS, 600);
  }
  function battery(r, w) {
    var pad = padX(r), iw = r.w - 2 * pad;
    caption(r.x + pad, r, iw, ['Batteries', 'Battery']);
    var area = R(r.x + pad, r.y + 34, iw, r.y + r.h - 10 - (r.y + 34)), big = w.h >= 2, gapX = big ? 14 : 10;
    var maxRows = Math.max(1, Math.floor(area.h / (big ? 40 : 26))), maxCols = Math.max(1, Math.floor((area.w + gapX) / (80 + gapX)));
    var n = Math.min(S.bats.length, maxRows * maxCols, 8), cols = 1;
    while (Math.ceil(n / cols) > maxRows) cols++;
    var rows = Math.ceil(n / cols), cw = (area.w - (cols - 1) * gapX) / cols, chh = Math.min(big ? 52 : 30, area.h / rows);
    for (var j = 0; j < n; j++) {
      var b = S.bats[j], c = R(area.x + j % cols * (cw + gapX), area.y + Math.floor(j / cols) * chh, cw, chh);
      var bar = R(c.x, c.y + c.h - 4 - 3, c.w, 4), by = bar.y - 5, shown = shownPct(b[1]), pct = shown + '%';
      var pw = mw(pct, CAPS, 700, true), lv = batLevel(shown), dot = lv && L.badge;
      text(pct, c.x + c.w, by, level(lv, L.text), CAPS, 700, true, 'end');
      if (dot) statusDot(c.x + c.w - pw - 4 - 10, by - CAPS * DIGH / 2, lv);
      deviceName(c.x, by, b[0].toUpperCase(), b[2], c.x + c.w - pw - 5 - (dot ? 22 : 0) - c.x);
      meter(bar.x, bar.y, bar.w, bar.h, b[1], level(lv, L.ok));
    }
  }
  function volume(r, w, pi, i) {
    var pad = padX(r), iw = r.w - 2 * pad, pct = Math.round(S.vol * 100) + '%', bar = S.muted ? L.faint : L.accent;
    caption(r.x + pad, r, iw, ['Volume']);
    var bs = 44, by = r.y + 31, bx = r.x + r.w - pad - bs, key = 'v' + pi + '.' + i;
    press(key + '+', R(bx, by, bs, bs), 'plus', null, false, 18, true, function () { setVol(S.vol + 0.05); });
    bx -= bs + 6;
    press(key + '-', R(bx, by, bs, bs), 'minus', null, false, 18, true, function () { setVol(S.vol - 0.05); });
    var vy = by + bs / 2 + HERO * DIGH / 2, room = bx - 10 - (r.x + pad);
    if (S.muted) {
      icon('speaker', r.x + pad + 15, vy - HERO * DIGH / 2, HERO, L.badInk);
      text(pct, r.x + pad + HERO + 6, vy, L.sub, SMALL, 600);
    } else valueUnit(r.x + pad, vy, room, pct, null, HERO, 20, L.text);
    meter(r.x + pad, r.y + r.h - 15, iw, 4, S.vol, bar);
  }
  function setVol(v) { S.vol = Math.round(clamp(v, 0, 1) * 20) / 20; }
  function roomCard(r, w) {
    var pad = padX(r), iw = r.w - 2 * pad, x = r.x + pad;
    if (w.w >= 3) {
      // the world big, the people as initials (friends first, ringed), "Mira, Tobi + 10"
      var order = PEOPLE.filter(function (p) { return FRIENDS[p]; }).concat(PEOPLE.filter(function (p) { return !FRIENDS[p]; })).slice(0, 6);
      captionRow(r, x, r.x + r.w - pad, PEOPLE.length + ' here', L.cap, ['VRChat room', 'Room']);
      var compact = r.h < 120, ws = compact ? 18 : 20;
      text(ell(WORLD, iw, ws, 600), x, r.y + (compact ? 50 : 62), L.text, ws, 600);
      var rad = compact ? 12 : 15, pitch = compact ? 18 : 23, cy = r.y + r.h - (compact ? 10 : 13) - rad;
      for (var i = 0; i < order.length; i++) avatar(x + rad + i * pitch, cy, rad, order[i], !!FRIENDS[order[i]]);
      var tx = x + 2 * rad + (order.length - 1) * pitch + 12, room = r.x + r.w - pad - tx;
      if (room > 40) text(ell(order[0] + ', ' + order[1] + ' + ' + (PEOPLE.length - 2), room, SMALL, 600), tx, cy + 6, L.sub, SMALL, 600);
      return;
    }
    // two cells: the world as the note, how many big, the friends among them
    captionRow(r, x, r.x + r.w - pad, WORLD, L.cap, ['Room']);
    var vw = valueUnit(x, r.y + MBASE, 90, String(PEOPLE.length), null, 40, 24, L.text), lx = x + vw + 12, rm = r.x + r.w - pad - lx;
    text(ell('Mira, Tobi +1', rm, SMALL, 600), lx, r.y + MBASE, L.ok, SMALL, 600);
  }
  function nameHash(s) { var h = 17; for (var i = 0; i < s.length; i++) h = (Math.imul(h, 31) + s.charAt(i).toUpperCase().charCodeAt(0)) | 0; return h & 0x7FFFFFFF; }
  function avatarColor(name) { return AVATARS[nameHash(name) % AVATARS.length]; }
  function avatar(cx, cy, rad, name, friend) {
    haloDisc(cx, cy, rad + 2.5);
    if (friend) circle(cx, cy, rad, L.ok);
    circle(cx, cy, friend ? rad - 2.5 : rad, avatarColor(name));
    var fs = Math.max(14, Math.round(rad * 1.05));
    textMid(name.charAt(0).toUpperCase(), cx, cy + 0.5, DARK, fs, 700, false, 'middle');
  }
  function dock(r) {
    var a = inflate(r, -9, -9), gap = 3, cell = Math.floor(Math.min((a.w - 2 * gap) / 3, (a.h - gap) / 2));
    var gw = 3 * cell + 2 * gap, gh = 2 * cell + gap, x0 = Math.floor(a.x + (a.w - gw) / 2), y0 = Math.floor(a.y + (a.h - gh) / 2);
    for (var i = 0; i < 6; i++) keyFace(x0 + i % 3 * (cell + gap), y0 + Math.floor(i / 3) * (cell + gap), cell, 4, i);
  }
  // a Hexpad key: the made-up faces of the app's pictures - a colour with a white dot
  function keyFace(x, y, s, rad, i) {
    rect(x, y, s, s, BLACK, rad);
    rect(x, y, s, s, KEYCOL[i], rad);
    circle(x + s / 2, y + s / 2, s * 18 / 68, PURE);
  }
  // now playing on a coloured card (MediaOrbit): the cover, the caption, title and artist, the play disc
  function media(r, w, pi, i) {
    var pad = padX(r), x = r.x + pad, m = 12, s = r.h - 2 * m, cover = R(r.x + m, r.y + (r.h - s) / 2, s, s);
    rect(cover.x, cover.y, s, s, L.tile, Math.max(10, s * 0.1));
    coverImage(cover, Math.max(10, s * 0.1));
    x = cover.x + s + 14;
    var big = 48, bx = r.x + r.w - 12 - big, cy = r.y + r.h / 2;
    press('m' + pi + '.' + i, R(bx, cy - big / 2, big, big), S.playing ? 'pause' : 'play', null, true, 20, true, togglePlay);
    var right = bx - 10, tw = right - x;
    captionRow(r, x, right, null, L.cap, ['Now playing', 'Playing']);
    text(ell('Midnight City', tw, SMALL, 600), x, r.y + 54, L.text, SMALL, 600);
    text(ell('M83', tw, SMALL, 600), x, r.y + 78, L.sub, SMALL, 600);
  }
  function togglePlay() { S.playing = !S.playing; }
  var cover = null;
  function coverImage(r, rad) {
    if (!cover) return;
    var id = 'wc' + (++gid);
    o.push('<clipPath id="' + id + '"><rect x="' + n2(r.x) + '" y="' + n2(r.y) + '" width="' + n2(r.w) + '" height="' + n2(r.h) + '" rx="' + n2(rad) + '"/></clipPath>');
    o.push('<image href="' + cover.url + '" x="' + n2(r.x) + '" y="' + n2(r.y) + '" width="' + n2(r.w) + '" height="' + n2(r.h) + '" preserveAspectRatio="none" clip-path="url(#' + id + ')"/>');
  }
  // now playing in its cover's colours (MediaCover, the M size)
  function mediaCover(r, pi, i) {
    var pal = cover ? cover.pal : [0xFF6A2A4A, 0xFF4A2A6A, 0xFF2A2A4A], g = grad(r, pal[0], pal[1], 45, 1, pal[2]);
    rect(r.x, r.y, r.w, r.h, g, RAD);
    rect(r.x, r.y, r.w, r.h, grad(r, 0x00000000, 0x4D000000, 90, 1), RAD);
    strokeRect(r.x + 0.6, r.y + 0.6, r.w - 1.2, r.h - 1.2, 0x1AFFFFFF, 1.25, RAD - 0.6);
    var pad = 14, x = r.x + pad, iw = r.w - 2 * pad, compact = r.h < 120, disc = compact ? 44 : 48;
    var dcx = r.x + r.w - pad - disc / 2, dcy = compact ? r.y + r.h / 2 : r.y + 70, key = 'mc' + pi + '.' + i;
    hit(key, inflate(R(dcx - disc / 2, dcy - disc / 2, disc, disc), 0, 0), togglePlay, true);
    var ds = isHeld(key) ? 0.9 : 1;
    circle(dcx, dcy, disc * ds / 2, 0xFFF2F3F7);
    icon(S.playing ? 'pause' : 'play', dcx, dcy, Math.round(disc * 0.4 * ds), pal[1]);
    var right = dcx - disc / 2 - 12, tw = right - x, cw = r.x + r.w - padX(r) - x;
    var capS = mtr('NOW PLAYING', CAPS, 600, 1) <= cw ? 'NOW PLAYING' : 'PLAYING';
    capText(capS, x, r.y + CAPBASE, 0xC0FFFFFF);
    text(ell('Midnight City', tw, compact ? 18 : 20, 600), x, r.y + (compact ? 52 : 80), PURE, compact ? 18 : 20, 600);
    var time = clock2(S.pos) + ' / ' + clock2(S.dur);
    text('M83 · ' + time, x, r.y + (compact ? 72 : 102), 0xD0FFFFFF, 15, 600);
    var bar = R(x, r.y + r.h - (compact ? 14 : 16), iw, 4);
    rect(bar.x, bar.y, bar.w, bar.h, 0x40FFFFFF, 2);
    rect(bar.x, bar.y, Math.max(4, bar.w * S.pos / S.dur), 4, PURE, 2);
  }
  function statS(r, id) {
    var v, unit = null, frac, lv = 0;
    if (id === 'fps') { v = String(S.fps); unit = '/' + S.hz; frac = S.fps / S.hz; lv = S.fps < S.hz * 0.85 ? 1 : 0; }
    else if (id === 'gpu') { v = dec(S.gpu); unit = 'ms'; frac = S.gpu / (1000 / S.hz); }
    else if (id === 'reproj') { v = S.reproj + '%'; frac = Math.min(1, S.reproj / 50); lv = S.reproj > 25 ? 2 : S.reproj > 8 ? 1 : 0; }
    else if (id === 'vram') { v = dec(S.vram); unit = 'GB'; frac = S.vram / S.vramTotal; }
    else { v = dec(S.ram); unit = 'GB'; frac = S.ram / S.ramTotal; }
    var cap = { fps: 'FPS', reproj: 'Reproj', gpu: 'GPU', vram: 'VRAM', ram: 'RAM' }[id];
    small(r, [cap], v, unit, frac, level(lv, roleColor(id)), level(lv, L.text), true, id === 'ram' || id === 'vram', lv);
  }
  function cpuS(r) {
    var pad = padX(r), iw = r.w - 2 * pad, x = r.x + pad, c = S.cpu[S.cpu.length - 1], v = Math.round(c * 100) + '%';
    caption(x, r, iw, ['CPU']);
    valueText(x, r.y + SBASE, v, Math.max(24, fitSize(v, iw, HERO, 700, true)), L.text);
    var n = Math.min(30, S.cpu.length), pitch = iw / 30, bw = Math.max(1.6, pitch - 0.8), bottom = r.y + r.h - 10, top = 0.25, role = roleColor('cpu');
    for (var i = 0; i < n; i++) top = Math.max(top, S.cpu[S.cpu.length - n + i]);
    for (i = 0; i < n; i++) {
      var val = S.cpu[S.cpu.length - n + i], h = Math.max(3, val / top * 16);
      rect(x + iw - (n - i) * pitch + (pitch - bw) / 2, bottom - h, bw, h, role, 1);
    }
  }
  // the frame graph, two rows tall (GraphLine): the last 30 s as a line, the budget dashed, the peak a dot; the numbers under it
  function graph(r) {
    var pad = padX(r), iw = r.w - 2 * pad, x = r.x + pad, budget = 1000 / S.hz, coloured = capB();
    captionRow(r, x, r.x + r.w - pad, dec(budget) + ' ms budget', coloured ? L.cap : L.warnInk, ['Frame time · 30 s', 'Frame time', 'Frames']);
    var g = R(x, r.y + 40, r.x + r.w - pad - x, r.y + r.h - 72 - (r.y + 40)), n = S.frames.length, peak = 0, peakAt = 0, i;
    for (i = 0; i < n; i++) if (S.frames[i][0] > peak) { peak = S.frames[i][0]; peakAt = i; }
    var top = Math.max(budget * 1.5, peak * 1.1), lineC = L.graphBar ? L.text : L.accent, pts = [];
    for (i = 0; i < n; i++) pts.push(n2(g.x + g.w * (i + 300 - n) / 299) + ' ' + n2(g.y + g.h - g.h * clamp(S.frames[i][0] / top, 0, 1)));
    path('M' + pts.join('L') + 'L' + n2(g.x + g.w) + ' ' + n2(g.y + g.h) + 'L' + n2(g.x + g.w * (300 - n) / 299) + ' ' + n2(g.y + g.h) + 'Z', alpha(lineC, 0.18));
    path('M' + pts.join('L'), 'none', pa('stroke', lineC) + ' stroke-width="2.5" stroke-linejoin="round"');
    var by = g.y + g.h - g.h * (budget / top);
    line(g.x, by, g.x + g.w, by, coloured ? alpha(L.text, 0.6) : alpha(L.warnInk, 0.8), 1.5);
    o[o.length - 1] = o[o.length - 1].replace('/>', ' stroke-dasharray="5 4"/>');
    var px = g.x + g.w * (peakAt + 300 - n) / 299, py = g.y + g.h - g.h * clamp(peak / top, 0, 1), flag = S.frames[peakAt][1];
    haloDisc(px, py, 7);
    circle(px, py, 4.5, flag === 2 ? L.flagBad : peak > budget ? L.flagWarn : L.text);
    var cw = iw / 3, vb = r.y + r.h - 36, lb = r.y + r.h - 13;
    statCol(x, vb, lb, cw, String(S.fps), '/ ' + S.hz, 'fps');
    statCol(x + cw, vb, lb, cw, String(S.low), null, '1% low fps');
    statCol(x + 2 * cw, vb, lb, cw, S.reproj + '%', null, 'reprojected');
  }
  function statCol(x, vb, lb, cw, v, unit, label) {
    valueUnit(x, vb, cw - 8, v, unit, 28, 20, L.text);
    text(ell(label, cw - 6, 15, 600), x, lb, L.sub, 15, 600);
  }
  function mic(r, w, pi, i) {
    var pad = padX(r), iw = r.w - 2 * pad, x = r.x + pad;
    hit('mic' + pi + '.' + i, r, function () { S.mic = !S.mic; });
    caption(x, r, iw, ['Microphone', 'Mic']);
    var word = S.mic ? 'Muted' : 'Live', by = r.y + SBASE, mid = by - HERO * DIGH / 2;
    icon(S.mic ? 'micoff' : 'mic', x + HERO / 2, mid, HERO, S.mic ? L.badInk : L.text);
    var tx = x + HERO + 8, room = r.x + r.w - pad - tx;
    var wt = S.mic ? 700 : 600, below = mw(word, SMALL, wt) > room;
    if (!below) text(word, tx, by, S.mic ? L.text : L.sub, SMALL, wt);
    else text(ell(word, iw, SMALL, wt), x, r.y + r.h - 12, S.mic ? L.text : L.sub, SMALL, wt);
    if (S.mic || below) return;
    var f = Math.min(1, Math.sqrt(S.micPeak));
    rect(x, r.y + r.h - 16, iw, 4, L.track, 2);
    if (f > 0.01) rect(x, r.y + r.h - 16, Math.max(4, iw * f), 4, L.ok, 2);
  }
  function notes(r, now) {
    var pad = padX(r), iw = r.w - 2 * pad, x = r.x + pad;
    captionRow(r, x, r.x + r.w - pad, String(NOTES.length), L.cap, ['Notifications', 'Notices']);
    var top = r.y + 34, bottom = r.y + r.h - 8, slots = Math.max(1, Math.floor((bottom - top) / 27)), rh = (bottom - top) / slots;
    for (var i = 0; i < Math.min(slots, NOTES.length); i++) {
      var nt = NOTES[i], cy = top + rh * (i + 0.5), by = cy + 6, secs = nt[2] + (Date.now() - T0) / 1000;
      circle(x + 11, cy, 11, avatarColor(nt[1]));
      textMid(nt[1].charAt(0).toUpperCase(), x + 11, cy + 0.5, DARK, 14, 700, false, 'middle');
      var age = secs < 60 ? 'now' : secs < 3600 ? Math.floor(secs / 60) + ' m' : Math.floor(secs / 3600) + ' h', aw = mw(age, 15, 600);
      text(age, r.x + r.w - pad - aw - 2, by, L.sub, 15, 600);
      var tx = x + 31, tw = r.x + r.w - pad - aw - 10 - tx;
      text(ell(nt[0], tw, SMALL, 600), tx, by, L.text, SMALL, 600);
    }
  }

  // ---- buttons ----
  var BTN = { keyboard: ['keyboard', 'Keyboard'], desktops: ['monitor', 'Monitors'], recenter: ['recenter', 'Recenter'], layout: ['layouts', 'Layout'],
    night: ['night', 'Night mode'], settings: ['settings', 'Settings'], status: ['status', 'Status line'], theater: ['theater', 'Theater'] };
  function isOn(id) { return (id === 'keyboard' && S.keyboard) || (id === 'desktops' && S.desktops) || (id === 'night' && S.night) || (id === 'status' && S.status) || (id === 'theater' && S.theater); }
  function isToggle(id) { return /^(keyboard|desktops|night|status|theater)$/.test(id); }
  function btnIcon(id) { return id === 'desktops' && !S.desktops ? 'hide' : BTN[id][0]; }
  function btnName(id, wide) { return id === 'layout' ? LAYOUTS[S.layout] : wide ? BTN[id][1] : id === 'night' ? 'Night' : id === 'status' ? 'Status' : BTN[id][1]; }
  // a button that is its card (TileButton)
  function tileButton(key, r, id, wide) {
    var on = isOn(id), full = !on && !isToggle(id) && L.check, hv = isHov(key);
    hit(key, r, function () { act(id); });
    var s = isHeld(key) ? 0.92 : 1;
    if (s !== 1) o.push('<g transform="translate(' + n2(r.x + r.w / 2) + ' ' + n2(r.y + r.h / 2) + ') scale(' + s + ') translate(' + n2(-r.x - r.w / 2) + ' ' + n2(-r.y - r.h / 2) + ')">');
    if (on) rect(r.x, r.y, r.w, r.h, grad(r, L.on0, L.on1, L.angle, L.span), RAD);
    else if (full) rect(r.x, r.y, r.w, r.h, grad(r, mix(L.on0, L.onText, hv ? 0.12 : 0), mix(L.on1, L.onText, hv ? 0.12 : 0), L.angle, L.span), RAD);
    else if (hv) rect(r.x, r.y, r.w, r.h, L.offHover, RAD);
    else rect(r.x, r.y, r.w, r.h, grad(r, L.off0, L.off1, L.angle, L.span), RAD);
    if (!on && !full && L.offBorder) { var bi = L.offBorderW * 0.48; strokeRect(r.x + bi, r.y + bi, r.w - 2 * bi, r.h - 2 * bi, L.offBorder, L.offBorderW, RAD - bi); }
    var fg = on || full ? L.onText : L.offText, lw = on ? 700 : 600, pad = padX(r), check = on && L.check, label = btnName(id, wide);
    if (check) {
      var ccx = r.x + r.w - pad - 9 + 2, ccy = wide ? r.y + r.h / 2 : r.y + 24;
      circle(ccx, ccy, 9, fg);
      icon('check', ccx, ccy, 11, mix(L.on0, L.on1, 0.5));
    }
    var iw = r.w - 2 * pad;
    if (wide) {
      icon(btnIcon(id), r.x + pad + 11, r.y + r.h / 2, 22, fg);
      var lx = r.x + pad + 32, room = r.x + r.w - pad - lx - (check ? 26 : 0), ls1 = Math.max(15, fitSize(label, room, SMALL, lw));
      textMid(ell(label, room, ls1, lw), lx, r.y + r.h / 2, fg, ls1, lw);
    } else {
      icon(btnIcon(id), r.x + pad + 12, r.y + 10 + 14, 24, fg);
      var ls = Math.max(15, fitSize(label, iw, SMALL, lw));
      text(ell(label, iw, ls, lw), r.x + pad, r.y + r.h - 15, fg, ls, lw);
    }
    if (s !== 1) o.push('</g>');
  }
  // a group of buttons in one card (GroupOrbit / GroupTile)
  function group(r, w, pi, wi) {
    var ids = w.items, n = ids.length, smallRow = r.h < 120 && w.h === 1, gap = smallRow ? 5 : 6, a = inflate(r, -8, -8), cols, rows, mode;
    if (L.band) a = R(a.x, r.y + 12, a.w, a.y + a.h - r.y - 12);
    if (w.h >= 2) { cols = n <= 3 ? 1 : 2; rows = Math.ceil(n / cols); mode = 2; }
    else if (n <= 3 || a.w / n >= 90) { cols = n; rows = 1; mode = a.w / n >= 150 ? 2 : 0; }
    else { cols = Math.ceil(n / 2); rows = 2; mode = 1; }
    var tw = (a.w - (cols - 1) * gap) / cols, th = (a.h - (rows - 1) * gap) / rows, j;
    if (mode === 0) {
      if (th < 64) mode = 1;
      else for (j = 0; j < n; j++) if (fitSize(btnName(ids[j]), tw - 8, 15, 600) < 14) { mode = 1; break; }
    } else if (mode === 2) {
      for (j = 0; j < n; j++) if (mw(btnName(ids[j]), 15, 700) > tw - 58) { mode = 1; cols = Math.min(n, 3); rows = Math.ceil(n / cols); tw = (a.w - (cols - 1) * gap) / cols; th = (a.h - (rows - 1) * gap) / rows; break; }
    }
    for (j = 0; j < n; j++) groupTile('g' + pi + '.' + wi + '.' + j, R(a.x + j % cols * (tw + gap), a.y + Math.floor(j / cols) * (th + gap), tw, th), ids[j], mode);
  }
  function groupTile(key, t, id, mode) {
    var on = isOn(id), s = isHeld(key) ? 0.92 : 1;
    hit(key, t, function () { act(id); });
    if (s !== 1) o.push('<g transform="translate(' + n2(t.x + t.w / 2) + ' ' + n2(t.y + t.h / 2) + ') scale(' + s + ') translate(' + n2(-t.x - t.w / 2) + ' ' + n2(-t.y - t.h / 2) + ')">');
    rect(t.x, t.y, t.w, t.h, on ? L.accentFill : isHov(key) ? L.tileHover : L.tile, 10);
    var fg = on ? L.onAccent : L.text, lw = on ? 700 : 600, label = btnName(id);
    if (mode === 2) {
      var room = t.w - 58, ls = Math.max(15, fitSize(label, room, SMALL, lw)), lab = ell(label, room, ls, lw), lwid = mw(lab, ls, lw), lx = t.x + (t.w - 34 - lwid) / 2;
      icon(btnIcon(id), lx + 12, t.y + t.h / 2, 24, fg);
      textMid(lab, lx + 34, t.y + t.h / 2, fg, ls, lw);
    } else if (mode === 0) {
      var ls0 = Math.max(14, fitSize(label, t.w - 8, 15, lw));
      icon(btnIcon(id), t.x + t.w / 2, t.y + t.h * 0.4, 24, fg);
      text(ell(label, t.w - 8, ls0, lw), t.x + t.w / 2, t.y + t.h - 14, fg, ls0, lw, false, 'middle');
    } else icon(btnIcon(id), t.x + t.w / 2, t.y + t.h / 2, 22, fg);
    if (s !== 1) o.push('</g>');
  }

  // ---- Minesweeper: the game (MinesGame) and the small tile ----
  function Mines(w, h, count) { this.w = w; this.h = h; this.count = count; this.reset(); }
  Mines.prototype.reset = function () {
    var n = this.w * this.h;
    this.mine = new Array(n).fill(false); this.adj = new Array(n).fill(0); this.st = new Array(n).fill(0);
    this.state = 0; this.placed = false; this.flags = 0; this.opened = 0; this.hitAt = -1; this.t0 = 0; this.t1 = 0;
  };
  Mines.prototype.cells = function () { return this.w * this.h; };
  Mines.prototype.around = function (i) {
    var x = i % this.w, y = Math.floor(i / this.w), out = [];
    for (var dy = -1; dy <= 1; dy++) for (var dx = -1; dx <= 1; dx++) {
      if (!dx && !dy) continue;
      var nx = x + dx, ny = y + dy;
      if (nx >= 0 && ny >= 0 && nx < this.w && ny < this.h) out.push(ny * this.w + nx);
    }
    return out;
  };
  Mines.prototype.over = function () { return this.state >= 2; };
  Mines.prototype.seconds = function () { return this.state === 0 ? 0 : Math.max(0, ((this.over() ? this.t1 : Date.now()) - this.t0) / 1000); };
  Mines.prototype.place = function (first) {
    var keep = this.around(first).concat([first]), free = [], c;
    for (c = 0; c < this.cells(); c++) if (keep.indexOf(c) < 0) free.push(c);
    for (var k = 0; k < this.count; k++) { var j = k + Math.floor(Math.random() * (free.length - k)), t = free[k]; free[k] = free[j]; free[j] = t; this.mine[free[k]] = true; }
    for (c = 0; c < this.cells(); c++) { var a = 0, nb = this.around(c); for (var q = 0; q < nb.length; q++) if (this.mine[nb[q]]) a++; this.adj[c] = a; }
    this.placed = true;
  };
  Mines.prototype.flood = function (i) {
    var stack = [i];
    while (stack.length) {
      var c = stack.pop();
      if (this.st[c] !== 0 || this.mine[c]) continue;
      this.st[c] = 1; this.opened++;
      if (this.adj[c] === 0) { var nb = this.around(c); for (var q = 0; q < nb.length; q++) if (this.st[nb[q]] === 0) stack.push(nb[q]); }
    }
  };
  Mines.prototype.lose = function (i) { this.state = 3; this.hitAt = i; this.t1 = Date.now(); };
  Mines.prototype.check = function () { if (this.opened === this.cells() - this.count) { this.state = 2; this.t1 = Date.now(); } };
  Mines.prototype.click = function (i) {
    if (this.over() || this.st[i] === 2) return;
    if (this.st[i] === 1) {
      if (!this.adj[i]) return;
      var nb = this.around(i), f = 0, hidden = 0, q, hitAt = -1;
      for (q = 0; q < nb.length; q++) { if (this.st[nb[q]] === 2) f++; else if (this.st[nb[q]] === 0) hidden++; }
      if (f !== this.adj[i] || !hidden) return;
      for (q = 0; q < nb.length; q++) { if (this.st[nb[q]] !== 0) continue; if (this.mine[nb[q]]) { if (hitAt < 0) hitAt = nb[q]; continue; } this.flood(nb[q]); }
      if (hitAt >= 0) this.lose(hitAt); else this.check();
      return;
    }
    if (!this.placed) this.place(i);
    if (this.state === 0) { this.state = 1; this.t0 = Date.now(); }
    if (this.mine[i]) { this.lose(i); return; }
    this.flood(i);
    this.check();
  };
  Mines.prototype.flag = function (i) {
    if (this.over() || this.st[i] === 1) return;
    if (this.st[i] === 2) { this.st[i] = 0; this.flags--; } else { this.st[i] = 2; this.flags++; }
  };
  var games = [new Mines(9, 9, 10), new Mines(16, 16, 40)], level9 = 0, flagMode = false, best = [0, 0];
  try { best = JSON.parse(localStorage.getItem('orbit-mines-best') || '[0,0]'); } catch (e) {}
  function game() { return games[level9]; }
  function minesTime(g) { var s = Math.min(5999, Math.floor(g.seconds())); return Math.floor(s / 60) + ':' + two(s % 60); }
  function smiley(cx, cy, rad, st, oh, c, lw) {
    ring(cx, cy, rad, c, lw);
    var ex = rad * 0.36, ey = cy - rad * 0.2, er = Math.max(1.4, rad * 0.11);
    if (st === 3) {
      var s = rad * 0.13;
      [cx - ex, cx + ex].forEach(function (sx) { line(sx - s, ey - s, sx + s, ey + s, c, lw * 0.8, true); line(sx - s, ey + s, sx + s, ey - s, c, lw * 0.8, true); });
      arc(cx, cy + rad * 0.66, rad * 0.4, 0.85, 0.3, c, lw);
      return;
    }
    if (st === 2) {
      var gw = rad * 0.5, gh = rad * 0.32;
      rect(cx - ex - gw / 2, ey - gh / 2, gw, gh, c, gh * 0.45);
      rect(cx + ex - gw / 2, ey - gh / 2, gw, gh, c, gh * 0.45);
      line(cx - ex - gw / 2 - rad * 0.12, ey - gh * 0.3, cx + ex + gw / 2 + rad * 0.12, ey - gh * 0.3, c, Math.max(1.2, lw * 0.8));
      arc(cx, cy, rad * 0.55, 0.35, 0.3, c, lw);
      return;
    }
    circle(cx - ex, ey, er, c);
    circle(cx + ex, ey, er, c);
    if (oh) ring(cx, cy + rad * 0.36, rad * 0.16, c, lw);
    else arc(cx, cy, rad * 0.55, 0.35, 0.3, c, lw);
  }
  // an arc in turns (0 = 12 o'clock, clockwise), round ends
  function arc(cx, cy, r, start, sweep, c, sw) {
    if (sweep >= 0.9995) { ring(cx, cy, r, c, sw); return; }
    function at(t) { return n2(cx + r * Math.sin(t * Math.PI * 2)) + ' ' + n2(cy - r * Math.cos(t * Math.PI * 2)); }
    path('M' + at(start) + 'A' + n2(r) + ' ' + n2(r) + ' 0 ' + (sweep > 0.5 ? 1 : 0) + ' 1 ' + at(start + sweep), 'none', pa('stroke', c) + ' stroke-width="' + n2(sw) + '" stroke-linecap="round"');
  }
  function faceColor(g, normal) { return g.state === 2 ? (L.badge ? normal : L.ok) : g.state === 3 ? (L.badge ? normal : L.badInk) : normal; }
  var HUES = [0, 0xFF5AA9FF, 0xFF3DDC84, 0xFFFF5A5A, 0xFF8C93FF, 0xFFFF9C85, 0xFF2DD4BF, 0, 0];
  function hueOf(n) { return n === 7 ? L.text : n === 8 ? L.sub : HUES[n]; }
  function minesTile(r) {
    var g = game(), pad = padX(r), iw = r.w - 2 * pad, x = r.x + pad, left = String(g.count - g.flags);
    smiley(x + 9, r.y + 19, 9, g.state, false, faceColor(g, L.capText), 1.8);
    text(minesTime(g), r.x + r.w - pad, r.y + CAPBASE + 2, L.cap, SMALL, 600, false, 'end');
    var hp = Math.floor(34 / g.h * 2) / 2, bs = hp * g.w, bh = hp * g.h, bx = r.x + r.w - pad - bs, by = Math.round(r.y + 61 - bh / 2);
    var hidden = mix(L.tile, L.text, 0.2), lost = g.state === 3;
    for (var i = 0; i < g.cells(); i++) {
      var mine = lost && g.mine[i] && g.st[i] !== 2;
      rect(bx + i % g.w * hp, by + Math.floor(i / g.w) * hp, hp - 0.8, hp - 0.8, mine || i === g.hitAt ? BAD : g.st[i] === 2 ? L.accent : g.st[i] === 1 ? 0x47000000 : hidden, 0.8);
    }
    valueText(x, r.y + SBASE, left, Math.max(17, fitSize(left, iw - bs - 6, HERO, 700, true)), g.count - g.flags < 0 ? level(1, L.text) : L.text);
  }

  // ================= one widget, one page =================
  function widget(w, pi, wi, now) {
    var r = cellRect(w.x, w.y, w.w, w.h), danger = (w.type === 'volume' && S.muted) || (w.type === 'mic' && S.mic);
    L = danger ? K.D : lookOf(w);
    var button = !!BTN[w.type] && w.type !== 'buttons';
    if (w.type === 'media' && !w.style && w.color == null) { mediaCover(r, pi, wi); return; }
    if (!button) cardBack(r, w, danger);
    var opens = { time: 'clock', analog: 'clock', session: 'clock', battery: 'battery', volume: 'volume', media: 'media', minesweeper: 'mines', room: 'room', dock: 'dock' }[w.type];
    if (opens) hit('w' + pi + '.' + wi, r, function () { openDetail(opens, w); });
    switch (w.type) {
      case 'time': timeWidget(r, w, now); break;
      case 'analog': analogS(r, w, now); break;
      case 'session': session(r, now); break;
      case 'battery': battery(r, w); break;
      case 'volume': volume(r, w, pi, wi); break;
      case 'room': roomCard(r, w); break;
      case 'dock': dock(r); break;
      case 'media': media(r, w, pi, wi); break;
      case 'vram': case 'ram': case 'fps': case 'gpu': case 'reproj': statS(r, w.type); break;
      case 'cpu': cpuS(r); break;
      case 'graph': graph(r); break;
      case 'mic': mic(r, w, pi, wi); break;
      case 'notifications': notes(r, now); break;
      case 'minesweeper': minesTile(r); break;
      case 'buttons': group(r, w, pi, wi); break;
      default: if (button) tileButton('b' + pi + '.' + wi, r, w.type, w.w >= 2);
    }
  }

  // ================= the opened views (a tap on a widget grows it into the whole watch) =================
  var openAnim = 1, openFrom = null, closing = false;
  function openDetail(kind, w) {
    if (S.open) return;
    S.open = { kind: kind, w: w };
    closing = false;
    openFrom = cellRect(w.x, w.y, w.w, w.h);
    if (style95) { openAnim = 1; draw95(); return; }
    animateOpen(0, 1);
  }
  function closeDetail() {
    if (!S.open || closing) return;
    if (style95) { S.open = null; draw95(); return; }
    closing = true;
    animateOpen(1, 0);
  }
  function redrawOpen() { if (style95) draw95(); else drawDetail(); }
  var animRaf = 0, animGen = 0;
  function animateOpen(from, to) {
    cancelAnimationFrame(animRaf);
    if (reduce) { openAnim = to; if (to === 0) { S.open = null; closing = false; } drawAll(); return; }
    var t0 = performance.now(), dur = to ? 380 : 220, done = false, gen = ++animGen;
    // where frames do not come (a background tab), it still ends where it should
    setTimeout(function () { if (!done) step(t0 + dur); }, dur + 120);
    function step(t) {
      if (done || gen !== animGen) return;
      var p = Math.min(1, (t - t0) / dur), q = p - 1;
      // opening: a spring that overshoots a little (the app's 240 / 24); closing: straight back into the tile
      openAnim = to ? 1 + 2.2 * q * q * q + 1.2 * q * q : 1 - p * p * (3 - 2 * p);
      if (p >= 1) { openAnim = to; done = true; if (to === 0) { S.open = null; closing = false; } }
      drawDetail();
      if (p < 1) animRaf = requestAnimationFrame(step);
      else drawAll();
    }
    step(t0);
  }
  function lerpR(a, b, t) { return R(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.w + (b.w - a.w) * t, a.h + (b.h - a.h) * t); }
  function detailSvg(now) {
    o = [];
    if (!S.open) return '';
    var e = clamp(openAnim, 0, 1.2), ec = clamp(e, 0, 1), E = cellRect(0, 0, 6, 4), kind = S.open.kind;
    L = K.D;
    hits = hitsOpen = [];
    halo = K.card;
    rect(0, 0, TW, TH, alpha(BLACK, 0.62 * ec), 26);
    hit('scrim', R(0, 0, TW, TH), closeDetail);
    var r = lerpR(openFrom, E, e);
    rect(r.x, r.y, r.w, r.h, K.card, RAD);
    strokeRect(r.x + 0.6, r.y + 0.6, r.w - 1.2, r.h - 1.2, L.edge, 1.25, RAD - 0.6);
    hit('card', r, null);
    var grow = kind === 'mines' || kind === 'room' || kind === 'dock';
    var ca = grow ? clamp(e * 3, 0, 1) : clamp((e - 0.5) / 0.45, 0, 1);
    if (ca <= 0.01) return o.join('');
    var cid = 'wc' + (++gid);
    o.push('<clipPath id="' + cid + '"><rect x="' + n2(r.x) + '" y="' + n2(r.y) + '" width="' + n2(r.w) + '" height="' + n2(r.h) + '" rx="' + RAD + '"/></clipPath>');
    o.push('<g clip-path="url(#' + cid + ')"' + (ca < 0.999 ? ' opacity="' + n2(ca) + '"' : '') + '>');
    var gs = grow ? Math.min(r.w / E.w, r.h / E.h) : 1;
    if (Math.abs(gs - 1) > 0.002) {
      var tx = E.x + E.w / 2, ty = E.y + E.h / 2, rx = r.x + r.w / 2, ry = r.y + r.h / 2;
      o.push('<g transform="translate(' + n2(rx - gs * tx) + ' ' + n2(ry - gs * ty) + ') scale(' + n2(gs) + ')">');
    } else gs = 1;
    var ready = !closing && e > 0.98;
    if (!ready) hits = [];
    if (!grow) textMid(ell({ clock: 'Time', battery: 'Batteries', volume: 'Volume', media: 'Now playing' }[kind], E.w - 120, 24, 600), E.x + 24, E.y + 36, L.text, 24, 600);
    press('close', R(E.x + E.w - 22 - 46, E.y + 14, 46, 46), 'close', null, false, 18, true, closeDetail);
    var c = R(E.x + 24, E.y + 74, E.w - 48, E.h - 96);
    if (kind === 'mines') minesDetail(E);
    else if (kind === 'room') roomDetail(E, now);
    else if (kind === 'dock') dockDetail(E);
    else if (kind === 'clock') clockDetail(c, now);
    else if (kind === 'battery') batteryDetail(c);
    else if (kind === 'volume') volumeDetail(c);
    else if (kind === 'media') mediaDetail(c);
    if (gs !== 1) o.push('</g>');
    o.push('</g>');
    if (!ready) hitsOpen = [{ id: 'scrim', x: 0, y: 0, w: TW, h: TH, act: null }];
    return o.join('');
  }
  var hitsOpen = [];
  function minesDetail(E) {
    var g = game(), pitch = g.w > 9 ? 24 : 41, bw = pitch * g.w, bh = pitch * g.h;
    var board = R(E.x + 18 + rte((384 - bw) / 2), E.y + rte((E.h - bh) / 2), bw, bh);
    var gap = pitch >= 32 ? 3 : 2, cs = pitch - gap, rad = Math.max(3, Math.round(cs * 0.18)), open = mix(K.card, BLACK, 0.3);
    var raised = mix(L.tile, L.text, 0.07), ns = Math.max(SMALL, Math.round(cs * 0.58)), lost = g.state === 3;
    hits.push({ id: 'board', x: board.x, y: board.y, w: board.w, h: board.h, board: { g: g, pitch: pitch, x: board.x, y: board.y } });
    for (var i = 0; i < g.cells(); i++) {
      var c = R(board.x + i % g.w * pitch + gap / 2, board.y + Math.floor(i / g.w) * pitch + gap / 2, cs, cs), cx = c.x + cs / 2, cy = c.y + cs / 2;
      var isOpen = g.st[i] === 1, flag = g.st[i] === 2, mine = lost && g.mine[i] && !flag, down = pressCell === i && !isOpen && !flag && !flagMode;
      if (isOpen || mine) {
        var hitHere = i === g.hitAt;
        rect(c.x, c.y, cs, cs, hitHere ? BAD : open, rad);
        if (mine || hitHere) mineIcon(cx, cy, cs, hitHere ? DARK : L.badInk);
        else if (g.adj[i] > 0) text(String(g.adj[i]), cx, cy + midK(true) * ns, mineNum(g.adj[i], open), ns, 700, true, 'middle');
        continue;
      }
      if (down) rect(c.x, c.y, cs, cs, open, rad);
      else if (hoverCell === i && !g.over()) rect(c.x, c.y, cs, cs, L.tileHover, rad);
      else rect(c.x, c.y, cs, cs, grad(c, raised, L.tile, 90, 1), rad);
      var fc = R(c.x + cs * 0.1, c.y + cs * 0.1, cs * 0.8, cs * 0.8);
      if (pressCell === i && holdProg > 0) {
        var a = flag ? 1 - holdProg : holdProg;
        flagIcon(fc, alpha(L.text, a), alpha(L.accent, a));
        arc(cx, cy, cs * 0.44, 0, holdProg, L.accent, Math.max(2.5, cs * 0.08));
        continue;
      }
      if (!flag) continue;
      flagIcon(fc, L.text, L.accent);
      if (lost && !g.mine[i]) { var k = cs * 0.24; line(c.x + k, c.y + k, c.x + cs - k, c.y + cs - k, L.badInk, Math.max(2, cs * 0.08), true); line(c.x + k, c.y + cs - k, c.x + cs - k, c.y + k, L.badInk, Math.max(2, cs * 0.08), true); }
    }
    var x = E.x + 18 + 384 + 20, w = E.x + E.w - 18 - x, ts = Math.max(SMALL, fitSize('Minesweeper', w - 58, 24, 600));
    textMid(ell('Minesweeper', w - 58, ts, 600), x, E.y + 36, L.text, ts, 600);
    var cy0 = E.y + 78, half = (w - 10) / 2, left = String(g.count - g.flags), time = minesTime(g);
    capText('MINES', x, cy0 + 15, L.sub);
    capText('TIME', x + half + 10, cy0 + 15, L.sub);
    text(left, x, cy0 + 54, g.count - g.flags < 0 ? L.warnInk : L.text, Math.max(SMALL, fitSize(left, half, 34, 700, true)), 700, true);
    text(time, x + half + 10, cy0 + 54, L.text, Math.max(SMALL, fitSize(time, half, 34, 700, true)), 700, true);
    var fr = R(x, E.y + 150, 60, 60);
    press('mnface', fr, null, null, false, 24, true, function () { game().reset(); });
    smiley(fr.x + 30, fr.y + 30, 60 * 0.34, g.state, pressCell >= 0 && !g.over(), faceColor(g, L.text), 2.6);
    var tx = fr.x + 74, tw = x + w - tx, st = ['Ready', 'Playing', 'Cleared!', 'Boom!'][g.state];
    text(ell(st, tw, 20, 600), tx, fr.y + 26, g.state === 2 ? L.ok : g.state === 3 ? L.badInk : L.text, 20, 600);
    var bt = best[level9] > 0 ? 'Best ' + Math.floor(best[level9] / 60) + ':' + two(best[level9] % 60) : 'No best yet';
    if (g.state === 2 && best[level9] > 0 && best[level9] === Math.max(1, Math.floor(g.seconds()))) bt = 'New best!';
    text(ell(bt, tw, SMALL, 400), tx, fr.y + 50, L.sub, SMALL, 400);
    press('mnflag', R(x, E.y + 230, w, 52), 'flag', 'Flag mode', flagMode, 20, false, function () { flagMode = !flagMode; });
    var sy = E.y + 298, bwid = (w - 10) / 2;
    capText('BOARD', x, sy + 15, L.sub);
    press('mnl0', R(x, sy + 26, bwid, 44), null, '9 × 9', level9 === 0, 24, false, function () { level9 = 0; });
    press('mnl1', R(x + bwid + 10, sy + 26, bwid, 44), null, '16 × 16', level9 === 1, 24, false, function () { level9 = 1; });
    text(ell(HOLD, w, SMALL, 400), x, E.y + 396, L.sub, SMALL, 400);
  }
  var HOLD = 'Hold to place a flag';
  function mineNum(n, under) { var c = hueOf(n), to = lum(under) < 0.18 ? PURE : BLACK; for (var i = 0; i < 16 && contrast(c, under) < 4.5; i++) c = mix(c, to, 0.08); return c; }
  function flagIcon(c, pole, flag) {
    var s = c.w, px = c.x + s * 0.38, top = c.y + s * 0.18, bot = c.y + s * 0.8, lw = Math.max(1.6, s * 0.07);
    path('M' + n2(px) + ' ' + n2(top) + 'L' + n2(px + s * 0.4) + ' ' + n2(top + s * 0.16) + 'L' + n2(px) + ' ' + n2(top + s * 0.32) + 'Z', flag);
    line(px, top, px, bot, pole, lw, true);
    rect(px - s * 0.17, bot - lw * 0.5, s * 0.34, lw, pole, lw / 2);
  }
  function mineIcon(cx, cy, s, body) {
    var r = s * 0.22, sp = s * 0.34, d = sp * 0.72, lw = Math.max(1.5, s * 0.07);
    line(cx - sp, cy, cx + sp, cy, body, lw, true); line(cx, cy - sp, cx, cy + sp, body, lw, true);
    line(cx - d, cy - d, cx + d, cy + d, body, lw, true); line(cx - d, cy + d, cx + d, cy - d, body, lw, true);
    circle(cx, cy, r, body);
    circle(cx - r * 0.35, cy - r * 0.35, r * 0.3, mix(body, PURE, 0.55));
  }
  function roomDetail(E, now) {
    var tw = E.w - 24 - 86, ts = Math.max(19, fitSize(WORLD, tw, 24, 600));
    textMid(ell(WORLD, tw, ts, 600), E.x + 24, E.y + 36, L.text, ts, 600);
    var by = E.y + 82, x = E.x + 24;
    function seg(t, x0, c, wt) { text(t, x0, by, c, 19, wt); return x0 + mw(t, 19, wt); }
    function dot(x0) { return seg('·', x0 + 9, L.sub, 400) + 9; }
    x = seg(PEOPLE.length + ' people', x, L.text, 600);
    x = seg('3 friends', dot(x), L.ok, 600);
    seg('here for 40 min', dot(x), L.sub, 400);
    var order = PEOPLE.map(function (p, i) { return { name: p, ago: 3 * i + 1, friend: !!FRIENDS[p] }; });
    order.sort(function (a, b) { return (b.friend - a.friend) || (a.ago - b.ago); });
    var Lr = R(E.x + 24, E.y + 104, E.w - 48, 5 * 52), rw = Lr.w - 14, cw = (rw - 16) / 2;
    for (var i = 0; i < Math.min(10, order.length); i++) {
      var p = order[i], rr = R(Lr.x + i % 2 * (cw + 16), Lr.y + Math.floor(i / 2) * 52, cw, 52), cx = rr.x + 21, cy = rr.y + 26;
      halo = K.card;
      avatar(cx, cy, 19, p.name, p.friend);
      var nx = cx + 31, nw = rr.x + rr.w - nx;
      text(ell(p.name, nw, 19, 600), nx, rr.y + 23, L.text, 19, 600);
      var mb = rr.y + 44, mx = nx;
      if (p.friend) { text('friend', mx, mb, L.ok, SMALL, 600); mx += mw('friend', SMALL, 600) + 7; text('·', mx, mb, L.sub, SMALL, 400); mx += mw('·', SMALL, 400) + 7; }
      text(p.ago < 1 ? 'just now' : p.ago + ' min ago', mx, mb, L.sub, SMALL, 400);
    }
    var content = 6 * 52, bh = Lr.h * Lr.h / content;
    rect(Lr.x + Lr.w - 4, Lr.y, 4, bh, alpha(L.text, 0.3), 2);
    var fb = E.y + E.h - 22, more = '+' + (order.length - 10) + ' more · the stick scrolls';
    text(more, Lr.x + Lr.w, fb, L.sub, SMALL, 600, false, 'end');
    text('VRCX: 120 friends', Lr.x, fb, L.sub, SMALL, 400);
  }
  var KNOBS = [['Volume', '42%', 'Mute', 0.42], ['Bright', '30%', 'Set', 0.3], ['Light', '', 'Toggle', -1]];
  function dockDetail(E) {
    textMid('Hexpad', E.x + 24, E.y + 36, L.text, 24, 600);
    var hx = E.x + 24 + mw('Hexpad', 24, 600) + 14, chip = 'Home · 1/3', cw = mw(chip, SMALL, 600) + 28;
    rect(hx, E.y + 20, cw, 32, L.tile, 16);
    textMid(chip, hx + 14, E.y + 36, L.sub, SMALL, 600);
    var x0 = E.x + 24, y0 = E.y + 76, T = 104, G = 10, i;
    for (i = 0; i < 6; i++) {
      var kr = R(x0 + i % 3 * (T + G), y0 + Math.floor(i / 3) * (T + G), T, T), down = pressKey === i;
      if (down) kr = inflate(kr, -4, -4);
      hits.push({ id: 'key' + i, x: kr.x, y: kr.y, w: kr.w, h: kr.h, act: null, key: i });
      keyFace(kr.x, kr.y, kr.w, 14, i);
      if (hov === 'key' + i) strokeRect(kr.x - 3, kr.y - 3, kr.w + 6, kr.h + 6, alpha(L.accent, 0.85), 3, 17);
    }
    var by = y0 + 2 * T + G + 30, labels = ['Page: prev', 'Page: goto', 'Page: next'];
    for (i = 0; i < 3; i++) {
      var cx = x0 + i * (T + G) + T / 2;
      circle(cx, by, 22, L.tile);
      text(labels[i], cx, by + 46, L.sub, SMALL, 600, false, 'middle');
    }
    var kx = x0 + 3 * T + 2 * G + 20, kw = E.x + E.w - 24 - kx;
    for (i = 0; i < 3; i++) {
      var k = KNOBS[i], card = R(kx, y0 + i * (92 + 14), kw, 92);
      rect(card.x, card.y, card.w, card.h, alpha(L.text, 0.06), 16);
      var bw = clamp(mw(k[2], 15, 600) + 30, 64, 100), b = R(card.x + card.w - 10 - bw, card.y + 10, bw, 34);
      rect(b.x, b.y, b.w, b.h, L.tile, 17);
      textMid(k[2], b.x + b.w / 2, b.y + 17, L.text, 15, 600, false, 'middle');
      textMid(k[0], card.x + 14, card.y + 27, L.text, 18, 600);
      if (k[1]) textMid(k[1], card.x + 14 + mw(k[0], 18, 600) + 8, card.y + 27, L.sub, 16, 600);
      var cy = card.y + 66, tr = R(card.x + 24, cy - 6, card.w - 48, 12);
      rect(tr.x, tr.y, tr.w, tr.h, L.track, 6);
      if (k[3] >= 0) {
        var kxp = tr.x + tr.w * k[3];
        rect(tr.x, tr.y, Math.max(12, kxp - tr.x + 6), tr.h, L.accent, 6);
        circle(kxp, cy, 12, L.text);
      } else {
        var mid = tr.x + tr.w / 2;
        for (var d = 24; d < tr.w / 2 - 6; d += 24) { circle(mid - d, cy, 2, alpha(L.text, 0.3)); circle(mid + d, cy, 2, alpha(L.text, 0.3)); }
        circle(mid, cy, 12, L.text);
      }
    }
  }
  function clockDetail(c, now) {
    var a = R(c.x - 8, c.y - 8, c.h + 16, c.h + 16), cx = a.x + a.w / 2, cy = a.y + a.h / 2, rad = Math.min(a.w, a.h) / 2 - 12, rim = rad * 0.94, i;
    ring(cx, cy, rim, L.track, Math.max(2, rad * 0.02));
    for (i = 0; i < 12; i++) {
      var an = i / 12 * Math.PI * 2, q = i % 3 === 0, dr = rad * 0.76;
      circle(cx + dr * Math.sin(an), cy - dr * Math.cos(an), q ? Math.max(3, rad * 0.05) : Math.max(2, rad * 0.026), q ? L.text : L.sub);
    }
    var sec = now.getSeconds(), m = now.getMinutes() + sec / 60, h = now.getHours() % 12 + m / 60;
    function hand(t, len, wd) { var an = t * Math.PI * 2; line(cx, cy, cx + len * Math.sin(an), cy - len * Math.cos(an), L.text, wd, true); }
    hand(h / 12, rad * 0.46, Math.max(5, rad * 0.075));
    hand(m / 60, rad * 0.66, Math.max(3.5, rad * 0.048));
    var sa = sec / 60 * Math.PI * 2;
    circle(cx + rim * Math.sin(sa), cy - rim * Math.cos(sa), Math.max(5, rad * 0.075), K.card);
    circle(cx + rim * Math.sin(sa), cy - rim * Math.cos(sa), Math.max(3.5, rad * 0.05), L.accent);
    circle(cx, cy, Math.max(4, rad * 0.06), L.text);
    circle(cx, cy, Math.max(2, rad * 0.03), L.accent);
    var x = c.x + c.h + 30, w = c.x + c.w - x, top = c.y + Math.max(0, (c.h - 230) / 2), t = hhmm(now) + ':' + two(now.getSeconds());
    var size = fitSize(t, w, 66, 600, true);
    textMid(t, x, top + 42, L.text, size, 600, true);
    var dl = dateForms(now)[0], ds = Math.max(SMALL, fitSize(dl, w, 22, 600));
    textMid(ell(dl, w, ds, 600), x, top + 108, L.text, ds, 600);
    var start = new Date(now.getFullYear(), 0, 1), day = Math.floor((now - start) / 86400000) + 1;
    textMid(ell('Week ' + isoWeek(now) + ' · day ' + day + ' of ' + (now.getFullYear() % 4 ? 365 : 366), w, SMALL, 400), x, top + 139, L.sub, SMALL, 400);
    capText('IN VR', x, top + 192, L.sub);
    var mins = vrMinutes(now);
    textMid(ell(Math.floor(mins / 60) + ':' + two(mins % 60) + '  ·  since ' + hhmm(S.vrSince), w, 22, 600), x, top + 214, L.text, 22, 600);
  }
  function isoWeek(d) { var t = new Date(Date.UTC(d.getFullYear(), d.getMonth(), d.getDate())), dn = t.getUTCDay() || 7; t.setUTCDate(t.getUTCDate() + 4 - dn); var y0 = new Date(Date.UTC(t.getUTCFullYear(), 0, 1)); return Math.ceil(((t - y0) / 86400000 + 1) / 7); }
  function batteryDetail(c) {
    var n = S.bats.length, chh = Math.min(96, c.h / n), y0 = c.y + (c.h - n * chh) / 2, ts = clamp(chh * 0.36, SMALL, 34);
    for (var i = 0; i < n; i++) {
      var b = S.bats[i], cell = R(c.x, y0 + i * chh, c.w, chh), shown = shownPct(b[1]), lv = batLevel(shown), pct = shown + '%';
      var lt = clamp(cell.h * 0.08, 4, 8), th = ts * 1.3, gap = Math.max(4, ts * 0.2), y = cell.y + (cell.h - th - gap - lt) / 2, ns = Math.max(CAPS, Math.round(ts * 0.7));
      var x = cell.x + 10, iw = cell.w - 20;
      if (b[2]) { icon('bolt', x + 7, y + th / 2, ns, L.text); x += ns + 2; }
      textMid(b[0].toUpperCase(), x, y + th / 2, L.sub, ns, 600);
      textMid(pct, cell.x + 10 + iw, y + th / 2, level(lv, L.text), ts, 600, true, 'end');
      meter(cell.x + 10, y + th + gap, iw, lt, b[1], level(lv, L.ok));
    }
  }
  function volumeDetail(c) {
    var left = R(c.x, c.y, c.h + 16, c.h), v = S.muted ? 'Muted' : Math.round(S.vol * 100) + '%';
    textMid(v, left.x + left.w / 2, left.y + left.h * 0.12 + left.h * 0.21, S.muted ? BAD : L.text, fitSize(v, left.w - 48, 96, 600, true), 600, true, 'middle');
    var bar = R(left.x + 28, left.y + left.h * 0.6, left.w - 56, 14);
    hits.push({ id: 'vslider', x: bar.x - 8, y: bar.y - 20, w: bar.w + 16, h: bar.h + 40, slider: bar });
    rect(bar.x, bar.y, bar.w, bar.h, L.track, 7);
    var kx = bar.x + bar.w * S.vol;
    rect(bar.x, bar.y, Math.max(14, kx - bar.x), bar.h, S.muted ? L.faint : L.accent, 7);
    circle(kx, bar.y + 7, 21, K.card);
    circle(kx, bar.y + 7, 14, S.muted ? L.faint : L.accent);
    textMid('drag to set', left.x + left.w / 2, left.y + left.h * 0.6 + 45, L.sub, SMALL, 400, false, 'middle');
    var x = c.x + c.h + 36, w = c.x + c.w - x;
    capText('SYSTEM VOLUME', x, c.y + 29, L.sub);
    press('dvm', R(x, c.y + 46, 64, 64), 'minus', null, false, 22, true, function () { setVol(S.vol - 0.05); });
    press('dvp', R(x + 78, c.y + 46, 64, 64), 'plus', null, false, 22, true, function () { setVol(S.vol + 0.05); });
    press('dvx', R(x, c.y + c.h - 64, Math.min(w, 220), 56), 'speaker', S.muted ? 'Unmute' : 'Mute', S.muted, 22, false, function () { S.muted = !S.muted; }, BAD);
  }
  function mediaDetail(c) {
    var s = Math.min(c.h, c.w * 0.44), cv = R(c.x, c.y + (c.h - s) / 2, s, s);
    rect(cv.x, cv.y, s, s, L.tile, 20);
    coverImage(cv, 20);
    var x = cv.x + s + 30, w = c.x + c.w - x;
    textMid('Midnight City', x, c.y + 27, L.text, 30, 600);
    textMid('M83', x, c.y + 66, L.sub, 21, 400);
    var gap = 14, big = Math.min(84, (w - 2 * gap) * 0.4), sm = Math.min(64, (w - 2 * gap - big) / 2), by = c.y + 116;
    press('dm0', R(x, by + (big - sm) / 2, sm, sm), 'prev', null, false, 24, true, function () { S.pos = 0; });
    press('dm1', R(x + sm + gap, by, big, big), S.playing ? 'pause' : 'play', null, true, 32, true, togglePlay);
    press('dm2', R(x + sm + gap + big + gap, by + (big - sm) / 2, sm, sm), 'next', null, false, 24, true, function () { S.pos = 0; });
    var vy = c.y + c.h - 56, bar = R(x + 72, vy + 19, w - 94, 14);
    press('dmv', R(x, vy, 52, 52), 'speaker', null, S.muted, 20, true, function () { S.muted = !S.muted; }, BAD);
    textMid(S.muted ? 'Muted' : 'Volume ' + Math.round(S.vol * 100) + '%', bar.x, vy - 13, S.muted ? BAD : L.sub, SMALL, 600);
    rect(bar.x, bar.y, bar.w, bar.h, L.track, 7);
    var kx = bar.x + bar.w * S.vol;
    rect(bar.x, bar.y, Math.max(14, kx - bar.x), 14, S.muted ? L.faint : L.accent, 7);
    circle(kx, bar.y + 7, 21, K.card);
    circle(kx, bar.y + 7, 14, S.muted ? L.faint : L.accent);
    hits.push({ id: 'vslider', x: bar.x - 8, y: bar.y - 20, w: bar.w + 16, h: bar.h + 40, slider: bar });
  }

  // ================= what the buttons do here (in VR they do the real thing) =================
  var TOAST = HU ? {
    keyboard: 'VR-ben ez előhozza a billentyűzetet', recenter: 'VR-ben ez eléd igazítja a paneleket', settings: 'VR-ben ez megnyitja a beállításokat',
    theater: 'VR-ben ez mozi módba vált', status: 'VR-ben ez a chatboxba írja, mit hallgatsz', desktops: 'VR-ben ez elrejti vagy előhozza a monitorokat',
    layout: 'VR-ben ez átrendezi a paneleket'
  } : {
    keyboard: 'In VR this brings up the keyboard', recenter: 'In VR this puts your panels back in front of you', settings: 'In VR this opens the settings',
    theater: 'In VR this switches to theater mode', status: 'In VR this shows what you play in your chatbox', desktops: 'In VR this hides or shows your monitors',
    layout: 'In VR this moves your panels to that layout'
  };
  function act(id) {
    if (id === 'night') { S.night = !S.night; looks = {}; }
    else if (id === 'desktops') S.desktops = !S.desktops;
    else if (id === 'keyboard') S.keyboard = !S.keyboard;
    else if (id === 'status') S.status = !S.status;
    else if (id === 'theater') S.theater = !S.theater;
    else if (id === 'layout') S.layout = (S.layout + 1) % LAYOUTS.length;
    if (TOAST[id] && !(isToggle(id) && !isOn(id))) toast(TOAST[id]);
    drawAll();
  }

  // ================= the DOM =================
  var svg, track, pageEls = [], dotsEl, openEl, toastEl, canvas95, pageHits = [[], [], []];
  root.innerHTML = '';
  root.classList.add('wd-live');
  svg = document.createElementNS(NS, 'svg');
  svg.setAttribute('viewBox', '0 0 ' + TW + ' ' + TH);
  svg.setAttribute('class', 'wd');
  svg.setAttribute('role', 'img');
  svg.setAttribute('tabindex', '0');
  svg.setAttribute('aria-label', HU ? 'Az OrbitOverlay órája, kipróbálható másolat: három oldal, húzd el vagy használd a nyilakat.' :
    'A working copy of the OrbitOverlay watch: three pages, swipe or use the arrow keys.');
  track = document.createElementNS(NS, 'g');
  track.setAttribute('class', 'wd-track');
  svg.appendChild(track);
  for (var p = 0; p < PAGES.length; p++) {
    var pg = document.createElementNS(NS, 'g');
    pg.setAttribute('transform', 'translate(' + p * TW + ' 0)');
    track.appendChild(pg);
    var els = [];
    for (var wi = 0; wi < PAGES[p].list.length; wi++) {
      var we = document.createElementNS(NS, 'g'), ww = PAGES[p].list[wi], rr = cellRect(ww.x, ww.y, ww.w, ww.h);
      we.setAttribute('class', 'wd-w');
      we.style.setProperty('--d', ((ww.y * 6 + ww.x) * 18) + 'ms');
      we.style.transformOrigin = (rr.x + rr.w / 2) + 'px ' + (rr.y + rr.h / 2) + 'px';
      pg.appendChild(we);
      els.push(we);
    }
    pageEls.push(els);
  }
  dotsEl = document.createElementNS(NS, 'g');
  svg.appendChild(dotsEl);
  openEl = document.createElementNS(NS, 'g');
  svg.appendChild(openEl);
  root.appendChild(svg);
  toastEl = document.createElement('div');
  toastEl.className = 'wd-toast';
  toastEl.setAttribute('role', 'status');
  toastEl.hidden = true;
  root.appendChild(toastEl);
  canvas95 = document.createElement('canvas');
  canvas95.className = 'wd95';
  canvas95.width = 320; canvas95.height = 220;
  canvas95.setAttribute('role', 'img');
  canvas95.setAttribute('aria-label', svg.getAttribute('aria-label'));
  canvas95.hidden = true;
  root.appendChild(canvas95);

  var toastTimer = 0;
  function toast(msg) {
    toastEl.textContent = msg;
    toastEl.hidden = false;
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { toastEl.hidden = true; }, 2400);
  }

  // ---- drawing ----
  function drawWidget(pi, wi, now) {
    o = []; gid = gid > 1e6 ? 0 : gid;
    hits = [];
    widget(PAGES[pi].list[wi], pi, wi, now);
    pageEls[pi][wi].innerHTML = o.join('');
    pageHits[pi][wi] = hits;
  }
  function drawPage(pi, now) { for (var wi = 0; wi < PAGES[pi].list.length; wi++) drawWidget(pi, wi, now); }
  function drawDots() {
    o = [];
    L = K.D;
    var span = 2 * 18, x0 = TW / 2 - span / 2, cy = TH - 5;
    rect(x0 - 13, TH - 10, span + 26, 10, 0xA6000000, 5);
    for (var i = 0; i < 3; i++) circle(x0 + i * 18, cy, 3.5, alpha(GREY, 0.75));
    var ax = x0 + clamp(pagePos, 0, 2) * 18;
    rect(ax - 9, cy - 3.5, 18, 7, K.D.accent, 3.5);
    dotsEl.innerHTML = o.join('');
  }
  function drawDetail() { openEl.innerHTML = detailSvg(nowDate()); }
  function drawAll() {
    if (style95) { draw95(); return; }
    var now = nowDate();
    for (var pi = 0; pi < PAGES.length; pi++) drawPage(pi, now);
    drawDots();
    drawDetail();
  }
  var pagePos = 0;
  function setPos(pos, animate) {
    pagePos = pos;
    track.style.transition = animate && !reduce ? 'transform .42s cubic-bezier(.25,.9,.3,1.06)' : 'none';
    track.style.transform = 'translateX(' + (-pos * TW) + 'px)';
    drawDots();
  }
  function goPage(p, animate) {
    p = clamp(p, 0, PAGES.length - 1);
    if (S.open) { S.open = null; closing = false; openAnim = 0; drawDetail(); }
    S.page = p;
    setPos(p, animate);
    if (style95) draw95();
  }

  // ---- input: taps, the swipe, a held square in Minesweeper ----
  function toTex(e) {
    var b = (style95 ? canvas95 : svg).getBoundingClientRect();
    return { x: (e.clientX - b.left) / b.width * TW, y: (e.clientY - b.top) / b.height * TH };
  }
  function find(list, pt) {
    for (var i = list.length - 1; i >= 0; i--) {
      var h = list[i], x = h.x, y = h.y, w = h.w, hh = h.h;
      if (w < 40) { x -= (40 - w) / 2; w = 40; }
      if (hh < 40) { y -= (40 - hh) / 2; hh = 40; }
      if (pt.x >= x && pt.x < x + w && pt.y >= y && pt.y < y + hh) return h;
    }
    return null;
  }
  function targetAt(pt) {
    if (style95) return find(pageHits95, pt);
    if (S.open) return find(hitsOpen, pt);
    if (pt.y > TH - 16 && Math.abs(pt.x - TW / 2) < 50) return { id: 'dots', act: function () { var at = Math.round((pt.x - (TW / 2 - 18)) / 18); goPage(at >= 0 && at < 3 && Math.abs(pt.x - (TW / 2 - 18 + at * 18)) <= 6 ? at : S.page + (pt.x < TW / 2 ? -1 : 1), true); } };
    var list = pageHits[S.page], best = null;
    for (var wi = 0; wi < list.length; wi++) { var h = find(list[wi] || [], pt); if (h) best = h; }
    return best;
  }
  var down = null, pressCell = -1, hoverCell = -1, holdProg = 0, holdTimer = 0, pressKey = -1, dragX = 0;
  function onDown(e) {
    if (e.button > 0 && e.pointerType === 'mouse' && e.button !== 2) return;
    var pt = toTex(e);
    down = { x: pt.x, y: pt.y, cx: e.clientX, cy: e.clientY, t: targetAt(pt), swipe: false, id: e.pointerId, right: e.button === 2 };
    if (down.t && down.t.board) startHold(down.t, pt);
    if (down.t && (down.t.slider || down.t.track)) setSlider(down.t, pt);
    if (down.t && down.t.key !== undefined) { pressKey = down.t.key; drawDetail(); }
    held = down.t && down.t.id;
    if (held && !S.open) redrawPage(); else if (held) redrawOpen();
    try { (style95 ? canvas95 : svg).setPointerCapture(e.pointerId); } catch (x) {}
  }
  function onMove(e) {
    var pt = toTex(e);
    if (down && down.id === e.pointerId) {
      var dx = e.clientX - down.cx, dy = e.clientY - down.cy, k = TW / (style95 ? canvas95 : svg).getBoundingClientRect().width;
      if (down.t && (down.t.slider || down.t.track)) { setSlider(down.t, pt); return; }
      if (!down.swipe && !S.open && Math.abs(dx) * k > 16 && Math.abs(dx) > Math.abs(dy) * 1.3) { down.swipe = true; cancelHold(); held = null; redrawPage(); }
      if (down.swipe && !style95) {
        var pos = S.page - dx * k / TW;
        if (pos < 0) pos *= 0.3; else if (pos > 2) pos = 2 + (pos - 2) * 0.3;
        setPos(pos, false);
        return;
      }
      if (down.t && down.t.board) moveHold(down.t, pt);
      return;
    }
    if (e.pointerType !== 'mouse') return;
    var t = targetAt(pt), id = t ? t.id : null, cell = -1;
    if (t && t.board) { cell = cellAt(t, pt); }
    if (!style95 && (id !== hov || cell !== hoverCell)) { hov = id; hoverCell = cell; if (S.open) drawDetail(); else redrawPage(); }
    (style95 ? canvas95 : svg).style.cursor = t && (t.act || t.board || t.slider || t.key !== undefined) ? 'pointer' : '';
  }
  function onUp(e) {
    if (!down || down.id !== e.pointerId) return;
    var d = down, pt = toTex(e);
    down = null; held = null;
    if (d.swipe) {
      var k = TW / (style95 ? canvas95 : svg).getBoundingClientRect().width, dx = (e.clientX - d.cx) * k, to = S.page;
      if (Math.abs(dx) > TW * 0.2) to = clamp(S.page + (dx < 0 ? 1 : -1), 0, 2);
      goPage(to, true);
      redrawPage();
      return;
    }
    if (d.t && d.t.board) { endHold(d.t, pt, d.right); return; }
    if (d.t && d.t.key !== undefined) { pressKey = -1; drawDetail(); return; }
    var t = targetAt(pt);
    if (d.t && t && t.id === d.t.id && d.t.act) d.t.act();
    if (S.open) redrawOpen(); else redrawPage();
  }
  function redrawPage() { if (style95) { draw95(); return; } drawPage(S.page, nowDate()); }
  function setSlider(t, pt) {
    var b = t.slider || t.track;
    S.vol = Math.round(clamp((pt.x - b.x) / b.w, 0, 1) * 100) / 100;
    if (style95) draw95(); else drawDetail();
  }
  function cellAt(t, pt) {
    var b = t.board, cx = Math.floor((pt.x - b.x) / b.pitch), cy = Math.floor((pt.y - b.y) / b.pitch);
    return cx >= 0 && cy >= 0 && cx < b.g.w && cy < b.g.h ? cy * b.g.w + cx : -1;
  }
  function startHold(t, pt) {
    var g = t.board.g, i = cellAt(t, pt);
    pressCell = i; holdProg = 0;
    if (i < 0 || g.over() || g.st[i] === 1) return;
    var t0 = Date.now();
    clearInterval(holdTimer);
    holdTimer = setInterval(function () {
      var el = (Date.now() - t0) / 1000;
      holdProg = clamp((el - 0.1) / 0.3, 0, 1);
      if (el >= 0.4) { clearInterval(holdTimer); g.flag(pressCell); pressCell = -2; holdProg = 0; }
      redrawOpen();
    }, 30);
    redrawOpen();
  }
  function moveHold(t, pt) { if (pressCell >= 0 && cellAt(t, pt) !== pressCell) { cancelHold(); redrawOpen(); } }
  function cancelHold() { clearInterval(holdTimer); pressCell = -1; holdProg = 0; }
  function endHold(t, pt, right) {
    clearInterval(holdTimer);
    var g = t.board.g, i = cellAt(t, pt), was = pressCell;
    pressCell = -1; holdProg = 0;
    if (was >= 0 && i === was) {
      if (right || (flagMode && g.st[i] !== 1)) g.flag(i); else g.click(i);
      if (g.state === 2) {
        var s = Math.max(1, Math.floor(g.seconds())), lv = g === games[1] ? 1 : 0;
        if (!best[lv] || s < best[lv]) { best[lv] = s; try { localStorage.setItem('orbit-mines-best', JSON.stringify(best)); } catch (e) {} }
      }
    }
    redrawOpen();
  }
  [svg, canvas95].forEach(function (el) {
    el.addEventListener('pointerdown', onDown);
    el.addEventListener('pointermove', onMove);
    el.addEventListener('pointerup', onUp);
    el.addEventListener('pointercancel', function () { down = null; held = null; cancelHold(); goPage(S.page, true); });
    el.addEventListener('pointerleave', function (e) { if (e.pointerType === 'mouse' && !down) { hov = null; hoverCell = -1; if (S.open) drawDetail(); else redrawPage(); } });
    el.addEventListener('contextmenu', function (e) { if (S.open && S.open.kind === 'mines') e.preventDefault(); });
    el.addEventListener('keydown', function (e) {
      if (e.key === 'Escape' && S.open) { closeDetail(); e.preventDefault(); }
      else if (e.key === 'ArrowRight' && !S.open) { goPage(S.page + 1, true); e.preventDefault(); }
      else if (e.key === 'ArrowLeft' && !S.open) { goPage(S.page - 1, true); e.preventDefault(); }
    });
  });

  // ================= the made-up data, moving =================
  function fakeCover() {
    var n = 96, cv = document.createElement('canvas');
    cv.width = cv.height = n;
    var ctx = cv.getContext('2d'), img = ctx.createImageData(n, n), px = [];
    for (var y = 0; y < n; y++) for (var x = 0; x < n; x++) {
      var t = (x + y) / (2 * (n - 1)), c = t < 0.5 ? mix(0xFF2B1B6B, 0xFFD9467D, t * 2) : mix(0xFFD9467D, 0xFFF59E42, (t - 0.5) * 2);
      var dx = x - n * 0.62, dy = y - n * 0.66;
      if (dx * dx + dy * dy < n * n * 0.04) c = mix(c, 0xFFFFE6B0, 0.85);
      if (y > n * 0.78) c = mix(c, 0xFF120A2A, 0.7);
      px.push(c);
      var k = (y * n + x) * 4;
      img.data[k] = ch(c, 16); img.data[k + 1] = ch(c, 8); img.data[k + 2] = ch(c, 0); img.data[k + 3] = 255;
    }
    ctx.putImageData(img, 0, 0);
    return { url: cv.toDataURL('image/png'), pal: artPalette(px) };
  }
  function deep(c) { c = (c | 0xFF000000) >>> 0; for (var i = 0; i < 20 && luma(c) > 0.42; i++) c = mix(c, BLACK, 0.1); return c; }
  // three colours of the cover (MediaCover's ArtPalette): its most common colours, distinct, lightest first, made dark enough
  function artPalette(px) {
    var count = new Array(4096).fill(0), sum = new Array(4096 * 3).fill(0), b, i;
    for (i = 0; i < px.length; i++) {
      var r = ch(px[i], 16), g = ch(px[i], 8), bb = ch(px[i], 0), bin = (r >> 4) << 8 | (g >> 4) << 4 | (bb >> 4);
      count[bin]++; sum[bin * 3] += r; sum[bin * 3 + 1] += g; sum[bin * 3 + 2] += bb;
    }
    var picked = [];
    for (var round = 0; round < 2 && picked.length < 3; round++)
      for (var k = 0; k < 64 && picked.length < 3; k++) {
        var bestB = -1;
        for (b = 0; b < 4096; b++) {
          if (!count[b] || picked.indexOf(b) >= 0) continue;
          var r0 = b >> 8, g0 = (b >> 4) & 15, b0 = b & 15;
          if (round === 0 && Math.max(r0, g0, b0) - Math.min(r0, g0, b0) < 3) continue;
          var far = true;
          for (var q = 0; q < picked.length; q++) { var pq = picked[q]; if (Math.abs((pq >> 8) - r0) + Math.abs(((pq >> 4) & 15) - g0) + Math.abs((pq & 15) - b0) < 5) { far = false; break; } }
          if (far && (bestB < 0 || count[b] > count[bestB])) bestB = b;
        }
        if (bestB < 0) break;
        picked.push(bestB);
      }
    var cols = picked.map(function (bin) { var nn = Math.max(1, count[bin]); return argb(255, Math.floor(sum[bin * 3] / nn), Math.floor(sum[bin * 3 + 1] / nn), Math.floor(sum[bin * 3 + 2] / nn)); });
    if (!cols.length) cols.push(0xFF3A3F55);
    cols.sort(function (a, c) { return luma(c) - luma(a); });
    while (cols.length < 3) cols.push(mix(cols[cols.length - 1], BLACK, 0.3));
    return cols.slice(0, 3).map(deep);
  }
  // the last 30 s of frames in tenths of a second (PagesData's: a gentle wave, one spike with a dropped frame at its top)
  var fseed = 5;
  function rnd() { fseed = (fseed * 16807) % 2147483647; return (fseed - 1) / 2147483646; }
  function bucket(i) {
    var bump = i >= 236 && i <= 246 ? 9.5 * (1 - Math.abs(i - 241) / 5.5) : 0, worst = 0;
    for (var f = 0; f < 9; f++) worst = Math.max(worst, 7.6 + 0.9 * Math.sin(i / 11) + rnd() * 0.8 + bump);
    return [worst, i === 241 ? 2 : worst > 1000 / 90 ? 1 : 0];
  }
  for (var bi = 0; bi < 300; bi++) S.frames.push(bucket(bi));
  var fnext = 300, spikeAt = 300 + 160;
  for (var ci = 0; ci < 30; ci++) S.cpu.push(0.1 + 0.05 * Math.sin(ci / 2.3) + 0.03 * Math.sin(ci * 1.7) + (ci % 23 === 5 ? 0.12 : 0));
  S.cpu[29] = 0.14;

  var pageHits95 = [], draw95;
  // ================= the Windows 95 face (the site's 1990s mode) =================
  // The app's 95 face at its real pixel size, 320 x 220 (doubled on screen without smoothing): little 95 windows, MS Sans Serif,
  // the app's own pixel icons (W95Icons.cs) and the real 1995 Minesweeper's pictures (MineArt in Mines.cs). Text is drawn
  // with the browser's font and then cut to whole pixels, the way the app's aliased GDI text looks.
  var style95 = false;
  var P95W = 320, P95H = 220, F95 = "'Microsoft Sans Serif','MS Sans Serif',Tahoma,Arial,sans-serif";
  var ctx95 = canvas95.getContext('2d'), img95 = ctx95.createImageData(P95W, P95H), buf = new Uint32Array(img95.data.buffer);
  var tcv = document.createElement('canvas');
  tcv.width = P95W; tcv.height = 64;
  var tctx = tcv.getContext("2d", { willReadFrequently: true });   // read back for every 95 text: keep it on the CPU
  var FACE = 0xFFC0C0C0, LT = 0xFFDFDFDF, HI = 0xFFFFFFFF, SH = 0xFF808080, BK = 0xFF000000, NAVY = 0xFF000080, TEAL = 0xFF008080;
  var PAL = { k: 0xFF000000, w: 0xFFFFFFFF, s: 0xFFC0C0C0, g: 0xFF808080, n: 0xFF000080, b: 0xFF0000FF, t: 0xFF008080, c: 0xFF00FFFF, r: 0xFFFF0000,
    m: 0xFF800000, y: 0xFFFFFF00, o: 0xFF808000, e: 0xFF008000, l: 0xFF00FF00, p: 0xFF800080, f: 0xFFFF00FF };
  var PIX = {
    Clock: ["................", "....kkkkkkkk....", "...kkwwwkwwkk...", "..kkwgwwwwgwkk..", ".kkwwwwwkwwwwkk.", ".kwgwwwwkwwwgwk.", ".kwwwwwwkwwwwwk.", ".kwwwwwmkwwwwwk.", ".kkwwwwwnnwwwkk.", ".kwwwwwwwnnwwwk.", ".kwgwwwwwwnwgwk.", ".kkwwwwwwwwwwkk.", "..kkwgwwwwgwkk..", "...kkwwwkwwkk...", "....kkkkkkkk....", "................"],
    Moon: ["................", "................", "....kk..........", "...kyyk.........", "..kyyk..........", ".kyyyk..........", ".kyyyk..........", ".kyyyk..........", ".kyyyyk.........", ".kyyyykk........", ".kyyyyyykkkk....", "..kyyyyyyyyyk...", "...kyyyyyyyk....", "....kkyyykk.....", "......kkk.......", "................"],
    Gear: [".......kk.......", "......kggk......", "...kk.kggk.kk...", "..kggkkkkkkggk..", "..kgksssssskgk..", "...ksswwggssk...", ".kkkswgkkggskkk.", "kggksgkwwkgskggk", "kggksgkwwkgskggk", ".kkksggkkggskkk.", "...kssggggssk...", "..kgksssssskgk..", "..kggkkkkkkggk..", "...kk.kggk.kk...", "......kggk......", ".......kk......."],
    Center: ["........k.......", "........k.......", ".......nk.......", ".....nn.knn.....", "....n...k..n....", "...n....k...n...", "...n........n...", "..n....rr....n..", "kkkkkk.rr..kkkkk", "...n........n...", "...n........n...", "....n...k..n....", ".....nn.knn.....", ".......nk.......", "........k.......", "........k......."],
    Computer: ["................", "..kkkkkkkkkkkk..", "..kssssssssssk..", "..kskkkkkkkksk..", "..ksktttttcksk..", "..kskttttttksk..", "..kskttttttksk..", "..kskttttttksk..", "..kskkkkkkkksk..", "..kssssssssesk..", "..kkkkkkkkkkkk..", ".....kssssk.....", "...kkkkkkkkkk...", "...kssssssssk...", "...kkkkkkkkkk...", "................"],
    ComputerOff: ["................", "..kkkkkkkkkkkk..", "..kssssssssssk..", "..kskkkkkkkksk..", "..kskkkkkkkksk..", "..kskkkkkkkksk..", "..kskkkkkkkksk..", "..kskkkkkkkksk..", "..kskkkkkkkksk..", "..kssssssssesk..", "..kkkkkkkkkkkk..", ".....kssssk.....", "...kkkkkkkkkk...", "...kssssssssk...", "...kkkkkkkkkk...", "................"],
    Note: ["................", "......nnnnnnnn..", "......nnnnnnnn..", "......n......n..", "......n......n..", "......n......n..", "......n......n..", "......n......n..", "......n......n..", "......n......n..", "...nnnn...nnnn..", "..nnnnn..nnnnn..", "..nnnnn..nnnnn..", "...nnn....nnn...", "................", "................"],
    Layouts: ["................", "kkkkkkkkkk......", "knnnnnnnnk......", "kkkkkkkkkk......", "kwwwwwwwwk......", "kwwwwkkkkkkkkkkk", "kwwwwknnnnnnnnnk", "kwwwwkkkkkkkkkkk", "kwwwwkwwwwwwwwwk", "kkkkkkwwwwwwwwwk", ".....kwwwwwwwwwk", ".....kwwwwwwwwwk", ".....kwwwwwwwwwk", ".....kkkkkkkkkkk", "................", "................"],
    Keyboard: ["................", "................", "................", "kkkkkkkkkkkkkkkk", "kwwwwwwwwwwwwwsk", "kwkskskskskskgsk", "kwssssssssssssgk", "kwskskskskskssgk", "kwssssssssssssgk", "kwsskskskskskggk", "kwssssssssssssgk", "kwssskkkkkkksssk", "kwsssssssssssssk", "kggggggggggggggk", "kkkkkkkkkkkkkkkk", "................"],
    Dock: ["................", "................", "................", "kkkkkkkkkkkkkkkk", "kggggggggggggggk", "kgbbgrrgllggkkgk", "kgbbgrrgllgkwwkk", "kggggggggggkwwkk", "kgyygppgccggkkgk", "kgyygppgccggggk.", "kggggggggggkkgkk", "kgkkgkkgkkgkwkwk", "kggggggggggkkgkk", "kkkkkkkkkkkkkkkk", "................", "................"],
    People: ["................", "................", ".....kkk........", "....kyyyk.kkk...", "....kyyyk.kyyk..", "....kyyyk.kyyyk.", ".....kkk..kyyyk.", "...kkbbbkk.kkk..", "..kbbbbbbbk.kkk.", "..kbbbbbbbkkrrrk", "..kbbbbbbbkrrrrk", "..kbbbbbbbkrrrrk", "..kkkkkkkkkkkkkk", "................", "................", "................"],
    Chat: ["................", "..kkkkkkkkkkkk..", ".kwwwwwwwwwwwwk.", ".kwkkkkwkkkkwwk.", ".kwwwwwwwwwwwwk.", ".kwkkkwkkkkkwwk.", ".kwwwwwwwwwwwwk.", ".kwkkkkkwwwwwwk.", ".kwwwwwwwwwwwwk.", "..kkkwwkkkkkkk..", "....kwk.........", "....kk..........", "....k...........", "................", "................", "................"],
    Theater: ["................", "kkkkkkkkkkkkkkkk", "krrmkkkkkkkkmrrk", "krrmkcccccckmrrk", "krrmkccccyckmrrk", "krrmkcccccckmrrk", "krrmkcceccckmrrk", "krrmkceeecckmrrk", "krrmkeeeeeekmrrk", "krrmkkkkkkkkmrrk", "krrm........mrrk", "krrrm......mrrrk", "kkkkk......kkkkk", "................", "................", "................"],
    Mine16: ["................", "................", "........k.......", "........k.......", "....k.kkkkk.k...", ".....kkkkkkk....", "....kkwwkkkkk...", "....kkwwkkkkk...", "..kkkkkkkkkkkkk.", "....kkkkkkkkk...", "....kkkkkkkkk...", ".....kkkkkkk....", "....k.kkkkk.k...", "........k.......", "........k.......", "................"],
    Flag16: ["................", "................", "................", "......rrr.......", "....rrrrr.......", "...rrrrrr.......", "....rrrrr.......", "......rrr.......", "........k.......", "........k.......", "......kkkkk.....", "....kkkkkkkkk...", "....kkkkkkkkk...", "................", "................", "................"],
    FaceDisc: ["......kkkkk......", "....kkyyyyykk....", "...kyyyyyyyyyk...", "..kyyyyyyyyyyyk..", ".kyyyyyyyyyyyyyk.", ".kyyyyyyyyyyyyyk.", "kyyyyyyyyyyyyyyyk", "kyyyyyyyyyyyyyyyk", "kyyyyyyyyyyyyyyyk", "kyyyyyyyyyyyyyyyk", "kyyyyyyyyyyyyyyyk", ".kyyyyyyyyyyyyyk.", ".kyyyyyyyyyyyyyk.", "..kyyyyyyyyyyyk..", "...kyyyyyyyyyk...", "....kkyyyyykk....", "......kkkkk......"]
  };
  function abgr(c) { return ((c & 0xFF00FF00) | ((c >>> 16) & 0xFF) | ((c & 0xFF) << 16)) >>> 0; }
  function px(x, y, c) { x |= 0; y |= 0; if (x >= 0 && y >= 0 && x < P95W && y < P95H) buf[y * P95W + x] = abgr(c); }
  function fill(x, y, w, h, c) {
    x = Math.round(x); y = Math.round(y); w = Math.round(w); h = Math.round(h);
    var a = abgr(c), x0 = Math.max(0, x), x1 = Math.min(P95W, x + w), y1 = Math.min(P95H, y + h);
    for (var yy = Math.max(0, y); yy < y1; yy++) buf.fill(a, yy * P95W + x0, yy * P95W + Math.max(x0, x1));
  }
  function frame(x, y, w, h, tl, br) { fill(x, y, w, 1, tl); fill(x, y, 1, h, tl); fill(x, y + h - 1, w, 1, br); fill(x + w - 1, y, 1, h, br); }
  // the two-pixel 3D edges: raised (a button), window, pressed, sunken
  function bevel(x, y, w, h, e) {
    var s = { raised: [HI, BK, LT, SH], window: [LT, BK, HI, SH], pressed: [BK, HI, SH, LT], sunken: [SH, HI, BK, LT] }[e];
    frame(x, y, w, h, s[0], s[1]);
    frame(x + 1, y + 1, w - 2, h - 2, s[2], s[3]);
  }
  function checker(x, y, w, h) { for (var yy = y; yy < y + h; yy++) for (var xx = x; xx < x + w; xx++) px(xx, yy, (xx + yy) % 2 ? FACE : HI); }
  function stamp(map, x, y, only) {
    for (var r = 0; r < map.length; r++) for (var c = 0; c < map[r].length; c++) { var k = map[r].charAt(c); if (k !== '.') px(x + c, y + r, only || PAL[k]); }
  }
  function w95(s, bold, size) { tctx.font = (bold ? 'bold ' : '') + (size || 11) + 'px ' + F95; return Math.ceil(tctx.measureText(s).width); }
  function fit95(s, maxW, bold, size) {
    if (w95(s, bold, size) <= maxW + 4) return s;          // the browser's metrics run a little wider than GDI's
    var n = s.length;
    while (n > 0 && w95(s.slice(0, n).replace(/\s+$/, '') + '...', bold, size) > maxW) n--;
    return s.slice(0, n).replace(/\s+$/, '') + '...';
  }
  // text in a box, centred on its height (align: 0 left, 1 centre, 2 right), each of its pixels either on or off
  function t95(s, x, y, w, h, c, bold, align, size) {
    if (!s) return;
    size = size || 11;
    var tw = w95(s, bold, size), tx = align === 1 ? x + Math.round((w - tw) / 2) : align === 2 ? x + w - tw : x;
    var asc = Math.ceil(size * 1.05), base = Math.round(y + h / 2 + size * 0.36), top = base - asc, hh = Math.ceil(size * 1.4), cw = Math.min(P95W, tw + 2);
    tctx.clearRect(0, 0, P95W, 64);
    tctx.font = (bold ? 'bold ' : '') + size + 'px ' + F95;
    tctx.fillStyle = '#fff';
    tctx.fillText(s, 0, asc);
    var d = tctx.getImageData(0, 0, cw, hh).data, cut = bold ? 165 : 150;
    for (var yy = 0; yy < hh; yy++) for (var xx = 0; xx < cw; xx++) if (d[(yy * cw + xx) * 4 + 3] > cut) px(tx + xx, top + yy, c);
  }
  function cell95(x, y, w, h) {
    var m = 4, g = 4, mb = 4 + 22, cw = (P95W - 2 * m - 5 * g) / 6, chh = (P95H - m - mb - 3 * g) / 4;
    var x0 = rte(m + x * (cw + g)), y0 = rte(m + y * (chh + g)), x1 = rte(m + (x + w) * (cw + g) - g), y1 = rte(m + (y + h) * (chh + g) - g);
    return R(x0, y0, x1 - x0, y1 - y0);
  }
  var hits95 = [];
  function hit95(id, r, actf, extra) {
    var h = { id: id, x: r.x * 2, y: r.y * 2, w: r.w * 2, h: r.h * 2, act: actf };
    if (extra) for (var k in extra) h[k] = extra[k];
    hits95.push(h);
  }
  function button95(id, r, label, latched, icon, actf, bold) {
    if (actf) hit95(id, r, actf);
    var down = held === id, b = r;
    if (down) { frame(r.x, r.y, r.w, r.h, BK, BK); b = R(r.x + 1, r.y + 1, r.w - 2, r.h - 2); }
    if (latched && !down) checker(b.x, b.y, b.w, b.h); else fill(b.x, b.y, b.w, b.h, FACE);
    if (down) frame(b.x, b.y, b.w, b.h, SH, SH); else bevel(b.x, b.y, b.w, b.h, latched ? 'pressed' : 'raised');
    var o = down || latched ? 1 : 0;
    if (icon) {
      var n = 16, tw = label ? w95(label) + 4 : 0, ix = Math.round(b.x + (b.w - n - tw) / 2 + o), iy = Math.round(b.y + (b.h - n) / 2 + o);
      stamp(PIX[icon], ix, iy);
      if (label) t95(label, ix + n + 4, b.y + o, tw, b.h, BK, false, 0);
    } else if (label) t95(label, b.x + o, b.y + o, b.w, b.h, BK, bold, 1);
  }
  function mirror(r) { return r.split('').reverse().join(''); }
  var GL = { play: ['k...', 'kk..', 'kkk.', 'kkkk', 'kkk.', 'kk..', 'k...'], pause: ['kk.kk', 'kk.kk', 'kk.kk', 'kk.kk', 'kk.kk', 'kk.kk', 'kk.kk'] };
  GL.prev = GL.play.map(function (r) { return 'k' + mirror(r) + mirror(r); });
  GL.next = GL.play.map(function (r) { return r + r + 'k'; });
  GL.check = ['......k', '.....kk', 'k...kkk', 'kk.kkk.', 'kkkkk..', '.kkk...', '..k....'];
  GL.close = ['kk....kk', '.kk..kk.', '..kkkk..', '...kk...', '..kkkk..', '.kk..kk.', 'kk....kk'];
  function glyphButton95(id, r, g, actf) {
    hit95(id, r, actf);
    var down = held === id;
    fill(r.x, r.y, r.w, r.h, FACE);
    if (down) { frame(r.x, r.y, r.w, r.h, BK, BK); frame(r.x + 1, r.y + 1, r.w - 2, r.h - 2, SH, SH); } else bevel(r.x, r.y, r.w, r.h, 'raised');
    var o = down ? 1 : 0;
    stamp(GL[g], r.x + Math.floor((r.w - GL[g][0].length) / 2) + o, r.y + Math.floor((r.h - GL[g].length) / 2) + o, BK);
  }
  function progress95(x, y, w, h, frac, block) {
    fill(x, y, w, h, FACE); fill(x, y, w, 1, SH); fill(x, y, 1, h, SH); fill(x, y + h - 1, w, 1, HI); fill(x + w - 1, y, 1, h, HI);
    var ix = x + 2, iw = w - 4, ih = h - 4, bw = Math.max(4, Math.round(ih * 0.66)), n = Math.floor((iw + 2) / (bw + 2)), on = Math.round(clamp(frac, 0, 1) * n);
    if (on === 0 && frac >= 0.01) on = 1;
    var x0 = ix + Math.floor((iw + 2 - n * (bw + 2)) / 2);
    for (var i = 0; i < on; i++) fill(x0 + i * (bw + 2), y + 2, bw, ih, block || NAVY);
  }
  function value95(c, v, cap, color) {
    var vy = Math.floor(c.y + (c.h - 28) / 2);
    t95(fit95(v, c.w - 2, true, 13), c.x, vy, c.w, 15, color || BK, true, 1, 13);
    t95(fit95(cap, c.w - 2), c.x, vy + 15, c.w, 13, BK, false, 1);
  }
  function longDate(now) {
    var d = DAY[now.getDay()], m = MON[now.getMonth()], n = now.getDate();
    return HU ? [now.getFullYear() + '. ' + m + ' ' + n + '., ' + d, d + ', ' + m + ' ' + n + '.', m + ' ' + n + '.']
      : [d + ', ' + m + ' ' + n + ', ' + now.getFullYear(), d + ', ' + m + ' ' + n, m + ' ' + n];
  }
  function time95(c, now, tall) {
    var t = now.getHours() + ':' + two(now.getMinutes()) + ':' + two(now.getSeconds()), withDate = tall && c.h >= 44;
    var size = Math.max(11, Math.floor(Math.min(c.h * (withDate ? 0.62 : 0.82), 100 * (c.w - 8) / w95(t, false, 100))));
    var th = Math.ceil(size * 1.2), tr = withDate ? R(c.x, c.y, c.w, c.h - 15) : c;
    if (withDate && c.h - th - 15 > 12) tr = R(c.x, Math.floor(c.y + (c.h - th - 15) / 2), c.w, th);
    t95(t, tr.x, tr.y, tr.w, tr.h, BK, false, 1, size);
    if (!withDate) return;
    var forms = longDate(now), ds = forms[forms.length - 1];
    for (var i = 0; i < forms.length; i++) if (w95(forms[i]) <= c.w - 2) { ds = forms[i]; break; }
    t95(ds, c.x, Math.min(c.y + c.h - 15, tr.y + tr.h + 1), c.w, 13, BK, false, 1);
  }
  function line95(x0, y0, x1, y1, c) {
    x0 = Math.round(x0); y0 = Math.round(y0); x1 = Math.round(x1); y1 = Math.round(y1);
    var dx = Math.abs(x1 - x0), dy = -Math.abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
    for (var k = 0; k < 400; k++) {
      px(x0, y0, c);
      if (x0 === x1 && y0 === y1) break;
      var e2 = 2 * err;
      if (e2 >= dy) { err += dy; x0 += sx; }
      if (e2 <= dx) { err += dx; y0 += sy; }
    }
  }
  // the teal hands of 1995's clock: a kite, lit from the top left
  function hand95(cx, cy, frac, len, half) {
    var a = frac * Math.PI * 2 - Math.PI / 2, dx = Math.cos(a), dy = Math.sin(a), qx = -dy, qy = dx, ox = cx + 0.5, oy = cy + 0.5;
    var pts = [[ox - dx * len * 0.14, oy - dy * len * 0.14], [ox + dx * len * 0.24 + qx * half, oy + dy * len * 0.24 + qy * half], [ox + dx * len, oy + dy * len],
      [ox + dx * len * 0.24 - qx * half, oy + dy * len * 0.24 - qy * half]];
    var xs = pts.map(function (p) { return p[0]; }), ys = pts.map(function (p) { return p[1]; });
    for (var y = Math.floor(Math.min.apply(null, ys)); y <= Math.ceil(Math.max.apply(null, ys)); y++)
      for (var x = Math.floor(Math.min.apply(null, xs)); x <= Math.ceil(Math.max.apply(null, xs)); x++) {
        var inside = false;
        for (var i = 0, j = 3; i < 4; j = i++) {
          var xi = pts[i][0], yi = pts[i][1], xj = pts[j][0], yj = pts[j][1];
          if ((yi > y + 0.5) !== (yj > y + 0.5) && x + 0.5 < (xj - xi) * (y + 0.5 - yi) / (yj - yi) + xi) inside = !inside;
        }
        if (inside) px(x, y, TEAL);
      }
    for (var e = 0; e < 4; e++) {
      var p0 = pts[e], p1 = pts[(e + 1) % 4], ex = p1[0] - p0[0], ey = p1[1] - p0[1];
      line95(p0[0], p0[1], p1[0], p1[1], ey * 1 - ex * 1 < 0 ? HI : 0xFF004040);
    }
  }
  function dial95(now, cx, cy, r) {
    for (var i = 0; i < 60; i++) {
      var a = i / 60 * Math.PI * 2 - Math.PI / 2, x = Math.round(cx + r * Math.cos(a)), y = Math.round(cy + r * Math.sin(a));
      if (i % 5 === 0) {
        fill(x - 1, y - 1, 3, 3, TEAL); fill(x - 1, y - 1, 2, 1, 0xFF00FFFF); px(x - 1, y, 0xFF00FFFF);
        fill(x + 1, y - 1, 1, 3, 0xFF004040); fill(x - 1, y + 1, 2, 1, 0xFF004040);
      } else px(x, y, SH);
    }
    var s = now.getSeconds(), m = now.getMinutes() + s / 60, h = now.getHours() % 12 + m / 60;
    hand95(cx, cy, h / 12, r * 0.55, 4);
    hand95(cx, cy, m / 60, r * 0.82, 3);
    var sa = s / 60 * Math.PI * 2 - Math.PI / 2;
    line95(cx, cy, cx + r * 0.86 * Math.cos(sa), cy + r * 0.86 * Math.sin(sa), BK);
    fill(cx - 1, cy - 1, 3, 3, BK);
  }
  function track95(x, y, w, frac) {
    var chx = x + 6, chy = y + 8, chw = w - 12, i;
    fill(chx, chy, chw, 4, HI);
    bevel(chx, chy, chw, 4, 'sunken');
    var tx = Math.round(chx + chw * frac) - 5, ty = y;
    fill(tx, ty, 11, 15, FACE);
    for (i = 0; i < 5; i++) fill(tx + i + 1, ty + 15 + i, 9 - 2 * i, 1, FACE);
    fill(tx, ty, 10, 1, HI); fill(tx, ty, 1, 15, HI); fill(tx + 10, ty, 1, 15, BK); fill(tx + 9, ty + 1, 1, 14, SH);
    for (i = 0; i < 5; i++) { px(tx + i, ty + 15 + i, HI); px(tx + 10 - i, ty + 15 + i, BK); if (i < 4) px(tx + 9 - i, ty + 15 + i, SH); }
    px(tx + 5, ty + 20, BK);
    for (i = 0; i < 6; i++) fill(Math.round(chx + (chw - 1) * i / 5), y + 24, 1, i === 0 || i === 5 ? 4 : 3, BK);
  }
  function check95(x, y, label, on) {
    fill(x, y, 13, 13, HI);
    bevel(x, y, 13, 13, 'sunken');
    if (on) stamp(GL.check, x + 3, y + 3, BK);
    t95(label, x + 19, y - 1, 200, 15, BK);
  }
  // the dock's keys at their own pixels (the made-up faces: a colour, a white dot)
  function dock95(c) {
    var a = R(c.x + 3, c.y + 3, c.w - 6, c.h - 6), gap = 2, cell = Math.floor(Math.min((a.w - 2 * gap) / 3, (a.h - gap) / 2));
    var x0 = Math.floor(a.x + (a.w - (3 * cell + 2 * gap)) / 2), y0 = Math.floor(a.y + (a.h - (2 * cell + gap)) / 2);
    for (var i = 0; i < 6; i++) {
      var kx = x0 + i % 3 * (cell + gap), ky = y0 + Math.floor(i / 3) * (cell + gap), d = Math.max(2, Math.round(cell * 0.4));
      fill(kx, ky, cell, cell, KEYCOL[i]);
      fill(kx + Math.round((cell - d) / 2), ky + Math.round((cell - d) / 2), d, d, HI);
    }
  }
  var LIT = ['abcdef', 'bc', 'abged', 'abgcd', 'fgbc', 'afgcd', 'afgedc', 'abc', 'abcdefg', 'abcdfg'];
  function inSeg(sg, x, y) {
    switch (sg) {
      case 'a': return y >= 1 && y <= 3 && x >= y + 1 && x <= 11 - y;
      case 'g': return (y === 11 && x >= 2 && x <= 10) || ((y === 10 || y === 12) && x >= 3 && x <= 9);
      case 'd': return y >= 19 && y <= 21 && x >= 23 - y && x <= y - 11;
      case 'f': return x >= 1 && x <= 3 && y >= x + 1 && y <= 11 - x;
      case 'b': return x >= 9 && x <= 11 && y >= 13 - x && y <= x - 1;
      case 'e': return x >= 1 && x <= 3 && y >= x + 11 && y <= 21 - x;
      case 'c': return x >= 9 && x <= 11 && y >= 23 - x && y <= x + 9;
    }
    return false;
  }
  function ledDigit(chr, x0, y0) {
    var lit = chr === '-' ? 'g' : chr >= '0' && chr <= '9' ? LIT[+chr] : '';
    fill(x0, y0, 13, 23, BK);
    'abcdefg'.split('').forEach(function (sg) {
      for (var y = 0; y < 23; y++) for (var x = 0; x < 13; x++) if (inSeg(sg, x, y)) px(x0 + x, y0 + y, lit.indexOf(sg) >= 0 ? 0xFFFF0000 : 0xFF800000);
    });
  }
  function led95(x, y, value) {
    frame(x, y, 41, 25, SH, HI);
    var s = value < 0 ? '-' + two(Math.min(99, -value)) : ('00' + Math.min(999, value)).slice(-3);
    for (var k = 0; k < 3; k++) ledDigit(s.charAt(k), x + 1 + 13 * k, y + 1);
  }
  var EYES = [[5, 5], [6, 5], [5, 6], [6, 6], [10, 5], [11, 5], [10, 6], [11, 6]], SMILE = [[4, 10], [12, 10], [5, 11], [11, 11], [6, 12], [7, 12], [8, 12], [9, 12], [10, 12]],
    OHM = [[7, 9], [8, 9], [9, 9], [6, 10], [10, 10], [6, 11], [10, 11], [7, 12], [8, 12], [9, 12]],
    XEYES = [[4, 4], [6, 4], [5, 5], [4, 6], [6, 6], [10, 4], [12, 4], [11, 5], [10, 6], [12, 6]],
    FROWN = [[6, 11], [7, 11], [8, 11], [9, 11], [10, 11], [5, 12], [11, 12], [4, 13], [12, 13]],
    SHADES = [[2, 5], [3, 5], [4, 5], [5, 5], [6, 5], [7, 5], [8, 5], [9, 5], [10, 5], [11, 5], [12, 5], [13, 5], [14, 5], [3, 6], [4, 6], [5, 6], [6, 6], [7, 6],
      [9, 6], [10, 6], [11, 6], [12, 6], [13, 6], [4, 7], [5, 7], [6, 7], [10, 7], [11, 7], [12, 7]];
  function face95(x, y, f) {
    stamp(PIX.FaceDisc, x, y);
    var sets = f === 'oh' ? [EYES, OHM] : f === 'dead' ? [XEYES, FROWN] : f === 'cool' ? [SHADES, SMILE] : [EYES, SMILE];
    sets.forEach(function (st) { st.forEach(function (p) { px(x + p[0], y + p[1], BK); }); });
  }
  function faceOf(g) { return g.state === 2 ? 'cool' : g.state === 3 ? 'dead' : pressCell >= 0 && !g.over() ? 'oh' : 'smile'; }
  var ICON95 = { keyboard: 'Keyboard', recenter: 'Center', night: 'Moon', settings: 'Gear', status: 'Chat', theater: 'Theater', layout: 'Layouts' };
  function icon95(id) { return id === 'desktops' ? (S.desktops ? 'Computer' : 'ComputerOff') : ICON95[id]; }

  function widget95(w, pi, wi, now) {
    var r = cell95(w.x, w.y, w.w, w.h), t = w.type, k;
    if (BTN[t]) {
      if (t === 'layout') button95('b' + pi + '.' + wi, r, LAYOUTS[S.layout], false, null, function () { act(t); });
      else button95('b' + pi + '.' + wi, r, w.w >= 2 ? BTN[t][1] : null, isOn(t), icon95(t), function () { act(t); });
      return;
    }
    fill(r.x, r.y, r.w, r.h, FACE);
    bevel(r.x, r.y, r.w, r.h, 'window');
    var c = R(r.x + 3, r.y + 3, r.w - 6, r.h - 6);
    if (w.h >= 2) {
      fill(c.x, c.y, c.w, 18, NAVY);
      t95(fit95({ time: 'Clock', graph: 'Frame Graph' }[t] || t, c.w - 6, true), c.x + 3, c.y, c.w - 4, 18, HI, true, 0);
      c = R(c.x + 1, c.y + 19, c.w - 2, c.h - 20);
    }
    if (t === 'minesweeper') hit95('w' + pi + '.' + wi, r, function () { openDetail('mines', w); });
    switch (t) {
      case 'time': time95(c, now, w.h >= 2); break;
      case 'analog': dial95(now, Math.floor(c.x + c.w / 2), Math.floor(c.y + c.h / 2), Math.floor(Math.min(c.w, c.h) / 2) - 5); break;
      case 'dock': dock95(c); break;
      case 'volume':
        track95(c.x + 2, c.y, c.w - 4, S.vol);
        hit95('vtrack' + pi, R(c.x + 2, c.y, c.w - 4, 26), null, { track: { x: (c.x + 8) * 2, w: (c.w - 16) * 2 } });
        check95(c.x + 4, c.y + c.h - 16, 'Mute', S.muted);
        hit95('vmute' + pi, R(c.x + 4, c.y + c.h - 18, 50, 17), function () { S.muted = !S.muted; });
        t95(Math.round(S.vol * 100) + '%', c.x + c.w - 36, c.y + c.h - 17, 32, 13, BK, false, 2);
        break;
      case 'battery':
        var rows = Math.min(4, Math.floor(c.h / 13)), pick = S.bats.map(function (b, i) { return i; });
        if (pick.length > rows) pick = pick.slice().sort(function (a, b) { return S.bats[a][1] - S.bats[b][1]; }).slice(0, rows).sort(function (a, b) { return a - b; });
        var y0 = Math.floor(c.y + (c.h - pick.length * 13) / 2);
        pick.forEach(function (bi, i) {
          var b = S.bats[bi], y = y0 + i * 13, low = b[1] <= 0.2 && !b[2];
          t95(b[0], c.x + 2, y, 34, 12, BK);
          progress95(c.x + 36, y + 1, c.w - 36 - 32, 10, b[1], low ? 0xFF800000 : NAVY);
          t95(Math.round(b[1] * 100) + '%', c.x + c.w - 32, y, 30, 12, low ? 0xFF800000 : BK, true, 2);
        });
        break;
      case 'room':
        t95(fit95(PEOPLE.length + ' people', c.w - 8, true), c.x + 4, c.y + c.h / 2 - 14, c.w - 8, 13, BK, true);
        t95(fit95('3 friends: Mira, Tobi +1', c.w - 8), c.x + 4, c.y + c.h / 2 + 1, c.w - 8, 13, 0xFF008000);
        break;
      case 'media':
        var lcd = R(c.x, c.y + 1, c.w - 1, 18);
        fill(lcd.x, lcd.y, lcd.w, lcd.h, BK);
        bevel(lcd.x, lcd.y, lcd.w, lcd.h, 'sunken');
        t95(fit95('Midnight City - M83', lcd.w - 8), lcd.x + 4, lcd.y + 2, lcd.w - 6, 14, 0xFF00FF00);
        var nb = lcd.w >= 86 ? 3 : 1, bx = Math.floor(lcd.x + (lcd.w - (nb * 28 - 2)) / 2), byy = lcd.y + lcd.h + 3, gl = ['prev', S.playing ? 'pause' : 'play', 'next'];
        for (k = 0; k < nb; k++) glyphButton95('mb' + pi + '.' + k, R(bx + k * 28, byy, 26, 18), nb === 1 ? gl[1] : gl[k], k === 1 || nb === 1 ? togglePlay : function () { S.pos = 0; });
        break;
      case 'vram': value95(c, dec(S.vram), 'VRAM'); break;
      case 'ram': value95(c, dec(S.ram), 'RAM'); break;
      case 'fps': value95(c, String(S.fps), 'fps'); break;
      case 'gpu': value95(c, dec(S.gpu), 'GPU ms'); break;
      case 'reproj': value95(c, S.reproj + '%', 'reproj.'); break;
      case 'cpu': value95(c, Math.round(S.cpu[S.cpu.length - 1] * 100) + '%', 'CPU'); break;
      case 'session': var mm = vrMinutes(now); value95(c, Math.floor(mm / 60) + ':' + two(mm % 60), 'in VR'); break;
      case 'mic':
        hit95('mic' + pi, r, function () { S.mic = !S.mic; });
        value95(c, S.mic ? 'Muted' : 'Live', w95('microphone') <= c.w - 2 ? 'microphone' : 'mic', S.mic ? 0xFF800000 : BK);
        break;
      case 'notifications':
        var box = R(c.x + 1, c.y + 1, c.w - 2, c.h - 2);
        fill(box.x, box.y, box.w, box.h, HI);
        bevel(box.x, box.y, box.w, box.h, 'sunken');
        var inr = R(box.x + 4, box.y + 3, box.w - 8, box.h - 6), nrows = Math.max(1, Math.floor(inr.h / 13));
        for (var q = 0; q < Math.min(nrows, NOTES.length); q++) {
          var nt = NOTES[q], secs = nt[2] + (Date.now() - T0) / 1000, age = secs < 60 ? 'now' : Math.floor(secs / 60) + ' m', aw = w95(age) + 4;
          t95(fit95(nt[0], inr.w - aw - 4), inr.x, inr.y + q * 13, inr.w - aw, 13, BK);
          t95(age, inr.x + inr.w - aw, inr.y + q * 13, aw, 13, SH, false, 2);
        }
        break;
      case 'minesweeper':
        var g = games[0], yy = Math.floor(c.y + (c.h - 44) / 2);
        led95(Math.floor(c.x + (c.w - 41) / 2), yy, g.count - g.flags);
        face95(Math.floor(c.x + (c.w - 17) / 2), yy + 27, faceOf(g));
        break;
      case 'buttons':
        var ids = w.items, n = ids.length, a = R(c.x + 2, c.y + 2, c.w - 4, c.h - 4);
        var bw = Math.floor((a.w - (n - 1) * 2) / n), bh = Math.floor(Math.min(26, a.h)), by0 = Math.floor(a.y + (a.h - bh) / 2);
        ids.forEach(function (id, j) {
          button95('g' + pi + '.' + wi + '.' + j, R(a.x + j * (bw + 2), by0, bw, bh), bw >= 72 ? fit95(btnName(id), bw - 30) : null, isOn(id), icon95(id), function () { act(id); });
        });
        break;
      case 'graph': graph95(c); break;
    }
  }
  // the last frames on black: green bars, yellow over budget, red dropped; the budget a white line
  function graph95(r) {
    fill(r.x, r.y, r.w, r.h, BK);
    bevel(r.x, r.y, r.w, r.h, 'sunken');
    var inn = R(r.x + 2, r.y + 2, r.w - 4, r.h - 4), x, y;
    for (x = inn.x + 6; x < inn.x + inn.w; x += 12) fill(x, inn.y, 1, inn.h, 0xFF008000);
    for (y = inn.y + 5; y < inn.y + inn.h; y += 6) fill(inn.x, y, inn.w, 1, 0xFF008000);
    // the last 450 frames (the app's made-up ones: 7.5 to 10 ms, a slow one every 97th)
    var budget = 1000 / S.hz, top = budget * 2, n = 450, step = inn.w / n, off = Math.floor((Date.now() - T0) / 11.1);
    for (var i = 0; i < n; i++) {
      var k = i + off, slow = k % 97 === 0, ms = 7.5 + ((k * 7919) % 101) / 101 * 2.5 + (slow ? 6 : 0), bh = Math.max(1, Math.round(clamp(ms / top, 0.02, 1) * inn.h));
      fill(Math.floor(inn.x + inn.w - (n - i) * step), inn.y + inn.h - bh, Math.max(1, Math.ceil(step)), bh, slow || ms > budget ? 0xFFFFFF00 : 0xFF00FF00);
    }
    fill(inn.x, Math.round(inn.y + inn.h - inn.h * (budget / top)), inn.w, 1, HI);
    var label = S.fps + ' fps  ' + dec(S.gpu) + ' ms GPU  ' + S.reproj + '% reproj.';
    if (w95(label) > inn.w - 12) label = S.fps + ' fps  ' + dec(S.gpu) + ' ms  ' + S.reproj + '%';
    fill(inn.x + 2, inn.y + 1, Math.min(w95(label) + 4, inn.w - 4), 12, BK);
    t95(label, inn.x + 4, inn.y + 1, inn.w - 8, 12, 0xFF00FF00);
  }
  // the pages: a little taskbar along the bottom, the page in view pushed in
  function taskbar95() {
    var y = P95H - 22 - 2, bw = Math.floor(Math.min(104, (P95W - 6 - 2 * 3) / 3));
    fill(0, y, P95W, P95H - y, FACE); fill(0, y, P95W, 1, LT); fill(0, y + 1, P95W, 1, HI);
    PAGES.forEach(function (pg, i) { button95('tb' + i, R(3 + i * (bw + 3), y + 4, bw, P95H - y - 6), fit95(pg.name, bw - 10), i === S.page, null, function () { goPage(i); }); });
  }
  // the opened Minesweeper: winmine's window inside at its real metrics, its title bar and buttons in a narrow window beside it
  function mines95Detail() {
    var g = games[0], win = cell95(0, 0, 6, 4), cw = 24 + g.w * 16, chh = 67 + g.h * 16, cl = R(win.x, win.y + Math.floor((win.h - chh) / 2), cw, chh), k;
    fill(cl.x, cl.y, cw, chh, FACE);
    for (k = 0; k < 3; k++) frame(cl.x + k, cl.y + k, cw - 2 * k, chh - 2 * k, HI, SH);
    frame(cl.x + 9, cl.y + 9, cw - 18, 37, SH, HI);
    frame(cl.x + 10, cl.y + 10, cw - 20, 35, SH, HI);
    led95(cl.x + 16, cl.y + 15, g.count - g.flags);
    led95(cl.x + cw - 57, cl.y + 15, Math.min(999, Math.floor(g.seconds())));
    var fx = cl.x + (cw - 24) / 2, fy = cl.y + 16, faceDown = held === 'mn95face';
    frame(fx - 1, fy - 1, 26, 26, SH, SH);
    fill(fx, fy, 24, 24, FACE);
    if (faceDown) { fill(fx, fy, 24, 1, SH); fill(fx, fy, 1, 24, SH); }
    else { frame(fx, fy, 24, 24, HI, SH); frame(fx + 1, fy + 1, 22, 22, HI, SH); }
    face95(fx + (faceDown ? 4 : 3), fy + (faceDown ? 4 : 3), faceDown ? 'smile' : faceOf(g));
    hit95('mn95face', R(fx - 1, fy - 1, 26, 26), function () { g.reset(); });
    for (k = 0; k < 3; k++) frame(cl.x + 9 + k, cl.y + 52 + k, cw - 18 - 2 * k, g.h * 16 + 6 - 2 * k, SH, HI);
    var bx = cl.x + 12, by = cl.y + 55, lost = g.state === 3;
    hit95('board', R(bx, by, g.w * 16, g.h * 16), null, { board: { g: g, pitch: 32, x: bx * 2, y: by * 2 } });
    for (var i = 0; i < g.w * g.h; i++) {
      var x = bx + i % g.w * 16, y = by + Math.floor(i / g.w) * 16, open = g.st[i] === 1, flag = g.st[i] === 2, sq;
      if (lost && i === g.hitAt) sq = 'hit'; else if (lost && g.mine[i]) sq = flag ? 'flag' : 'mine'; else if (lost && flag) sq = 'wrong';
      else if (open) sq = g.adj[i] ? 'n' : 'open'; else if (flag) sq = 'flag'; else sq = pressCell === i && !flagMode ? 'open' : 'hidden';
      fill(x, y, 16, 16, FACE);
      if (sq === 'hidden' || sq === 'flag') {
        frame(x, y, 16, 16, HI, SH); frame(x + 1, y + 1, 14, 14, HI, SH);
        if (sq === 'flag') stamp(PIX.Flag16, x, y);
        if (pressCell === i && holdProg > 0) {
          var rows = Math.round((flag ? 1 - holdProg : holdProg) * 10);
          for (var rr = 13 - rows; rr < 13; rr++) for (var cc = 0; cc < 16; cc++) { var c2 = PIX.Flag16[rr].charAt(cc); if (c2 !== '.') px(x + cc, y + rr, PAL[c2]); }
        }
        continue;
      }
      if (sq === 'hit') fill(x + 1, y + 1, 15, 15, 0xFFFF0000);
      fill(x, y, 16, 1, SH); fill(x, y, 1, 16, SH);
      if (sq === 'n') { var nn = g.adj[i]; t95(String(nn), x, y, 16, 16, [0, 0xFF0000FF, 0xFF008000, 0xFFFF0000, 0xFF000080, 0xFF800000, 0xFF008080, BK, SH][nn], true, 1, 13); }
      if (sq === 'mine' || sq === 'hit' || sq === 'wrong') stamp(PIX.Mine16, x, y);
      if (sq === 'wrong') for (var q = 0; q <= 10; q++) { px(x + 3 + q, y + 3 + q, 0xFFFF0000); px(x + 4 + q, y + 3 + q, 0xFFFF0000); px(x + 13 - q, y + 3 + q, 0xFFFF0000); px(x + 12 - q, y + 3 + q, 0xFFFF0000); }
    }
    var sw = R(cl.x + cw, win.y, win.x + win.w - cl.x - cw, win.h);
    fill(sw.x, sw.y, sw.w, sw.h, FACE);
    bevel(sw.x, sw.y, sw.w, sw.h, 'window');
    fill(sw.x + 3, sw.y + 3, sw.w - 6, 18, NAVY);
    stamp(PIX.Mine16, sw.x + 5, sw.y + 4);
    t95('Minesweeper', sw.x + 24, sw.y + 3, sw.w - 50, 18, HI, true, 0);
    var cx0 = sw.x + sw.w - 5 - 16, cy0 = sw.y + 5;
    fill(cx0, cy0, 16, 14, FACE);
    bevel(cx0, cy0, 16, 14, held === 'mn95x' ? 'pressed' : 'raised');
    stamp(GL.close, cx0 + 4, cy0 + 3, BK);
    hit95('mn95x', R(cx0 - 6, cy0 - 6, 28, 26), closeDetail);
    var x0 = sw.x + 8, w = sw.w - 16;
    button95('mn95new', R(x0, sw.y + 30, w, 23), 'New game', false, null, function () { g.reset(); });
    button95('mn95flag', R(x0, sw.y + 59, w, 23), 'Flag mode', flagMode, null, function () { flagMode = !flagMode; });
    var lines = g.state === 2 ? ['You win!', 'Click the face to', 'play again.'] : g.state === 3 ? ['Game over.', 'Click the face to', 'play again.']
      : g.state === 0 ? ['Click a square to', 'start.'] : ['Hold a square to', 'flag it.'];
    lines.forEach(function (l, i2) { t95(l, x0, sw.y + 92 + i2 * 14, w, 15, BK, i2 === 0 && g.state >= 2); });
    t95(best[0] > 0 ? 'Best time: ' + best[0] + ' s' : 'Best time: none', x0, sw.y + sw.h - 23, w, 15, BK);
  }
  draw95 = function () {
    var now = nowDate();
    buf.fill(0);
    hits95 = [];
    var list = PAGES[S.page].list;
    for (var i = 0; i < list.length; i++) widget95(list[i], S.page, i, now);
    taskbar95();
    if (S.open && S.open.kind === 'mines') { hits95 = []; mines95Detail(); }
    else if (S.open) S.open = null;
    ctx95.putImageData(img95, 0, 0);
    pageHits95 = hits95;
  };

  // ================= start =================
  var startNow = nowDate();
  S.vrSince = new Date(startNow.getTime() - 83 * 60000 - (TEST ? 0 : startNow.getSeconds() * 1000));
  try { cover = fakeCover(); } catch (e) { cover = null; }
  derive(PANEL, ACCENT);
  if (TEST && TEST.page) S.page = TEST.page;
  if (TEST && TEST.open) setTimeout(function () { var w = PAGES[S.page].list.filter(function (x) { return x.type === TEST.open[1]; })[0]; openDetail(TEST.open[0], w); }, 0);
  if (TEST && TEST.night === false) S.night = false;
  style95 = html.getAttribute('data-style') === '95';
  svg.style.display = style95 ? 'none' : '';
  canvas95.hidden = !style95;
  drawAll();
  setPos(S.page, false);

  // the cascade when the watch comes into view (the app's: row by row, a little spring each)
  var cascade = !reduce;
  if (cascade && 'IntersectionObserver' in window) {
    root.classList.add('wd-wait');
    var started = false;
    var intro = function () {
      if (started) return;
      started = true;
      io.disconnect();
      root.classList.remove('wd-wait');
      root.classList.add('wd-intro');
      setTimeout(function () { root.classList.remove('wd-intro'); }, 1400);     // done; nothing left waiting on an animation
    };
    var io = new IntersectionObserver(function (es) { if (es[0].isIntersecting) intro(); }, { threshold: 0.35 });
    io.observe(root);
    // in view but no word from the observer (a throttled frame): show it anyway
    setTimeout(function () { var b = root.getBoundingClientRect(); if (!started && b.top < innerHeight && b.bottom > 0) intro(); }, 2500);
  }

  var lastSec = -1, inView = true;
  if ('IntersectionObserver' in window) new IntersectionObserver(function (es) { inView = es[0].isIntersecting; }).observe(root);
  function tick() {
    if (document.hidden || !inView) return;
    var now = nowDate(), t = Date.now();
    // 10 frame buckets a second; a spike now and then
    if (!TEST) {
      while (S.frameAt < t - 100) {
        S.frameAt = S.frameAt ? S.frameAt + 100 : t;
        var b = bucket(fnext % 200 + 20);
        if (fnext === spikeAt) { b = [17.2 + rnd() * 3, 2]; spikeAt = fnext + 120 + Math.floor(rnd() * 120); }
        else if (fnext > spikeAt - 6 && fnext < spikeAt) b[0] += (6 - (spikeAt - fnext)) * 1.4;
        S.frames.push(b); S.frames.shift();
        fnext++;
      }
    }
    var sec = now.getSeconds();
    if (sec !== lastSec) {
      lastSec = sec;
      if (!TEST) {
        if (S.playing) { S.pos += 1; if (S.pos > S.dur) S.pos = 0; }
        S.cpu.push(clamp(S.cpu[S.cpu.length - 1] + (rnd() - 0.5) * 0.05, 0.06, 0.32)); S.cpu.shift();
        S.micPeak = S.mic ? 0 : rnd() < 0.5 ? rnd() * 0.5 : 0.02;
      }
      if (style95) draw95();
      else {
        drawPage(S.page, now);
        if (S.open) drawDetail();
      }
    } else if (style95 && S.page === 2 && !S.open) draw95();
    else if (!style95 && S.page === 2 && !S.open) {
      var wi = 0;   // the frame graph moves ten times a second
      drawWidget(2, wi, now);
    }
  }
  if (!TEST || !TEST.still) setInterval(tick, 100);
  document.addEventListener('orbit-style', function () {
    style95 = html.getAttribute('data-style') === '95';
    svg.style.display = style95 ? 'none' : '';
    canvas95.hidden = !style95;
    drawAll();
    setPos(S.page, false);
  });

  // expose the 95 hooks to the code below
  window.__orbitWatch = { S: S, PAGES: PAGES, set95: function (f) { draw95 = f; if (style95) draw95(); }, canvas: canvas95, goPage: goPage,
    nowDate: nowDate, dec: dec, two: two, hhmm: hhmm, vrMinutes: vrMinutes, games: games, act: act, setVol: setVol, toast: toast, HU: HU,
    pageHits: pageHits, openDetail: openDetail };
})();
