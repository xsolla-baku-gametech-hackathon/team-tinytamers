namespace PetPal.Shared.Scenes;

/// <summary>
/// Ay macəralarının səhnə AİLƏSİ — bir MƏKAN, bir dekor qaydası.
///
/// <para>Hekayə yetmişdən çox səhnə variantı yaradır, çünki variant hekayənin
/// ANIdır: «baza qaranlıqdır» və «baza oyandı» eyni dəhlizin iki kadrıdır.
/// Dekor isə məkana bağlıdır. Ailə bu iki səviyyəni ayırır: variant nə vaxt
/// olduğumuzu, ailə isə harada olduğumuzu deyir.</para>
/// </summary>
public enum MoonSceneFamily
{
    /// <summary>Gecə pəncərəsi — macəra hələ Yerdədir.</summary>
    EarthNight,

    /// <summary>Ev laboratoriyası: siqnal masası və antena.</summary>
    EarthDesk,

    /// <summary>Çanta masası — uşaq alət seçir.</summary>
    KitTable,

    /// <summary>Start meydanı.</summary>
    Launch,

    /// <summary>Ay düzündə eniş — bazanın çölü.</summary>
    Arrival,

    /// <summary>Bazanın İÇİ: dəhliz, panel, xəritə divarı, rabitə, təmir.</summary>
    BaseInside,

    /// <summary>Yolların ayrıldığı təpə.</summary>
    Fork,

    /// <summary>Kristal mağarası.</summary>
    Cave,

    /// <summary>Kölgəli krater.</summary>
    Crater,

    /// <summary>Gizli keçid — yalnız xəritə ilə açılır.</summary>
    Passage,

    /// <summary>Roverin izləri və özü.</summary>
    RoverSite,

    /// <summary>Ay bağçası.</summary>
    Garden,

    /// <summary>Rəsədxananın zalı — ipucu lövhəsi.</summary>
    Observatory,

    /// <summary>Rəsədxananın nüvəsi — parçaların yuvası.</summary>
    Core,

    /// <summary>İşığın qayıtdığı an və sonluqlar.</summary>
    Finale
}

/// <summary>
/// Serverin verdiyi səhnə açarı ilə ekranın çəkdiyi dekor arasındakı
/// <b>MÜQAVİLƏ</b>.
///
/// <para>Paylaşılan layihədə saxlanılır, çünki hər iki tərəf ona baxır: server
/// açarı hekayədən qaytarır, ekran isə dekoru buradan seçir. Əvvəl siyahı
/// yalnız UI-da idi və qısa macəranın on iki açarını tanıyırdı — uzun macəranın
/// yeni açarları səssizcə «eniş» səhnəsinə düşürdü. Müqaviləni ortaya
/// çıxarmaq həmin boşluğu testlə tutula bilən edir.</para>
///
/// <para>Naməlum açar səssizcə ÜMUMİ ailəyə düşür (ekran heç vaxt boş
/// qalmamalıdır), amma müqavilə testi hekayədə istifadə olunub burada olmayan
/// açarı dərhal tapır.</para>
/// </summary>
public static class MoonSceneVariants
{
    private static readonly Dictionary<string, MoonSceneFamily> Map = new(StringComparer.Ordinal)
    {
        ["moon-window-night"] = MoonSceneFamily.EarthNight,
        ["moon-window-listen"] = MoonSceneFamily.EarthNight,

        ["moon-signal-desk"] = MoonSceneFamily.EarthDesk,
        ["moon-signal-wave"] = MoonSceneFamily.EarthDesk,
        ["moon-rhythm-core"] = MoonSceneFamily.EarthDesk,

        ["moon-kit-table"] = MoonSceneFamily.KitTable,
        ["moon-launch"] = MoonSceneFamily.Launch,
        ["moon-arrival"] = MoonSceneFamily.Arrival,

        ["moon-base-dark"] = MoonSceneFamily.BaseInside,
        ["moon-base-log"] = MoonSceneFamily.BaseInside,
        ["moon-base-awake"] = MoonSceneFamily.BaseInside,
        ["moon-corridor-bot"] = MoonSceneFamily.BaseInside,
        ["moon-corridor-happy"] = MoonSceneFamily.BaseInside,
        ["moon-power-panel"] = MoonSceneFamily.BaseInside,
        ["moon-power-lines"] = MoonSceneFamily.BaseInside,
        ["moon-map-wall"] = MoonSceneFamily.BaseInside,
        ["moon-map-glow"] = MoonSceneFamily.BaseInside,
        ["moon-comms-room"] = MoonSceneFamily.BaseInside,
        ["moon-repair-station"] = MoonSceneFamily.BaseInside,
        ["moon-crystal-broken"] = MoonSceneFamily.BaseInside,

        ["moon-fork"] = MoonSceneFamily.Fork,
        ["moon-three-craters"] = MoonSceneFamily.Fork,

        ["moon-cave-lights"] = MoonSceneFamily.Cave,
        ["moon-cave-echo"] = MoonSceneFamily.Cave,
        ["moon-cave-bridge"] = MoonSceneFamily.Cave,
        ["moon-cave-follow"] = MoonSceneFamily.Cave,
        ["moon-cave-song-echo"] = MoonSceneFamily.Cave,
        ["moon-shard-cave"] = MoonSceneFamily.Cave,
        ["moon-echo-walls"] = MoonSceneFamily.Cave,
        ["moon-deep-hum"] = MoonSceneFamily.Cave,

        ["moon-crater-descend"] = MoonSceneFamily.Crater,
        ["moon-crater-dark"] = MoonSceneFamily.Crater,
        ["moon-crater-lit"] = MoonSceneFamily.Crater,
        ["moon-crater-crossing"] = MoonSceneFamily.Crater,
        ["moon-shard-crater"] = MoonSceneFamily.Crater,
        ["moon-light-ribbon"] = MoonSceneFamily.Crater,
        ["moon-mirror-field"] = MoonSceneFamily.Crater,

        ["moon-hidden-passage"] = MoonSceneFamily.Passage,

        ["moon-tracks-east"] = MoonSceneFamily.RoverSite,
        ["moon-track-compare"] = MoonSceneFamily.RoverSite,
        ["moon-tracker-eye"] = MoonSceneFamily.RoverSite,
        ["moon-rover-found"] = MoonSceneFamily.RoverSite,
        ["moon-rover-base"] = MoonSceneFamily.RoverSite,
        ["moon-rover-memory"] = MoonSceneFamily.RoverSite,
        ["moon-rover-team"] = MoonSceneFamily.RoverSite,
        ["moon-rover-helps"] = MoonSceneFamily.RoverSite,
        ["moon-memory-playback"] = MoonSceneFamily.RoverSite,
        ["moon-carry"] = MoonSceneFamily.RoverSite,

        ["moon-garden-glow"] = MoonSceneFamily.Garden,
        ["moon-plant-seed"] = MoonSceneFamily.Garden,

        ["moon-board-log"] = MoonSceneFamily.Observatory,
        ["moon-board-rover"] = MoonSceneFamily.Observatory,
        ["moon-board-science"] = MoonSceneFamily.Observatory,
        ["moon-clue-board"] = MoonSceneFamily.Observatory,
        ["moon-truth-reveal"] = MoonSceneFamily.Observatory,
        ["moon-shards-join"] = MoonSceneFamily.Observatory,
        ["moon-shield-prep"] = MoonSceneFamily.Observatory,

        ["moon-observatory-core"] = MoonSceneFamily.Core,
        ["moon-socket-table"] = MoonSceneFamily.Core,
        ["moon-circuit-ready"] = MoonSceneFamily.Core,
        ["moon-circuit-upgraded"] = MoonSceneFamily.Core,
        ["moon-circuit-hands"] = MoonSceneFamily.Core,
        ["moon-crystal-bright"] = MoonSceneFamily.Core,

        ["moon-light-returns"] = MoonSceneFamily.Finale,
        ["moon-pet-helps"] = MoonSceneFamily.Finale,
        ["moon-quiet-moment"] = MoonSceneFamily.Finale,
        ["moon-crystal-glow"] = MoonSceneFamily.Finale,
        ["moon-summit-finale"] = MoonSceneFamily.Finale,
        ["moon-signal-finale"] = MoonSceneFamily.Finale,
        ["moon-lantern-finale"] = MoonSceneFamily.Finale,
        ["moon-ending-guardian"] = MoonSceneFamily.Finale,
        ["moon-ending-explorer"] = MoonSceneFamily.Finale,
        ["moon-ending-robots"] = MoonSceneFamily.Finale
    };

