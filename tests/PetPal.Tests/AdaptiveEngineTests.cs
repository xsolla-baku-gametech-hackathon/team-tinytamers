using PetPal.Api.Learning;

namespace PetPal.Tests;

/// <summary>Adaptiv mühərrik saf funksiyalardan ibarətdir — birbaşa yoxlanılır.</summary>
public class AdaptiveEngineTests
{
    [Theory]
    [InlineData(100, 1)]
    [InlineData(300, 3)]
    [InlineData(550, 6)]
    [InlineData(1000, 10)]
    public void TargetDifficulty_ReytinqiCetinliyeCevirir(int rating, int expected)
    {
        Assert.Equal(expected, AdaptiveEngine.TargetDifficulty(rating));
    }

    [Fact]
    public void UpdateRating_DogruCavabdaReytinqArtir()
    {
        var updated = AdaptiveEngine.UpdateRating(AdaptiveEngine.StartingRating, difficulty: 3, isCorrect: true);

        Assert.True(updated > AdaptiveEngine.StartingRating);
    }

    [Fact]
    public void UpdateRating_SehvCavabdaReytinqAzalir()
    {
        var updated = AdaptiveEngine.UpdateRating(AdaptiveEngine.StartingRating, difficulty: 3, isCorrect: false);

        Assert.True(updated < AdaptiveEngine.StartingRating);
    }

    [Fact]
    public void UpdateRating_CetinSualiDogruBilmekDahaCoxQazandirir()
    {
        var easyGain = AdaptiveEngine.UpdateRating(500, difficulty: 2, isCorrect: true) - 500;
        var hardGain = AdaptiveEngine.UpdateRating(500, difficulty: 9, isCorrect: true) - 500;

        Assert.True(hardGain > easyGain);
    }

    [Fact]
    public void UpdateRating_AsanSualiSehvBilmekDahaCoxItirdir()
    {
        var easyLoss = 500 - AdaptiveEngine.UpdateRating(500, difficulty: 2, isCorrect: false);
        var hardLoss = 500 - AdaptiveEngine.UpdateRating(500, difficulty: 9, isCorrect: false);

        Assert.True(easyLoss > hardLoss);
    }

    [Fact]
    public void UpdateRating_ReytinqSerhedlerdenKenaraCixmir()
    {
        var floor = AdaptiveEngine.MinRating;
        var ceiling = AdaptiveEngine.MaxRating;

        for (var i = 0; i < 100; i++)
        {
            floor = AdaptiveEngine.UpdateRating(floor, difficulty: 10, isCorrect: false);
            ceiling = AdaptiveEngine.UpdateRating(ceiling, difficulty: 1, isCorrect: true);
        }

        Assert.Equal(AdaptiveEngine.MinRating, floor);
        Assert.Equal(AdaptiveEngine.MaxRating, ceiling);
    }

    [Fact]
    public void ExpectedScore_OzSeviyyesindekiSualdaTeqriben50Faizdir()
    {
        var expected = AdaptiveEngine.ExpectedScore(childRating: 400, difficulty: 4);

        Assert.InRange(expected, 0.49, 0.51);
    }

    [Fact]
    public void StarsFor_YalnizDogruCavabdaUlduzVerir()
    {
        Assert.Equal(0, AdaptiveEngine.StarsFor(5, isCorrect: false));
        Assert.True(AdaptiveEngine.StarsFor(5, isCorrect: true) > 0);
    }

    [Fact]
    public void XpFor_SehvCavabdaDaKicikXpVerir()
    {
        // Cəhd etmək cəzalandırılmır — uşaq davam etməyə həvəsləndirilir.
        Assert.True(AdaptiveEngine.XpFor(5, isCorrect: false) > 0);
        Assert.True(AdaptiveEngine.XpFor(5, isCorrect: true) > AdaptiveEngine.XpFor(5, isCorrect: false));
    }

    [Theory]
    [InlineData(100, 0)]
    [InlineData(1000, 100)]
    public void MasteryPercent_0_100_AraliginaNormallasdirir(int rating, int expected)
    {
        Assert.Equal(expected, AdaptiveEngine.MasteryPercent(rating));
    }
}
