namespace PetPal.Api.Discoveries;

/// <summary>
/// Bazadakı şəkil açarının normallaşdırılması.
///
/// <para>Köhnə qeydlərdə açar QOVLUQ PREFİKSİ İLƏ yazılırdı
/// (<c>uploads/discoveries/&lt;uşaq&gt;/&lt;fayl&gt;</c>), yeni açar isə yalnız
/// <c>&lt;uşaq&gt;/&lt;fayl&gt;</c>-dır — belə olanda saxlama kökü dəyişsə
/// (volume mount edilsə və ya obyekt saxlamaya keçilsə) köhnə sətirlər sınmır.
/// Miqrasiya yazmaqdansa oxu anında prefiks kəsilir: baza toxunulmur və
/// hər iki format eyni faylı tapır.</para>
/// </summary>
public static class DiscoveryPhotoKey
{
    public static string Normalize(string key, string storageFolder)
    {
        var clean = key.Replace('\\', '/').TrimStart('/');
        var prefix = Folder(storageFolder) + "/";

        return clean.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? clean[prefix.Length..]
            : clean;
    }

    /// <summary>
    /// Qovluq adının təmiz forması: sonda «/» olsa da olmasa da eyni nəticə.
    ///
    /// <para>Bu, göründüyündən vacibdir: konfiqurasiyada
    /// <c>"uploads/discoveries/"</c> yazılsa, <c>Path.Combine</c> faylı düzgün
    /// yerə YAZIRDI, oxu tərəfi isə kökü fərqli hesabladığına görə heç bir
    /// şəkli tapa bilmirdi — yəni tək bir «/» simvolu bütün kolleksiyanı
    /// görünməz edirdi.</para>
    /// </summary>
    public static string Folder(string storageFolder) =>
        storageFolder.Replace('\\', '/').Trim('/');
}
