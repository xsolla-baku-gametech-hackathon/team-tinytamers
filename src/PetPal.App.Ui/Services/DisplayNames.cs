using PetPal.Shared.Enums;

namespace PetPal.App.Ui.Services;

/// <summary>
/// Enum dəyərlərinin ekranda göstərilən adları.
///
/// Enum-un öz adı (<c>Math</c>, <c>Egg</c>) heç vaxt birbaşa render edilməməlidir:
/// uşaq üçün bu, tərcüməsi olmayan bir kod sözüdür. Bu sinif həmin çevrilməni
/// tək yerdə saxlayır ki, yeni dəyər əlavə olunanda hər iki dildəki adı
/// unudulmasın — ona görə hər metod <see cref="Loc"/> alır.
/// </summary>
public static class DisplayNames
{
    public static string Skill(Loc loc, SkillArea skill) => skill switch
    {
        SkillArea.Math => loc.T("Riyaziyyat", "Math"),
        SkillArea.Vocabulary => loc.T("Söz ehtiyatı", "Vocabulary"),
        SkillArea.Logic => loc.T("Məntiq", "Logic"),
        SkillArea.Reading => loc.T("Oxu", "Reading"),
        SkillArea.Science => loc.T("Təbiət", "Science"),
        _ => skill.ToString()
    };

    /// <summary>
    /// Uşaq avatarının emoji qarşılığı. Açar API-dəki <c>ChildProfile.AvatarKey</c>
    /// ilə eynidir; "avatar-robot" isə arenanın MƏŞQ rəqibinə aiddir — o, əsl
    /// uşaq deyil və ekranda da belə görünməlidir.
    /// </summary>
    public static string Avatar(string? avatarKey) => avatarKey switch
    {
        "avatar-cat" => "🐱",
        "avatar-bunny" => "🐰",
        "avatar-dragon" => "🐲",
        "avatar-bear" => "🐻",
        "avatar-robot" => "🤖",
        _ => "🦊"
    };

    public static string SkillIcon(SkillArea skill) => skill switch
    {
        SkillArea.Math => "🔢",
        SkillArea.Vocabulary => "📚",
        SkillArea.Logic => "🧩",
        SkillArea.Reading => "📖",
        SkillArea.Science => "🔬",
        _ => "✨"
    };

    /// <summary>
    /// Seçilə bilən pet növləri — açar API-dəki <c>Pet.Species</c> ilə eynidir.
    /// Siyahı burada saxlanılır ki, profil yaratma ekranı ilə şkaf eyni
    /// mənbədən oxusun və biri digərindən geri qalmasın.
    /// </summary>
    public static (string Key, string Label)[] SpeciesOptions(Loc loc) =>
    [
        ("fox", loc.T("Tülkü", "Fox")),
        ("cat", loc.T("Pişik", "Cat")),
        ("dragon", loc.T("Əjdaha", "Dragon")),
        ("bunny", loc.T("Dovşan", "Bunny"))
    ];

    public static string Stage(Loc loc, PetStage stage) => stage switch
    {
        PetStage.Egg => loc.T("Yumurta", "Egg"),
        PetStage.Newborn => loc.T("Körpə", "Newborn"),
        PetStage.Baby => loc.T("Balaca", "Baby"),
        PetStage.Child => loc.T("Uşaq", "Child"),
        PetStage.Teen => loc.T("Yeniyetmə", "Teen"),
        PetStage.Adult => loc.T("Yetkin", "Grown-up"),
        PetStage.Elder => loc.T("Müdrik", "Wise one"),
        _ => stage.ToString()
    };

    public static string Mood(Loc loc, PetMood mood) => mood switch
    {
        PetMood.Hungry => loc.T("Ac", "Hungry"),
        PetMood.Sleepy => loc.T("Yuxulu", "Sleepy"),
        PetMood.Dirty => loc.T("Çirkli", "Dirty"),
        PetMood.Sad => loc.T("Kədərli", "Sad"),
        PetMood.Excited => loc.T("Həyəcanlı", "Excited"),
        PetMood.Happy => loc.T("Şən", "Happy"),
        _ => loc.T("Sakit", "Calm")
    };
}
