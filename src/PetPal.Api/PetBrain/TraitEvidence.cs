using PetPal.Api.Entities;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain;

/// <summary>
/// Sübut modeli — <b>saf qaydalar</b>, I/O yoxdur.
///
/// <para><b>Problem.</b> Tək bal monotondur: hər müsbət hadisə onu qaldırır,
/// heç nə endirmir, ona görə zamanla bütün açarlar 100-ə yaxınlaşır və
/// profillər bir-birinə oxşayır. Daha pisi — bal «uşaq bunu sevir» ilə «uşağa
/// bu göstərildi və başqa yolu yox idi» arasındakı fərqi saxlamır.</para>
///
/// <para><b>Həll.</b> Bal qalır (direktor onu oxuyur), amma yanında sübut
/// yazılır: neçə müşahidə, neçəsi uşağın öz seçimi, neçəsi kənara qoyma, neçə
/// fərqli mənbə və nə vaxt. Direktora verilən dəyər isə bunlardan
/// hesablanır.</para>
///
/// <para><b>Dörd qayda dəyişməzdir:</b> bir skip bəyənməmək deyil, bir səhv
/// cavab maraqsızlıq deyil, ipucu istəmək cəzalandırılmır, göstərilmə
/// üstünlük deyil.</para>
/// </summary>
public static class TraitEvidence
{
    /// <summary>Bu qədər gündən sonra köhnəlmə başlayır.</summary>
    public const int GraceDays = 14;

    /// <summary>Köhnəlmənin sürəti: hər bu qədər gündə bir bal geri gedir.</summary>
    public const int DecayEveryDays = 7;

    /// <summary>
    /// Köhnəlmənin ƏN ÇOX apara biləcəyi bal.
    ///
    /// <para>Sərhəd qəsdən var: uşaq bir ay gəlmirsə profili silinməməlidir.
    /// Köhnəlmə «unutmaq» deyil, «əmin olmamaq»dır.</para>
    /// </summary>
    public const int MaxDecay = 12;

    /// <summary>Bu saydan sonra müşahidələr inamı artırmır — doyma nöqtəsi.</summary>
    public const int ConfidenceSaturation = 12;

    /// <summary>Güclü siqnal sayılan ən kiçik artım.</summary>
    public const int StrongDelta = 2;

    /// <summary>
    /// AÇIQ mənfi sözün ziddiyyət hesabındakı çəkisi.
    ///
    /// <para>Dolayı siqnaldan ağırdır: uşaq «istəmirəm» deyəndə sistem onun
    /// davranışını təfsir etməməlidir.</para>
    /// </summary>
    public const int ExplicitWeight = 2;

    /// <summary>
    /// Direktorun oxuduğu EFFEKTİV bal: saxlanan bal, köhnəlmə çıxılmaqla.
    ///
    /// <para>Köhnəlmə heç vaxt başlanğıc balından aşağı endirmir — uşağın
    /// tarixçəsi silinmir, sadəcə köhnə zəif siqnal öz çəkisini itirir.</para>
    /// </summary>
    public static int EffectiveScore(PlayerTrait trait, DateTime now)
    {
        var score = TraitKeys.Clamp(trait.Score);
        var decay = DecayFor(trait, now);

        return Math.Max(TraitKeys.StartingScore, score - decay);
    }

    /// <summary>Bu açar üçün neçə bal köhnəlib.</summary>
    public static int DecayFor(PlayerTrait trait, DateTime now)
    {
        var seen = trait.LastObservedAt ?? trait.UpdatedAt;

        if (seen == default)
            return 0;

        var idleDays = (int)(now - seen).TotalDays - GraceDays;

        if (idleDays <= 0)
            return 0;

        // Güclü, müxtəlif mənbəli sübut YAVAŞ köhnəlir: onu bir həftəlik
        // fasilə ilə şübhə altına almaq düzgün deyil.
        var slowdown = 1 + SourceCount(trait);

        return Math.Min(MaxDecay, idleDays / (DecayEveryDays * slowdown));
    }

