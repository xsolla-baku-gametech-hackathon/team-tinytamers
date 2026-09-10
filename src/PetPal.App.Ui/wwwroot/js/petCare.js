// Pet-ə birbaşa qulluq. Bütün hərəkət DOM-da baş verir — hər pointermove-da
// Blazor render etsəydi hərəkət kəkələyərdi. .NET tərəfə yalnız vəziyyət keçir.
//
// Üç rejim var:
//   drag  — əşyanı pet-in üstünə aparmaq (yem → ağız)
//   bath  — sabunu pet-in üstündə gəzdirib sabunlamaq, sonra su ilə yaxalamaq
//   yuxu  — pet yatağa uzanır, işıq sönəndə yatır (sürükləmə yoxdur)
//
// Pointer hadisələri işlədilir, HTML5 drag-and-drop yox: sonuncu toxunma
// ekranlarında etibarsızdır və uşaq telefonu əsas hədəfdir.

const state = {
    dotNet: null,
    mode: null,
    ghost: null,
    itemKey: null,
    zone: 'none',

    // bath
    sudsLayer: null,
    tool: null,
    lathered: null,     // xana → köpük elementləri
    rinsed: null,       // yuyulmuş xanalar
    lastLather: -1,
    lastRinse: -1,
    lastDropAt: 0,
    wetShown: false,   // parıltı və gölməçə bir dəfə göstərilir
};

/// Pet-in "ağız" sahəsi — SVG viewBox 0..200 daxilində nisbi qutu.
/// Baş ellipsi cx=100, cy=74; ağız ~92..108 x, ~84..100 y.
const MOUTH_BOX = { x0: 0.34, x1: 0.66, y0: 0.36, y1: 0.56 };

/// Çimizdirmə şəbəkəsi: pet 5x5 xanaya bölünür, hər xana əvvəlcə sabunlanır,
/// sonra yuyulur.
const SCRUB_GRID = 5;
const CELL_COUNT = SCRUB_GRID * SCRUB_GRID;

/// Su damcıları hər pointermove-da deyil, bu aralıqla düşür — əks halda
/// saniyədə yüzlərlə element yaranar və hərəkət ağırlaşar.
const DROP_INTERVAL_MS = 55;

export function init(dotNetRef) {
    state.dotNet = dotNetRef;
    resumeRoomFit();
}

/// Sürükləmə boyunca otağın miqyası DONDURULUR. Səbəb mobil brauzerdir:
/// barmaq ekranda gəzəndə ünvan zolağı gizlənib-görünür və hər dəfə `resize`
/// atılır — miqyas yenidən hesablansa, otaq (deməli pet də) barmağın altında
/// sürüşür və yem ağıza düşmür.
///
/// Cüt işlədilməlidir: əvvəl sürükləmə bitəndə yalnız dayandırılırdı və
/// dinləyici bir daha qoşulmurdu — yəni ilk yemdən sonra telefonu çevirmək
/// otağı ekrana yenidən oturtmurdu.
function pauseRoomFit() {
    window.removeEventListener('resize', fitRooms);
}

/// Eyni funksiya ikinci dəfə qoşulmur (brauzer eyni tip+funksiya cütünü
/// təkrar saymır), ona görə bunu çağırmaq həmişə təhlükəsizdir.
function resumeRoomFit() {
    window.addEventListener('resize', fitRooms);
}

// ============================== Otaqların miqyası ==============================
// Otaq illüstrasiyaları 390x620 piksel üçün çəkilib, ekran isə hər ölçüdə ola
// bilər. Qutunu DARTMIRIQ (mebel əyilər) — MİQYASLAYIRIQ: nisbət qorunur, artıq
// hissə kəsilir ("cover"). Əmsal JS-dədir, çünki CSS-də konteynerin ölçüsündən
// transform üçün lazım olan VAHİDSİZ ədədi çıxarmağın yolu yoxdur.

const ROOM_WIDTH = 390;

