using PetPal.Shared.Enums;

namespace PetPal.App.Ui.Services;

/// <summary>Bir ekran növünün TƏQDİMATI — ikon, davam düyməsinin sözü, üslub açarı.</summary>
/// <param name="Icon">Səhnənin xarakterini bildirən işarə.</param>
/// <param name="StyleKey">CSS modifikatoru — səhnə öz görünüşünü alsın deyə.</param>
public sealed record NodeLook(string Icon, string StyleKey);

/// <summary>
/// Ekran növü → GÖRÜNÜŞ reyestri.
///
/// <para>Hər növ üçün ayrıca səhifə yazmaq əvəzinə bir cədvəl var: yeni növ
/// əlavə etmək UI-a yeni budaq deyil, cədvələ yeni sətir deməkdir.</para>
///
/// <para><b>Nə üçün lazımdır?</b> Serverdə on yeddi ekran növü var, amma
/// onların çoxu eyni məntiqlə işləyir (variant seç və ya davam et). Fərq
/// TƏQDİMATDADIR: kəşf səhnəsində «Bax» yazılır, təmir səhnəsində «Düzəlt»,
/// qayğı səhnəsində «Kömək et». Hamısına «Davam et» yazmaq isə məhz qadağan
/// olunan şeydir — eyni ritmli, fərqsiz ekranlar zənciri.</para>
///
/// <para>Naməlum növ neytral görünüş alır (fail safe): ekran hər halda
/// oynanandır.</para>
/// </summary>
public static class NodePresentation
{
    private static readonly NodeLook Neutral = new("•", "plain");

    private static readonly Dictionary<PetBrainStageKind, NodeLook> Looks = new()
    {
        [PetBrainStageKind.Intro] = new("✨", "intro"),
        [PetBrainStageKind.Narration] = new("📖", "narration"),
        [PetBrainStageKind.Choice] = new("🔀", "choice"),
        [PetBrainStageKind.Consequence] = new("💫", "consequence"),
        [PetBrainStageKind.Puzzle] = new("🧩", "puzzle"),
        [PetBrainStageKind.Exploration] = new("🔍", "explore"),
        [PetBrainStageKind.Investigation] = new("🔎", "investigate"),
        [PetBrainStageKind.ObjectInteraction] = new("🛠️", "interact"),
        [PetBrainStageKind.RouteSelection] = new("🧭", "route"),
        [PetBrainStageKind.Building] = new("🧱", "build"),
        [PetBrainStageKind.Navigation] = new("🥾", "navigate"),
        [PetBrainStageKind.StealthObservation] = new("🤫", "stealth"),
        [PetBrainStageKind.CooperativePetAction] = new("🐾", "together"),
        [PetBrainStageKind.Caring] = new("💛", "caring"),
        [PetBrainStageKind.ChapterRecap] = new("🏁", "recap"),
        [PetBrainStageKind.FinaleChallenge] = new("🌟", "finale"),
        [PetBrainStageKind.Epilogue] = new("🌙", "epilogue"),
        [PetBrainStageKind.Ending] = new("🏆", "ending")
    };

    public static NodeLook For(PetBrainStageKind kind) =>
        Looks.TryGetValue(kind, out var look) ? look : Neutral;

    /// <summary>
    /// Variantsız ekranda davam düyməsinin SÖZÜ.
    ///
    /// <para>Fel səhnənin nə olduğunu deyir. Uşaq düyməni oxumasa da, ritm
    /// dəyişir — bu, «hər ekranda eyni Davam et» hissini aradan qaldırır.</para>
    /// </summary>
    public static string ContinueLabel(PetBrainStageKind kind, Loc loc) => kind switch
    {
        PetBrainStageKind.Intro => loc.T("Gedək!", "Let us go!"),
        PetBrainStageKind.Investigation => loc.T("Oxu", "Read it"),
        PetBrainStageKind.ObjectInteraction => loc.T("Düzəlt", "Fix it"),
        PetBrainStageKind.Building => loc.T("Qur", "Build it"),
        PetBrainStageKind.Navigation => loc.T("İrəli get", "Move on"),
        PetBrainStageKind.StealthObservation => loc.T("Sakitcə izlə", "Watch quietly"),
        PetBrainStageKind.CooperativePetAction => loc.T("Birlikdə!", "Together!"),
        PetBrainStageKind.Caring => loc.T("Kömək et", "Help out"),
        PetBrainStageKind.Narration => loc.T("Sonra?", "And then?"),
        PetBrainStageKind.FinaleChallenge => loc.T("Başlayaq", "Let us begin"),
        PetBrainStageKind.ChapterRecap => loc.T("Fəsli bitir", "Finish the chapter"),
        _ => loc.T("Davam", "Keep going")
    };
}
