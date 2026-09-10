using Foundation;
using PetPal.App.Ui.Services;
using UIKit;

namespace PetPal.App;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	/// <summary>
	/// Dəvət linki: <c>petpal://friend/AB12CD</c> (sxem Info.plist-də elan olunub).
	///
	/// <para>Link dostluq QURMUR — kodu yalnız formaya yazır; sorğunu uşaq özü
	/// göndərir və qarşı tərəf özü qəbul edir.</para>
	/// </summary>
	public override bool OpenUrl(UIApplication application, NSUrl url, NSDictionary options)
	{
		if (string.Equals(url.Scheme, "petpal", StringComparison.OrdinalIgnoreCase))
		{
			// petpal://friend/AB12CD → son seqment koddur.
			var code = url.LastPathComponent;

			if (!string.IsNullOrWhiteSpace(code))
			{
				IPlatformApplication.Current?.Services
					.GetService<PendingInvite>()?
					.Set(code);

				return true;
			}
		}

		return base.OpenUrl(application, url, options);
	}
}
