namespace PetPal.Api.Progress;

/// <summary>
/// Ekran vaxtı mühafizəsinin açarı.
///
/// <see cref="Enforced"/> <c>false</c> olduqda qaydalar hesablanmır və uşaq
/// heç vaxt bloklanmır — nə yuxu rejimi, nə gündəlik limit. Qaydaların özü
/// yerində qalır, sadəcə tətbiq olunmur; geri qaytarmaq bir konfiqurasiya
/// sətridir.
///
/// Bu açar inkişaf/nümayiş üçündür: gecə saatlarında tətbiqi yoxlamaq mümkün
/// olmurdu, çünki 21:00-dan sonra bütün ekranlar "yuxu vaxtıdır" ilə əvəz olunurdu.
/// </summary>
public class ScreenTimeOptions
{
    public const string SectionName = "ScreenTime";

    public bool Enforced { get; set; } = true;
}
