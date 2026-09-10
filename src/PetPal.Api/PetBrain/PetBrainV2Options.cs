namespace PetPal.Api.PetBrain;

/// <summary>
/// Pet Brain V2 mərhələlərinin açarları.
///
/// <para><b>Standartlar qəsdən qarışıqdır.</b> Uşağın gördüyü, deterministik və
/// tam test olunmuş hissələr (ortaq kontekst, qərar qeydi, "başqa fikir",
/// budaqlanan hekayə) AÇIQDIR — onlar məhsulun özüdür. Modelə plan qurdurmaq
/// isə BAĞLIDIR: o, hələ nəzarət altında sınaqdan keçir və heç bir uşaq onu
/// təsadüfən almamalıdır.</para>
///
/// <para><b>Hamısı bağlı olsa da app tam işləyir</b> — bu, arxitekturanın
/// şərtidir, konfiqurasiya seçimi deyil: deterministik yol həmişə tam yoldur.</para>
/// </summary>
public class PetBrainV2Options
{
    public const string SectionName = "PetBrainV2";

    /// <summary>
    /// Ortaq <c>PetMindContext</c>, bağ pillələri və xarakterin görünən səsi.
    /// Bağlandıqda ekranlar köhnə, dar kontekstlə işləyir.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Budaqlanan hekayə qrafı.
    ///
    /// <para>Bağlandıqda kataloqdakı bütün macəralar xətti şablonla oynanır.
    /// <b>Yarımçıq qraf run-ları bundan zərər görmür</b>: run özü hansı modeldə
    /// olduğunu daşıyır və başladığı yolla bitirilir.</para>
    /// </summary>
    public bool StoryGraphEnabled { get; set; } = true;

    /// <summary>Pet-in məhdud avtonomiyası (niyyətlər). Hələ qurulmayıb — bax docs/PET_BRAIN.md.</summary>
    public bool IntentEnabled { get; set; }

    /// <summary>
    /// Modelin təsdiqlənmiş ID-lər arasından plan təklif etməsi. Hələ
    /// qurulmayıb; validator olmadan HEÇ VAXT açılmamalıdır.
    /// </summary>
    public bool BoundedAiEnabled { get; set; }

    /// <summary>
    /// Bir sessiyada "başqa fikir" düyməsinin işləyə biləcəyi ən çox say.
    ///
    /// <para>Limit uşağı cəzalandırmaq üçün deyil: sonsuz yeniləmə kartı
    /// "əyləncəli slot maşını"na çevirər və uşaq macəra oynamaq əvəzinə kart
    /// çevirməyə başlayardı.</para>
    /// </summary>
    public int MaxShowAnotherPerSession { get; set; } = 3;

    /// <summary>Qərar siyasətinin versiyası — balans dəyişəndə artırılır.</summary>
    public int PolicyVersion { get; set; } = 1;
}