export function fitRooms() {
    for (const room of document.querySelectorAll('[data-room]')) {
        const host = room.parentElement;
        if (!host) { continue; }

        // Miqyas yalnız ENƏ görədir. Hündürlüyə görə "cover" etsək, uzun
        // ekranda otağın yanları kəsilir və lampa kimi TOXUNULAN əşyalar
        // kadrdan çıxır. Yuxarıda qalan boşluğu divarın özü doldurur —
        // divar elementləri qəsdən otaq qutusundan yuxarı uzanır.
        const scale = host.clientWidth / ROOM_WIDTH;

        room.style.setProperty('--room-scale', scale.toFixed(4));
    }
}

export function dispose() {
    pauseRoomFit();
    cancelDrag();
    stopBath();
    state.dotNet = null;
}

// ============================== 1. Əşyanı sürükləmək ==============================

/// visualId — sürüklənən şəklin data-drag-id atributu. Element tapılarsa kölgə
/// məhz ondan klonlanır, ona görə emoji yem də, SVG sabun da eyni yolla gedir.
export function startDrag(itemKey, icon, visualId, pointerId, clientX, clientY) {
    pauseRoomFit();
    cancelDrag();

    state.mode = 'drag';
    state.itemKey = itemKey;
    state.ghost = makeGhost(icon, visualId, clientX, clientY);

    listen(onDragMove, onDragUp);
}

function onDragMove(event) {
    if (!state.ghost) { return; }

    event.preventDefault();
    moveGhost(event.clientX, event.clientY);

    const zone = zoneAt(event.clientX, event.clientY);
    if (zone !== state.zone) {
        state.zone = zone;
        state.dotNet?.invokeMethodAsync('OnZoneChange', zone);
    }
}

function onDragUp(event) {
    if (!state.ghost) { return; }

    const zone = zoneAt(event.clientX, event.clientY);
    const itemKey = state.itemKey;

    // Əvvəl buraxılış, sonra təmizləmə: beləliklə pet ağzını qulluq başlayana
    // qədər açıq saxlayır və hadisə sırası intuitiv qalır.
    state.dotNet?.invokeMethodAsync('OnDrop', itemKey, zone);
    cancelDrag();
    resumeRoomFit();
}

function cancelDrag() {
    unlisten(onDragMove, onDragUp);

    state.ghost?.remove();
    state.ghost = null;
    state.itemKey = null;

    if (state.mode === 'drag') { state.mode = null; }

    if (state.zone !== 'none') {
        state.zone = 'none';
        state.dotNet?.invokeMethodAsync('OnZoneChange', 'none');
    }
}

// ============================== 2. Çimizdirmə ==============================
//
// İki addım və hər ikisi əl hərəkətidir — "təmizlə" düyməsi yoxdur:
//   1) sabun pet-in üstündə gəzdirilir → 5×5 xana köpüklə dolur
//   2) su pet-in üstündə gəzdirilir    → köpük yuyulur, damcılar tökülür
// "Təmizləndi" .NET-ə yalnız bütün köpük yuyulandan sonra gedir.

export function startBath() {
    stopBath();

    state.mode = 'bath';
    state.lathered = new Map();
    state.rinsed = new Set();
    state.lastLather = -1;
    state.lastRinse = -1;
}

/// <param name="kind">'soap' — sabunlamaq, 'water' — yaxalamaq.</param>
export function startBathTool(kind, visualId, pointerId, clientX, clientY) {
    if (state.mode !== 'bath') { return; }

    stopBathTool(false);
    ensureSudsLayer();

    state.tool = kind;
    state.ghost = makeGhost(kind === 'water' ? '🚿' : '🧼', visualId, clientX, clientY);
    state.ghost.classList.add(kind === 'water' ? 'care-ghost--water' : 'care-ghost--soap');

    // Duşdan tökülən axın barmağın arxasınca gedir — alət "işləyir" görünsün.
    if (kind === 'water') {
        const stream = document.createElement('div');
        stream.className = 'bath__ghost-stream';
        stream.appendChild(buildStream());
        state.ghost.appendChild(stream);
    }

    listen(onBathToolMove, onBathToolUp);
    applyToolAt(clientX, clientY);
}

