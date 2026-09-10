using PetPal.Api.Entities;
using PetPal.Api.Pets;

namespace PetPal.Tests;

public class PetAccessoryTests
{
    /// <summary>Yumurtaya boyunbağı taxmaq olmaz — əşyalar yalnız açılışdan sonra gəlir.</summary>
    [Fact]
    public void UnlockEarned_YumurtaRejimindeHecNeAcilmir()
    {
        var pet = new Pet { Level = 12, Happiness = 100, HatchedAt = null };

        var unlocked = PetAccessories.UnlockEarned(pet);

        Assert.Empty(unlocked);
        Assert.Empty(pet.UnlockedAccessories);
    }

    [Fact]
    public void UnlockEarned_SertOdenendeEsyaAcilirVeDerhalTaxilir()
    {
        var pet = new Pet { Level = 2, Happiness = 50, HatchedAt = DateTime.UtcNow };

        var unlocked = PetAccessories.UnlockEarned(pet);

        Assert.Contains("collar-classic", unlocked);
        Assert.Contains("collar-classic", pet.UnlockedAccessories);

        // Mükafat dərhal görünməlidir — uşaq onu sonra şkafdan çıxara bilər.
        Assert.Contains("collar-classic", pet.EquippedAccessories);
    }

    // ---------- Görünüşü uşaq qurur ----------

    [Fact]
    public void TryEquip_AcilmisEsyaTaxilir()
    {
        var pet = new Pet { Level = 3, Happiness = 95, HatchedAt = DateTime.UtcNow };
        PetAccessories.UnlockEarned(pet);

        var ok = PetAccessories.TryEquip(pet, ["bow-red"]);

        Assert.True(ok);
        Assert.Equal(["bow-red"], pet.EquippedAccessories);
    }

    [Fact]
    public void TryEquip_BosSiyahiHamisiniCixarir()
    {
        var pet = new Pet { Level = 3, Happiness = 95, HatchedAt = DateTime.UtcNow };
        PetAccessories.UnlockEarned(pet);

        var ok = PetAccessories.TryEquip(pet, []);

        Assert.True(ok);
        Assert.Empty(pet.EquippedAccessories);

        // Çıxarmaq açılışı ləğv etmir — əşya şkafda qalır.
        Assert.NotEmpty(pet.UnlockedAccessories);
    }

    [Fact]
    public void TryEquip_KilidliVeNamelumKodImtinaEdilir()
    {
        var pet = new Pet { Level = 2, Happiness = 50, HatchedAt = DateTime.UtcNow };
        PetAccessories.UnlockEarned(pet);
        var before = pet.EquippedAccessories.ToList();

        Assert.False(PetAccessories.TryEquip(pet, ["crown-gold"]));
        Assert.False(PetAccessories.TryEquip(pet, ["sombrero"]));

        // İmtina olunan sorğu görünüşə heç nə etməməlidir.
        Assert.Equal(before, pet.EquippedAccessories);
    }

    [Fact]
    public void TryEquip_TekrarlananKodBirDefeYazilir()
    {
        var pet = new Pet { Level = 2, Happiness = 50, HatchedAt = DateTime.UtcNow };
        PetAccessories.UnlockEarned(pet);

        Assert.True(PetAccessories.TryEquip(pet, ["collar-classic", "collar-classic"]));
        Assert.Equal(["collar-classic"], pet.EquippedAccessories);
    }

    [Fact]
    public void UnlockEarned_SertOdenmeyibseAcilmir()
    {
        var pet = new Pet { Level = 1, Happiness = 100, HatchedAt = DateTime.UtcNow };

        var unlocked = PetAccessories.UnlockEarned(pet);

        Assert.Empty(unlocked);
        Assert.Empty(pet.UnlockedAccessories);
    }

    [Fact]
    public void UnlockEarned_XosbextlikSertiDeYoxlanilir()
    {
        // bow-red: səviyyə 3 + xoşbəxtlik 90
        var pet = new Pet { Level = 5, Happiness = 89, HatchedAt = DateTime.UtcNow };

        PetAccessories.UnlockEarned(pet);
        Assert.DoesNotContain("bow-red", pet.UnlockedAccessories);

        pet.Happiness = 90;
        PetAccessories.UnlockEarned(pet);
        Assert.Contains("bow-red", pet.UnlockedAccessories);
    }

    /// <summary>
    /// Ən vacib qayda: xoşbəxtlik vaxt keçdikcə özü azalır. Qazanılmış əşyanın
    /// geri alınması uşaq üçün cəza kimi görünərdi.
    /// </summary>
    [Fact]
    public void UnlockEarned_AcilmisEsyaXosbextlikDusendeDeQalir()
    {
        var pet = new Pet { Level = 5, Happiness = 95, HatchedAt = DateTime.UtcNow };
        PetAccessories.UnlockEarned(pet);
        Assert.Contains("bow-red", pet.UnlockedAccessories);

        pet.Happiness = 10;
        PetAccessories.UnlockEarned(pet);

        Assert.Contains("bow-red", pet.UnlockedAccessories);
    }

    [Fact]
    public void UnlockEarned_TekrarCagirisdaDublikatYaratmir()
    {
        var pet = new Pet { Level = 12, Happiness = 100, HatchedAt = DateTime.UtcNow };

        PetAccessories.UnlockEarned(pet);
        var second = PetAccessories.UnlockEarned(pet);

        Assert.Empty(second);
        Assert.Equal(pet.UnlockedAccessories.Distinct().Count(), pet.UnlockedAccessories.Count);
    }

    [Fact]
    public void Describe_KilidliEsyalariDaSertiIleQaytarir()
    {
        var pet = new Pet { Level = 2, Happiness = 50, HatchedAt = DateTime.UtcNow };
        PetAccessories.UnlockEarned(pet);

        var described = PetAccessories.Describe(pet, "az");

        Assert.Equal(PetAccessories.Catalog.Count, described.Count);

        var collar = described.Single(a => a.Code == "collar-classic");
        Assert.True(collar.IsUnlocked);

        var crown = described.Single(a => a.Code == "crown-gold");
        Assert.False(crown.IsUnlocked);
        Assert.Contains("12", crown.Requirement);
    }

    [Fact]
    public void Describe_DiliNezereAlir()
    {
        var pet = new Pet { Level = 1, HatchedAt = DateTime.UtcNow };

        Assert.Equal("Qızıl tac", PetAccessories.Describe(pet, "az").Single(a => a.Code == "crown-gold").Name);
        Assert.Equal("Golden crown", PetAccessories.Describe(pet, "en").Single(a => a.Code == "crown-gold").Name);
    }

    [Fact]
    public void Refresh_HemStatlariAzaldirHemEsyaAcir()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var pet = new Pet
        {
            Level = 6,
            Happiness = 80,
            Fullness = 100,
            HatchedAt = now.AddDays(-1),
            LastDecayAt = now.AddHours(-5)
        };

        var unlocked = PetProgression.Refresh(pet, now);

        Assert.Contains("glasses-round", unlocked);
        Assert.True(pet.Fullness < 100);
    }
}
