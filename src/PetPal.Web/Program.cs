using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PetPal.App.Ui;
using PetPal.App.Ui.Services;
using PetPal.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Bütün ekranlar PetPal.App.Ui kitabxanasındadır — MAUI host-u da eyni <Routes /> render edir.
builder.RootComponents.Add<Routes>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// API ünvanı wwwroot/appsettings.json-dan gəlir. Konteynerdə həmin fayl start zamanı
// PETPAL_API_BASE_URL environment variable-ı ilə yenidən yazılır (bax nginx-init-api-base-url.sh),
// beləliklə eyni image hər mühitə uyğun gəlir.
var apiBaseUrl = builder.Configuration["ApiBaseUrl"];
if (string.IsNullOrWhiteSpace(apiBaseUrl))
    apiBaseUrl = builder.HostEnvironment.BaseAddress;

if (!apiBaseUrl.EndsWith('/'))
    apiBaseUrl += "/";

// Platformaya bağlı implementasiyalar UI kitabxanasından ƏVVƏL qeydiyyata alınır ki,
// AddPetPalUi-dəki TryAdd default-ları (in-memory store, null picker) onları əvəz etməsin.
builder.Services.AddSingleton<ITokenStore, LocalStorageTokenStore>();
builder.Services.AddSingleton<IPhotoPicker, BrowserPhotoPicker>();
builder.Services.AddSingleton<IPetSpeech, BrowserPetSpeech>();
builder.Services.AddSingleton<IShareService, BrowserShareService>();

builder.Services.AddPetPalUi(new Uri(apiBaseUrl));

await builder.Build().RunAsync();
