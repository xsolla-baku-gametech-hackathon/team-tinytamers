using PetPal.Api.Entities;

namespace PetPal.Api.Progress;

public interface IDailyGoalService
{
    /// <summary>Bugünkü hədəf sətrini qaytarır, yoxdursa yaradır (hələ yazmır).</summary>
    Task<DailyGoal> GetOrCreateTodayAsync(ChildProfile child, CancellationToken ct = default);

    /// <summary>
    /// Hədəf ilk dəfə tamamlananda bonus verir və streak-i yeniləyir.
    /// Təkrar çağırışlar təsirsizdir.
    /// </summary>
    Task<bool> SettleIfReachedAsync(ChildProfile child, DailyGoal goal, CancellationToken ct = default);
}
