// Ekran gövdələrindən .dc.html artboardları yığır.
//
// İki iş görür:
//   1. Palitra tokenlərini helmet-ə yerləşdirir — B istiqaməti seçilib.
//   2. Təkrarlanan hissələri (alt naviqasiya, üst panel) şərh markerlərindən
//      açır, ona görə gövdə faylları qısa qalır.
//
// Marker sintaksisi qəsdən HTML şərhidir: dc runtime-ın {{ }} sintaksisi ilə
// toqquşmasın deyə.

import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const out = join(here, '..');
const read = (name) => readFileSync(join(here, name), 'utf8');

const PALETTE = 'tokens.b.css';

const tokens = read(PALETTE)
  .split('\n')
  .map((line) => (line.trim() ? '      ' + line.trim() : ''))
  .join('\n')
  .trimEnd();

/* ---------- ikonlar ---------- */

const ICON = {
  ev: '<path d="M4 11.2L12 4l8 7.2V20a1 1 0 0 1-1 1h-4.5v-6h-5v6H5a1 1 0 0 1-1-1z" stroke="COLOR" stroke-width="2" stroke-linejoin="round" />',
  oyren: '<path d="M4 5.5A2 2 0 0 1 6 4h5v15H6a2 2 0 0 0-2 1.5zM20 5.5A2 2 0 0 0 18 4h-5v15h5a2 2 0 0 1 2 1.5z" stroke="COLOR" stroke-width="2" stroke-linejoin="round" />',
  qulluq: '<path d="M12 20s-7-4.4-7-9.2A4 4 0 0 1 12 8a4 4 0 0 1 7 2.8C19 15.6 12 20 12 20z" stroke="COLOR" stroke-width="2" stroke-linejoin="round" />',
  dunya: '<path d="M9 5L3.5 7v12L9 17l6 2 5.5-2V5L15 7z" stroke="COLOR" stroke-width="2" stroke-linejoin="round" /><path d="M9 5v12M15 7v12" stroke="COLOR" stroke-width="2" />',
  kesf: '<path d="M20 4c0 9-5.4 13-9.5 13A4.5 4.5 0 0 1 6 12.5C6 7 12 4 20 4z" stroke="COLOR" stroke-width="2" stroke-linejoin="round" /><path d="M4 20c2.5-4.5 6-7.5 10-9.5" stroke="COLOR" stroke-width="2" stroke-linecap="round" />',
};

const NAV = [
  ['ev', 'Ev'],
  ['oyren', 'Öyrən'],
  ['qulluq', 'Otaq'],
  ['dunya', 'Dünya'],
  ['kesf', 'Kəşf'],
];

/** Alt naviqasiya. Aktiv bölmənin ikonu dairədə dolur və zolağın üstünə qalxır. */
function nav(active) {
  const items = NAV.map(([key, label]) => {
    const on = key === active;
    const glyph = ICON[key].replace(/COLOR/g, on ? '#FFFFFF' : 'var(--pp-ink-muted)');
    const holder = on
      ? 'display: inline-flex; align-items: center; justify-content: center; width: 46px; height: 46px; border-radius: 999px; background: linear-gradient(180deg, var(--pp-primary-lift) 0%, var(--pp-primary) 100%); box-shadow: 0 4px 0 var(--pp-primary-edge), var(--pp-gloss); transform: translateY(-16px) scale(1.06);'
      : 'display: inline-flex; align-items: center; justify-content: center; width: 46px; height: 46px; border-radius: 999px; background: var(--pp-card); box-shadow: 0 2px 0 var(--pp-edge-neutral);';
    const text = on
      ? 'font-size: 11px; font-weight: 900; color: var(--pp-primary-dark); transform: translateY(-14px);'
      : 'font-size: 11px; font-weight: 800; color: var(--pp-ink-soft);';
    return `    <div style="flex-grow: 1; display: flex; flex-direction: column; align-items: center; gap: 5px;">
      <span style="${holder}">
        <svg viewBox="0 0 24 24" width="24" height="24" fill="none" aria-hidden="true">${glyph}</svg>
      </span>
      <span style="${text}">${label}</span>
    </div>`;
  }).join('\n');

  return `  <div style="position: absolute; left: 0; right: 0; bottom: 0; display: flex; align-items: stretch; gap: 4px; padding: 22px 10px 14px; background: linear-gradient(180deg, transparent 0%, rgba(240, 235, 224, 0.55) 55%, rgba(240, 235, 224, 0.85) 100%); z-index: 20;">
${items}
  </div>`;
}

