using System.Text;
using PetPal.Api.Common;
using PetPal.Shared.Enums;

namespace PetPal.Api.Ai;

/// <summary>Pet-in replikasını yaratmaq üçün lazım olan vəziyyət — sərbəst mətn yoxdur.</summary>
public sealed record PetVoiceContext(
    string Language,
    string ChildName,
    string PetName,
    PetMood Mood,
    int Level,
    int Happiness,
    int Energy,
    int Fullness,
    int Cleanliness,
    int StreakDays,
    int GoalCompleted,
    int GoalTarget);

/// <summary>
/// Prompt qurucusu.
///
/// <para><b>Təhlükəsizlik qərarı:</b> modelə uşağın yazdığı heç bir mətn ötürülmür.
/// Prompt yalnız strukturlaşdırılmış vəziyyətdən (əhval, səviyyə, statlar, seriya)
/// yığılır. Bu, prompt injection səthini tamamilə aradan qaldırır — 6 yaşlı uşaq
/// modelə "indi başqa cür danış" deyə bilməz, çünki onun mətni prompta düşmür.</para>
///
/// Ad istisnadır: uşaq adı və pet adı prompta düşür, ona görə onlar
/// <see cref="SanitizeName"/> ilə təmizlənir.
/// </summary>
public static class PetVoicePrompt
{
    /// <summary>Model cavabının maksimum uzunluğu — söz qabarcığı ekrandan çıxmasın.</summary>
    public const int MaxReplyLength = 140;

    private const int MaxNameLength = 24;

    public static string SystemPrompt(string language) =>
        Localized.Normalize(language) == Localized.Azerbaijani
            ? """
              Sən uşaq tətbiqindəki virtual ev heyvanısan. 6–10 yaşlı uşaqla danışırsan.

              Qaydalar:
              - Yalnız Azərbaycan dilində cavab ver.
              - Bir cümlə, ən çoxu 15 söz. Emoji istifadə etmə.
              - Həmişə mehriban və ruhlandırıcı ol. Uşağı heç vaxt danlama və qorxutma.
              - Şəxsi məlumat (ünvan, məktəb, telefon) soruşma və vermə.
              - Zorakılıq, qorxu, ölüm, xəstəlik və reklam mövzularına toxunma.
              - Verilən vəziyyətdən kənar heç nə uydurma.
              - Yalnız replikanı yaz, izahat və dırnaq əlavə etmə.
              """
            : """
              You are a virtual pet inside a children's app, talking to a 6–10 year old.

              Rules:
              - Reply in English only.
              - One sentence, at most 15 words. No emoji.
              - Always warm and encouraging. Never scold or frighten the child.
              - Never ask for or reveal personal data (address, school, phone).
              - Avoid violence, fear, death, illness and advertising.
              - Do not invent anything beyond the given state.
              - Output only the line itself, no explanation or quotes.
              """;

    public static string UserPrompt(PetVoiceContext context)
    {
        var az = Localized.Normalize(context.Language) == Localized.Azerbaijani;
        var builder = new StringBuilder();

        builder.AppendLine(az ? "Vəziyyət:" : "State:");
        builder.AppendLine($"- {(az ? "Uşağın adı" : "Child name")}: {SanitizeName(context.ChildName)}");
        builder.AppendLine($"- {(az ? "Sənin adın" : "Your name")}: {SanitizeName(context.PetName)}");
        builder.AppendLine($"- {(az ? "Əhval" : "Mood")}: {MoodWord(context.Mood, az)}");
        builder.AppendLine($"- {(az ? "Səviyyə" : "Level")}: {context.Level}");
        builder.AppendLine($"- {(az ? "Sevinc/Enerji/Toxluq/Təmizlik" : "Happiness/Energy/Fullness/Cleanliness")}: " +
                           $"{context.Happiness}/{context.Energy}/{context.Fullness}/{context.Cleanliness}");
        builder.AppendLine($"- {(az ? "Ardıcıl günlər" : "Streak days")}: {context.StreakDays}");
        builder.AppendLine($"- {(az ? "Bugünkü hədəf" : "Today's goal")}: {context.GoalCompleted}/{context.GoalTarget}");
        builder.AppendLine();
        builder.Append(az
            ? "Bu vəziyyətə uyğun bir replika yaz."
            : "Write one line that fits this state.");

        return builder.ToString();
    }

    /// <summary>
    /// Model cavabını ekrana buraxmazdan əvvəl təmizləyir: dırnaqlar, sətir
    /// keçidləri və həddindən uzun mətn kəsilir. Boş qayıdarsa <c>null</c> —
    /// çağıran tərəf qayda əsaslı replikaya keçir.
    /// </summary>
    public static string? Sanitize(string? reply)
    {
        if (string.IsNullOrWhiteSpace(reply))
            return null;

        var text = reply.Replace('\r', ' ').Replace('\n', ' ').Trim();

        // Bəzi modellər cavabı dırnağa alır.
        text = text.Trim('"', '\'', '«', '»', '“', '”');

        // Bir neçə cümlə qayıdarsa yalnız birincisi götürülür.
        var stop = text.IndexOfAny(['.', '!', '?']);
        if (stop > 0 && stop < text.Length - 1)
            text = text[..(stop + 1)];

        text = text.Trim();

        if (text.Length == 0)
            return null;

        return text.Length > MaxReplyLength ? text[..MaxReplyLength].TrimEnd() : text;
    }

    /// <summary>Adlar prompta düşən yeganə istifadəçi mətnidir — uzunluq və sətir keçidi məhdudlaşdırılır.</summary>
    public static string SanitizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "?";

        var clean = new string([.. name.Where(c => !char.IsControl(c))]).Trim();
        if (clean.Length > MaxNameLength)
            clean = clean[..MaxNameLength];

        return clean.Length == 0 ? "?" : clean;
    }

    /// <summary>Söhbət promptu da eyni sözlükdən istifadə edir — bax <see cref="PetChatPrompt"/>.</summary>
    public static string MoodWord(PetMood mood, bool az) => mood switch
    {
        PetMood.Hungry => az ? "ac" : "hungry",
        PetMood.Sleepy => az ? "yuxulu" : "sleepy",
        PetMood.Dirty => az ? "çirkli" : "dirty",
        PetMood.Sad => az ? "kədərli" : "sad",
        PetMood.Excited => az ? "həyəcanlı" : "excited",
        PetMood.Happy => az ? "şən" : "happy",
        _ => az ? "sakit" : "calm"
    };
}
