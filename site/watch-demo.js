// A browser copy of the OrbitOverlay custom watch face (6 x 4 grid, Bento look) for the website.
// Live time, a moving frame graph, a working timer and stopwatch; tap a widget to open it big like in VR.
// The numbers that come from SteamVR in the app (batteries, fps, GPU...) are demo values here.
(function () {
  var root = document.getElementById('watch-demo');
  if (!root) return;
  var HU = (document.documentElement.lang || '').slice(0, 2) === 'hu';
  var LOC = HU ? 'hu-HU' : 'en-GB';
  var T = HU ? {
    // the watch itself is English in the app (only dates follow Windows), so only the page's own hints are Hungarian
    back: 'Back', page: 'oldal', inVr: 'IN VR', cpu: 'CPU', load: 'total load', ram: 'GB RAM', vram: 'VRAM',
    fps: 'fps', reproj: 'reproj.', gpu: 'ms GPU', batteries: 'Batteries', charging: 'charging', nowPlaying: 'Now playing',
    frame: 'Frame time', avg: 'average', worst: 'worst', target: 'target', timer: 'Timer', stopwatch: 'Stopwatch', lap: 'Lap',
    night: 'Éjszakai mód', layouts: ['Desk', 'Bed', 'Couch'],
    toast: { keyboard: 'VR-ben ez előhozza a billentyűzetet', recenter: 'VR-ben ez eléd igazítja a paneleket', theater: 'VR-ben ez mozi módba vált', settings: 'VR-ben ez megnyitja a beállításokat' },
    tracker: 'Tracker', hmd: 'Headset', left: 'Left controller', right: 'Right controller'
  } : {
    back: 'Back', page: 'page', inVr: 'IN VR', cpu: 'CPU', load: 'total load', ram: 'GB RAM', vram: 'VRAM',
    fps: 'fps', reproj: 'reproj.', gpu: 'ms GPU', batteries: 'Batteries', charging: 'charging', nowPlaying: 'Now playing',
    frame: 'Frame time', avg: 'average', worst: 'worst', target: 'target', timer: 'Timer', stopwatch: 'Stopwatch', lap: 'Lap',
    night: 'Night mode', layouts: ['Desk', 'Bed', 'Couch'],
    toast: { keyboard: 'In VR this brings up the keyboard', recenter: 'In VR this puts your panels back in front of you', theater: 'In VR this switches to theater mode', settings: 'In VR this opens the settings' },
    tracker: 'Tracker', hmd: 'Headset', left: 'Left controller', right: 'Right controller'
  };
  var H12 = !HU;
  var reduce = window.matchMedia && matchMedia('(prefers-reduced-motion: reduce)').matches;

  // ---------- state (demo values where the app reads SteamVR) ----------
  var S = {
    bat: [{ k: 'HMD', n: T.hmd, v: 71 }, { k: 'L', n: T.left, v: 82 }, { k: 'R', n: T.right, v: 18, c: true }, { k: 'Trk×6', n: T.tracker + ' ×6', v: 44 }],
    playing: true, song: 'Midnight City', artist: 'M83', vol: 30,
    frames: [], fps: 90, night: false, layout: 0,
    timerSet: 300, timerLeft: 300, timerRun: false, timerT: 0,
    swRun: false, swAcc: 0, swT: 0, laps: [],
    start: Date.now() - (83 * 60 * 1000), cpu: 10
  };
  for (var i = 0; i < 120; i++) S.frames.push(frameMs());
  function frameMs() { var r = Math.random(); return r > 0.97 ? 14 + Math.random() * 5 : 8.6 + Math.random() * 1.6; }

  // ---------- icons ----------
  function ic(d, s) { return '<svg viewBox="0 0 24 24" width="' + (s || 24) + '" height="' + (s || 24) + '" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' + d + '</svg>'; }
  var I = {
    play: '<path d="M8 5v14l11-7z" fill="currentColor" stroke="none"/>',
    pause: '<path d="M8 5v14M16 5v14" stroke-width="3"/>',
    prev: '<path d="M6 5v14M19 5l-10 7 10 7z"/>', next: '<path d="M18 5v14M5 5l10 7-10 7z"/>',
    note: '<path d="M9 18V5l11-2v13"/><circle cx="6" cy="18" r="3"/><circle cx="17" cy="16" r="3"/>',
    moon: '<path d="M20 14.5A8 8 0 0 1 9.5 4 8 8 0 1 0 20 14.5z"/>',
    kb: '<rect x="2" y="6" width="20" height="12" rx="2"/><path d="M6 10h1M10 10h1M14 10h1M18 10h0M7 14h10"/>',
    recenter: '<circle cx="12" cy="12" r="3"/><path d="M12 2v4M12 18v4M2 12h4M18 12h4"/>',
    theater: '<rect x="2" y="5" width="20" height="14" rx="2"/><path d="M10 9l5 3-5 3z"/>',
    gear: '<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z"/>',
    vol: '<path d="M11 5L6 9H2v6h4l5 4z"/><path d="M15.5 8.5a5 5 0 0 1 0 7M19 5a10 10 0 0 1 0 14"/>',
    minus: '<path d="M5 12h14"/>', plus: '<path d="M12 5v14M5 12h14"/>', reset: '<path d="M3 12a9 9 0 1 0 3-6.7L3 8"/><path d="M3 3v5h5"/>',
    bolt: '<path d="M13 2L4 14h7l-1 8 9-12h-7z" fill="currentColor" stroke="none"/>', flag: '<path d="M5 21V4M5 4h11l-2 4 2 4H5"/>'
  };

  // ---------- layout: two pages of the 6 x 4 grid ----------
  var PAGES = [
    [['analog', 0, 0, 2, 2], ['time', 2, 0, 2, 1], ['date', 2, 1, 2, 1], ['battery', 4, 0, 2, 2],
     ['media', 0, 2, 3, 1], ['graph', 3, 2, 3, 1],
     ['fps', 0, 3, 1, 1], ['reproj', 1, 3, 1, 1], ['gpu', 2, 3, 1, 1], ['vram', 3, 3, 1, 1], ['night', 4, 3, 1, 1], ['layout', 5, 3, 1, 1]],
    [['calendar', 0, 0, 2, 2], ['timer', 2, 0, 2, 2], ['session', 4, 0, 2, 1], ['group', 4, 1, 2, 1],
     ['stopwatch', 0, 2, 3, 1], ['volume', 3, 2, 3, 1],
     ['cpu', 0, 3, 2, 1], ['ram', 2, 3, 1, 1], ['day', 3, 3, 1, 1], ['theater', 4, 3, 1, 1], ['settings', 5, 3, 1, 1]]
  ];

  // ---------- markup ----------
  root.innerHTML =
    '<div class="wd-frame"><div class="wd-track">' +
    PAGES.map(function (p, pi) {
      return '<div class="wd-face" data-page="' + pi + '"' + (pi ? ' aria-hidden="true"' : '') + '>' + p.map(function (w) {
        return '<div class="wd-w wd-' + w[0] + '" data-t="' + w[0] + '" style="grid-area:' + (w[2] + 1) + '/' + (w[1] + 1) + '/span ' + w[4] + '/span ' + w[3] + '"' +
          ' role="button" tabindex="0">' + tile(w[0], w[3], w[4]) + '</div>';
      }).join('') + '</div>';
    }).join('') +
    '</div><div class="wd-detail" hidden></div><div class="wd-toast" hidden></div></div>' +
    '<div class="wd-nav"><button type="button" class="wd-dot" data-go="0" aria-label="1. ' + T.page + '"></button><button type="button" class="wd-dot" data-go="1" aria-label="2. ' + T.page + '"></button></div>';

  function tile(t, w, h) {
    switch (t) {
      case 'analog': return dial('a');
      case 'time': return '<div class="wd-time"><span data-v="time"></span><small data-v="ampm"></small></div>';
      case 'date': return '<div class="wd-date"><span class="wd-acc" data-v="weekday"></span><b data-v="mday"></b></div>';
      case 'battery': return '<div class="wd-bats">' + S.bat.map(function (b, i) { return batRow(b, i); }).join('') + '</div>';
      case 'media': return '<div class="wd-media"><span class="wd-cover">' + ic(I.note) + '</span><span class="wd-mt"><b>' + S.song + '</b><small>' + S.artist + '</small></span><button type="button" class="wd-round wd-purple" data-act="play" aria-label="Play / pause">' + ic(I.pause) + '</button></div>';
      case 'graph': return '<div class="wd-graph"><small data-v="graphhead"></small><canvas data-c="g"></canvas></div>';
      case 'fps': return stat('fps', T.fps, 'var(--wd-acc)');
      case 'reproj': return stat('reproj', T.reproj, '#5b5bd6');
      case 'gpu': return stat('gpu', T.gpu, '#a970ff');
      case 'vram': return stat('vram', T.vram, '#6366f1');
      case 'night': return '<span class="wd-btn">' + ic(I.moon, 30) + '</span>';
      case 'layout': return '<span class="wd-btn"><b data-v="layout"></b></span>';
      case 'calendar': return '<div class="wd-cal" data-v="cal"></div>';
      case 'timer': return '<div class="wd-timer"><b data-v="timer"></b><i class="wd-line"><i data-v="timerbar"></i></i><span class="wd-row"><button type="button" class="wd-round" data-act="tminus" aria-label="-1 min">' + ic(I.minus) + '</button><button type="button" class="wd-round wd-pink" data-act="tplay" aria-label="Start / pause">' + ic(I.play) + '</button><button type="button" class="wd-round" data-act="tplus" aria-label="+1 min">' + ic(I.plus) + '</button></span></div>';
      case 'session': return '<div class="wd-session"><small>' + T.inVr + '</small><b data-v="session"></b></div>';
      case 'group': return '<div class="wd-group"><span data-toast="keyboard">' + ic(I.kb, 26) + '</span><span data-toast="recenter">' + ic(I.recenter, 26) + '</span></div>';
      case 'stopwatch': return '<div class="wd-sw"><b data-v="sw"></b><span class="wd-row"><button type="button" class="wd-round" data-act="swlap" aria-label="' + T.lap + '">' + ic(I.flag) + '</button><button type="button" class="wd-round wd-pink" data-act="swplay" aria-label="Start / pause">' + ic(I.play) + '</button></span></div>';
      case 'volume': return '<div class="wd-vol"><span class="wd-round">' + ic(I.vol) + '</span><input type="range" min="0" max="100" value="' + S.vol + '" data-act="vol" aria-label="Volume"><b data-v="vol"></b></div>';
      case 'cpu': return '<div class="wd-cpu"><span class="wd-row2"><small>' + T.cpu + '</small><small>' + T.load + '</small></span><b data-v="cpu"></b><i class="wd-line"><i data-v="cpubar" style="background:var(--wd-acc)"></i></i></div>';
      case 'ram': return stat('ram', T.ram, '#a970ff');
      case 'day': return '<div class="wd-day"><span class="wd-acc" data-v="wd"></span><b data-v="dnum"></b></div>';
      case 'theater': return '<span class="wd-btn" data-toast="theater">' + ic(I.theater, 30) + '</span>';
      case 'settings': return '<span class="wd-btn" data-toast="settings">' + ic(I.gear, 28) + '</span>';
    }
    return '';
  }
  function stat(k, label, col) { return '<div class="wd-stat"><b data-v="' + k + '"></b><i class="wd-line"><i data-v="' + k + 'bar" style="background:' + col + '"></i></i><small>' + label + '</small></div>'; }
  function batCol(v) { return v < 20 ? '#4ade80' : v < 50 ? '#f5b82e' : '#4ade80'; }
  function batRow(b, i) {
    var col = b.c ? '#4ade80' : (b.v < 50 ? '#f5b82e' : '#4ade80');
    return '<div class="wd-bat"><span class="wd-row2"><small>' + (b.c ? '<span style="color:#4ade80">' + ic(I.bolt, 12) + '</span>' : '') + b.k + '</small><b>' + b.v + '%</b></span><i class="wd-line" style="background:' + col + '33"><i style="width:' + b.v + '%;background:' + col + '"></i></i></div>';
  }
  function dial(id) {
    var s = '<svg class="wd-dial" viewBox="0 0 100 100" aria-hidden="true"><circle cx="50" cy="50" r="46" fill="none" stroke="rgba(255,93,147,0.16)" stroke-width="0.8"/>';
    for (var h = 0; h < 12; h++) {
      var a = h * Math.PI / 6, big = h % 3 === 0, r = 38;
      s += '<circle cx="' + (50 + Math.sin(a) * r).toFixed(2) + '" cy="' + (50 - Math.cos(a) * r).toFixed(2) + '" r="' + (big ? 2.6 : 1.4) + '" fill="' + (big ? '#e8ebf2' : '#7c8399') + '"/>';
    }
    return s + '<line data-h="' + id + 'h" x1="50" y1="50" x2="50" y2="29" stroke="#e8ebf2" stroke-width="3.2" stroke-linecap="round"/>' +
      '<line data-h="' + id + 'm" x1="50" y1="50" x2="50" y2="17" stroke="#e8ebf2" stroke-width="2.4" stroke-linecap="round"/>' +
      '<line data-h="' + id + 's" x1="50" y1="56" x2="50" y2="14" stroke="var(--wd-acc)" stroke-width="1" stroke-linecap="round"/>' +
      '<circle cx="50" cy="50" r="2.6" fill="var(--wd-acc)"/></svg>';
  }

  // ---------- helpers ----------
  function $(sel, el) { return (el || root).querySelector(sel); }
  function $$(sel, el) { return Array.prototype.slice.call((el || root).querySelectorAll(sel)); }
  function setV(k, v) { $$('[data-v="' + k + '"]').forEach(function (e) { if (e.textContent !== v) e.textContent = v; }); }
  function setW(k, pct) { $$('[data-v="' + k + '"]').forEach(function (e) { e.style.width = Math.max(0, Math.min(100, pct)) + '%'; }); }
  function two(n) { return (n < 10 ? '0' : '') + n; }
  function mmss(s) { s = Math.max(0, Math.ceil(s)); return Math.floor(s / 60) + ':' + two(s % 60); }
  function hm(ms) { var m = Math.floor(ms / 60000); return Math.floor(m / 60) + ':' + two(m % 60); }
  function swTime() { return S.swAcc + (S.swRun ? Date.now() - S.swT : 0); }
  function swFmt(ms) { var t = Math.floor(ms / 100); return Math.floor(t / 600) + ':' + two(Math.floor(t / 10) % 60) + '.' + (t % 10); }
  function avg(a) { return a.reduce(function (x, y) { return x + y; }, 0) / a.length; }
  function rotate(id, deg) { var l = $('[data-h="' + id + '"]'); if (l) l.setAttribute('transform', 'rotate(' + deg.toFixed(2) + ' 50 50)'); }

  // ---------- calendar ----------
  function calendar(now) {
    var y = now.getFullYear(), m = now.getMonth(), first = new Date(y, m, 1), days = new Date(y, m + 1, 0).getDate();
    var startDow = HU ? (first.getDay() + 6) % 7 : first.getDay();
    var names = [];
    for (var d = 0; d < 7; d++) names.push(new Date(2024, 0, (HU ? 1 : 7) + d).toLocaleDateString(LOC, { weekday: 'narrow' }));
    var h = '<span class="wd-row2"><b>' + now.toLocaleDateString(LOC, { month: 'long' }) + '</b><small>' + y + '</small></span><div class="wd-days">' +
      names.map(function (n) { return '<small>' + n + '</small>'; }).join('');
    for (var e = 0; e < startDow; e++) h += '<span></span>';
    for (var k = 1; k <= days; k++) h += '<span' + (k === now.getDate() ? ' class="wd-today"' : '') + '>' + k + '</span>';
    return h + '</div>';
  }

  // ---------- frame graph ----------
  function drawGraph(cv, big) {
    var r = cv.getBoundingClientRect();
    if (!r.width) return;
    var dpr = window.devicePixelRatio || 1, W = Math.round(r.width * dpr), H = Math.round(r.height * dpr);
    if (cv.width !== W || cv.height !== H) { cv.width = W; cv.height = H; }
    var c = cv.getContext('2d'), n = S.frames.length, bw = W / n, max = 20;
    c.clearRect(0, 0, W, H);
    var budget = 1000 / S.fps, by = H - (budget / max) * H;
    c.fillStyle = 'rgba(232,235,242,0.35)'; c.fillRect(0, Math.round(by), W, Math.max(1, dpr));
    for (var i = 0; i < n; i++) {
      var v = S.frames[i], hh = Math.min(1, v / max) * H;
      c.fillStyle = v > budget ? '#f5b82e' : 'rgba(255,93,147,' + (big ? 0.95 : 0.85) + ')';
      c.fillRect(i * bw, H - hh, Math.max(1, bw - (big ? dpr : 0)), hh);
    }
  }

  // ---------- detail views (tap a widget) ----------
  var detail = $('.wd-detail'), openT = null;
  function open(t) {
    var h = '';
    var now = new Date();
    if (t === 'analog' || t === 'time' || t === 'session') {
      h = '<div class="wd-big-clock">' + dial('d') + '<div><b data-v="timefull"></b><span data-v="datefull"></span><small>' + T.inVr + ' <b data-v="session"></b></small></div></div>';
    } else if (t === 'date' || t === 'day' || t === 'calendar') {
      h = '<div class="wd-cal wd-cal-big">' + calendar(now) + '</div>';
    } else if (t === 'battery') {
      h = '<div class="wd-bat-list">' + S.bat.map(function (b) {
        var col = b.c ? '#4ade80' : (b.v < 50 ? '#f5b82e' : '#4ade80');
        return '<div><span class="wd-row2"><span>' + b.n + (b.c ? ' · <span style="color:#4ade80">' + T.charging + '</span>' : '') + '</span><b>' + b.v + '%</b></span><i class="wd-line" style="background:' + col + '33"><i style="width:' + b.v + '%;background:' + col + '"></i></i></div>';
      }).join('') + '</div>';
    } else if (t === 'media') {
      h = '<div class="wd-player"><span class="wd-cover wd-cover-big">' + ic(I.note, 48) + '</span><div><small>' + T.nowPlaying + '</small><b>' + S.song + '</b><span>' + S.artist + '</span>' +
        '<span class="wd-row"><button type="button" class="wd-round" data-act="noop" aria-label="Previous">' + ic(I.prev) + '</button><button type="button" class="wd-round wd-purple" data-act="play" aria-label="Play / pause">' + ic(S.playing ? I.pause : I.play) + '</button><button type="button" class="wd-round" data-act="noop" aria-label="Next">' + ic(I.next) + '</button></span></div></div>';
    } else if (['graph', 'fps', 'reproj', 'gpu', 'vram'].indexOf(t) >= 0) {
      h = '<div class="wd-graph-big"><span class="wd-row2"><b>' + T.frame + '</b><small data-v="graphhead"></small></span><canvas data-c="gd"></canvas>' +
        '<span class="wd-row3"><span><small>' + T.avg + '</small><b data-v="gavg"></b></span><span><small>' + T.worst + '</small><b data-v="gmax"></b></span><span><small>' + T.target + '</small><b>' + (1000 / S.fps).toFixed(1) + ' ms</b></span></span></div>';
    } else if (t === 'cpu' || t === 'ram') {
      h = '<div class="wd-graph-big"><span class="wd-row2"><b>' + T.cpu + '</b><small data-v="cpu"></small></span><canvas data-c="cpu"></canvas></div>';
    } else return false;
    openT = t;
    detail.innerHTML = '<button type="button" class="wd-back">‹ ' + T.back + '</button><div class="wd-dbody">' + h + '</div>';
    detail.hidden = false;
    if (!reduce) { detail.classList.remove('wd-in'); void detail.offsetWidth; detail.classList.add('wd-in'); }
    tick(true);
    $('.wd-back', detail).focus({ preventScroll: true });
    return true;
  }
  function close() { detail.hidden = true; detail.innerHTML = ''; openT = null; }

  var toastEl = $('.wd-toast'), toastTimer = 0;
  function toast(msg) {
    toastEl.textContent = msg; toastEl.hidden = false;
    clearTimeout(toastTimer); toastTimer = setTimeout(function () { toastEl.hidden = true; }, 2200);
  }

  // ---------- input ----------
  root.addEventListener('click', function (e) {
    var a = e.target.closest('[data-act]');
    if (a && a.tagName !== 'INPUT') { e.stopPropagation(); act(a.getAttribute('data-act')); return; }
    if (e.target.closest('.wd-back')) { close(); return; }
    var go = e.target.closest('[data-go]');
    if (go) { page(+go.getAttribute('data-go')); return; }
    if (e.target.closest('.wd-detail') || dragged) return;
    var ts = e.target.closest('[data-toast]');
    if (ts) { flash(ts); toast(T.toast[ts.getAttribute('data-toast')]); return; }
    var w = e.target.closest('.wd-w');
    if (!w) return;
    var t = w.getAttribute('data-t');
    if (t === 'night') { S.night = !S.night; root.classList.toggle('wd-night', S.night); w.classList.toggle('wd-on', S.night); toast(T.night + (S.night ? ' ✓' : '')); return; }
    if (t === 'layout') { S.layout = (S.layout + 1) % 3; flash(w); tick(true); return; }
    if (!open(t)) flash(w);
  });
  root.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && openT) { close(); return; }
    if ((e.key === 'Enter' || e.key === ' ') && e.target.classList.contains('wd-w')) { e.preventDefault(); e.target.click(); }
    if (e.key === 'ArrowRight' && !openT) page(1);
    if (e.key === 'ArrowLeft' && !openT) page(0);
  });
  root.addEventListener('input', function (e) { if (e.target.getAttribute('data-act') === 'vol') { S.vol = +e.target.value; tick(true); } });
  function flash(el) { el.classList.remove('wd-flash'); void el.offsetWidth; el.classList.add('wd-flash'); }

  function act(a) {
    if (a === 'play') { S.playing = !S.playing; $$('[data-act="play"]').forEach(function (b) { b.innerHTML = ic(S.playing ? I.pause : I.play); }); }
    if (a === 'tplay') { if (S.timerLeft <= 0) S.timerLeft = S.timerSet; S.timerRun = !S.timerRun; S.timerT = Date.now(); }
    if (a === 'tminus' && !S.timerRun) { S.timerSet = Math.max(60, S.timerSet - 60); S.timerLeft = S.timerSet; }
    if (a === 'tplus' && !S.timerRun) { S.timerSet = Math.min(5940, S.timerSet + 60); S.timerLeft = S.timerSet; }
    if (a === 'swplay') { if (S.swRun) { S.swAcc += Date.now() - S.swT; S.swRun = false; } else { S.swT = Date.now(); S.swRun = true; } }
    if (a === 'swlap') { if (S.swRun) S.laps.unshift(swTime()); else { S.swAcc = 0; S.laps = []; } }
    $$('[data-act="tplay"]').forEach(function (b) { b.innerHTML = ic(S.timerRun ? I.pause : I.play); });
    $$('[data-act="swplay"]').forEach(function (b) { b.innerHTML = ic(S.swRun ? I.pause : I.play); });
    $$('[data-act="swlap"]').forEach(function (b) { b.innerHTML = ic(S.swRun || !swTime() ? I.flag : I.reset); });
    tick(true);
  }

  // pages: dots, arrow keys, swipe
  var track = $('.wd-track'), cur = 0, dragged = false;
  function page(p) {
    cur = p; close();
    track.style.transform = 'translateX(' + (-50 * p) + '%)';
    $$('.wd-dot').forEach(function (d, i) { d.setAttribute('aria-current', i === p ? 'true' : 'false'); });
    $$('.wd-face').forEach(function (f, i) { if (i === p) f.removeAttribute('aria-hidden'); else f.setAttribute('aria-hidden', 'true'); });
  }
  var sx = null, sy = 0;
  track.addEventListener('pointerdown', function (e) { sx = e.clientX; sy = e.clientY; dragged = false; });
  track.addEventListener('pointerup', function (e) {
    if (sx === null) return;
    var dx = e.clientX - sx, dy = e.clientY - sy; sx = null;
    if (Math.abs(dx) > 40 && Math.abs(dx) > Math.abs(dy)) { dragged = true; page(dx < 0 ? 1 : 0); setTimeout(function () { dragged = false; }, 0); }
  });
  page(0);

  // ---------- update loop ----------
  var lastFrame = 0, cpuHist = [];
  for (var q = 0; q < 60; q++) cpuHist.push(8 + Math.random() * 6);
  function tick(force) {
    var now = new Date(), t = Date.now();
    var hr = now.getHours(), mn = now.getMinutes(), sc = now.getSeconds() + (reduce ? 0 : now.getMilliseconds() / 1000);
    var h12 = H12 ? (hr % 12 || 12) : hr;
    setV('time', h12 + ':' + two(mn));
    setV('ampm', H12 ? (hr < 12 ? 'AM' : 'PM') : '');
    setV('timefull', h12 + ':' + two(mn) + ':' + two(now.getSeconds()) + (H12 ? (hr < 12 ? ' AM' : ' PM') : ''));
    setV('weekday', now.toLocaleDateString(LOC, { weekday: 'long' }));
    setV('mday', now.toLocaleDateString(HU ? 'hu-HU' : 'en-US', { month: 'long', day: 'numeric' }));
    setV('datefull', now.toLocaleDateString(HU ? 'hu-HU' : 'en-US', { weekday: 'long', month: 'long', day: 'numeric' }));
    setV('wd', now.toLocaleDateString(LOC, { weekday: 'short' }));
    setV('dnum', String(now.getDate()));
    ['a', 'd'].forEach(function (id) {
      rotate(id + 'h', (hr % 12 + mn / 60) * 30); rotate(id + 'm', (mn + sc / 60) * 6); rotate(id + 's', sc * 6);
    });
    setV('session', hm(t - S.start));
    setV('layout', T.layouts[S.layout]);

    // frames: about 10 new samples a second
    if (t - lastFrame > 100 || force) {
      if (t - lastFrame > 100) {
        lastFrame = t; S.frames.push(frameMs()); S.frames.shift();
        cpuHist.push(Math.max(3, Math.min(40, cpuHist[cpuHist.length - 1] + (Math.random() - 0.5) * 3))); cpuHist.shift();
      }
      var recent = S.frames.slice(-30), a = avg(recent), worst = Math.max.apply(null, S.frames);
      var budget = 1000 / S.fps, over = recent.filter(function (v) { return v > budget; }).length / recent.length;
      var fps = Math.round(Math.min(S.fps, 1000 / a)), gpu = a.toFixed(1), rp = Math.round(over * 100);
      setV('graphhead', fps + ' fps · ' + gpu + ' ms · ' + rp + '%');
      setV('fps', String(fps)); setW('fpsbar', fps / S.fps * 100);
      setV('reproj', rp + '%'); setW('reprojbar', rp * 4);
      setV('gpu', gpu); setW('gpubar', a / budget * 80);
      setV('vram', '9.4'); setW('vrambar', 59);
      setV('ram', '15.3'); setW('rambar', 48);
      setV('gavg', a.toFixed(1) + ' ms'); setV('gmax', worst.toFixed(1) + ' ms');
      var c = cpuHist[cpuHist.length - 1];
      setV('cpu', Math.round(c) + '%'); setW('cpubar', c);
      $$('canvas[data-c="g"], canvas[data-c="gd"]').forEach(function (cv) { drawGraph(cv, cv.getAttribute('data-c') === 'gd'); });
      var cc = $('canvas[data-c="cpu"]');
      if (cc) { var keep = S.frames; S.frames = cpuHist.map(function (v) { return v / 2; }); drawGraph(cc, true); S.frames = keep; }
    }

    // timer and stopwatch
    if (S.timerRun) { S.timerLeft -= (t - S.timerT) / 1000; S.timerT = t; if (S.timerLeft <= 0) { S.timerLeft = 0; S.timerRun = false; act('none'); toast('⏰ ' + T.timer); } }
    setV('timer', mmss(S.timerLeft)); setW('timerbar', S.timerLeft / S.timerSet * 100);
    setV('sw', swFmt(swTime()));
    setV('vol', S.vol + '%');

    var cal = $('[data-v="cal"]');
    if (cal && (force || !cal.firstChild || cal.getAttribute('data-d') !== now.toDateString())) { cal.innerHTML = calendar(now); cal.setAttribute('data-d', now.toDateString()); }
  }
  function loop() { if (!document.hidden) tick(false); }
  tick(true);
  setInterval(loop, reduce ? 1000 : 100);
  window.addEventListener('resize', function () { tick(true); });
})();
