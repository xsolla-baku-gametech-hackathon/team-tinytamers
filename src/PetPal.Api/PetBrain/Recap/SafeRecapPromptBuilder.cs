using System.Security.Cryptography;
using System.Text;

namespace PetPal.Api.PetBrain.Recap;

/// <summary>
/// Video promptunu <b>nəzərdən keçirilmiş ifadələrdən</b> yığır.
///
/// <para>Klientin heç bir sətri bura düşmür: hər kadrın mətni
/// <see cref="RecapStoryboard"/>-un qapalı açar → ifadə cədvəlindən gəlir.
/// Sonda dəyişməz təhlükəsizlik bəndi əlavə olunur.</para>
///
/// <para><b>Ən vacib bənd sondadır:</b> «seçilmiş marşrutu və xilasetməni
/// dəyişmə». Model uşağın seçmədiyi variantı çəkə bilməz — recap yalnız o
/// zaman doğru olur.</para>
/// </summary>
public static class SafeRecapPromptBuilder
{
    /// <summary>Şablon versiyası — bəndlər dəyişəndə artır və hash-a düşür.</summary>
    public const int TemplateVersion = 2;

    /// <summary>
    /// Promptun ehtiyat həddi.
    ///
    /// <para>Runway <c>promptText</c>-i 1000 simvolla məhdudlaşdırır. Ölçülmüş
    /// bir səhv: Marsın «krater + batareya» birləşməsi 1002 simvol idi, yəni
    /// məhz o seçimləri edən uşaq heç vaxt video almazdı. İfadələr indi
    /// qısadır, bu hədd isə gələcək redaktələr üçün marja saxlayır və testlə
    /// qorunur.</para>
    /// </summary>
    public const int SafeLength = 960;

    private const string SafetyClause =
        "No children, speech, lip-sync, text, letters, numbers, subtitles, logos, UI, weapons, injury, " +
        "frightening imagery, route arrows, puzzle answer, or extra characters.";

    public static string Build(AdventureRecapSpec spec, IReadOnlyList<RecapShot> shots)
    {
        var builder = new StringBuilder();

        builder
            .Append("Create one continuous exactly ")
            .Append(spec.DurationSeconds)
            .Append("-second vertical 9:16 child-friendly 2D storybook game animation matching the supplied scene reference. ")
            .Append("Keep the same pet design in all shots. ");

        for (var i = 0; i < shots.Count; i++)
        {
            var shot = shots[i];

            builder
                .Append("Shot ").Append(i + 1).Append(" (")
                .Append(shot.StartSeconds.ToString("0.#")).Append('–')
                .Append(shot.EndSeconds.ToString("0.#")).Append("s): ")
                .Append(shot.Motion).Append(". ");
        }

        builder.Append(Mood(spec.Mood)).Append(", smooth restrained motion, visual continuity, no peril. ");

        // İpucu sayı animasiyanı YÜNGÜLCƏ dəyişir — utandırmadan.
        if (Encouragement(spec.PuzzleOutcome) is { Length: > 0 } encouragement)
            builder.Append(encouragement).Append(". ");

        builder.Append(SafetyClause).Append(' ').Append(Lock(spec));

        return builder.ToString();
    }

    public static string HashOf(string prompt) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(prompt)));

    private static string Mood(string key) => key switch
    {
        "hopeful-adventurous" => "Hopeful adventurous mood",
        "gentle-wonder" => "Gentle wonder, warm and safe",
        _ => "Calm and curious mood"
    };

    /// <summary>
    /// İpucu istəmək UĞURSUZLUQ DEYİL. Ona görə ifadə pet-in köməyini göstərir,
    /// uşağın səhvini yox — «çətinlik çəkdi» tipli heç nə modelə getmir.
    /// </summary>
    private static string Encouragement(string outcome) => outcome switch
    {
        "one-hint" or "several-hints" => "The pet points encouragingly along the way",
        _ => string.Empty
    };

    /// <summary>
    /// Seçimi KİLİDLƏYƏN bənd — əks variant çəkilə bilməz.
    ///
    /// <para>Yalnız kadrlarda GÖRÜNƏN seçimlər kilidlənir
    /// (<see cref="RecapStoryboard.DepictedBeats"/>): kadrda olmayan seçimi
    /// kilidləmək modelə o hadisəni çəkməyi təklif etmək olardı.</para>
    /// </summary>
    private static string Lock(AdventureRecapSpec spec)
    {
        var depicted = RecapStoryboard.DepictedBeats(spec.ExperienceKey);

        var chosen = spec.Beats
            .Where(b => depicted.Contains(b.BeatKey, StringComparer.Ordinal))
            .Select(b => $"{b.BeatKey.Replace('-', ' ')} {b.ChoiceKey.Replace('-', ' ')}")
            .ToList();

        return chosen.Count == 0
            ? "Do not add story events that were not described."
            : $"Do not change the selected {string.Join(" or ", chosen)}.";
    }
}