function onBathToolMove(event) {
    if (!state.ghost) { return; }

    event.preventDefault();
    moveGhost(event.clientX, event.clientY);
    applyToolAt(event.clientX, event.clientY);
}

function onBathToolUp() {
    stopBathTool();
}

/// Alət buraxıldı — kölgə itir, amma köpük və yuyulmuş xanalar qalır: uşaq
/// barmağını qaldırıb sonra davam edə bilər.
///
/// notify: .NET-ə xəbər verilsin? Otaqdakı əşya (sabun/duş) alət əldə olanda
/// gizlənir, ona görə buraxılış Blazor-a çatmalıdır. Yeni alət götürüləndə
/// köhnəsi susqun bağlanır — yoxsa "buraxıldı" siqnalı elə həmin an gələn
/// "götürüldü" siqnalını ləğv edər.
function stopBathTool(notify = true) {
    const held = state.tool !== null;

    unlisten(onBathToolMove, onBathToolUp);
    state.ghost?.remove();
    state.ghost = null;
    state.tool = null;

    if (notify && held) { state.dotNet?.invokeMethodAsync("OnBathToolReleased"); }
}

/// Barmağın altındakı nöqtə pet-in BƏDƏNİNƏ düşürmü? SVG qutusu kvadratdır və
/// kənarlarında boş sahə var — bütün qutunu qəbul etsək, uşaq pet-in yanındakı
/// boşluğu sürtəndə də köpük yaranır. Ona görə qutunun içindən daha dar
/// düzbucaqlı götürülür və 5x5 şəbəkə məhz ona bölünür (yoxsa kənar xanalara
/// heç vaxt toxunulmur və "hamısı sabunlandı" şərti işə düşmür).
const BODY_BOX = { x0: 0.16, x1: 0.84, y0: 0.12, y1: 0.9 };

function cellIndex(value) {
    return Math.min(SCRUB_GRID - 1, Math.floor(value * SCRUB_GRID));
}

function applyToolAt(x, y) {
    const pet = petElement();
    if (!pet || !state.sudsLayer || !state.lathered) { return; }

    const box = pet.getBoundingClientRect();
    if (x < box.left || x > box.right || y < box.top || y > box.bottom) { return; }

    const rx = (x - box.left) / box.width;
    const ry = (y - box.top) / box.height;

    if (rx < BODY_BOX.x0 || rx > BODY_BOX.x1 || ry < BODY_BOX.y0 || ry > BODY_BOX.y1)
        return;

    const gx = (rx - BODY_BOX.x0) / (BODY_BOX.x1 - BODY_BOX.x0);
    const gy = (ry - BODY_BOX.y0) / (BODY_BOX.y1 - BODY_BOX.y0);
    // Sərhəddəki nöqtə (gx = 1) şəbəkədən kənara düşməsin deyə indeks kəsilir.
    const cell = `${cellIndex(gx)}:${cellIndex(gy)}`;

    if (state.tool === 'water') {
        rinseCell(cell, rx, ry);
    } else {
        latherCell(cell, rx, ry);
    }
}