    /// <summary>Tanınan bütün açarlar — müqavilə testi bunu hekayə ilə tutuşdurur.</summary>
    public static IReadOnlyCollection<string> All => Map.Keys;

    public static bool Knows(string? sceneVariant) =>
        sceneVariant is not null && Map.ContainsKey(sceneVariant);

    /// <summary>
    /// Açarın ailəsi. Naməlum açar <see cref="MoonSceneFamily.Arrival"/>
    /// qaytarır: ekran boş qalmaqdansa Ay düzünü çəkməlidir.
    /// </summary>
    public static MoonSceneFamily FamilyOf(string? sceneVariant) =>
        sceneVariant is not null && Map.TryGetValue(sceneVariant, out var family)
            ? family
            : MoonSceneFamily.Arrival;

    /// <summary>CSS sinfi üçün kiçik hərfli ad — <c>base-inside</c>, <c>rover-site</c>.</summary>
    public static string CssName(MoonSceneFamily family) => family switch
    {
        MoonSceneFamily.EarthNight => "earth-night",
        MoonSceneFamily.EarthDesk => "earth-desk",
        MoonSceneFamily.KitTable => "kit-table",
        MoonSceneFamily.Launch => "launch",
        MoonSceneFamily.Arrival => "arrival",
        MoonSceneFamily.BaseInside => "base-inside",
        MoonSceneFamily.Fork => "fork",
        MoonSceneFamily.Cave => "cave",
        MoonSceneFamily.Crater => "crater",
        MoonSceneFamily.Passage => "passage",
        MoonSceneFamily.RoverSite => "rover-site",
        MoonSceneFamily.Garden => "garden",
        MoonSceneFamily.Observatory => "observatory",
        MoonSceneFamily.Core => "core",
        MoonSceneFamily.Finale => "finale",
        _ => "arrival"
    };

    /// <summary>Səhnə Yerdə keçirmi — göy, ulduzlar və relyef fərqlidir.</summary>
    public static bool IsOnEarth(MoonSceneFamily family) =>
        family is MoonSceneFamily.EarthNight or MoonSceneFamily.EarthDesk
            or MoonSceneFamily.KitTable or MoonSceneFamily.Launch;

    /// <summary>Səhnə QAPALI məkandadırmı — açıq səma çəkilmir.</summary>
    public static bool IsIndoors(MoonSceneFamily family) =>
        family is MoonSceneFamily.BaseInside or MoonSceneFamily.Cave
            or MoonSceneFamily.Passage or MoonSceneFamily.Observatory
            or MoonSceneFamily.Core or MoonSceneFamily.EarthDesk
            or MoonSceneFamily.KitTable;
}
