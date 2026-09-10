using PetPal.Shared.Dtos.Home;
using PetPal.Shared.Dtos.Learning;
using PetPal.Shared.Dtos.Pets;
using PetPal.Shared.Dtos.Progress;
using PetPal.Shared.Dtos.Rewards;

namespace PetPal.App.Ui.Services;

/// <summary>
/// Ekranlar arasında paylaşılan oyun vəziyyəti. Üst paneldəki ulduz sayı
/// sual cavablandırılan kimi dəyişməlidir — hər ekran öz nüsxəsini saxlasa,
/// bu mümkün olmazdı.
/// </summary>
public class AppState
{
    public event Action? Changed;

    public WalletDto Wallet { get; private set; } = new();

    public PetDto? Pet { get; private set; }

    public DailyGoalDto DailyGoal { get; private set; } = new();

    public void ApplyHome(HomeStateDto home)
    {
        Wallet = home.Wallet;
        Pet = home.Pet;
        DailyGoal = home.DailyGoal;
        Notify();
    }

    public void ApplyWallet(WalletDto wallet)
    {
        Wallet = wallet;
        Notify();
    }

    public void ApplyPet(PetDto pet)
    {
        Pet = pet;
        Notify();
    }

    /// <summary>
    /// Cavabdan sonra serverə əlavə sorğu göndərmədən balansı və hədəfi yeniləyir —
    /// rəqəmlər dərhal artır, uşaq gecikmə hiss etmir.
    /// </summary>
    public void ApplyAnswer(AnswerResultDto result)
    {
        Wallet = new WalletDto { Stars = Wallet.Stars + result.StarsEarned, Gems = Wallet.Gems };
        DailyGoal = new DailyGoalDto
        {
            Date = DailyGoal.Date,
            Target = result.DailyGoalTarget,
            Completed = result.DailyGoalDone,
            StreakDays = DailyGoal.StreakDays
        };
        Notify();
    }

    public void Reset()
    {
        Wallet = new WalletDto();
        Pet = null;
        DailyGoal = new DailyGoalDto();
        Notify();
    }

    private void Notify() => Changed?.Invoke();
}
