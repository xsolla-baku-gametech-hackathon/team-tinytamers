using PetPal.Shared.Enums;

namespace PetPal.Api.Entities;

/// <summary>
/// Uşağın bir xassəsi — maraq ("kosmos") və ya oyun üslubu ("kəşfiyyatçı").
///
/// <para>Sətir modeli qəsdən seçilib: onlarla nullable sütun əvəzinə hər xassə
/// bir sətirdir, yəni yeni açar əlavə etmək miqrasiya tələb etmir. Açarlar isə
/// SƏRBƏST DEYİL — <see cref="PetBrain.TraitKeys"/> siyahısındadır və klientdən
/// heç vaxt qəbul edilmir.</para>
///
/// <para>Bu, <see cref="SkillMastery"/> ilə qarışdırılmamalıdır: ora məktəb
/// mənimsəməsidir (Elo), bura isə maraqdır. Arena, yaradıcı seçim və mini oyun
/// davranışı reytinqə toxunmamalıdır.</para>
/// </summary>
public class PlayerTrait
{
    public Guid Id { get; set; }

    public Guid ChildProfileId { get; set; }
    public ChildProfile ChildProfile { get; set; } = null!;

    public PetBrainTraitCategory Category { get; set; }

    /// <summary>Təsdiqlənmiş açar: space, science, puzzles, creative, explorer…</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>0–100 aralığında. Bir hərəkət bunu beş baldan çox dəyişə bilməz.</summary>
    public int Score { get; set; }

    public DateTime UpdatedAt { get; set; }
}
