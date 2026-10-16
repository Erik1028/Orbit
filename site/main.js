// Release info next to the download button, and the changelog built from docs/CHANGELOG.md
// (the Pages workflow copies it next to the pages). Everything here is optional: without it the page still works.
(function () {
  var HU = (document.documentElement.lang || '').slice(0, 2) === 'hu';
  var base = document.querySelector('meta[name="site-root"]');
  base = base ? base.getAttribute('content') : '';
  // Show the download count only once it is a number worth showing.
  var SHOW_DOWNLOADS_FROM = 500;

  var ver = document.getElementById('ver');
  if (ver) {
    fetch('https://api.github.com/repos/Erik1028/OrbitOverlay/releases?per_page=100')
      .then(function (r) { return r.ok ? r.json() : null; })
      .then(function (list) {
        if (!list || !list.length) return;
        var rel = list.filter(function (x) { return !x.draft && !x.prerelease; })[0] || list[0];
        ver.textContent = rel.tag_name || rel.name || ver.textContent;
        var extra = '';
        if (rel.published_at) {
          extra += ' · ' + new Date(rel.published_at).toLocaleDateString(HU ? 'hu-HU' : 'en-GB', { day: 'numeric', month: 'short', year: 'numeric' });
        }
        // OrbitSetup.exe is the same file under the old name, for the updater of 1.6 and older; count both
        var total = 0;
        list.forEach(function (x) { (x.assets || []).forEach(function (a) { if (/setup\.exe$/i.test(a.name)) total += a.download_count || 0; }); });
        if (total >= SHOW_DOWNLOADS_FROM) extra += ' · ' + total.toLocaleString(HU ? 'hu-HU' : 'en-GB') + (HU ? ' letöltés' : ' downloads');
        document.getElementById('verdate').textContent = extra;
      })
      .catch(function () {});
  }

  // A clip from the headset: shown only once docs/img/demo.mp4 exists (the workflow copies docs/img next to the page).
  var clip = document.getElementById('clip');
  if (clip) {
    var src = clip.getAttribute('data-src');
    fetch(src, { method: 'HEAD' }).then(function (r) {
      if (!r.ok) return;
      var v = document.createElement('video');
      v.src = src; v.muted = true; v.loop = true; v.playsInline = true; v.preload = 'metadata';
      v.setAttribute('aria-label', clip.querySelector('figcaption').textContent);
      var still = window.matchMedia && matchMedia('(prefers-reduced-motion: reduce)').matches;
      if (still) v.controls = true; else v.autoplay = true;
      clip.querySelector('.shot').appendChild(v);
      clip.hidden = false;
      if (!still) v.play().catch(function () { v.controls = true; });
    }).catch(function () {});
  }

  var log = document.getElementById('log');
  if (!log) return;
  function esc(s) { return s.replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; }); }
  function inline(s) { return esc(s).replace(/\*\*(.+?)\*\*/g, '<b>$1</b>').replace(/`(.+?)`/g, '<kbd>$1</kbd>'); }
  fetch(base + 'CHANGELOG.md')
    .then(function (r) { return r.ok ? r.text() : null; })
    .then(function (md) {
      if (!md) return;
      var versions = [], cur = null;
      md.split(/\r?\n/).forEach(function (line) {
        var h = /^##\s+([^|]+)\|\s*(.+)$/.exec(line);
        if (h) { cur = { v: h[1].trim(), t: h[2].trim(), pts: [] }; versions.push(cur); return; }
        if (!cur) return;
        if (/^- /.test(line)) { cur.pts.push(line.slice(2).trim()); return; }
        if (/^\s+\S/.test(line) && cur.pts.length) { cur.pts[cur.pts.length - 1] += ' ' + line.trim(); }
      });
      if (!versions.length) return;
      log.innerHTML = '';
      var limit = +log.getAttribute('data-count') || 0;
      if (limit) {
        // the home page: only the newest version, then a link to all of them
        var all = document.createElement('p');
        all.className = 'all';
        all.innerHTML = '<a href="https://github.com/Erik1028/OrbitOverlay/blob/main/docs/CHANGELOG.md">' + (HU ? 'Az összes változás' : 'All changes') + '</a>';
        log.appendChild(all);
        versions.slice(0, limit).forEach(function (x) {
          var el = document.createElement('article');
          el.className = 'ver';
          el.innerHTML = '<span class="v">' + esc(x.v) + '</span><div><h3>' + esc(x.t) + '</h3><ul>' +
            x.pts.map(function (p) { return '<li>' + inline(p) + '</li>'; }).join('') + '</ul></div>';
          log.insertBefore(el, all);
        });
        return;
      }
      var more = document.createElement('button');
      more.className = 'more';
      more.type = 'button';
      more.textContent = HU ? 'Régebbi verziók' : 'Show older versions';
      log.appendChild(more);
      function render(list) {
        list.forEach(function (x) {
          var el = document.createElement('article');
          el.className = 'ver';
          el.innerHTML = '<span class="v">' + esc(x.v) + '</span><div><h3>' + esc(x.t) + '</h3><ul>' +
            x.pts.map(function (p) { return '<li>' + inline(p) + '</li>'; }).join('') + '</ul></div>';
          log.insertBefore(el, more);
        });
      }
      render(versions.slice(0, 3));
      var rest = versions.slice(3);
      if (!rest.length) more.remove();
      more.addEventListener('click', function () { render(rest); more.remove(); });
    })
    .catch(function () {});
})();
