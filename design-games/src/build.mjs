// Mini oyun artboardlarını gövdə fayllarından yığır.
//
// Palitra və pet həndəsəsi ../../design-canvas/src ilə eynidir — mini oyunlar
// app-in başqa bir səthi deyil, elə HƏMİN səthdir. Fərq yalnız ondadır ki,
// oyun kartın içində deyil, tam ekran səhnədədir (hamam və yataq otağı kimi).
//
// Marker sintaksisi HTML şərhidir: dc runtime-ın {{ }} sintaksisi ilə
// toqquşmasın deyə.

import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const out = join(here, '..');
const read = (name) => readFileSync(join(here, name), 'utf8');

const tokens = readFileSync(join(here, '..', '..', 'design-canvas', 'src', 'tokens.b.css'), 'utf8')
  .split('\n')
  .map((line) => (line.trim() ? '      ' + line.trim() : ''))
  .join('\n')
  .trimEnd();

/* ---------- oyun xromu ---------- */

/** Üç can. İtən can həm rəngini itirir, həm kiçilir — rəng tək daşıyıcı deyil. */
function lives(left) {
  const hearts = [0, 1, 2]
    .map((i) => {
      const on = i < left;
      const fill = on ? 'var(--pp-alert)' : 'var(--pp-track)';
      const size = on ? 20 : 16;
      return `<svg viewBox="0 0 24 24" width="${size}" height="${size}" aria-hidden="true"><path d="M12 20.6C12 20.6 3.8 15.6 3.8 10.1A4.4 4.4 0 0 1 12 7.3a4.4 4.4 0 0 1 8.2 2.8c0 5.5-8.2 10.5-8.2 10.5z" fill="${fill}" /></svg>`;
    })
    .join('');

  return `<div style="display: flex; align-items: center; gap: 4px; height: 40px; padding: 0 14px; border-radius: 999px; background: var(--pp-glass); box-shadow: 0 3px 0 var(--pp-edge-neutral), var(--pp-shadow-sm), var(--pp-gloss);">${hearts}</div>`;
}

/** Oyunun üst xromu: çıxış, ad + raund nöqtələri, canlar. Səhnənin üstündə üzür. */
function chrome(title, round, total, left) {
  const dots = Array.from({ length: Number(total) }, (_, i) => {
    const done = i < Number(round);
    return `<span style="width: 7px; height: 7px; border-radius: 999px; background: ${done ? 'var(--pp-primary)' : 'var(--pp-track)'};"></span>`;
  }).join('');

  return `  <div style="position: absolute; top: 52px; left: 14px; right: 14px; display: flex; align-items: center; justify-content: space-between; gap: 10px; z-index: 20;">

    <span style="display: inline-flex; align-items: center; justify-content: center; width: 46px; height: 46px; flex-shrink: 0; border-radius: 999px; background: var(--pp-glass); box-shadow: 0 3px 0 var(--pp-edge-neutral), var(--pp-shadow-sm), var(--pp-gloss);">
      <svg viewBox="0 0 24 24" width="22" height="22" fill="none" aria-hidden="true">
        <path d="M15 5l-7 7 7 7" stroke="var(--pp-ink-soft)" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round" />
      </svg>
    </span>

    <div style="display: flex; flex-direction: column; align-items: center; gap: 5px; padding: 7px 18px 8px; border-radius: 999px; background: var(--pp-glass); box-shadow: 0 3px 0 var(--pp-edge-neutral), var(--pp-shadow-sm), var(--pp-gloss);">
      <span style="font-size: 14px; font-weight: 900; line-height: 1; color: var(--pp-ink);">${title}</span>
      <span style="display: flex; align-items: center; gap: 4px;">${dots}</span>
    </div>

    ${lives(Number(left))}
  </div>`;
}

/** Tapşırıq zolağı — oyunun QAYDASI. Ekranda ən böyük yazı budur. */
function rule(text, tone) {
  const family = tone || 'primary';
  const soft = `var(--pp-${family}-soft)`;
  const deep = family === 'primary' ? 'var(--pp-primary-dark)' : `var(--pp-${family}-deep)`;
  return `  <div style="position: absolute; top: 122px; left: 20px; right: 20px; display: flex; align-items: center; justify-content: center; min-height: 54px; padding: 8px 18px; border-radius: 22px; background: ${soft}; box-shadow: 0 3px 0 var(--pp-edge-neutral); text-align: center; z-index: 15;">
    <span style="font-size: 19px; font-weight: 900; line-height: 1.2; color: ${deep};">${text}</span>
  </div>`;
}

