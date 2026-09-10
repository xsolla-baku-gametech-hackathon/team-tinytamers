using PetPal.App.Ui.Services;
using PetPal.Shared.Dtos.Home;
using PetPal.Shared.Dtos.Learning;
using PetPal.Shared.Dtos.Pets;
using PetPal.Shared.Dtos.Progress;
using PetPal.Shared.Dtos.Rewards;

namespace PetPal.App.Tests;

/// <summary>
/// Üst paneldəki ulduz sayı cavabdan dərhal sonra artmalıdır — bu davranış
/// <see cref="AppState"/>-in üzərindədir və serverdən asılı olmadan test olunur.
/// </summary>
public class AppStateTests
{
    [Fact]
    public void ApplyAnswer_UlduzlariDerhalArtirir()
    {
        var state = new AppState();
        state.ApplyWallet(new WalletDto { Stars = 100, Gems = 3 });

        state.ApplyAnswer(new AnswerResultDto { StarsEarned = 12, DailyGoalDone = 2, DailyGoalTarget = 5 });

        Assert.Equal(112, state.Wallet.Stars);
        Assert.Equal(3, state.Wallet.Gems);
    }

    [Fact]
    public void ApplyAnswer_GundelikHedefiYenileyir()
    {
        var state = new AppState();

        state.ApplyAnswer(new AnswerResultDto { StarsEarned = 10, DailyGoalDone = 5, DailyGoalTarget = 5 });

        Assert.Equal(5, state.DailyGoal.Completed);
        Assert.Equal(5, state.DailyGoal.Target);
        Assert.True(state.DailyGoal.IsReached);
    }

    [Fact]
    public void ApplyAnswer_SehvCavabdaBalansDeyismir()
    {
        var state = new AppState();
        state.ApplyWallet(new WalletDto { Stars = 40 });

        state.ApplyAnswer(new AnswerResultDto { StarsEarned = 0, DailyGoalDone = 1, DailyGoalTarget = 5 });

        Assert.Equal(40, state.Wallet.Stars);
    }

    [Fact]
    public void ApplyHome_ButunVeziyyetiQurur()
    {
        var state = new AppState();

        state.ApplyHome(new HomeStateDto
        {
            Pet = new PetDto { Name = "Max", Level = 4 },
            Wallet = new WalletDto { Stars = 250, Gems = 12 },
            DailyGoal = new DailyGoalDto { Target = 5, Completed = 3, StreakDays = 2 }
        });

        Assert.Equal("Max", state.Pet?.Name);
        Assert.Equal(250, state.Wallet.Stars);
        Assert.Equal(3, state.DailyGoal.Completed);
    }

    [Fact]
    public void Changed_DeyisiklikdeIsleyir()
    {
        var state = new AppState();
        var notifications = 0;
        state.Changed += () => notifications++;

        state.ApplyWallet(new WalletDto { Stars = 1 });
        state.ApplyPet(new PetDto { Name = "Luna" });

        Assert.Equal(2, notifications);
    }

    [Fact]
    public void Reset_ProfilDeyisendeVeziyyetiTemizleyir()
    {
        var state = new AppState();
        state.ApplyWallet(new WalletDto { Stars = 500, Gems = 20 });
        state.ApplyPet(new PetDto { Name = "Max" });

        state.Reset();

        Assert.Equal(0, state.Wallet.Stars);
        Assert.Equal(0, state.Wallet.Gems);
        Assert.Null(state.Pet);
    }
}
