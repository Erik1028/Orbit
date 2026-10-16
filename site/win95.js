// "95" mode: the website as a 1990s homepage (a 1996 space-themed fan page).
// The 95 button in the top bar switches it on; "Back to 2026" in its own nav bar switches it off.
// Remembered per browser; a #win95 link opens it directly. A tiny script in each page's <head> sets
// data-style="95" before the first paint, so there is no flash. Every number shown is real:
// the counter is the GitHub download count, "last updated" is this page's own date.
(function () {
  var html = document.documentElement;
  var HU = (html.lang || '').slice(0, 2) === 'hu';
  var base = (document.querySelector('meta[name="site-root"]') || {}).content || '';
  var KEY = 'orbit-style';
  var onGuide = /guide\.html$/.test(location.pathname);
  var home = onGuide ? base + './' : '';
  var L = HU ? {
    toggle: '1990-es évekbeli honlap', welcome: 'Üdvözöllek az OrbitOverlay honlapján!',
    marquee: function (v) { return '*** Üdvözöllek az OrbitOverlay honlapján! *** ' + (v ? 'Megjelent a ' + v + '! ' : '') + 'Ingyenes letöltés lent! *** Óra a csuklódon, asztal a VR-ben *** Köszönöm, hogy benéztél! ***'; },
    nav: [['', 'Kezdőlap'], ['#features', 'Funkciók'], ['#install', 'Letöltés'], ['guide', 'Útmutató']],
    lang: ['English', base + './'], back: 'Vissza 2026-ba',
    modem: function (mb) { var h = Math.round(mb * 8 * 1048.576 / 56 / 3600 * 2) / 2; return '(' + String(mb).replace('.', ',') + ' MB · 56k modemmel kb. ' + String(h).replace('.', ',') + ' óra)'; },
    counter: 'Letöltések eddig:', guest: 'Írj a vendégkönyvembe!', guestNote: '(GitHub Issues)',
    best: 'Bármilyen böngészővel, 800 × 600-as felbontásban a legszebb.', updated: 'Utoljára frissítve: ',
    sign: 'ÉPÍTÉS ALATT', signNote: 'Ez az oldal folyamatosan bővül.'
  } : {
    toggle: '1990s homepage', welcome: 'Welcome to the OrbitOverlay homepage!',
    marquee: function (v) { return '*** Welcome to the OrbitOverlay homepage! *** ' + (v ? v + ' is out now! ' : '') + 'Free download below! *** A watch on your wrist and your desktop in VR *** Thanks for stopping by! ***'; },
    nav: [['', 'Home'], ['#features', 'Features'], ['#install', 'Download'], ['guide', 'Guide']],
    lang: ['Magyar', base + 'hu/'], back: 'Back to 2026',
    modem: function (mb) { var h = Math.round(mb * 8 * 1048.576 / 56 / 3600 * 2) / 2; return '(' + mb + ' MB · about ' + h + ' hour' + (h === 1 ? '' : 's') + ' on a 56k modem)'; },
    counter: 'Downloads so far:', guest: 'Sign my guestbook!', guestNote: '(GitHub Issues)',
    best: 'Best viewed with any browser at 800 × 600.', updated: 'Last updated: ',
    sign: 'UNDER CONSTRUCTION', signNote: 'This page is always growing.'
  };

  function el(tag, cls, inner) { var e = document.createElement(tag); if (cls) e.className = cls; if (inner != null) e.innerHTML = inner; return e; }
  function esc(s) { return String(s).replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; }); }
  function isOn() { return html.getAttribute('data-style') === '95'; }

  // ---- the switch in the modern top bar ----
  var nav = document.querySelector('.top nav');
  var toggle = el('button', 'lang w95-toggle', '95');
  toggle.type = 'button';
  toggle.title = L.toggle;
  toggle.setAttribute('aria-label', L.toggle);
  if (nav) nav.insertBefore(toggle, nav.querySelector('.lang'));
  toggle.addEventListener('click', function () { set(true); window.scrollTo(0, 0); });

  // ---- the 1990s parts (hidden by CSS unless the mode is on) ----
  var main = document.querySelector('main');
  var marquee = el('div', 'r90 r90-marquee', '<span></span>');
  marquee.setAttribute('aria-hidden', 'true');
  var links = L.nav.map(function (n) {
    var href = n[0] === 'guide' ? base + 'guide.html' : (n[0] === '' ? (onGuide ? base + './' : '#top') : home + n[0]);
    return '<a href="' + href + '">' + n[1] + '</a>';
  });
  links.push('<a href="' + L.lang[1] + '">' + L.lang[0] + '</a>');
  var r90nav = el('nav', 'r90 r90-nav', '[ ' + links.join(' | ') + ' ]<br>[ <button type="button" class="r90-back">★ ' + L.back + ' ★</button> ]');
  r90nav.setAttribute('aria-label', 'Retro');
  if (main) { main.insertBefore(r90nav, main.firstChild); main.insertBefore(marquee, main.firstChild); }
  r90nav.querySelector('.r90-back').addEventListener('click', function () { set(false); window.scrollTo(0, 0); });

  function setMarquee(v) { marquee.firstChild.textContent = L.marquee(v) + '   ' + L.marquee(v); }
  setMarquee('');

  // next to the download button: NEW! and the size in modem minutes
  var get = document.querySelector('.get');
  var newBadge = el('span', 'r90 r90-blink', 'NEW!');
  var modem = el('span', 'r90 r90-modem', '');
  if (get) {
    var mono = get.querySelector('.mono');
    if (mono) mono.appendChild(document.createTextNode(' '));
    if (mono) mono.appendChild(newBadge);
    get.appendChild(modem);
  }

  // guide page: the sign
  var head = document.querySelector('.page-head');
  if (head) head.appendChild(el('div', 'r90 r90-sign', '<b>' + L.sign + '</b>'));
  if (head) head.appendChild(el('p', 'r90 r90-small', L.signNote));

  // bottom of the page: counter, guestbook, buttons, "best viewed", last updated
  var updated = new Date(document.lastModified);
  var upd = isNaN(updated) ? '' : updated.toLocaleDateString(HU ? 'hu-HU' : 'en-US', { year: 'numeric', month: 'long', day: 'numeric' });
  var badges = [['SteamVR', 'ready', '#000080', '#ff0'], ['Made with', 'AI', '#000', '#0f0'], ['MIT', 'license', '#800000', '#fff'],
    ['No', 'tracking', '#006000', '#fff'], ['VR', 'overlay', '#303030', '#f0f'], ['Free', 'download', '#004060', '#0ff']];
  var foot = el('div', 'r90 r90-foot',
    '<div class="r90-counter"><span>' + L.counter + '</span><span class="r90-digits" aria-live="polite"></span></div>' +
    '<div><a href="https://github.com/Erik1028/OrbitOverlay/issues">' + L.guest + '</a> <span class="r90-small">' + L.guestNote + '</span></div>' +
    '<div class="r90-badges" aria-hidden="true">' + badges.map(function (b) {
      return '<span style="background:' + b[2] + ';color:' + b[3] + '">' + esc(b[0]) + '<br>' + esc(b[1]) + '</span>';
    }).join('') + '</div>' +
    '<div class="r90-small">' + L.best + (upd ? '<br>' + L.updated + upd : '') + '</div>');
  if (main) main.appendChild(foot);
  var digits = foot.querySelector('.r90-digits');
  function setCount(n) {
    var s = String(n);
    while (s.length < 6) s = '0' + s;
    digits.innerHTML = s.split('').map(function (d) { return '<i>' + d + '</i>'; }).join('');
    digits.setAttribute('aria-label', String(n));
  }
  setCount(0);

  // real numbers from GitHub, fetched once the mode is on
  var fetched = false;
  function load() {
    if (fetched) return;
    fetched = true;
    fetch('https://api.github.com/repos/Erik1028/OrbitOverlay/releases?per_page=100')
      .then(function (r) { return r.ok ? r.json() : null; })
      .then(function (list) {
        if (!list || !list.length) return;
        var total = 0, rel = list.filter(function (x) { return !x.draft && !x.prerelease; })[0] || list[0], size = 0;
        list.forEach(function (x) { (x.assets || []).forEach(function (a) { if (/setup\.exe$/i.test(a.name)) total += a.download_count || 0; }); });
        (rel.assets || []).forEach(function (a) { if (/^OrbitOverlaySetup\.exe$/i.test(a.name)) size = a.size; });
        setCount(total);
        setMarquee(rel.tag_name || '');
        if (size) modem.textContent = L.modem(Math.round(size / 1048576 * 10) / 10);
      })
      .catch(function () {});
  }

  // screenshots: in this mode show the app's own Windows 95 look. A picture with a data-95="img/NAME-95.png" attribute
  // swaps to it; one without (no 95 version yet, e.g. the keyboard) is hidden meanwhile. Nothing is probed, so a
  // missing file never shows up as a 404.
  var shots = Array.prototype.slice.call(document.querySelectorAll('main .shot img')).filter(function (img) {
    return !/windows95\.png$/.test(img.getAttribute('src'));
  });
  function swapShots(on) {
    shots.forEach(function (img) {
      var modern = img.getAttribute('data-modern') || img.getAttribute('src');
      img.setAttribute('data-modern', modern);
      var box = img.closest('.shot');
      var alt95 = img.getAttribute('data-95');
      if (!on) { if (img.getAttribute('src') !== modern) img.setAttribute('src', modern); box.classList.remove('r90-nopic'); return; }
      if (alt95) { img.setAttribute('src', alt95); box.classList.remove('r90-nopic'); }
      else box.classList.add('r90-nopic');
    });
  }

  var theme = document.querySelector('meta[name="theme-color"]');
  function save(on) { try { if (on) localStorage.setItem(KEY, '95'); else localStorage.removeItem(KEY); } catch (e) {} }
  function set(on) {
    if (on) html.setAttribute('data-style', '95'); else html.removeAttribute('data-style');
    save(on);
    if (!on && location.hash === '#win95' && history.replaceState) history.replaceState(null, '', location.pathname + location.search);
    toggle.setAttribute('aria-pressed', on ? 'true' : 'false');
    if (theme) theme.setAttribute('content', on ? '#000000' : '#171a24');
    if (on) load();
    swapShots(on);
    document.dispatchEvent(new CustomEvent('orbit-style'));
  }
  if (location.hash === '#win95') set(true);
  else set(isOn());
})();