/** Vaxt zolağı — raundun qalan hissəsi. */
function timer(pct) {
  return `  <div style="position: absolute; top: 190px; left: 20px; right: 20px; height: 8px; border-radius: 999px; background: var(--pp-track); overflow: hidden; z-index: 15;">
    <div style="width: ${pct}%; height: 100%; border-radius: 999px; background: linear-gradient(90deg, var(--pp-spark-lift) 0%, var(--pp-spark) 100%);"></div>
  </div>`;
}

/** Nəticə sayğacı — səhnənin altında. */
function score(value, label) {
  return `  <div style="position: absolute; left: 0; right: 0; bottom: 34px; display: flex; align-items: center; justify-content: center; gap: 8px; z-index: 20;">
    <div style="display: flex; align-items: center; gap: 9px; height: 46px; padding: 0 20px; border-radius: 999px; background: var(--pp-glass); box-shadow: 0 3px 0 var(--pp-edge-neutral), var(--pp-shadow-md), var(--pp-gloss);">
      <svg viewBox="0 0 24 24" width="19" height="19" aria-hidden="true">
        <path d="M12 3.2l2.6 5.6 6.1.8-4.5 4.2 1.2 6-5.4-3-5.4 3 1.2-6L3.3 9.6l6.1-.8z" fill="var(--pp-spark)" stroke="var(--pp-spark-edge)" stroke-width="1.6" stroke-linejoin="round" />
      </svg>
      <span style="font-size: 17px; font-weight: 900; color: var(--pp-ink);">${value}</span>
      <span style="font-size: 12px; font-weight: 800; color: var(--pp-ink-soft);">${label}</span>
    </div>
  </div>`;
}

/** Pet SVG — PetAvatar.razor həndəsəsi (Young mərhələ, scale 0.94). */
function pet(mood, opts) {
  const o = opts || {};
  const eyes =
    mood === 'sleepy'
      ? '<path d="M78 72q8 8 16 0" stroke="#2B2140" stroke-width="4" fill="none" stroke-linecap="round" /><path d="M106 72q8 8 16 0" stroke="#2B2140" stroke-width="4" fill="none" stroke-linecap="round" />'
      : '<path d="M78 74q8 -11 16 0" stroke="#2B2140" stroke-width="5" fill="none" stroke-linecap="round" /><path d="M106 74q8 -11 16 0" stroke="#2B2140" stroke-width="5" fill="none" stroke-linecap="round" />';

  const mouth = o.mouthOpen
    ? '<ellipse cx="100" cy="97" rx="11" ry="9" fill="#2B2140" /><ellipse cx="100" cy="101" rx="6" ry="4.5" fill="#FF8FA0" />'
    : '<path d="M92 92q8 9 16 0" stroke="#2B2140" stroke-width="3.5" fill="none" stroke-linecap="round" />';

  const cheeks = '<ellipse cx="72" cy="86" rx="7" ry="4.5" fill="#FF9EA8" opacity="0.65" /><ellipse cx="128" cy="86" rx="7" ry="4.5" fill="#FF9EA8" opacity="0.65" />';

  return `<svg viewBox="0 0 200 200" width="${o.size || 200}" height="${o.size || 200}" role="img" aria-label="${o.label || 'Pet'}">
      <ellipse cx="100" cy="168" rx="43.24" ry="9" fill="rgba(0,0,0,0.12)" />
      <g transform="translate(100 162) scale(0.94) translate(-100 -162)">
        <path d="M132 132c22 4 34-10 34-26 0-12-7-22-16-24 4 10 2 22-6 29-7 6-15 8-22 8z" fill="#F3A05A" />
        <ellipse cx="100" cy="128" rx="38" ry="34" fill="#F3A05A" />
        <ellipse cx="100" cy="136" rx="24" ry="23" fill="#FFF3E2" />
        <ellipse cx="80" cy="158" rx="13" ry="8" fill="#FFF3E2" />
        <ellipse cx="120" cy="158" rx="13" ry="8" fill="#FFF3E2" />
        <path d="M64 60l-6-30 28 16z" fill="#F3A05A" />
        <path d="M136 60l6-30-28 16z" fill="#F3A05A" />
        <path d="M67 57l-3-17 16 9z" fill="#FFD3A8" />
        <path d="M133 57l3-17-16 9z" fill="#FFD3A8" />
        <ellipse cx="100" cy="74" rx="36" ry="32" fill="#F3A05A" />
        <ellipse cx="100" cy="86" rx="26" ry="18" fill="#FFF3E2" />
        ${eyes}
        <ellipse cx="100" cy="84" rx="6" ry="4.5" fill="#2B2140" />
        ${mouth}
        ${cheeks}
        <path d="M76 100q24 14 48 0" stroke="var(--pp-primary)" stroke-width="7" fill="none" stroke-linecap="round" />
        <circle cx="100" cy="110" r="7" fill="var(--pp-spark)" stroke="var(--pp-spark-deep)" stroke-width="2" />
        ${o.extra || ''}
      </g>
    </svg>`;
}

