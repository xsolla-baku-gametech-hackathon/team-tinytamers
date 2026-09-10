using PetPal.Api.Entities;
using PetPal.Api.Pets;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

public class PetProgressionTests
{
    [Theory]
    [InlineData(1, PetStage.Newborn)]
    [InlineData(2, PetStage.Newborn)]
    [InlineData(3, PetStage.Baby)]
    [InlineData(5, PetStage.Baby)]
    [InlineData(6, PetStage.Child)]
    [InlineData(9, PetStage.Child)]
    [InlineData(10, PetStage.Teen)]
    [InlineData(14, PetStage.Teen)]
    [InlineData(15, PetStage.Adult)]
    [InlineData(21, PetStage.Adult)]
    [InlineData(22, PetStage.Elder)]
    [InlineData(40, PetStage.Elder)]
    public void StageFor_SeviyyeyeGoreMerheleSecir(int level, PetStage expected)
    {
        Assert.Equal(expected, PetProgression.StageFor(level));
    }

    /// <summary>
    /// Hər yaş əvvəlkindən bahadır — bu qayda pozulsa, pet bir günə "böyük"
    /// olar və mərhələ dəyişikliyi mükafat olmaqdan çıxar.
    /// </summary>
    [Fact]
    public void XpToNextLevel_HerYasEvvelkindenBahadir()
    {
        for (var level = 1; level < 30; level++)
        {
            Assert.True(
                PetProgression.XpToNextLevel(level + 1) > PetProgression.XpToNextLevel(level),
                $"{level + 1} yaş {level} yaşdan ucuz çıxdı.");
        }

        Assert.Equal(100, PetProgression.XpToNextLevel(1));
        Assert.Equal(165, PetProgression.XpToNextLevel(2));
        Assert.Equal(1405, PetProgression.XpToNextLevel(10));
    }

    [Fact]
    public void AddXp_HeddiKecendeSeviyyeArtir()
    {
        var pet = new Pet { Level = 1, Xp = 0 };

        var leveledUp = PetProgression.AddXp(pet, PetProgression.XpToNextLevel(1));

        Assert.True(leveledUp);
        Assert.Equal(2, pet.Level);
        Assert.Equal(0, pet.Xp);
    }

    [Fact]
    public void AddXp_BirDefedeBirNeceSeviyyeQaldiraBiler()
    {
        var pet = new Pet { Level = 1, Xp = 0 };

        PetProgression.AddXp(pet, 1000);

        Assert.True(pet.Level >= 4);
    }

    [Fact]
    public void AddXp_HeddenAsagidaSeviyyeDeyismir()
    {
        var pet = new Pet { Level = 3, Xp = 0 };

        var leveledUp = PetProgression.AddXp(pet, 10);

        Assert.False(leveledUp);
        Assert.Equal(3, pet.Level);
        Assert.Equal(10, pet.Xp);
    }

    [Fact]
    public void ApplyDecay_VaxtKecdikceStatlarAzalir()
    {
        var start = new DateTime(2026, 8, 4, 9, 0, 0, DateTimeKind.Utc);
        var pet = new Pet { Fullness = 100, Cleanliness = 100, Happiness = 100, Energy = 100, LastDecayAt = start };

        PetProgression.ApplyDecay(pet, start.AddHours(6));

        Assert.True(pet.Fullness < 100);
        Assert.True(pet.Happiness < 100);
    }

    [Fact]
    public void ApplyDecay_QisaMuddetdeHecNeDeyismir()
    {
        var start = new DateTime(2026, 8, 4, 9, 0, 0, DateTimeKind.Utc);
        var pet = new Pet { Fullness = 80, LastDecayAt = start };

        PetProgression.ApplyDecay(pet, start.AddMinutes(5));

        Assert.Equal(80, pet.Fullness);
        Assert.Equal(start, pet.LastDecayAt);
    }

    [Fact]
    public void ApplyDecay_StatlarSifirinAltinaDusmur()
    {
        var start = new DateTime(2026, 8, 4, 9, 0, 0, DateTimeKind.Utc);
        var pet = new Pet { Fullness = 10, Cleanliness = 10, Happiness = 10, Energy = 10, LastDecayAt = start };

        PetProgression.ApplyDecay(pet, start.AddDays(30));

        Assert.Equal(0, pet.Fullness);
        Assert.Equal(0, pet.Energy);
        Assert.True(pet.Cleanliness >= 0);
        Assert.True(pet.Happiness >= 0);
    }

    [Fact]
    public void ApplyDecay_TekrarCagirisdaIkiQatAzalmaOlmur()
    {
        var start = new DateTime(2026, 8, 4, 9, 0, 0, DateTimeKind.Utc);
        var pet = new Pet { Fullness = 100, LastDecayAt = start };
        var now = start.AddHours(3);

        PetProgression.ApplyDecay(pet, now);
        var afterFirst = pet.Fullness;
        PetProgression.ApplyDecay(pet, now);

        Assert.Equal(afterFirst, pet.Fullness);
    }

    [Theory]
    [InlineData(0, 80, 80, 80, PetMood.Sad)]
    [InlineData(80, 10, 80, 80, PetMood.Sleepy)]
    [InlineData(80, 80, 10, 80, PetMood.Hungry)]
    [InlineData(80, 80, 80, 10, PetMood.Dirty)]
    [InlineData(95, 90, 90, 90, PetMood.Excited)]
    [InlineData(70, 90, 90, 90, PetMood.Happy)]
    [InlineData(45, 90, 90, 90, PetMood.Neutral)]
    public void MoodFor_StatlaraGoreEhvalSecir(
        int happiness, int energy, int fullness, int cleanliness, PetMood expected)
    {
        var pet = new Pet
        {
            Happiness = happiness,
            Energy = energy,
            Fullness = fullness,
            Cleanliness = cleanliness
        };

        Assert.Equal(expected, PetProgression.MoodFor(pet));
    }

    [Fact]
    public void PetVoice_UsaginAdiniIsledir()
    {
        var message = PetVoice.Idle("az", PetMood.Excited, "Ava", "Max");

        Assert.Contains("Ava", message);
    }

    [Fact]
    public void PetVoice_DilSecimineGoreFerqliMetnQaytarir()
    {
        var az = PetVoice.Idle("az", PetMood.Hungry, "Ava", "Max");
        var en = PetVoice.Idle("en", PetMood.Hungry, "Ava", "Max");

        Assert.NotEqual(az, en);
        Assert.Contains("Ac", az);
        Assert.Contains("hungry", en);
    }

    [Fact]
    public void PetVoice_SehvCavabdaDestekleyicidir()
    {
        var az = PetVoice.AnswerReaction("az", isCorrect: false, correctCount: 0, SkillArea.Math, "Max");

        // Ton qaydası: pet uşağı heç vaxt danlamır.
        Assert.Contains("Max", az);
        Assert.DoesNotContain("səhv", az, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PetVoice_ButunEhvallarUcunMetnVar()
    {
        foreach (var mood in Enum.GetValues<PetMood>())
        {
            Assert.False(string.IsNullOrWhiteSpace(PetVoice.Idle("az", mood, "Ava", "Max")));
            Assert.False(string.IsNullOrWhiteSpace(PetVoice.Idle("en", mood, "Ava", "Max")));
        }
    }
}
