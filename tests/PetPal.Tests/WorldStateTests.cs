using PetPal.Api.Missions;
using PetPal.Api.Rewards;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

public class WorldStateTests
{
    [Fact]
    public void EngagementScore_FealiyyetOlmayandaSifirdir()
    {
        var score = WorldStateCalculator.EngagementScore([]);

        Assert.Equal(0, score);
    }

    [Fact]
    public void EngagementScore_TapsiriqVeHedefUcunBalVerir()
    {
        var score = WorldStateCalculator.EngagementScore([(Completed: 5, GoalReached: true)]);

        Assert.Equal(25, score);
    }

    [Fact]
    public void EngagementScore_100_denYuxariQalxmir()
    {
        var week = Enumerable.Repeat((Completed: 30, GoalReached: true), 7);

        Assert.Equal(100, WorldStateCalculator.EngagementScore(week));
    }

    [Theory]
    [InlineData(0, WorldWeather.Storm)]
    [InlineData(20, WorldWeather.Rain)]
    [InlineData(40, WorldWeather.Cloudy)]
    [InlineData(60, WorldWeather.Clear)]
    [InlineData(100, WorldWeather.Sunny)]
    public void WeatherFor_IstirakBalinaGoreHavaniSecir(int score, WorldWeather expected)
    {
        Assert.Equal(expected, WorldStateCalculator.WeatherFor(score));
    }

    [Fact]
    public void HeadlineFor_HerHavaVeHerDilUcunMetnQaytarir()
    {
        foreach (var weather in Enum.GetValues<WorldWeather>())
        {
            Assert.False(string.IsNullOrWhiteSpace(WorldStateCalculator.HeadlineFor(weather, "az")));
            Assert.False(string.IsNullOrWhiteSpace(WorldStateCalculator.HeadlineFor(weather, "en")));
            Assert.NotEqual(
                WorldStateCalculator.HeadlineFor(weather, "az"),
                WorldStateCalculator.HeadlineFor(weather, "en"));
        }
    }
}

public class BadgeRulesTests
{
    [Fact]
    public void Evaluate_IlkCavabdanSonraFirstStepsVerilir()
    {
        var earned = BadgeRules.Evaluate(new BadgeStats { AnsweredCount = 1 });

        Assert.Contains("first-steps", earned);
    }

    [Fact]
    public void Evaluate_FealiyyetYoxdursaNisanYoxdur()
    {
        var earned = BadgeRules.Evaluate(new BadgeStats());

        Assert.Empty(earned);
    }

    [Fact]
    public void Evaluate_YeddiGunlukArdicilliqHerIkiStreakNisaniniVerir()
    {
        var earned = BadgeRules.Evaluate(new BadgeStats { StreakDays = 7 });

        Assert.Contains("streak-3", earned);
        Assert.Contains("streak-7", earned);
    }

    [Fact]
    public void Evaluate_HeddeCatmayanSayNisanVermir()
    {
        var earned = BadgeRules.Evaluate(new BadgeStats { MathCorrect = 24, DiscoveryCount = 4, CareActionCount = 19 });

        Assert.DoesNotContain("math-starter", earned);
        Assert.DoesNotContain("explorer", earned);
        Assert.DoesNotContain("pet-friend", earned);
    }

    [Fact]
    public void Evaluate_HeddeCatanSayNisanVerir()
    {
        var earned = BadgeRules.Evaluate(new BadgeStats { MathCorrect = 25, DiscoveryCount = 5, CareActionCount = 20 });

        Assert.Contains("math-starter", earned);
        Assert.Contains("explorer", earned);
        Assert.Contains("pet-friend", earned);
    }
}
