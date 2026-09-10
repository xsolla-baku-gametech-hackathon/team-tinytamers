namespace PetPal.Api.Entities;

/// <summary>
/// Uşağın bir oyun MEXANİKASI üzrə cari ustalığı.
///
/// <para><b>Nə üçün <see cref="SkillMastery"/>-dən ayrı?</b> Ora məktəb
/// mənimsəməsidir (riyaziyyat, oxu) və sual bankının çətinliyini idarə edir.
/// Bura isə «marşrut qura bilirmi», «sıralamanı tuturmu» sualıdır. İkisini
/// birləşdirmək riyaziyyatda güclü uşağa mürəkkəb marşrut tapmacası verərdi —
/// halbuki o, marşrutu heç görməyib.</para>
///
/// <para><b>Nə üçün <see cref="PlayerTrait"/>-dən ayrı?</b> Bacarmaq və sevmək
/// eyni şey deyil. Tapmacada uğursuzluq mövzunu sevməmək demək deyil, hint
/// istəmək isə nə birini, nə digərini göstərir. Ona görə maraq və ustalıq
/// iki fərqli cədvəldə yaşayır.</para>
///
/// <para><b>Model qəsdən sadədir</b> — məhdud Elo: izah edilə bilir, testdə
/// determinist qalır və «niyə bu çətinlik?» sualına bir cümlə ilə cavab verir.
/// Dərin öyrənmə modeli burada nə lazımdır, nə də auditə açıqdır.</para>
/// </summary>
public class MechanicMastery
{
    public Guid Id { get; set; }

    public Guid ChildProfileId { get; set; }
    public ChildProfile ChildProfile { get; set; } = null!;

    /// <summary>Təsdiqlənmiş mexanika açarı (<c>PetBrain.MechanicKeys</c>).</summary>
    public string Mechanic { get; set; } = string.Empty;

    /// <summary>
    /// 0–100 aralığında məhdud səviyyə. Başlanğıc <c>35</c> — nə «bacarmır»,
    /// nə də «bacarır», sadəcə «hələ bilmirik».
    /// </summary>
    public int EstimatedLevel { get; set; } = StartingLevel;

    public int Attempts { get; set; }

    /// <summary>Kömək OLMADAN həll edilənlər.</summary>
    public int Successes { get; set; }

    /// <summary>
    /// İpucu və ya təkrar cəhdlə həll edilənlər.
    ///
    /// <para>Ayrı sayılır, çünki köməklə gələn uğur həqiqi uğurdur, amma
    /// müstəqil uğurla eyni çətinlik siqnalı vermir. Onları qarışdırmaq
    /// uşağın çətinliyini haqsız yerə qaldırırdı.</para>
    /// </summary>
    public int AssistedSuccesses { get; set; }

    /// <summary>
    /// Son bir neçə cəhdin istiqaməti: müsbət = yaxşılaşır, mənfi = çətinləşir.
    /// Aralıq <c>-3…+3</c> — bir nəticə trendi çevirməsin deyə.
    /// </summary>
    public int RecentTrend { get; set; }

    public DateTime? LastPracticedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>Qiymətləndirmə qaydalarının versiyası — balans dəyişəndə artır.</summary>
    public int ModelVersion { get; set; } = 1;

    public const int StartingLevel = 35;
    public const int MinLevel = 0;
    public const int MaxLevel = 100;

    /// <summary>Trendin sərt sərhədləri — bir gündə istiqamət sıçramasın.</summary>
    public const int MinTrend = -3;
    public const int MaxTrend = 3;
}