/// Sabun bu xanadan ilk dəfə keçdi — qaymaq kimi köpük qalır. Bir xana = bir
/// köpük topası (`.fx__foam`), dairə yığını deyil: uşaq bədəndə köpüyün
/// YAYILDIĞINI görməlidir.
function latherCell(cell, rx, ry) {
    if (state.lathered.has(cell)) { return; }

    const foam = fxAt(rx, ry, 30 + Math.random() * 18);
    foam.appendChild(buildFoam());
    state.sudsLayer.appendChild(foam);

    state.lathered.set(cell, [foam]);

    // Hər üçüncü xanada bir sabun qabarcığı yuxarı süzülür — köpük "diri" olsun.
    if (state.lathered.size % 3 === 0) { spawnSoapBubble(rx, ry); }

    const percent = Math.min(100, Math.round((state.lathered.size / CELL_COUNT) * 100));

    // Hər xanada deyil, hər 8%-də bildiririk — Blazor lazımsız render etməsin.
    if (percent === 100 || percent - state.lastLather >= 8) {
        state.lastLather = percent;
        state.dotNet?.invokeMethodAsync('OnLatherProgress', percent);
    }

    // Bütün bədən sabunlandı: sabun əldən düşür, növbə suyundur.
    if (state.lathered.size >= CELL_COUNT) { stopBathTool(); }
}

/// Sabundan qopan qabarcıq yuxarı süzülüb partlayır. Animasiya təkrarlanandır,
/// ona görə element vaxtla silinir.
function spawnSoapBubble(rx, ry) {
    const host = fxAt(
        clamp01(rx + (Math.random() - 0.5) * 0.1),
        clamp01(ry - 0.06),
        20 + Math.random() * 16);

    host.appendChild(buildBubble());
    state.sudsLayer.appendChild(host);

    window.setTimeout(() => host.remove(), 2600);
}

/// Su bu xanadan keçdi — sıçrayış və damcılar hər halda olur, köpük varsa yuyulur.
function rinseCell(cell, rx, ry) {
    spawnDrops(rx, ry);

    const foam = state.lathered.get(cell);
    if (!foam || state.rinsed.has(cell)) { return; }

    state.rinsed.add(cell);
    spawnSplash(rx, ry);

    for (const host of foam) {
        host.classList.add('bath__fx--washed');
        host.addEventListener('animationend', () => host.remove());
    }

    const total = Math.max(1, state.lathered.size);
    const percent = Math.min(100, Math.round((state.rinsed.size / total) * 100));

    if (percent === 100 || percent - state.lastRinse >= 8) {
        state.lastRinse = percent;
        state.dotNet?.invokeMethodAsync('OnRinseProgress', percent);
    }

    if (state.rinsed.size >= total) { finishRinse(); }
}

/// Suyun bədənə dəydiyi yer: yastı tac və yana uçan damcılar.
function spawnSplash(rx, ry) {
    const host = fxAt(rx, ry, 54 + Math.random() * 26, 26);
    host.appendChild(buildSplash());
    state.sudsLayer.appendChild(host);

    window.setTimeout(() => host.remove(), 720);
}

/// Suyun düşdüyü yerdə aşağı qaçan damcılar. Ölçü və məsafə təsadüfidir ki,
/// axın mexaniki görünməsin. İlk toxunuşda pet islanır: parıltı bir dəfə
/// bədəndən keçir, ayaqların altında isə gölməçə qalır.
function spawnDrops(rx, ry) {
    const now = performance.now();
    if (now - state.lastDropAt < DROP_INTERVAL_MS) { return; }
    state.lastDropAt = now;

    petElement()?.classList.add('pet--wet');

    if (!state.wetShown) {
        state.wetShown = true;
        spawnSheen();
        spawnPuddle();
    }

    for (let i = 0; i < 2; i++) {
        const host = fxAt(
            clamp01(rx + (Math.random() - 0.5) * 0.18),
            clamp01(ry - 0.04),
            9 + Math.random() * 6,
            54 + Math.random() * 44);

        host.appendChild(buildDrop());
        state.sudsLayer.appendChild(host);

        window.setTimeout(() => host.remove(), 940);
    }
}

/// Yaş tükdən keçən işıq zolağı — bir dəfə, suyun ilk toxunuşunda.
function spawnSheen() {
    const box = petElement()?.getBoundingClientRect();
    if (!box) { return; }

    const host = fxAt(0.5, 0.46, box.width * 0.78, box.height * 0.68);
    host.appendChild(buildSheen());
    state.sudsLayer.appendChild(host);

    window.setTimeout(() => host.remove(), 1500);
}

