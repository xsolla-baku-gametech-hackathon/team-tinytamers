// Oyunda əşyanı sürükləmək: rəfdən götür, hədəfin üstündə burax.
//
// petCare.js-dəki sürükləmə pet-in BƏDƏNİNƏ bağlıdır (ağız / gövdə), burada isə
// hədəf adi DOM elementidir — ona görə ayrı modul. Ortaq qərar eynidir: HTML5
// drag-and-drop YOX, pointer hadisələri. Sonuncu toxunma ekranlarında
// etibarsızdır, uşaq telefonu isə əsas hədəfdir.
//
// Hərəkət DOM-da baş verir — hər pointermove-da Blazor render etsəydi, kölgə
// barmağın arxasınca kəkələyərək gedərdi. .NET tərəfə yalnız iki xəbər gedir:
// hədəfin üstünə girdik/çıxdıq (OnDragOver) və barmaq qalxdı (OnDragDrop).

const state = {
    dotNet: null,
    ghost: null,
    over: false,
};

/// Sürükləməni başladır. Hədəf `[data-drop-zone]` atributu olan elementdir.
export function start(dotNetRef, icon, clientX, clientY) {
    stop();

    state.dotNet = dotNetRef;
    state.over = false;
    state.ghost = makeGhost(icon, clientX, clientY);

    // passive: false — pointermove-da preventDefault çağırılır, əks halda
    // brauzer barmağı öz sürüşdürmə jestinə aparır.
    window.addEventListener('pointermove', onMove, { passive: false });
    window.addEventListener('pointerup', onUp);
    window.addEventListener('pointercancel', onUp);
}

/// Komponent bağlananda da çağırılır — yarımçıq sürükləmədən kölgə qalmasın.
export function stop() {
    window.removeEventListener('pointermove', onMove);
    window.removeEventListener('pointerup', onUp);
    window.removeEventListener('pointercancel', onUp);

    state.ghost?.remove();
    state.ghost = null;
    state.dotNet = null;
    state.over = false;
}

function onMove(event) {
    if (!state.ghost) { return; }

    event.preventDefault();
    moveGhost(event.clientX, event.clientY);

    // Xəbər yalnız SƏRHƏD keçiləndə gedir, hər hərəkətdə yox.
    const over = isOverZone(event.clientX, event.clientY);
    if (over !== state.over) {
        state.over = over;
        state.dotNet?.invokeMethodAsync('OnDragOver', over);
    }
}

function onUp(event) {
    if (!state.ghost) { return; }

    const dotNet = state.dotNet;
    const over = isOverZone(event.clientX, event.clientY);

    // Əvvəl kölgə götürülür, sonra xəbər gedir: .NET tərəf boşqabı yeniləyəndə
    // barmağın altında köhnə əşya qalmasın.
    stop();
    dotNet?.invokeMethodAsync('OnDragDrop', over);
}

/// Nöqtə testi `elementFromPoint` ilə aparılır, qutu ilə yox: boşqab
/// DAİRƏDİR — qutu testi künclərdə "düşdü" deyərdi, uşaq isə əşyanı
/// dairənin kənarına buraxdığını görürdü. Kölgənin özü `pointer-events: none`
/// olduğuna görə testə qarışmır.
function isOverZone(x, y) {
    return document.elementFromPoint(x, y)?.closest('[data-drop-zone]') != null;
}

function makeGhost(icon, x, y) {
    const ghost = document.createElement('div');
    ghost.className = 'drag-ghost';
    ghost.textContent = icon;

    document.body.appendChild(ghost);
    state.ghost = ghost;
    moveGhost(x, y);

    return ghost;
}

function moveGhost(x, y) {
    state.ghost.style.left = `${x}px`;
    state.ghost.style.top = `${y}px`;
}
