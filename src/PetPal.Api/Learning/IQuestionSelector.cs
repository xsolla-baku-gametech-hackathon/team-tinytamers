using PetPal.Api.Entities;
using PetPal.Shared.Enums;

namespace PetPal.Api.Learning;

public interface IQuestionSelector
{
    /// <summary>Uşağın dilində, cari reytinqinə uyğun və yaxınlarda görmədiyi sualları seçir.</summary>
    Task<List<Question>> SelectAsync(
        Guid childId, SkillArea skill, string languageCode, int rating, int age, int count, CancellationToken ct = default);
}
