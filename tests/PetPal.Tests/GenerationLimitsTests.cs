using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using PetPal.Api.PetBrain.Media;
using PetPal.Api.PetBrain.Puzzles;
using PetPal.Api.PetBrain.Scenery;
using PetPal.Api.Wardrobe;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Gündəlik generasiya hədləri 11.09.2026-da ləğv edildi.
///
/// <para>Səhnə, recap və paltar üçün say tavanı standart olaraq YOXDUR (0).
/// Xərci hash keşi, şəkil və video başına kredit tavanı və xərc kəsicisi
/// saxlayır. Müsbət dəyər yazılsa köhnə tavan geri qayıdır — o yol
/// <see cref="PetBrainAdventureSceneryTests"/>, recap və paltar testlərində
/// yoxlanılır.</para>
/// </summary>
public class GenerationLimitsTests
{
    [Fact]
    public void StandartDeyerler_GundelikSayHeddiYoxdur()
    {
        var media = new PetBrainMediaOptions();
        var wardrobe = new WardrobeOptions();

        Assert.Equal(0, media.MaxPaidScenesPerDay);
        Assert.Equal(0, media.MaxPaidRecapsPerChildPerDay);
        Assert.Equal(0, media.MaxPaidRecapsPerDay);
        Assert.Equal(0, wardrobe.DesignsPerChildPerDay);
        Assert.Equal(0, wardrobe.MaxPaidImagesPerDay);
    }

    /// <summary>Qiymət qoruyucuları yerində qalır — ləğv edilən yalnız say həddidir.</summary>
    [Fact]
    public void QiymetQoruyuculari_YerindeQalir()
    {
        var media = new PetBrainMediaOptions();
        var wardrobe = new WardrobeOptions();

        Assert.True(media.MaxImageCreditsPerRun > 0);
        Assert.True(media.MaxVideoCreditsPerRun > 0);
        Assert.True(wardrobe.MaxCreditsPerImage > 0);
    }

    /// <summary>
    /// Runway növbəsi dolu olanda şəkil iki dəqiqədən çox çəkir: paltar işi
    /// ondan tez kəsilməməlidir, yoxsa ödənmiş şəkil itir.
    /// </summary>
    [Fact]
    public void PaltarGozlemesi_IkiDeqiqedenUzundur()
    {
        Assert.True(new WardrobeOptions().TimeoutSeconds >= 240);
    }

    /// <summary>
    /// Köhnə 20-lik tavanı keçən sayda YENİ səhnə açılır və heç biri ehtiyata
    /// düşmür.
    /// </summary>
    [Fact]
    public async Task SehneHeddiYoxdursa_IyirmidenCoxYeniSehneNovbeyeDusur()
    {
        using var factory = new PetBrainIllustrationFactory();
        using var scope = factory.Services.CreateScope();

        var coordinator = scope.ServiceProvider.GetRequiredService<PuzzleIllustrationCoordinator>();

        for (var number = 0; number < 30; number++)
        {
            var row = await coordinator.EnsureRowAsync(new NumberedScene(number), CancellationToken.None);

            Assert.Equal(PetBrainIllustrationStatus.Pending, row.Status);
            Assert.Empty(row.FailureReason);
        }
    }

    /// <summary>Hər biri ayrı hash verən sadə səhnə — yalnız sayı yoxlamaq üçün.</summary>
    private sealed class NumberedScene(int number) : IStoryScene
    {
        public string SceneKey => "limit-probe";

        public int PromptVersion => 1;

        public string Hash() =>
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"limit-probe-{number}")));

        public string BuildPrompt() => $"A calm meadow, scene {number}.";

        public string AltText() => $"Scene {number}.";
    }
}