/** Üst panel: səviyyə halqası + ad solda, cüzdan sağda.
 *  variant "float" — səhnənin üstündə şüşə kapsullar; "flat" — adi zolaq. */
function topbar(stars, gems, variant) {
  const glass = variant === 'float';
  const capsule = glass
    ? 'background: var(--pp-glass); box-shadow: 0 3px 0 var(--pp-edge-neutral), var(--pp-shadow-sm), var(--pp-gloss);'
    : 'background: var(--pp-card); box-shadow: 0 3px 0 var(--pp-edge-neutral), var(--pp-shadow-sm);';
  const left = glass
    ? '<div style="display: flex; align-items: center; gap: 10px; height: 52px; padding: 0 16px 0 5px; border-radius: 999px; background: var(--pp-glass); box-shadow: 0 4px 0 var(--pp-edge-neutral), var(--pp-shadow-md), var(--pp-gloss);">'
    : '<div style="display: flex; align-items: center; gap: 10px;">';

  return `  <div style="position: absolute; top: 52px; left: 16px; right: 16px; display: flex; align-items: ${glass ? 'flex-start' : 'center'}; justify-content: space-between; gap: 12px; z-index: 10;">

    ${left}
      <div style="position: relative; width: 42px; height: 42px; flex-shrink: 0;">
        <svg viewBox="0 0 44 44" width="42" height="42" aria-hidden="true">
          <circle cx="22" cy="22" r="18" fill="none" stroke="var(--pp-track)" stroke-width="6" />
          <circle cx="22" cy="22" r="18" fill="none" stroke="var(--pp-primary)" stroke-width="6" stroke-linecap="round" stroke-dasharray="113" stroke-dashoffset="34" transform="rotate(-90 22 22)" />
          <circle cx="22" cy="22" r="14" fill="var(--pp-primary)" />
        </svg>
        <div style="position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; font-size: 16px; font-weight: 900; color: #FFFFFF;">7</div>
      </div>
      <div style="display: flex; flex-direction: column; gap: 2px;">
        <div style="font-size: 16px; font-weight: 900; line-height: 1;">Aylin</div>
        <div style="font-size: 11px; font-weight: 700; line-height: 1; color: var(--pp-ink-soft);">70 / 100 XP</div>
      </div>
    </div>

    <div style="display: flex; ${glass ? 'flex-direction: column; align-items: flex-end;' : 'align-items: center;'} gap: 8px;">
      <div style="display: flex; align-items: center; gap: 6px; height: ${glass ? '38px' : '34px'}; padding: 0 13px 0 8px; border-radius: 999px; ${capsule}">
        <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true">
          <path d="M12 3.2l2.6 5.6 6.1.8-4.5 4.2 1.2 6-5.4-3-5.4 3 1.2-6L3.3 9.6l6.1-.8z" fill="var(--pp-spark)" stroke="var(--pp-spark-edge)" stroke-width="1.6" stroke-linejoin="round" />
        </svg>
        <span style="font-size: 14px; font-weight: 900; color: var(--pp-spark-deep);">${stars}</span>
      </div>
      <div style="display: flex; align-items: center; gap: 6px; height: ${glass ? '38px' : '34px'}; padding: 0 13px 0 8px; border-radius: 999px; ${capsule}">
        <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true">
          <path d="M7.5 4h9l4 5-8.5 11L3.5 9z" fill="var(--pp-primary-lift)" stroke="var(--pp-primary-edge)" stroke-width="1.6" stroke-linejoin="round" />
          <path d="M7.5 4l4.5 5 4.5-5M3.5 9h17" fill="none" stroke="var(--pp-primary-edge)" stroke-width="1.4" />
        </svg>
        <span style="font-size: 14px; font-weight: 900; color: var(--pp-primary-dark);">${gems}</span>
      </div>
    </div>
  </div>`;
}

