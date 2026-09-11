using System.Text;

namespace PetPal.App.Ui.Components.PetBrain;

/// <summary>
/// Şəkil yığımının DETERMİNİSTİK şəkli — AI rəsmi hələ yoxdursa (və ya heç
/// gəlməyəcəksə) çərçivə məhz bunu kəsir.
///
/// <para>Şəkil portretdir (9:16), hekayə rəsmi ilə eyni nisbətdə — ona görə
/// parçaların həndəsəsi hər iki halda EYNİDİR və rəsm gələndə yalnız görüntü
/// dəyişir. Əşyalar çərçivənin kəsdiyi zolağa səpələnib ki, parçaların çoxu
/// fərqli bir şey göstərsin və uşaq onları bir-birindən ayıra bilsin.</para>
///
/// <para>Mətn, rəqəm və ox yoxdur — AI rəsmindəki qaydanın eynisi: şəkil
/// yalnız görüntüdür, parçanın yeri serverin rəqəmindən gəlir.</para>
/// </summary>
public static class PicturePostcard
{
    private sealed record Palette(
        string SkyTop, string SkyBottom, string Light, string Ground, string GroundDeep, string[] Props);

    /// <summary>Əşyaların yeri — çərçivənin kəsdiyi zolağın (y ≈ 205–565) içində.</summary>
    private static readonly (int X, int Y)[] Spots =
    [
        (28, 262), (196, 244), (292, 318), (104, 362), (236, 404), (30, 472), (172, 522), (292, 548)
    ];

    private static readonly Dictionary<string, string> Cache = new(StringComparer.Ordinal);

    public static string DataUri(string? sceneKey)
    {
        var key = sceneKey ?? string.Empty;

        lock (Cache)
        {
            if (Cache.TryGetValue(key, out var cached))
                return cached;

            var uri = Build(PaletteFor(key));
            Cache[key] = uri;

            return uri;
        }
    }

    private static string Build(Palette palette)
    {
        var svg = new StringBuilder()
            .Append("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 360 640'>")
            .Append("<defs><linearGradient id='s' x1='0' y1='0' x2='0' y2='1'>")
            .Append($"<stop offset='0' stop-color='{palette.SkyTop}'/><stop offset='1' stop-color='{palette.SkyBottom}'/>")
            .Append("</linearGradient></defs>")
            .Append("<rect width='360' height='640' fill='url(#s)'/>")
            .Append($"<circle cx='290' cy='250' r='40' fill='{palette.Light}'/>")
            .Append($"<path d='M0 380 Q90 330 180 372 T360 360 V640 H0 Z' fill='{palette.Ground}'/>")
            .Append($"<path d='M0 480 Q120 440 240 478 T360 468 V640 H0 Z' fill='{palette.GroundDeep}'/>");

        for (var i = 0; i < Spots.Length && i < palette.Props.Length; i++)
            svg.Append($"<text x='{Spots[i].X}' y='{Spots[i].Y}' font-size='52'>{palette.Props[i]}</text>");

        svg.Append("</svg>");

        return $"data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(svg.ToString()))}";
    }

    private static Palette PaletteFor(string sceneKey) => sceneKey switch
    {
        "mars" => new("#f6c08b", "#e0885a", "#fff1c9", "#c9673d", "#a8502e",
            ["🚀", "📡", "🤖", "🔆", "🪨", "🔋", "🛰️", "⛰️"]),

        "dragon" => new("#6d57b8", "#b39ae6", "#fff7d6", "#5b4a9e", "#473a82",
            ["🐉", "💎", "🌸", "✨", "🔮", "🌙", "🌷", "⭐"]),

        "moon" => new("#0f1b3d", "#2c3f75", "#7fb3ff", "#9aa3b8", "#7b8398",
            ["💎", "🛰️", "🚀", "⭐", "🔭", "✨", "🌑", "🌟"]),

        "ocean" => new("#7fd3e8", "#1f6fa8", "#fff6c7", "#e9d29b", "#d9bd7c",
            ["🐠", "🐬", "🪸", "🐚", "🫧", "🐙", "🌿", "⭐"]),

        "forest" => new("#bfe7ff", "#8fd18a", "#ffe27a", "#5fae4f", "#3f8f3b",
            ["🌳", "🍄", "🦔", "🐰", "🌼", "🦋", "🍂", "🎶"]),

        "lab" => new("#d9f5ee", "#a7e3d6", "#fff3b0", "#8fb3c9", "#6f94ab",
            ["🤖", "⚙️", "💡", "🔧", "🧪", "🔩", "📦", "🔌"]),

        _ => new("#cfe8ff", "#fef3d8", "#ffe89a", "#9bd08f", "#78b86c",
            ["🌟", "🌈", "☁️", "🌼", "🎈", "⭐", "🍀", "🦋"])
    };
}
