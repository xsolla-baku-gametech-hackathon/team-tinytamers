using Microsoft.Extensions.Logging;
using PetPal.App.Services;
using PetPal.App.Ui.Services;

namespace PetPal.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

        // Platformaya bağlı implementasiyalar UI kitabxanasından əvvəl qeydiyyata alınır ki,
        // AddPetPalUi-dəki TryAdd default-ları onları əvəz etməsin.
        builder.Services.AddSingleton<ITokenStore, MauiTokenStore>();
        builder.Services.AddSingleton<IPhotoPicker, MauiPhotoPicker>();
        builder.Services.AddSingleton<IPetSpeech, MauiPetSpeech>();
        builder.Services.AddSingleton<IShareService, MauiShareService>();
        builder.Services.AddSingleton<IBiometricGate, MauiBiometricGate>();

        builder.Services.AddPetPalUi(AppConfig.ApiBaseAddress);

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
