using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain;

/// <summary>
/// Modelə verilə bilən TƏSDİQLƏNMİŞ giriş dəsti.
///
/// <para>Burada uşağın yazdığı heç nə yoxdur: nə söhbət, nə şəkil, nə ad-soyad,
/// nə məktəb, nə də ixtiyari metadata. Yalnız dil, yaş zolağı, şablon açarı,
/// çətinlik, pet-in (onsuz da təmizlənmiş) adı və strukturlu yaddaş açarları.</para>
/// </summary>
public sealed record NarrativeContext(
    string Language,

    /// <summary>Dəqiq yaş yox, ZOLAQ — 5-6, 7-8, 9-10.</summary>
    string AgeBand,

    ExperienceTemplate Template,
    PetBrainDifficulty Difficulty,
    string PetName,

    /// <summary>Yaddaşdan yalnız AÇARLAR: "mars-rover-rescue", "space".</summary>
    IReadOnlyList<string> MemoryKeys);

/// <summary>Təqdimat mətni və onun MƏNBƏYİ. Mənbə nümayiş panelində açıq göstərilir.</summary>
public sealed record ExperienceNarrative(string Title, string Intro, string Source)
{
    public const string TemplateSource = "template";
    public const string AiSource = "ai";
}

/// <summary>
/// Başlıq və giriş cümləsini hazırlayır.
///
/// <para>Bu qat YALNIZ TƏQDİMATDIR. Model (açıq olsa belə) şablon seçə bilmir,
/// çətinlik təyin edə bilmir, mükafat verə bilmir, doğru cavabı müəyyən edə
/// bilmir və yeni mərhələ növü yarada bilmir — onların hamısı deterministik
/// serverin qərarıdır.</para>
/// </summary>
public interface IExperienceNarrativeProvider
{
    Task<ExperienceNarrative> DescribeAsync(NarrativeContext context, CancellationToken ct = default);
}

/// <summary>
/// Standart implementasiya: mətn kataloqdan gəlir.
///
/// <para>Bu, "ehtiyat variant" deyil — ƏSAS variantdır. AI heç vaxt qurulmasa
/// da Pet Brain tam işləyir və nümayiş tam keçir.</para>
/// </summary>
public sealed class TemplateNarrativeProvider : IExperienceNarrativeProvider
{
    public Task<ExperienceNarrative> DescribeAsync(NarrativeContext context, CancellationToken ct = default) =>
        Task.FromResult(new ExperienceNarrative(
            context.Template.Title(context.Language),
            context.Template.Intro(context.Language),
            ExperienceNarrative.TemplateSource));
}
