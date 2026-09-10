namespace PetPal.Api.Entities;

/// <summary>
/// Dünya xəritəsindəki bölgə. Ulduz həddi keçildikdə açılır, missiyalar
/// tamamlandıqca "bərpa" faizi artır.
/// </summary>
public class WorldZone
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconKey { get; set; } = string.Empty;

    // Azərbaycanca variantlar. Boş olarsa ingiliscə mətn göstərilir.
    public string? NameAz { get; set; }
    public string? DescriptionAz { get; set; }

    public int RequiredStars { get; set; }
    public int SortOrder { get; set; }

    public ICollection<Mission> Missions { get; set; } = new List<Mission>();
}
