using PetPal.Api.Pets;

namespace PetPal.Api.Ai;

/// <summary>
/// Standart implementasiya — mövcud <see cref="PetVoice"/> replikalarını qaytarır.
/// AI söndürülübsə bu işləyir və app tam funksionaldır.
/// </summary>
public sealed class RuleBasedPetVoiceGenerator : IPetVoiceGenerator
{
    public Task<string> IdleAsync(PetVoiceContext context, CancellationToken ct = default) =>
        Task.FromResult(PetVoice.Idle(context.Language, context.Mood, context.ChildName, context.PetName));
}
