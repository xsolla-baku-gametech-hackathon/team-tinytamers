using Microsoft.JSInterop;

namespace PetPal.App.Ui.Services;

/// <summary>Nailiyyət kartının məzmunu — canvas-a veriləcək dəyərlər.</summary>
public class AchievementCardData
{
    public string PetName { get; set; } = string.Empty;
    public string Avatar { get; set; } = "🦊";
    public int Age { get; set; } = 1;
    public int Stars { get; set; }
    public int Streak { get; set; }
    public string Caption { get; set; } = string.Empty;
    public string Badges { get; set; } = string.Empty;
    public string AppName { get; set; } = "AI Pets for Kids";
    public Dictionary<string, string> StatLabels { get; set; } = new();
}

/// <summary>
/// Nailiyyət kartını çəkir və base64 PNG qaytarır.
///
/// <para>Çəkiliş JS-dədir, çünki nəticə ŞƏKİL olmalıdır: valideyn onu
/// mesajlaşma proqramına atır və orada yalnız şəkil görünür.</para>
///
/// <para>Kartda uşağın ƏSL ADI YOXDUR — yalnız pet adı. Paylaşılan şəkil
/// harasa çata bilər, arenanın qaydası isə eynidir: tanımadığı adam uşağın
/// adını görməməlidir.</para>
/// </summary>
public class AchievementCard
{
    private readonly IJSRuntime _js;

    public AchievementCard(IJSRuntime js) => _js = js;

    /// <summary>Kartı çəkir; JS əlçatmazsa <c>null</c> — düymə sadəcə işləmir, app sınmır.</summary>
    public async Task<string?> RenderAsync(AchievementCardData data)
    {
        try
        {
            return await _js.InvokeAsync<string>("petpalShare.drawCard", data);
        }
        catch (Exception)
        {
            return null;
        }
    }

}
