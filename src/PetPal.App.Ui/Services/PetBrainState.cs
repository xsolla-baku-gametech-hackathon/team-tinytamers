using PetPal.Shared.Dtos.PetBrain;

namespace PetPal.App.Ui.Services;

/// <summary>
/// Pet Brain-in klient tərəfdəki keşi.
///
/// <para><b>Niyə <see cref="AppState"/>-ə qoyulmadı:</b> orada ekranlar arasında
/// PAYLAŞILAN oyun vəziyyəti (cüzdan, pet, gündəlik hədəf) durur və hər ekran
/// ona baxır. Pet Brain-in vəziyyəti isə yalnız iki yerə lazımdır — öz səhifəsi
/// və ana ekrandakı çip. Onu ümumi vəziyyətə qatmaq hər ekranı lazımsız
/// yenidən render etdirərdi.</para>
///
/// <para><b>Profil təhlükəsizliyi:</b> keş AKTİV UŞAĞIN id-si ilə açarlanır.
/// Profil dəyişəndə (<see cref="AppSession.Changed"/>) keş dərhal boşalır, yəni
/// Aylin-dən Mia-ya keçəndə ekranda bir an da olsa Aylin-in tövsiyəsi və ya
/// xatirəsi qalmır. Bu, sadəcə səliqə deyil — nümayişin özü məhz iki profilin
/// FƏRQİ üzərində qurulub.</para>
/// </summary>
public class PetBrainState
{
    private readonly AppSession _session;

    private Guid? _ownerChildId;
    private PetBrainStateDto? _state;

    public PetBrainState(AppSession session)
    {
        _session = session;
        _session.Changed += OnSessionChanged;
    }

    public event Action? Changed;

    /// <summary>Keşdəki vəziyyət — YALNIZ aktiv uşağa aiddirsə qaytarılır.</summary>
    public PetBrainStateDto? Current =>
        _ownerChildId is not null && _ownerChildId == _session.ActiveChildId ? _state : null;

    public void Apply(PetBrainStateDto state)
    {
        _ownerChildId = _session.ActiveChildId;
        _state = state;
        Changed?.Invoke();
    }

    /// <summary>Macəra bitəndə və ya başlayanda tövsiyə köhnəlir — növbəti açılışda yenidən alınır.</summary>
    public void Invalidate()
    {
        _state = null;
        Changed?.Invoke();
    }

    private void OnSessionChanged()
    {
        // Profil dəyişdi: köhnə uşağın məlumatı bir an da göstərilməməlidir.
        if (_ownerChildId is not null && _ownerChildId != _session.ActiveChildId)
        {
            _ownerChildId = null;
            _state = null;
            Changed?.Invoke();
        }
    }
}
