using PetPal.Shared.Enums;

namespace PetPal.Api.Entities;

/// <summary>
/// AÇIQ məzmun seçimi: «bunu bəyənirəm», «bunu daha az göstər», «bunu
/// bloklayıram».
///
/// <para><b>Nə üçün xassə cədvəlində deyil?</b> <see cref="PlayerTrait"/>
/// öyrənilən bir ehtimaldır: köhnəlir, inamı var və başqa siqnallarla
/// yumşalır. Açıq söz isə ehtimal deyil — o, uşağın və ya valideynin
/// qərarıdır və sistemin təxmini onu üstələməməlidir. İkisini bir sətirdə
/// saxlamaq «uşaq dedi ki, göstərmə» faktını üç macəradan sonra silərdi.</para>
///
/// <para><b>Blok yalnız valideyndən gəlir.</b> Uşaq «daha az göstər» deyə
/// bilir — bu, mövzunu YOX ETMİR, geri çəkir: uşaq öz dünyasını təsadüfən
/// bağlamamalıdır. Tam blok valideyn qərarıdır.</para>
/// </summary>
public class ContentPreference
{
    public Guid Id { get; set; }

    public Guid ChildProfileId { get; set; }
    public ChildProfile ChildProfile { get; set; } = null!;

    public PetBrainContentScope Scope { get; set; }

    /// <summary>Təsdiqlənmiş açar: mövzu, şablon və ya mexanika.</summary>
    public string Key { get; set; } = string.Empty;

    public PetBrainContentPreferenceKind Kind { get; set; }

    /// <summary>
    /// Seçimi kim etdi. Valideyn qeydini uşaq ləğv edə bilmir — endpoint
    /// səviyyəsində yoxlanılır.
    /// </summary>
    public PetBrainSettingSource Source { get; set; } = PetBrainSettingSource.Child;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// «Daha az göstər» üçün soyuma müddətinin sonu.
    ///
    /// <para>Müddət qəsdən var: uşağın bir gün dediyi «bunu istəmirəm»
    /// həmişəlik bir qapı bağlamamalıdır. Bəyənmə və valideyn bloku üçün
    /// <c>null</c> qalır — onlar öz-özünə keçmir.</para>
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>«Daha az göstər» neçə dəfə deyilib — təkrar müddəti uzadır.</summary>
    public int RepeatCount { get; set; }

    /// <summary>Bu qeyd hazırda qüvvədədirmi.</summary>
    public bool IsActiveAt(DateTime now) => ExpiresAt is null || ExpiresAt > now;
}
