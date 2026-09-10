using PetPal.Api.Common;
using PetPal.Api.Entities;
using PetPal.Shared.Enums;

namespace PetPal.Api.Data.Questions;

/// <summary>
/// Riyaziyyat sualları determinist qaydada generasiya olunur — çətinlik artdıqca
/// əməliyyat və ədəd aralığı böyüyür. Bu, bankı əl ilə yazmadan bütün 1–10
/// diapazonunu doldurmağa imkan verir və hər dildə eyni sual dəstini verir.
///
/// Hər çətinlikdə <see cref="PerDifficulty"/> sual var və hər birində BİR NEÇƏ TİP
/// növbələşir: quru əməliyyat, məsələ mətni, müqayisə, əskik toplanan. Əvvəl
/// yalnız bir tip vardı (məsələn 5-ci çətinlik həmişə "a × b neçədir?") — uşaq
/// sualı oxumadan cavab verməyə başlayırdı.
///
/// Eyni mətnin təkrarı prompt-a görə süzülür: təsadüf eyni cütü iki dəfə seçsə,
/// ikincisi sadəcə atılır və yerinə yenisi çəkilir.
/// </summary>
internal static class MathQuestionFactory
{
    /// <summary>Hər çətinlikdə bu qədər fərqli sual. 10 × 24 = 240 sual/dil.</summary>
    private const int PerDifficulty = 24;

    /// <summary>Təsadüf eyni mətni təkrarlayanda sonsuz döngəyə düşməmək üçün.</summary>
    private const int MaxAttempts = 600;

