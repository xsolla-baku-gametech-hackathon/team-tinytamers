using PetPal.Api.Common;
using PetPal.Shared.Dtos.Games;

namespace PetPal.Api.Games;

/// <summary>
/// Mini oyunlar. Hər oyunun konkret bir bacarıq hədəfi var — "sadəcə əyləncə"
/// olan oyun əlavə etmirik ki, ekran vaxtı həmişə nəyəsə xidmət etsin.
/// </summary>
public static class GameCatalog
{
    public const string MemoryMatch = "memory-match";
    public const string QuickTap = "quick-tap";
    public const string BubblePop = "bubble-pop";
    public const string ColorEcho = "color-echo";
    public const string StarRun = "star-run";
    public const string FruitSlice = "fruit-slice";
    public const string BasketCatch = "basket-catch";
    public const string CloudJump = "cloud-jump";
    public const string LetterHunt = "letter-hunt";
    public const string ChefOrder = "chef-order";

    /// <summary>Gündə bu qədər oyun mükafatlandırılır; sonrası pulsuz oynanır.</summary>
    public const int RewardedGamesPerDay = 3;

    /// <summary>
    /// Sıra kafel şəbəkəsinin sırasıdır: əvvəl pulsuzlar, sonra qiyməti artan
    /// açılışlar. Uşaq siyahını yuxarıdan aşağı "böyüyür" kimi oxuyur.
    /// </summary>
    public static readonly string[] Keys =
        [MemoryMatch, QuickTap, BubblePop, ColorEcho, StarRun, FruitSlice, BasketCatch, CloudJump, LetterHunt, ChefOrder];

    public static bool IsKnown(string gameKey) => Keys.Contains(gameKey);

    /// <summary>
    /// Açılış qiymətləri. İlk iki oyun pulsuzdur — uşaq oyunun nə olduğunu
    /// görməmiş ulduz xərcləməməlidir. Qalanlar toplanan ulduzla açılır və
    /// açıldıqdan sonra həmişəlik qalır.
    ///
    /// Əyri qəsdən aramla qalxır: gündəlik hədəflə ~75 ulduz qazanılır, yəni
    /// ilk açılış bir günlük, sonuncusu isə təxminən bir həftəlik məqsəddir.
    /// </summary>
    public static int UnlockCost(string gameKey) => gameKey switch
    {
        BubblePop => 60,
        ColorEcho => 120,
        StarRun => 180,
        FruitSlice => 240,
        BasketCatch => 300,
        CloudJump => 380,
        LetterHunt => 460,
        ChefOrder => 560,
        _ => 0
    };

    public static bool IsUnlocked(string gameKey, IReadOnlyCollection<string> unlockedGames) =>
        UnlockCost(gameKey) == 0 || unlockedGames.Contains(gameKey);

