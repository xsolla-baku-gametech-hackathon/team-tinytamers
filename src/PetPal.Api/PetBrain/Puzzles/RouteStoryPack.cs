using PetPal.Api.Common;

namespace PetPal.Api.PetBrain.Puzzles;

/// <summary>
/// Marşrut mexanikasının HEKAYƏ paketi: eyni qaydalar, başqa dünya.
///
/// <para>Mexanika (enerji büdcəsi, məcburi düyün, tələ yolu, həllin tapılması)
/// bir dənədir və <see cref="DeterministicPuzzleGenerator"/>-dədir. Uşağın
/// OXUDUĞU hər söz isə buradadır — ona görə yeni macərəyə marşrut tapmacası
/// vermək üçün qayda yazmaq lazım deyil, paket yazmaq kifayətdir.</para>
///
/// <para>Bu ayrılıq olmadan Ay macərası Mars mətnini alırdı: kataloqda bir
/// dənə marşrut şablonu vardı və o, açıq-aşkar Marsın hekayəsini danışırdı.</para>
/// </summary>
/// <param name="BlueprintKey">Hansı şablonun mətnidir.</param>
/// <param name="StartAz">Başlanğıc düyünün adı.</param>
/// <param name="RidgeAz">Birinci yaxınlaşma yolu.</param>
/// <param name="DuneAz">İkinci yaxınlaşma yolu.</param>
/// <param name="RechargeAz">Enerji doldurulan yer.</param>
/// <param name="RequiredAz">Məcburi keçilməli düyün — hekayənin şərti.</param>
/// <param name="DecoyAz">İnandırıcı, amma məqsədə aparmayan yol.</param>
/// <param name="GoalAz">Hədəf.</param>
/// <param name="ShelfAz">Çətin pillədə əlavə olunan uzun yol.</param>
public sealed record RouteStoryPack(
    string BlueprintKey,
    string StartAz, string StartEn, string StartIcon,
    string RidgeAz, string RidgeEn, string RidgeIcon,
    string DuneAz, string DuneEn, string DuneIcon,
    string RechargeAz, string RechargeEn, string RechargeIcon,
    string RequiredAz, string RequiredEn, string RequiredIcon,
    string DecoyAz, string DecoyEn, string DecoyIcon,
    string GoalAz, string GoalEn, string GoalIcon,
    string ShelfAz, string ShelfEn, string ShelfIcon,
    string TitleAz, string TitleEn,
    string StoryAz, string StoryEn,
    string InstructionAz, string InstructionEn,
    string HintAz, string HintEn,
    string AltTextAz, string AltTextEn)
{
    public string Title(string language) => Localized.T(language, TitleAz, TitleEn);
    public string Story(string language) => Localized.T(language, StoryAz, StoryEn);
    public string Instruction(string language) => Localized.T(language, InstructionAz, InstructionEn);
    public string Hint(string language) => Localized.T(language, HintAz, HintEn);
    public string AltText(string language) => Localized.T(language, AltTextAz, AltTextEn);
}

/// <summary>
/// Təsdiqlənmiş marşrut paketləri. Naməlum şablon Mars paketini ALMIR — o,
/// ümumi paketə düşür, yəni səhv konfiqurasiya yad hekayə ilə deyil, neytral
/// mətnlə nəticələnir.
/// </summary>
public static class RouteStoryPacks
{
    /// <summary>Marsda Robo ilə rabitəni bərpa etmək.</summary>
    public static RouteStoryPack Mars { get; } = new(
        PuzzleBlueprintCatalog.MarsSignalRouteKey,
        StartAz: "Eniş modulu", StartEn: "Lander", StartIcon: "🛰️",
        RidgeAz: "Silsilə", RidgeEn: "Ridge", RidgeIcon: "⛰️",
        DuneAz: "Qum təpəsi", DuneEn: "Dune", DuneIcon: "🏜️",
        RechargeAz: "Günəş stansiyası", RechargeEn: "Solar station", RechargeIcon: "🔆",
        RequiredAz: "Rabitə antenası", RequiredEn: "Relay antenna", RequiredIcon: "📡",
        DecoyAz: "Mağara", DecoyEn: "Cave", DecoyIcon: "🕳️",
        GoalAz: "Robo", GoalEn: "Robo", GoalIcon: "🤖",
        ShelfAz: "Qaya rəfi", ShelfEn: "Rock shelf", ShelfIcon: "🪨",
        TitleAz: "Robo ilə əlaqəni bərpa et",
        TitleEn: "Restore contact with Robo",
        StoryAz: "Toz fırtınası Robonun rabitəsini kəsdi. Təmir dronunu göndər.",
        StoryEn: "A dust storm cut Robo's link. Send the repair drone.",
        InstructionAz: "Dronu əvvəl enerji stansiyasına, sonra antenaya, sonda Roboya çatdır.",
        InstructionEn: "Take the drone to the solar station, then the antenna, then Robo.",
        HintAz: "Antena bərpa olunmayana qədər Robo siqnalı eşitmir.",
        HintEn: "Robo cannot hear the signal until the antenna is working again.",
        AltTextAz: "Mars səthinin xəritəsi: eniş modulu, günəş stansiyası, rabitə antenası və Robo.",
        AltTextEn: "A map of the Mars surface: the lander, the solar station, the relay antenna and Robo.");

