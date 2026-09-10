using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Recommendation;

/// <summary>
/// Bir namizədin İZAH EDİLƏ BİLƏN bal kartı.
///
/// <para>Tək bal «niyə bu?» sualına cavab verə bilmirdi: macəra mövzuya görə
/// seçildi, mexanikaya görə, yoxsa sadəcə çətinliyi tutdu? Komponentlər ayrı
/// qalanda izah HESABLANMIŞ olur — sonradan uydurulmur.</para>
/// </summary>
public sealed record CandidateScore(
    ExperienceTemplate Template,

    /// <summary>Mövzu uyğunluğu (0–100).</summary>
    int TopicFit,

    /// <summary>Mexanika uyğunluğu (0–100) — mövzudan AYRI.</summary>
    int MechanicFit,

    /// <summary>Çətinliyin ustalığa uyğunluğu (0–100).</summary>
    int MasteryChallengeFit,

    /// <summary>Oyun üslubu uyğunluğu (0–100).</summary>
    int StyleFit,

    /// <summary>Lazım olan dəstəyin mövcudluğu (0–100).</summary>
    int SupportFit,

    /// <summary>Sessiya uzunluğu və tempə uyğunluq (0–100).</summary>
    int PaceFit,

    /// <summary>Yarımçıq hekayənin davamı olma dərəcəsi (0–100).</summary>
    int ContinuityFit,

    /// <summary>Mükafat növünün uyğunluğu (0–100).</summary>
    int RewardFit,

    /// <summary>Yenilik (0–100) — son vaxt oynananlar aşağı alır.</summary>
    int NoveltyValue,

    /// <summary>Təkrar cəzası (0–100) — yekundan ÇIXILIR.</summary>
    int RepetitionPenalty,

    /// <summary>
    /// Açıq seçimin düzəlişi: bəyənmə müsbət, «daha az göstər» mənfi.
    ///
    /// <para>Ayrıca saxlanılır ki, «uşaq bunu özü istədi» ilə «sistem belə
    /// hesabladı» jurnalda qarışmasın.</para>
    /// </summary>
    int ExplicitAdjustment,

    /// <summary>Yekun bal (0–100).</summary>
    double Total,

    /// <summary>Uşağa göstəriləcək izahın SƏBƏB KODLARI.</summary>
    IReadOnlyList<PetBrainWhyReason> Why)
{
    public string Key => Template.Key;
    public string Theme => Template.Theme;
}

/// <summary>Sərt şərtdən keçməyən namizəd — jurnal üçün.</summary>
public sealed record FilteredCandidate(string TemplateKey, PetBrainFilterReason Reason)
{
    /// <summary>Jurnalda saxlanan kompakt forma: <c>"açar=səbəb"</c>.</summary>
    public override string ToString() => $"{TemplateKey}={Reason}";
}

/// <summary>Ekranda görünəcək BİR kart.</summary>
public sealed record RecommendationCard(
    CandidateScore Candidate,
    PetBrainRecommendationSlot Slot,

    /// <summary>Bu kart uyğunluq sırasından yox, KƏŞF payından gəldimi.</summary>
    bool WasExploration);

/// <summary>
/// Bir baxışın tam nəticəsi: əsas kart, alternativlər və qərarın izi.
///
/// <para>Nəticə bir MƏCBURİ macəra deyil — uşaq alternativi seçə bilir və
/// server onu qəbul edir. Yalnız «əsas» seçimi qəbul etmək uşağı öz
/// siyasətimizə məcbur etmək olardı.</para>
/// </summary>
public sealed record RecommendationSet(
    IReadOnlyList<RecommendationCard> Cards,

    /// <summary>Bala görə sıralanmış BÜTÜN keçən namizədlər — nümayiş paneli üçün.</summary>
    IReadOnlyList<CandidateScore> Ranked,

    /// <summary>Sərt şərtdən keçməyənlər və səbəbləri.</summary>
    IReadOnlyList<FilteredCandidate> Filtered,

    PetBrainDifficulty Difficulty,
    int PolicyVersion,

    /// <summary>Qərar anındakı profil inamı (0–100).</summary>
    int ProfileConfidence)
{
    public RecommendationCard? Primary => Cards.Count > 0 ? Cards[0] : null;

    public IReadOnlyList<RecommendationCard> Alternatives =>
        Cards.Count > 1 ? [.. Cards.Skip(1)] : [];
}