/// Ayaqların altındakı gölməçə — çimizdirmə bitənə qədər qalır.
function spawnPuddle() {
    const box = petElement()?.getBoundingClientRect();
    if (!box) { return; }

    const host = fxAt(0.5, 0.97, box.width * 0.72, 34);
    host.appendChild(buildPuddle());
    state.sudsLayer.appendChild(host);
}
/// Son xana da yuyuldu: qalan köpük aşağı axır, sonra .NET təmizlənməni qeydə alır.
function finishRinse() {
    stopBathTool();
    state.sudsLayer?.classList.add('bath__suds--rinsing');

    window.setTimeout(() => {
        stopBath();
        state.dotNet?.invokeMethodAsync('OnRinsed');
    }, 600);
}

function ensureSudsLayer() {
    const pet = petElement();
    if (!pet) { return; }

    if (!state.sudsLayer) {
        state.sudsLayer = document.createElement('div');
        state.sudsLayer.className = 'bath__suds';
        pet.parentElement.appendChild(state.sudsLayer);
    }

    syncSudsLayer();
}

export function stopBath() {
    stopBathTool();

    state.sudsLayer?.remove();
    state.sudsLayer = null;
    state.lathered = null;
    state.rinsed = null;
    state.lastLather = -1;
    state.lastRinse = -1;
    state.wetShown = false;

    petElement()?.classList.remove('pet--wet');

    if (state.mode === 'bath') { state.mode = null; }
}

/// Köpük qatı düz pet-in üstünə oturur — köpüklərin faizlə verilən mövqeyi
/// yalnız bu halda bədənə düşür.
function syncSudsLayer() {
    const pet = petElement();
    const layer = state.sudsLayer;
    if (!pet || !layer) { return; }

    const host = layer.offsetParent ?? pet.parentElement;
    const petBox = pet.getBoundingClientRect();
    const hostBox = host.getBoundingClientRect();

    // Otaq transform ilə miqyaslanır, ona görə ekran pikselini qatın ÖZ
    // koordinat sisteminə çeviririk — yoxsa köpük pet-dən sürüşür.
    const k = host.offsetWidth > 0 ? hostBox.width / host.offsetWidth : 1;

    layer.style.left = `${(petBox.left - hostBox.left) / k}px`;
    layer.style.top = `${(petBox.top - hostBox.top) / k}px`;
    layer.style.width = `${petBox.width / k}px`;
    layer.style.height = `${petBox.height / k}px`;
}

// ============================== Yuma effektləri ==============================
// Elementlərin şəkli CSS-dədir (`.fx__*`, Codex ilə çəkilib), quruluşu isə
// burada: hər effekt öz uşaqlarından ibarətdir və köpük qatında faizlə
// yerləşdirilir. Ölçü `--fx-size` / `--fx-height` ilə verilir.

/// Effekti köpük qatında (rx, ry) nöqtəsində mərkəzləyən sarğı.
function fxAt(rx, ry, size, height) {
    const host = document.createElement('div');
    host.className = 'bath__fx';
    host.style.left = `${rx * 100}%`;
    host.style.top = `${ry * 100}%`;

    if (size) { host.style.setProperty('--fx-size', `${Math.round(size)}px`); }
    if (height) { host.style.setProperty('--fx-height', `${Math.round(height)}px`); }

    return host;
}

/// Nömrələnmiş uşaq elementlərini doldurur: ad, ad--1 … ad--n.
function fillParts(parent, className, count, tag) {
    for (let index = 1; index <= count; index++) {
        const part = document.createElement(tag ?? 'i');
        part.className = `${className} ${className}--${index}`;
        parent.appendChild(part);
    }
}

function buildFoam() {
    const foam = document.createElement('div');
    foam.className = 'fx__foam';
    fillParts(foam, 'fx__foam-blob', 6);
    return foam;
}

