namespace PetPal.App;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();

		blazorWebView.BlazorWebViewInitialized += OnWebViewInitialized;
	}

	/// <summary>
	/// Android WebView-u recap videosu üçün hazırlayır.
	///
	/// <para>Video əvvəlcə tam yüklənir, sonra oynadılır. Android WebView isə
	/// standart olaraq yalnız toxunuşla başlayan oynatmaya icazə verir, yükləmə
	/// bitəndə isə toxunuşun vaxtı keçmiş ola bilər — video dayanıq qalır və
	/// uşaq onu əl ilə başlatmalı olur. Digər platformalarda heç nə dəyişmir.</para>
	/// </summary>
	private static void OnWebViewInitialized(object? sender, Microsoft.AspNetCore.Components.WebView.BlazorWebViewInitializedEventArgs e)
	{
#if ANDROID
		e.WebView.Settings.MediaPlaybackRequiresUserGesture = false;
#endif
	}
}
