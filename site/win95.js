// Windows 95 style for the whole website, like the app's own Settings > Look > Style.
// The "95" button in the top bar switches it on; the Start menu switches it off. Remembered per browser.
// A tiny script in each page's <head> sets data-style="95" before the first paint, so there is no flash.
(function () {
  var html = document.documentElement;
  var HU = (html.lang || '').slice(0, 2) === 'hu';
  var base = (document.querySelector('meta[name="site-root"]') || {}).content || '';
  var KEY = 'orbit-style';
  var L = HU ? {
    on: 'Windows 95 stílus', start: 'Start', watch: 'Az óra', desktop: 'Asztal', keyboard: 'Billentyűzet',
    install: 'Telepítés', guide: 'Útmutató', github: 'Forráskód (GitHub)', lang: 'English', langHref: base + './',
    off: 'Vissza a modern stílusra…', welcome: 'Üdvözlet', task: 'OrbitOverlay'
  } : {
    on: 'Windows 95 style', start: 'Start', watch: 'The watch', desktop: 'Desktop', keyboard: 'Keyboard',
    install: 'Install', guide: 'Guide', github: 'Source code (GitHub)', lang: 'Magyar', langHref: base + 'hu/',
    off: 'Back to the modern look…', welcome: 'Welcome', task: 'OrbitOverlay'
  };
  var home = /guide\.html$/.test(location.pathname) ? base + './' : '';
  var ids = HU ? { watch: 'ora', desktop: 'asztal', keyboard: 'billentyuzet', install: 'telepites' }
               : { watch: 'watch', desktop: 'desktop', keyboard: 'keyboard', install: 'install' };

  function save(on) { try { if (on) localStorage.setItem(KEY, '95'); else localStorage.removeItem(KEY); } catch (e) {} }
  function isOn() { return html.getAttribute('data-style') === '95'; }

  // ---- every section becomes a window: its heading moves into the title bar ----
  var sections = document.querySelectorAll('main > section, main > .page-head');
  Array.prototype.forEach.call(sections, function (s) {
    var h = s.querySelector('h2') || null;
    var title = h ? h.textContent : (s.getAttribute('aria-label') || (s.classList.contains('intro') ? L.welcome : (s.querySelector('h1') || {}).textContent || ''));
    if (h) h.classList.add('w95-ht');
    if (s.classList.contains('page-head')) { var h1 = s.querySelector('h1'); if (h1) h1.classList.add('w95-ht'); }
    s.setAttribute('data-w95-title', title.trim());
  });

  // ---- the switch in the top bar ----
  var nav = document.querySelector('.top nav');
  var toggle = document.createElement('button');
  toggle.type = 'button';
  toggle.className = 'lang w95-toggle';
  toggle.textContent = '95';
  toggle.title = L.on;
  toggle.setAttribute('aria-label', L.on);
  if (nav) nav.insertBefore(toggle, nav.querySelector('.lang'));
  toggle.addEventListener('click', function () { set(true); window.scrollTo(0, 0); });

  // ---- taskbar, Start menu, clock ----
  var bar = document.createElement('div');
  bar.className = 'w95-taskbar';
  bar.innerHTML =
    '<button type="button" class="w95-start" aria-expanded="false" aria-controls="w95-menu"><img src="' + base + 'favicon.svg" width="18" height="18" alt=""><b>' + L.start + '</b></button>' +
    '<a class="w95-task" href="#top"><img src="' + base + 'favicon.svg" width="16" height="16" alt="">' + L.task + '</a>' +
    '<span class="w95-tray"><time class="w95-clock"></time></span>';
  var menu = document.createElement('div');
  menu.className = 'w95-menu';
  menu.id = 'w95-menu';
  menu.hidden = true;
  var item = function (href, text, extra) { return '<a role="menuitem" href="' + href + '"' + (extra || '') + '>' + text + '</a>'; };
  menu.innerHTML =
    '<span class="w95-banner" aria-hidden="true"><b>Orbit</b>Overlay<i>95</i></span><div role="menu">' +
    item(home + '#' + ids.watch, L.watch) + item(home + '#' + ids.desktop, L.desktop) + item(home + '#' + ids.keyboard, L.keyboard) +
    item(home + '#' + ids.install, L.install) + item(base + 'guide.html', L.guide, HU ? ' hreflang="en"' : '') +
    item('https://github.com/Erik1028/OrbitOverlay', L.github) + item(L.langHref, L.lang) +
    '<hr><button type="button" role="menuitem" class="w95-off">' + L.off + '</button></div>';
  document.body.appendChild(menu);
  document.body.appendChild(bar);

  var startBtn = bar.querySelector('.w95-start');
  function openMenu(open) {
    menu.hidden = !open;
    startBtn.setAttribute('aria-expanded', open ? 'true' : 'false');
    if (open) { var first = menu.querySelector('[role="menuitem"]'); if (first) first.focus(); }
  }
  startBtn.addEventListener('click', function (e) { e.stopPropagation(); openMenu(menu.hidden); });
  menu.addEventListener('click', function (e) {
    if (e.target.closest('.w95-off')) { set(false); openMenu(false); window.scrollTo(0, 0); return; }
    if (e.target.closest('a')) openMenu(false);
  });
  document.addEventListener('click', function (e) { if (!menu.hidden && !menu.contains(e.target)) openMenu(false); });
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && !menu.hidden) { openMenu(false); startBtn.focus(); }
    if (!menu.hidden && (e.key === 'ArrowDown' || e.key === 'ArrowUp')) {
      var items = Array.prototype.slice.call(menu.querySelectorAll('[role="menuitem"]'));
      var i = items.indexOf(document.activeElement);
      items[(i + (e.key === 'ArrowDown' ? 1 : items.length - 1)) % items.length].focus();
      e.preventDefault();
    }
  });

  var clock = bar.querySelector('.w95-clock');
  function tick() {
    var d = new Date(), h = d.getHours(), m = d.getMinutes();
    clock.textContent = HU ? h + ':' + (m < 10 ? '0' : '') + m : (h % 12 || 12) + ':' + (m < 10 ? '0' : '') + m + ' ' + (h < 12 ? 'AM' : 'PM');
    clock.setAttribute('datetime', d.toISOString());
  }
  tick();
  setInterval(tick, 10000);

  var theme = document.querySelector('meta[name="theme-color"]');
  function set(on) {
    if (on) html.setAttribute('data-style', '95'); else html.removeAttribute('data-style');
    save(on);
    toggle.setAttribute('aria-pressed', on ? 'true' : 'false');
    if (theme) theme.setAttribute('content', on ? '#008080' : '#171a24');
    document.dispatchEvent(new CustomEvent('orbit-style'));
  }
  if (location.hash === '#win95') set(true);
  else set(isOn());
})();
