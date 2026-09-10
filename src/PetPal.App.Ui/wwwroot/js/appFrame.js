// ==================== KƏTANIN EKRANA SIĞIŞDIRILMASI ====================
//
// App-in bütün ekranları 390×690 piksellik kətan üçün çəkilib (bax theme.css
// → --pp-zoom). Bu skript həmin kətanın cari ekranda neçəyə vurulmalı olduğunu
// hesablayır və `--pp-zoom` dəyişəninə yazır; qalan işi CSS görür.
//
// Niyə CSS media query kifayət deyil: onlar yalnız pillə-pillə işləyir və hər
// pillə aralığın ən dar ucuna görə seçilməlidir, yəni 389 pikselli telefon
// 360-lıq kimi kiçildilir. Burada dəyər DƏQİQ hesablanır — ekranın hər pikseli
// istifadə olunur. Media query-lər isə yerində qalır: bu skript yüklənməmişdən
// əvvəl (Blazor WASM açılana qədər) ekran onlarla düzgün görünür.
//
// Blazor-dan asılı deyil, ona görə splash ekranında da işləyir.
(function () {
    'use strict';

    var DESIGN_W = 390;     // kətanın eni
    var DESIGN_H = 690;     // tam interfeysin (HUD + səhnə + naviqasiya) minimum hündürlüyü
    var MAX = 1.30;         // kompüterdə/planşetdə həddindən artıq böyüməsin
    var MIN = 0.45;         // yan tərəfə çevrilmiş telefonda da interfeys BÜTÖV qalsın

    // app.css-dəki `@media (min-width: 560px)` ilə eyni olmalıdır: orada kətan
    // ətrafında 24 piksel boşluq buraxılır və çərçivə kimi göstərilir.
    var DESKTOP_FROM = 560;
    var DESKTOP_PAD = 24;

    // Telefonda klaviatura açılanda (qeydiyyat, söhbət) Android WebView layout
    // hündürlüyünü yarıya endirir. Ona görə eyni en üçün görülmüş ƏN BÖYÜK
    // hündürlük yadda saxlanılır: yoxsa uşaq yazmağa başlayan kimi bütün ekran
    // gözü qarşısında kiçilərdi. En dəyişəndə (cihaz çevrilib) ölçü sıfırlanır.
    var lastWidth = 0;
    var tallest = 0;

    function fit() {
        var root = document.documentElement;
        var vw = root.clientWidth;
        var vh = root.clientHeight;
        if (!vw || !vh) { return; }

        var desktop = vw >= DESKTOP_FROM;
        if (vw !== lastWidth) { lastWidth = vw; tallest = vh; }

        // Kompüterdə pəncərənin hündürlüyünü İSTİFADƏÇİ dəyişir — orada hər
        // dəyişiklik nəzərə alınmalıdır; telefonda isə azalma klaviaturadır.
        if (desktop) { tallest = vh; }
        else if (vh > tallest) { tallest = vh; }

        var pad = desktop ? DESKTOP_PAD : 0;
        var raw = Math.min(vw / DESIGN_W, (tallest - pad) / DESIGN_H, MAX);

        // Ünvan zolağı gizlənəndə hündürlük bir neçə piksel dəyişir; dəyəri
        // 0.005-lik pillələrə yuvarlaqlaşdırmasaq, ekran həmin anda titrəyir.
        // Yuvarlaqlaşdırma AŞAĞIdır: yuxarı yuvarlaqlaşsaydı, kətan dizayn
        // ölçüsündən bir neçə piksel dar qalar və sağ kənar kəsilərdi.
        var zoom = Math.max(MIN, Math.floor(raw * 200) / 200);

        if (root.style.getPropertyValue('--pp-zoom') !== String(zoom)) {
            root.style.setProperty('--pp-zoom', String(zoom));
        }
    }

    fit();

    window.addEventListener('resize', fit, { passive: true });
    window.addEventListener('orientationchange', fit, { passive: true });

    // Klaviatura açılanda visualViewport dəyişir, window.resize isə hər
    // brauzerdə tetiklənmir.
    if (window.visualViewport) {
        window.visualViewport.addEventListener('resize', fit, { passive: true });
    }
})();