    public static List<GameCatalogItemDto> For(string language, IReadOnlyCollection<string> unlockedGames)
    {
        var az = Localized.Normalize(language) == Localized.Azerbaijani;

        List<GameCatalogItemDto> items =
        [
            new GameCatalogItemDto
            {
                Key = MemoryMatch,
                IconKey = "🃏",
                Title = az ? "Yaddaş cütləri" : "Memory Match",
                Description = az
                    ? "Kartları çevir və eyni cütləri tap."
                    : "Flip the cards and find the matching pairs.",
                SkillHint = az ? "Yaddaş və diqqət" : "Memory and focus"
            },
            new GameCatalogItemDto
            {
                Key = QuickTap,
                IconKey = "⚡",
                Title = az ? "Sürətli hesab" : "Quick Tap",
                Description = az
                    ? "Vaxt bitməmiş düzgün cavaba toxun."
                    : "Tap the right answer before the timer runs out.",
                SkillHint = az ? "Zehni hesablama sürəti" : "Mental maths speed"
            },
            new GameCatalogItemDto
            {
                Key = BubblePop,
                IconKey = "🫧",
                Title = az ? "Baloncuq ovu" : "Bubble Pop",
                Description = az
                    ? "Yalnız qaydaya uyğun rəqəmləri partlat."
                    : "Pop only the numbers that match the rule.",
                SkillHint = az ? "Rəqəm hissi və diqqət" : "Number sense and focus"
            },
            new GameCatalogItemDto
            {
                Key = ColorEcho,
                IconKey = "🎵",
                Title = az ? "Rəng sırası" : "Color Echo",
                Description = az
                    ? "Sıranı yadda saxla və eyni ardıcıllıqla təkrarla."
                    : "Watch the sequence, then repeat it in order.",
                SkillHint = az ? "İşlək yaddaş" : "Working memory"
            },
            new GameCatalogItemDto
            {
                Key = StarRun,
                IconKey = "🏃",
                Title = az ? "Ulduz qaçışı" : "Star Run",
                Description = az
                    ? "Ən çox ulduz gətirən qapıdan keç."
                    : "Run through the gate that gives the most stars.",
                SkillHint = az ? "Sürətli qərar" : "Quick decisions"
            },
            new GameCatalogItemDto
            {
                Key = FruitSlice,
                IconKey = "🍉",
                Title = az ? "Meyvə kəs" : "Fruit Slice",
                Description = az
                    ? "Hədəf cəmi quran meyvələri kəs, bombaya toxunma."
                    : "Slice the fruits that add up to the target, avoid the bomb.",
                SkillHint = az ? "Cəm qurmaq" : "Making sums"
            },
            new GameCatalogItemDto
            {
                Key = BasketCatch,
                IconKey = "🧺",
                Title = az ? "Səbət tut" : "Basket Catch",
                Description = az
                    ? "Səbətə yalnız qaydaya uyğun əşyaları tut."
                    : "Catch only the things that match the rule.",
                SkillHint = az ? "Təsnifat" : "Sorting things"
            },
            new GameCatalogItemDto
            {
                Key = CloudJump,
                IconKey = "☁️",
                Title = az ? "Bulud tullanışı" : "Cloud Jump",
                Description = az
                    ? "Sıranı davam etdirən buluda tullan."
                    : "Jump to the cloud that continues the pattern.",
                SkillHint = az ? "Ardıcıllıq" : "Number patterns"
            },
            new GameCatalogItemDto
            {
                Key = LetterHunt,
                IconKey = "🔤",
                Title = az ? "Hərf ovu" : "Letter Hunt",
                Description = az
                    ? "Şəkildəki sözü hərf-hərf yığ."
                    : "Spell the word in the picture, letter by letter.",
                SkillHint = az ? "Söz və oxu" : "Words and reading"
            },
            new GameCatalogItemDto
            {
                Key = ChefOrder,
                IconKey = "🍽️",
                Title = az ? "Balaca aşpaz" : "Little Chef",
                Description = az
                    ? "Pet-in sifarişini dəqiq say və hazırla."
                    : "Count out your pet's order exactly right.",
                SkillHint = az ? "Sayma və tərtib" : "Counting to order"
            }
        ];

        foreach (var item in items)
        {
            item.UnlockStarCost = UnlockCost(item.Key);
            item.IsUnlocked = IsUnlocked(item.Key, unlockedGames);
        }

        return items;
    }

    /// <summary>
    /// Mükafat nəticəyə bağlıdır və əyri QƏSDƏN sürətlənir: "bir-iki düz cavab"
    /// ilə "səhvsiz oyun" arasındakı fərq uşağa hiss olunmalıdır, yoxsa diqqətli
    /// oynamağın mənası qalmır. Əvvəl əyri düz xətt idi — 50 bal 5 XP, 100 bal
    /// cəmi 8 XP verirdi.
    ///
    /// XP: 40 → 6, 60 → 10, 80 → 15, 100 → 28 (səhvsiz oyuna görə əlavə 5).
    /// Ulduz isə yavaş qalır — oyun öyrənməni əvəz etməməlidir.
    /// </summary>
    public static (int Stars, int Xp) Reward(int score)
    {
        var clamped = Math.Clamp(score, 0, 100);
        var flawless = clamped >= 95;

        var xp = 3 + clamped * clamped / 500 + (flawless ? 5 : 0);
        var stars = clamped / 12 + (flawless ? 2 : 0);

        return (stars, xp);
    }

    public static string ResultMessage(string language, int score, string petName)
    {
        var az = Localized.Normalize(language) == Localized.Azerbaijani;

        return score switch
        {
            >= 90 => az ? $"İnanılmaz! {petName} heyran qaldı." : $"Amazing! {petName} is impressed.",
            >= 60 => az ? $"Çox yaxşı! {petName} səninlə əyləndi." : $"Nice one! {petName} had fun with you.",
            _ => az ? $"Yaxşı cəhd! {petName} növbəti dəfə də səninlədir." : $"Good try! {petName} is with you next time."
        };
    }

    public static string RewardCappedMessage(string language) =>
        Localized.Normalize(language) == Localized.Azerbaijani
            ? "Bu günlük oyun ulduzları bitdi — amma istədiyin qədər oynaya bilərsən!"
            : "Today's game stars are used up — but you can keep playing!";
}
