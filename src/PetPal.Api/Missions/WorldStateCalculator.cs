using PetPal.Api.Common;
using PetPal.Shared.Enums;

namespace PetPal.Api.Missions;

/// <summary>
/// "World changes based on engagement" qaydası: son 7 günün fəaliyyəti
/// dünyanın hava vəziyyətini müəyyən edir. Saf funksiyadır — test olunur.
/// </summary>
public static class WorldStateCalculator
{
    /// <summary>Hər tamamlanmış tapşırıq 3 bal verir, gündəlik hədəf tamamlanması əlavə 10 bal.</summary>
    public static int EngagementScore(IEnumerable<(int Completed, bool GoalReached)> lastSevenDays)
    {
        var score = lastSevenDays.Sum(day => day.Completed * 3 + (day.GoalReached ? 10 : 0));
        return Math.Clamp(score, 0, 100);
    }

    public static WorldWeather WeatherFor(int engagementScore) => engagementScore switch
    {
        < 15 => WorldWeather.Storm,
        < 35 => WorldWeather.Rain,
        < 55 => WorldWeather.Cloudy,
        < 80 => WorldWeather.Clear,
        _ => WorldWeather.Sunny
    };

    public static string HeadlineFor(WorldWeather weather, string language = Localized.English) =>
        Localized.Normalize(language) == Localized.Azerbaijani
            ? weather switch
            {
                WorldWeather.Storm => "Dünya solur. Pet-in sənə ehtiyac duyur!",
                WorldWeather.Rain => "Boz buludlar çəkilmir. Bir neçə tapşırıq onları dağıdar.",
                WorldWeather.Cloudy => "Səma açılır. Davam et!",
                WorldWeather.Clear => "Dünya yenidən canlanır. Çox gözəl!",
                _ => "Hər yer günəşli! Dünyan çiçəklənir."
            }
            : weather switch
            {
                WorldWeather.Storm => "The world is fading. Your pet needs you!",
                WorldWeather.Rain => "Grey clouds are hanging around. A few tasks will clear them.",
                WorldWeather.Cloudy => "The sky is brightening. Keep going!",
                WorldWeather.Clear => "The world looks alive again. Beautiful work!",
                _ => "Sunshine everywhere! Your world is thriving."
            };
}