/** Pet SVG — PetAvatar.razor həndəsəsi, Young mərhələ (scale 0.94). */
function pet(mood, opts = {}) {
  const eyes =
    mood === 'sleepy'
      ? '<path d="M78 72q8 8 16 0" stroke="#2B2140" stroke-width="4" fill="none" stroke-linecap="round" /><path d="M106 72q8 8 16 0" stroke="#2B2140" stroke-width="4" fill="none" stroke-linecap="round" />'
      : '<path d="M78 74q8 -11 16 0" stroke="#2B2140" stroke-width="5" fill="none" stroke-linecap="round" /><path d="M106 74q8 -11 16 0" stroke="#2B2140" stroke-width="5" fill="none" stroke-linecap="round" />';

  const mouth = opts.mouthOpen
    ? '<ellipse cx="100" cy="97" rx="11" ry="9" fill="#2B2140" /><ellipse cx="100" cy="101" rx="6" ry="4.5" fill="#FF8FA0" />'
    : '<path d="M92 92q8 9 16 0" stroke="#2B2140" stroke-width="3.5" fill="none" stroke-linecap="round" />';

  const cheeks =
    mood === 'sleepy'
      ? ''
      : '<ellipse cx="72" cy="86" rx="7" ry="4.5" fill="#FF9EA8" opacity="0.65" /><ellipse cx="128" cy="86" rx="7" ry="4.5" fill="#FF9EA8" opacity="0.65" />';

  const zzz =
    mood === 'sleepy'
      ? '<path d="M138 42h16l-16 17h16" stroke="var(--pp-ink-soft)" stroke-width="3.5" fill="none" stroke-linecap="round" stroke-linejoin="round" /><path d="M158 24h11l-11 12h11" stroke="var(--pp-ink-muted)" stroke-width="3" fill="none" stroke-linecap="round" stroke-linejoin="round" />'
      : '';

  return `<svg viewBox="0 0 200 200" width="${opts.size || 250}" height="${opts.size || 250}" role="img" aria-label="${opts.label || 'Pet'}">
      <ellipse cx="100" cy="168" rx="43.24" ry="9" fill="rgba(0,0,0,0.12)" />
      <g transform="translate(100 162) scale(0.94) translate(-100 -162)">
        <path d="M132 132c22 4 34-10 34-26 0-12-7-22-16-24 4 10 2 22-6 29-7 6-15 8-22 8z" fill="#F3A05A" />
        <path d="M150 84c6 3 10 10 10 18 0 6-2 11-5 15 1-12-1-24-5-33z" fill="#FFFFFF" opacity="0.75" />
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
        ${opts.extra || ''}
      </g>
      ${zzz}
    </svg>`;
}

/* ---------- markerlərin açılması ---------- */

function expand(src) {
  return src
    .replace(/<!--NAV:(\w+)-->/g, (_, k) => nav(k))
    .replace(/<!--TOPBAR:([\w-]+),(\d+),(\d+)-->/g, (_, v, s, g) => topbar(s, g, v))
    .replace(/<!--PET:(\w+)(?::(\d+))?-->/g, (_, m, size) =>
      pet(m, { size: size ? Number(size) : 250, label: 'Aylinin pet-i' }))
    .replace(/<!--PET-EAT-->/g, pet('happy', { mouthOpen: true, label: 'Pet ağzını açır' }));
}

function artboard({ body, label, w, h }) {
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
    /* ${label} */
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
${expand(read(body).trimEnd())}
</x-dc>
<script data-dc-script data-props='{"$preview":{"width":${w},"height":${h}}}'>
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
  { file: 'Main.dc.html',     body: 'home.body.html',     label: 'Ev',              ...P },
  { file: 'Yem.dc.html',      body: 'feed.body.html',     label: 'Qulluq — yem',    ...P },
  { file: 'Cimizdir.dc.html', body: 'bath.body.html',     label: 'Qulluq — çimizdirmə', ...P },
  { file: 'Yuxu.dc.html',     body: 'sleep.body.html',    label: 'Qulluq — yuxu',   ...P },
  { file: 'Skaf.dc.html',     body: 'closet.body.html',   label: 'Qulluq — şkaf',   ...P },
  { file: 'Oyunlar.dc.html',  body: 'play.body.html',     label: 'Oyunlar',         ...P },
  { file: 'Oyren.dc.html',    body: 'learn.body.html',    label: 'Öyrən',           ...P },
  { file: 'Dunya.dc.html',    body: 'world.body.html',    label: 'Dünya',           ...P },
  { file: 'Kesf.dc.html',     body: 'explore.body.html',  label: 'Kəşf',            ...P },
  { file: 'Dostlar.dc.html',  body: 'friends.body.html',  label: 'Dostlar',         ...P },
  { file: 'Inkisaf.dc.html',  body: 'progress.body.html', label: 'İnkişaf',         ...P },
  { file: 'Profil.dc.html',   body: 'profile.body.html',  label: 'Profil və PIN',   ...P },
  { file: 'Valideyn.dc.html', body: 'parent.body.html',   label: 'Valideyn paneli', ...P },
];

for (const b of builds) {
  writeFileSync(join(out, b.file), artboard(b), 'utf8');
  console.log('yazıldı:', b.file);
}