/* ---------- markerlərin açılması ---------- */

function expand(src) {
  return src
    .replace(/<!--CHROME:([^,]+),(\d+),(\d+),(\d+)-->/g, (_, t, r, n, l) => chrome(t, r, n, l))
    .replace(/<!--RULE:(.+),(\w+)-->/g, (_, t, tone) => rule(t, tone))
    .replace(/<!--TIMER:(\d+)-->/g, (_, p) => timer(p))
    .replace(/<!--SCORE:([^,]+),([^-]+)-->/g, (_, v, l) => score(v, l))
    .replace(/<!--LIVES:(\d+)-->/g, (_, l) => lives(Number(l)))
    .replace(/<!--PET:(\w+)(?::(\d+))?-->/g, (_, m, size) =>
      pet(m, { size: size ? Number(size) : 200, label: 'Aylinin pet-i' }))
    .replace(/<!--PET-EAT:(\d+)-->/g, (_, size) =>
      pet('happy', { size: Number(size), mouthOpen: true, label: 'Pet ağzını açır' }));
}

function artboard(b) {
  return `<!doctype html>
<html>
<head>
  <meta charset="utf-8">
  <script src="./support.js"></script>
</head>
<body>
<x-dc>
<helmet>
  <link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Nunito:wght@400;700;800;900&display=swap">
  <style>
    /* ${b.label} */
    body {
      margin: 0;
      font-family: 'Nunito', ui-rounded, 'Segoe UI Variable', 'Segoe UI', system-ui, sans-serif;
      -webkit-font-smoothing: antialiased;

${tokens}
    }
    a { color: var(--pp-primary-dark); }
    a:hover { color: var(--pp-primary); }
  </style>
</helmet>
${expand(read(b.body).trimEnd())}
</x-dc>
<script data-dc-script data-props='{"$preview":{"width":${b.w},"height":${b.h}}}'>
class Component extends DCLogic {
  renderVals() {
    return {};
  }
}
</script>
</body>
</html>
`;
}

const P = { w: 390, h: 844 };

const builds = [
  { file: 'Main.dc.html',       body: 'hub.body.html',      label: 'Oyun rəfi',        ...P },
  { file: 'Qacis.dc.html',      body: 'run.body.html',      label: 'Ulduz qaçışı',     ...P },
  { file: 'Kes.dc.html',        body: 'slice.body.html',    label: 'Meyvə kəs',        ...P },
  { file: 'Sebet.dc.html',      body: 'catch.body.html',    label: 'Səbət tut',        ...P },
  { file: 'Tullanis.dc.html',   body: 'jump.body.html',     label: 'Bulud tullanışı',  ...P },
  { file: 'Herfler.dc.html',    body: 'letters.body.html',  label: 'Hərf ovu',         ...P },
  { file: 'Aspaz.dc.html',      body: 'chef.body.html',     label: 'Balaca aşpaz',     ...P },
  { file: 'Netice.dc.html',     body: 'result.body.html',   label: 'Nəticə',           ...P },
  { file: 'Yaddas.dc.html',     body: 'memory.body.html',   label: 'Yaddaş cütləri',   ...P },
  { file: 'Hesab.dc.html',      body: 'quick.body.html',    label: 'Sürətli hesab',    ...P },
  { file: 'Baloncuq.dc.html',   body: 'bubble.body.html',   label: 'Baloncuq ovu',     ...P },
  { file: 'RengSirasi.dc.html', body: 'echo.body.html',     label: 'Rəng sırası',      ...P },
  { file: 'Sistem.dc.html',     body: 'system.body.html',   label: 'Oyun xromu',       w: 880, h: 1240 },
];

for (const b of builds) {
  writeFileSync(join(out, b.file), artboard(b), 'utf8');
  console.log('yazıldı:', b.file);
}
