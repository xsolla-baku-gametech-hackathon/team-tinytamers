using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Puzzles;

/// <summary>
/// Generasiyanın ETİBARLI konteksti — hamısı serverdə, autentifikasiya olunmuş
/// uşaqdan və cari run-dan yığılır.
///
/// <para>Klient bu sahələrin HEÇ BİRİNİ göndərə və ya üstələyə bilmir: nə
/// xassələri, nə mənimsəməni, nə hədəf çətinliyi, nə toxumu, nə həlli, nə
/// mükafatı, nə də doğruluğu.</para>
/// </summary>
public sealed record PuzzleGenerationContext(
    Guid ChildId,
    Guid RunId,
    int StageIndex,

    /// <summary>Dəqiq yaş — yalnız yaş həddi üçün.</summary>
    int Age,
    string Language,

    /// <summary>
    /// Hansı macərənin içindəyik — <c>moon-crystal-rescue</c>.
    ///
    /// <para>Hekayə mətni daşıyan mexanikalar buna görə süzülür: mexanika
    /// uyğun gəlsə də, mətn yad macərəyə düşməməlidir.</para>
    /// </summary>
    string TemplateKey,

    /// <summary>Təcrübənin janrı: yaradıcı yol məntiq tapmacası almır.</summary>
    PetBrainExperienceType ExperienceType,

    /// <summary>Təsdiqlənmiş mövzu — lüğəti bu çəkir.</summary>
    string Theme,

    IReadOnlyDictionary<string, int> Interests,
    IReadOnlyDictionary<string, int> PlayStyles,

    /// <summary>Pet Brain-in təcrübə pilləsi (məktəb Elo-su DEYİL).</summary>
    PetBrainDifficulty Difficulty,

    /// <summary>
    /// MEXANİKA ailəsi üzrə pillə: hansı tapmaca növündə uşağın necə getdiyi.
    ///
    /// <para>Qlobal pillə hekayənin ritmini seçir, bu isə tapmacanın özünü
    /// incələyir. Marşrutda güclü, sıralamada təzə olan uşaq üçün tək rəqəm
    /// yanlışdır: o, bir mexanikada darıxır, digərində əziyyət çəkir.</para>
    ///
    /// <para>Açar tapılmasa <see cref="Difficulty"/> işlənir — yeni mexanika
    /// heç bir tarixçə olmadan orta pillədən başlayır.</para>
    /// </summary>
    IReadOnlyDictionary<string, PetBrainDifficulty> MechanicTiers,

    /// <summary>Mövcud adaptiv mühərrikin hədəfi (1–10) — çətinliyi incələmək üçün.</summary>
    int MasteryTargetDifficulty,

    /// <summary>Dəstək rejimi: az variant, ipucu əvvəldən.</summary>
    bool Assisted,

    /// <summary>Son tapmacaların barmaq izləri — eyni sual dalbadal təkrarlanmasın.</summary>
    IReadOnlyCollection<string> RecentSignatures,

    /// <summary>
    /// Hekayənin İSTƏDİYİ mexanika açarı; boşdursa generator özü seçir.
    ///
    /// <para>Budaqlanan macərada düyün hansı tapmacanın hekayəyə uyğun
    /// olduğunu bilir — Ayda kristal marşrutu. Bu, uyğunluq süzgəcini ƏVƏZ
    /// ETMİR: açar yenə də şablonun icazə verdiyi mexanikalar arasından
    /// olmalıdır, əks halda nəzərə alınmır.</para>
    /// </summary>
    string PreferredBlueprintKey = "");

/// <summary>Serverdə saxlanan HƏLL — heç bir DTO-ya düşmür.</summary>
public sealed record PuzzleSolution(IReadOnlyList<string> Ids, PetBrainAnswerKind Kind);

/// <summary>
/// Generatorun nəticəsi: uşağa gedən cavabsız hissə + serverdə qalan həll.
/// </summary>
public sealed record GeneratedPuzzle(
    PuzzleBlueprint Blueprint,
    PetBrainPuzzleDto Public,
    PuzzleSolution Solution,

    /// <summary>Məzmunun barmaq izi — təkrar yoxlaması üçün.</summary>
    string Signature,

    /// <summary>Toxumun hex təsviri — eyni tapmacanı bərpa edir.</summary>
    string SeedHex,

    /// <summary>Neçənci cəhddə alındı (təkrar/yoxlama uğursuzluqları).</summary>
    int Attempt,

    /// <summary>Deterministik ehtiyat variantına düşüldümü.</summary>
    bool UsedFallback);

/// <summary>
/// Uşağa xas tapmaca yaradan qat.
///
/// <para>Bu qat <b>modelsiz və şəbəkəsiz</b> işləyir — AI ondan xəbərsizdir.
/// Model (açıq olsa belə) yalnız artıq təhlükəsiz olan mətni yenidən yaza
/// bilər; mexanikanı, rəqəmləri, variantları, həlli, çətinliyi və mükafatı
/// HEÇ VAXT.</para>
/// </summary>
public interface IPersonalizedPuzzleGenerator
{
    /// <summary>
    /// Kontekstə uyğun, yoxlanmış tapmaca qaytarır. Heç vaxt <c>null</c>
    /// qaytarmır: namizədlər uğursuz olsa deterministik ehtiyat variantı verilir.
    /// </summary>
    GeneratedPuzzle Generate(PuzzleGenerationContext context);
}