    /// <summary>
    /// İnam (0–100): nə qədər müşahidə, nə qədər müxtəlif mənbə, nə qədər
    /// təzə.
    ///
    /// <para>Kənara qoyma siqnalları inamı AZALDIR, balı yox: «bu uşaq bunu
    /// sevir» iddiasına şübhə qatır, «sevmir» demir.</para>
    /// </summary>
    public static int Confidence(PlayerTrait trait, DateTime now)
    {
        if (trait.ObservationCount <= 0)
            return 0;

        // Həcm YALNIZ müsbət sübutdan hesablanır.
        // Ümumi müşahidə sayını işlətmək gizli bir səhv yaradırdı: kənara
        // qoyma da müşahidədir, ona görə uşaq kartı dalbadal rədd etdikcə
        // «inam» ARTIRDI. Skip artıq yalnız ziddiyyət əmsalına düşür.
        var volume = Math.Min(1.0, trait.PositiveEvidence / (double)ConfidenceSaturation);
        var diversity = Math.Min(1.0, SourceCount(trait) / 3.0);

        // Təzəlik: son müşahidədən keçən vaxt inamı yumşaq şəkildə azaldır.
        var seen = trait.LastObservedAt ?? trait.UpdatedAt;
        var idleDays = seen == default ? 0 : Math.Max(0, (now - seen).TotalDays - GraceDays);
        var freshness = Math.Max(0.4, 1.0 - (idleDays / 90.0));

        // Ziddiyyət: uşaq bu mövzunu həm seçib, həm də kənara qoyub.
        var against = trait.SkipEvidence + (trait.NegativeEvidence * ExplicitWeight);

        var contested = trait.PositiveEvidence + against == 0
            ? 1.0
            : trait.PositiveEvidence / (double)(trait.PositiveEvidence + against);

        var raw = 100 * ((0.45 * volume) + (0.25 * diversity)) * freshness * (0.55 + (0.45 * contested));

        return Math.Clamp((int)Math.Round(raw, MidpointRounding.AwayFromZero), 0, 100);
    }

    /// <summary>Neçə FƏRQLİ mənbədən siqnal gəlib.</summary>
    public static int SourceCount(PlayerTrait trait) => System.Numerics.BitOperations.PopCount((uint)trait.SourceMask);

    /// <summary>Mənbə bit maskasına əlavə olunur — təkrar mənbə maskanı dəyişmir.</summary>
    public static int WithSource(int mask, PetBrainEvidenceSource source) =>
        source == PetBrainEvidenceSource.Unknown ? mask : mask | (1 << (int)source);

    /// <summary>
    /// Bir müşahidəni sətirə yazır. <b>Balı DƏYİŞMİR</b> — onu çağıran
    /// (gündəlik tavandan keçmiş) delta ilə özü edir.
    /// </summary>
    public static void Record(
        PlayerTrait trait, PetBrainEvidenceSource source, int delta, DateTime now)
    {
        trait.ObservationCount++;
        trait.SourceMask = WithSource(trait.SourceMask, source);
        trait.LastObservedAt = now;

        if (delta > 0)
        {
            trait.PositiveEvidence++;

            if (delta >= StrongDelta)
                trait.LastStrongEvidenceAt = now;
        }
        else if (delta < 0)
        {
            if (source == PetBrainEvidenceSource.Explicit)
                trait.NegativeEvidence++;
            else
                trait.SkipEvidence++;
        }
    }

    /// <summary>
    /// EXPOSURE qeydi: uşağa göstərildi, amma o, seçmədi.
    ///
    /// <para>Bal DƏYİŞMİR və müsbət sübut YAZILMIR — məhz bu, özünü təsdiqləyən
    /// dövrəni qıran yerdir. Yalnız «bunu gördü» faktı və inamın azalması
    /// qalır.</para>
    /// </summary>
    public static void RecordSkip(PlayerTrait trait, DateTime now)
    {
        trait.ObservationCount++;
        trait.SkipEvidence++;
        trait.LastObservedAt = now;
    }
}