    public static IEnumerable<Question> Build(string language)
    {
        // Sabit toxum: eyni baza həmişə eyni bankı alır, yəni bir cihazda
        // görünən sual başqasında da var.
        var rng = new Random(20260821);
        var az = language == Localized.Azerbaijani;

        // Süzgəc BÜTÜN bank üçün birdir, çətinlik üçün yox: «3 + 5 neçədir?»
        // həm 1-ci, həm 2-ci çətinlikdə doğula bilər və uşaq eyni sualı iki
        // dəfə görərdi — üstəlik ikincisi başqa çətinlik kimi qiymətləndirilərdi.
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var difficulty = 1; difficulty <= 10; difficulty++)
        {
            var made = 0;
            var attempts = 0;

            while (made < PerDifficulty && attempts++ < MaxAttempts)
            {
                var (prompt, answer, hint) = Item(difficulty, rng, az);

                if (!seen.Add(prompt))
                    continue;

                made++;

                yield return new Question
                {
                    Skill = SkillArea.Math,
                    LanguageCode = language,
                    Difficulty = difficulty,
                    Prompt = prompt,
                    Options = NumericOptions(answer, rng, out var correctIndex),
                    CorrectIndex = correctIndex,
                    Explanation = az ? $"Cavab {answer}-dir." : $"The answer is {answer}.",
                    Hint = hint,
                    MinAge = Math.Clamp(4 + difficulty / 2, 5, 12),
                    MaxAge = 12
                };
            }
        }
    }

    /// <summary>
    /// Bir sual. Çətinlik aralığı əməliyyatı, təsadüfi tip isə sualın FORMASINI
    /// seçir — beləliklə eyni bacarıq həm quru misal, həm məsələ kimi soruşulur.
    /// </summary>
    private static (string Prompt, int Answer, string Hint) Item(int difficulty, Random rng, bool az) =>
        difficulty switch
        {
            <= 2 => Easy(difficulty, rng, az),
            <= 4 => Subtraction(difficulty, rng, az),
            <= 6 => Multiplication(difficulty, rng, az),
            <= 8 => Division(difficulty, rng, az),
            _ => TwoStep(rng, az)
        };

    /// <summary>1–2: toplama, sadə məsələ, müqayisə.</summary>
    private static (string, int, string) Easy(int difficulty, Random rng, bool az)
    {
        var top = difficulty == 1 ? 10 : 20;

        switch (rng.Next(3))
        {
            case 0:
            {
                var a = rng.Next(1, top / 2 + 1);
                var b = rng.Next(1, top / 2 + 1);
                return az
                    ? ($"{a} + {b} neçədir?", a + b, "Böyük ədəddən başlayıb saymağa davam et.")
                    : ($"What is {a} + {b}?", a + b, "Count up from the bigger number.");
            }
            case 1:
            {
                var a = rng.Next(2, top / 2 + 1);
                var b = rng.Next(2, top / 2 + 1);
                return az
                    ? ($"Səbətdə {a} alma və {b} armud var. Cəmi neçə meyvə var?", a + b, "İki sayı bir yerə topla.")
                    : ($"A basket holds {a} apples and {b} pears. How many fruits in total?", a + b, "Add the two numbers together.");
            }
            default:
            {
                var a = rng.Next(2, top + 1);
                int b;
                do
                {
                    b = rng.Next(1, top + 1);
                }
                while (b == a);

                return az
                    ? ($"{a} və {b} — hansı ədəd daha böyükdür?", Math.Max(a, b), "Ədədləri sayarkən hansı sonra gəlir?")
                    : ($"Which number is bigger: {a} or {b}?", Math.Max(a, b), "Which one comes later when you count?");
            }
        }
    }

    /// <summary>3–4: çıxma, əskik toplanan, iki misli.</summary>
    private static (string, int, string) Subtraction(int difficulty, Random rng, bool az)
    {
        var top = difficulty == 3 ? 20 : 30;

        switch (rng.Next(4))
        {
            case 0:
            {
                var a = rng.Next(5, top + 1);
                var b = rng.Next(1, a);
                return az
                    ? ($"{a} − {b} neçədir?", a - b, "Bir-bir geriyə say.")
                    : ($"What is {a} - {b}?", a - b, "Count back one step at a time.");
            }
            case 1:
            {
                var had = rng.Next(8, top + 1);
                var gave = rng.Next(2, had - 1);
                return az
                    ? ($"Aylinin {had} stikeri var idi, {gave} dənəsini bacısına verdi. Neçə stiker qaldı?", had - gave, "Verdiyini çıx.")
                    : ($"Aylin had {had} stickers and gave {gave} to her sister. How many are left?", had - gave, "Take away what she gave.");
            }
            case 2:
            {
                var sum = rng.Next(8, top + 1);
                var known = rng.Next(1, sum);
                return az
                    ? ($"{known} + ? = {sum}. Sual işarəsinin yerində hansı ədəd olmalıdır?", sum - known, "Cəmdən bildiyin ədədi çıx.")
                    : ($"{known} + ? = {sum}. Which number belongs in place of the question mark?", sum - known, "Subtract the known number from the total.");
            }
            default:
            {
                var a = rng.Next(3, top / 2 + 1);
                return az
                    ? ($"{a} ədədinin iki misli neçədir?", a * 2, $"{a} üstəgəl {a} nə edir?")
                    : ($"What is double {a}?", a * 2, $"What is {a} plus {a}?");
            }
        }
    }

    /// <summary>5–6: vurma, dəst məsələsi, yarı.</summary>
    private static (string, int, string) Multiplication(int difficulty, Random rng, bool az)
    {
        var top = difficulty == 5 ? 6 : 10;

        switch (rng.Next(4))
        {
            case 0:
            {
                var a = rng.Next(2, top + 1);
                var b = rng.Next(2, top + 1);
                return az
                    ? ($"{a} × {b} neçədir?", a * b, $"{a} ədədini {b} dəfə topla.")
                    : ($"What is {a} × {b}?", a * b, $"Add {a} to itself {b} times.");
            }
            case 1:
            {
                var boxes = rng.Next(2, top + 1);
                var each = rng.Next(2, top + 1);
                return az
                    ? ($"{boxes} qutunun hər birində {each} top var. Cəmi neçə top var?", boxes * each, "Qutuların sayını hər qutudakına vur.")
                    : ($"There are {boxes} boxes with {each} balls in each. How many balls in total?", boxes * each, "Multiply the number of boxes by what is inside one.");
            }
            case 2:
            {
                var half = rng.Next(3, 25);
                return az
                    ? ($"{half * 2} ədədinin yarısı neçədir?", half, "Ədədi iki bərabər hissəyə böl.")
                    : ($"What is half of {half * 2}?", half, "Split the number into two equal parts.");
            }
            default:
            {
                var a = rng.Next(2, top + 1);
                var times = rng.Next(3, top + 1);
                return az
                    ? ($"{a} ədədinin {times} dəfəsi neçədir?", a * times, "«Dəfə» vurma deməkdir.")
                    : ($"What is {a} taken {times} times?", a * times, "'Times' means multiply.");
            }
        }
    }

    /// <summary>7–8: bölmə, paylaşma məsələsi, dörddə bir, müqayisə.</summary>
    private static (string, int, string) Division(int difficulty, Random rng, bool az)
    {
        var top = difficulty == 7 ? 10 : 12;

        switch (rng.Next(4))
        {
            case 0:
            {
                var b = rng.Next(2, top + 1);
                var answer = rng.Next(2, top + 1);
                return az
                    ? ($"{b * answer} ÷ {b} neçədir?", answer, $"{b * answer} içində neçə dənə {b} var?")
                    : ($"What is {b * answer} ÷ {b}?", answer, $"How many {b}s fit inside {b * answer}?");
            }
            case 1:
            {
                var kids = rng.Next(2, 7);
                var each = rng.Next(2, top + 1);
                return az
                    ? ($"{kids * each} peçenye {kids} uşağa bərabər paylanır. Hər uşağa neçə peçenye düşür?", each, "Bərabər paylamaq bölmə deməkdir.")
                    : ($"{kids * each} biscuits are shared equally between {kids} children. How many does each get?", each, "Sharing equally means dividing.");
            }
            case 2:
            {
                var quarter = rng.Next(2, 13);
                return az
                    ? ($"{quarter * 4} ədədinin dörddə biri neçədir?", quarter, "Ədədi dörd bərabər hissəyə böl.")
                    : ($"What is a quarter of {quarter * 4}?", quarter, "Split the number into four equal parts.");
            }
            default:
            {
                var a = rng.Next(3, 10);
                var b = rng.Next(3, 10);
                int c, d;
                do
                {
                    c = rng.Next(3, 10);
                    d = rng.Next(3, 10);
                }
                while (a * b == c * d);

                return az
                    ? ($"Hansı daha böyükdür: {a} × {b}, yoxsa {c} × {d}?", Math.Max(a * b, c * d), "Hər ikisini hesabla, sonra müqayisə et.")
                    : ($"Which is bigger: {a} × {b} or {c} × {d}?", Math.Max(a * b, c * d), "Work out both, then compare.");
            }
        }
    }

    /// <summary>9–10: iki addımlı ifadələr, mötərizə, hissə.</summary>
    private static (string, int, string) TwoStep(Random rng, bool az)
    {
        switch (rng.Next(4))
        {
            case 0:
            {
                var a = rng.Next(3, 12);
                var b = rng.Next(2, 9);
                var c = rng.Next(1, 20);
                return az
                    ? ($"{a} × {b} + {c} neçədir?", a * b + c, "Əvvəlcə vur, sonra topla.")
                    : ($"What is {a} × {b} + {c}?", a * b + c, "Multiply first, then add.");
            }
            case 1:
            {
                var a = rng.Next(2, 10);
                var b = rng.Next(2, 10);
                var c = rng.Next(2, 6);
                return az
                    ? ($"({a} + {b}) × {c} neçədir?", (a + b) * c, "Əvvəlcə mötərizənin içini hesabla.")
                    : ($"What is ({a} + {b}) × {c}?", (a + b) * c, "Work out the brackets first.");
            }
            case 2:
            {
                var third = rng.Next(3, 13);
                return az
                    ? ($"{third * 3} ədədinin üçdə biri neçədir?", third, "Ədədi üç bərabər hissəyə böl.")
                    : ($"What is a third of {third * 3}?", third, "Split the number into three equal parts.");
            }
            default:
            {
                var each = rng.Next(3, 10);
                var tasks = rng.Next(3, 8);
                var bonus = rng.Next(5, 26);
                return az
                    ? ($"Hər tapşırıq {each} ulduz gətirir. {tasks} tapşırıqdan sonra {bonus} ulduz bonus verilir. Cəmi neçə ulduz olur?",
                       each * tasks + bonus, "Əvvəlcə tapşırıqların ulduzunu hesabla, sonra bonusu əlavə et.")
                    : ($"Each task gives {each} stars. After {tasks} tasks you also get a {bonus} star bonus. How many stars in total?",
                       each * tasks + bonus, "Work out the task stars first, then add the bonus.");
            }
        }
    }

    /// <summary>
    /// Yanlış variantlar cavabın ətrafından seçilir: uzaq ədədlər sualı
    /// "gözlə seçilən" edir, uşaq isə hesablamağı dayandırır.
    /// </summary>
    private static List<string> NumericOptions(int answer, Random rng, out int correctIndex)
    {
        var values = new HashSet<int> { answer };
        while (values.Count < 4)
        {
            var offset = rng.Next(1, Math.Max(4, Math.Abs(answer) / 2 + 3));
            var candidate = rng.Next(2) == 0 ? answer + offset : answer - offset;
            if (candidate >= 0)
                values.Add(candidate);
        }

        var options = values.OrderBy(_ => rng.Next()).ToList();
        correctIndex = options.IndexOf(answer);
        return options.Select(v => v.ToString()).ToList();
    }
}