function buildBubble() {
    const bubble = document.createElement('div');
    bubble.className = 'fx__bubble';

    for (const part of ['core', 'highlight', 'sheen']) {
        const span = document.createElement('span');
        span.className = `fx__bubble-${part}`;
        bubble.appendChild(span);
    }

    const fragments = document.createElement('span');
    fragments.className = 'fx__bubble-fragments';
    fillParts(fragments, 'fx__bubble-fragment', 6);
    bubble.appendChild(fragments);

    return bubble;
}

function buildSplash() {
    const splash = document.createElement('div');
    splash.className = 'fx__splash';

    for (const part of ['ring', 'crown']) {
        const span = document.createElement('span');
        span.className = `fx__splash-${part}`;
        splash.appendChild(span);
    }

    fillParts(splash, 'fx__splash-drop', 4, 'span');
    return splash;
}

function buildDrop() {
    const drop = document.createElement('div');
    drop.className = 'fx__drop';

    const bead = document.createElement('span');
    bead.className = 'fx__drop-bead';
    drop.appendChild(bead);

    return drop;
}

function buildStream() {
    const stream = document.createElement('div');
    stream.className = 'fx__stream';

    const streak = document.createElement('span');
    streak.className = 'fx__stream-streak';
    stream.appendChild(streak);

    return stream;
}

function buildSheen() {
    const sheen = document.createElement('div');
    sheen.className = 'fx__sheen';

    for (const part of ['band', 'gloss']) {
        const span = document.createElement('span');
        span.className = `fx__sheen-${part}`;
        sheen.appendChild(span);
    }

    return sheen;
}

function buildPuddle() {
    const puddle = document.createElement('div');
    puddle.className = 'fx__puddle';

    for (const part of ['water', 'ripple']) {
        const span = document.createElement('span');
        span.className = `fx__puddle-${part}`;
        puddle.appendChild(span);
    }

    return puddle;
}



// ============================== Ortaq köməkçilər ==============================

function petElement() {
    return document.querySelector('[data-pet-target]');
}

function makeGhost(icon, visualId, x, y) {
    const ghost = document.createElement('div');
    ghost.className = 'care-ghost';

    const visual = visualId ? document.querySelector(`[data-drag-id="${visualId}"]`) : null;
    if (visual) {
        const clone = visual.cloneNode(true);
        clone.removeAttribute('data-drag-id');
        ghost.appendChild(clone);
    } else {
        ghost.textContent = icon;
    }

    document.body.appendChild(ghost);
    state.ghost = ghost;
    moveGhost(x, y);
    return ghost;
}

function moveGhost(x, y) {
    state.ghost.style.left = `${x}px`;
    state.ghost.style.top = `${y}px`;
}

/// Nöqtənin pet-in hansı hissəsinə düşdüyünü hesablayır.
function zoneAt(x, y) {
    const pet = petElement();
    if (!pet) { return 'none'; }

    const box = pet.getBoundingClientRect();
    if (x < box.left || x > box.right || y < box.top || y > box.bottom) { return 'none'; }

    const rx = (x - box.left) / box.width;
    const ry = (y - box.top) / box.height;

    const inMouth = rx >= MOUTH_BOX.x0 && rx <= MOUTH_BOX.x1 && ry >= MOUTH_BOX.y0 && ry <= MOUTH_BOX.y1;
    return inMouth ? 'mouth' : 'body';
}

function clamp01(value) {
    return Math.min(1, Math.max(0, value));
}

function listen(onMove, onUp) {
    window.addEventListener('pointermove', onMove, { passive: false });
    window.addEventListener('pointerup', onUp);
    window.addEventListener('pointercancel', onUp);
}

function unlisten(onMove, onUp) {
    window.removeEventListener('pointermove', onMove);
    window.removeEventListener('pointerup', onUp);
    window.removeEventListener('pointercancel', onUp);
}