    /// <summary>
    /// Ayda kristalı işığa qovuşdurmaq.
    ///
    /// <para>Məcburi düyün burada antena deyil, GÜZGÜ sahəsidir: kristal
    /// yalnız günəş işığı ona yönəldiləndən sonra oyanır. Qayda eynidir,
    /// səbəb isə Ayın öz hekayəsindəndir.</para>
    /// </summary>
    public static RouteStoryPack Moon { get; } = new(
        PuzzleBlueprintCatalog.MoonCrystalRouteKey,
        StartAz: "Ay modulu", StartEn: "Moon lander", StartIcon: "🛸",
        RidgeAz: "Ay silsiləsi", RidgeEn: "Moon ridge", RidgeIcon: "🌗",
        DuneAz: "Toz düzü", DuneEn: "Dust plain", DuneIcon: "🌑",
        RechargeAz: "İşıq gölməçəsi", RechargeEn: "Glow pool", RechargeIcon: "💧",
        RequiredAz: "Güzgü sahəsi", RequiredEn: "Mirror field", RequiredIcon: "🪞",
        DecoyAz: "Kölgə çuxuru", DecoyEn: "Shadow pit", DecoyIcon: "🕳️",
        GoalAz: "Kristal", GoalEn: "Crystal", GoalIcon: "💎",
        ShelfAz: "Krater kənarı", ShelfEn: "Crater rim", ShelfIcon: "🪨",
        TitleAz: "Kristalı işığa qovuşdur",
        TitleEn: "Bring the crystal into the light",
        StoryAz: "Kristal soyuyub və sönür. Ay roverini işıq yolu ilə ona çatdır.",
        StoryEn: "The crystal has gone cold and dim. Guide the moon rover to it along the light.",
        InstructionAz: "Roveri əvvəl işıq gölməçəsinə, sonra güzgü sahəsinə, sonda kristala çatdır.",
        InstructionEn: "Take the rover to the glow pool, then the mirror field, then the crystal.",
        HintAz: "Güzgü sahəsi işığı yönəltməyincə kristal oyanmır.",
        HintEn: "The crystal will not wake until the mirror field aims the light at it.",
        AltTextAz: "Ay səthinin xəritəsi: ay modulu, işıq gölməçəsi, güzgü sahəsi və parlayan kristal.",
        AltTextEn: "A map of the Moon surface: the lander, the glow pool, the mirror field and a shining crystal.");

    /// <summary>
    /// Ay bazasında enerjini seçilmiş sistemə çatdırmaq.
    ///
    /// <para>Məcburi düyün paylayıcı qutudur: enerji ondan keçməsə sistemə
    /// çatsa da işləmir — tələ yolu «qısa, amma qutusuz» köhnə kabeldir.</para>
    /// </summary>
    public static RouteStoryPack MoonBase { get; } = new(
        PuzzleBlueprintCatalog.MoonBasePowerKey,
        StartAz: "Enerji paneli", StartEn: "Power panel", StartIcon: "🔋",
        RidgeAz: "Dəhliz", RidgeEn: "Corridor", RidgeIcon: "🚪",
        DuneAz: "Anbar", DuneEn: "Storeroom", DuneIcon: "📦",
        RechargeAz: "Günəş paneli", RechargeEn: "Solar panel", RechargeIcon: "🔆",
        RequiredAz: "Paylayıcı qutu", RequiredEn: "Relay box", RequiredIcon: "🔌",
        DecoyAz: "Köhnə kabel", DecoyEn: "Old cable", DecoyIcon: "🪢",
        GoalAz: "Seçilmiş sistem", GoalEn: "Chosen system", GoalIcon: "🖥️",
        ShelfAz: "Hava borusu", ShelfEn: "Air duct", ShelfIcon: "🌀",
        TitleAz: "Enerjini xəttə çək",
        TitleEn: "Route the energy",
        StoryAz: "Xana doludur, amma kabellər qopub. Enerjini düzgün yolla apar.",
        StoryEn: "The cell is full, but the cables are torn. Take the energy the right way.",
        InstructionAz: "Enerjini günəş panelindən, sonra paylayıcı qutudan keçirib sistemə çatdır.",
        InstructionEn: "Pass the energy through the solar panel, then the relay box, then to the system.",
        HintAz: "Paylayıcı qutudan keçməyən enerji sistemə çatsa da onu oyatmır.",
        HintEn: "Energy that skips the relay box reaches the system but cannot wake it.",
        AltTextAz: "Ay bazasının sxemi: enerji paneli, günəş paneli, paylayıcı qutu və seçilmiş sistem.",
        AltTextEn: "A plan of the Moon base: the power panel, the solar panel, the relay box and the chosen system.");

    /// <summary>
    /// Şablona uyğun paket. Naməlum açar üçün <c>null</c> — çağıran onda
    /// marşrut mexanikasını ÜMUMİYYƏTLƏ işlətmir.
    /// </summary>
    public static RouteStoryPack? For(string? blueprintKey) => blueprintKey switch
    {
        PuzzleBlueprintCatalog.MarsSignalRouteKey => Mars,
        PuzzleBlueprintCatalog.MoonCrystalRouteKey => Moon,
        PuzzleBlueprintCatalog.MoonBasePowerKey => MoonBase,
        _ => null
    };
}
