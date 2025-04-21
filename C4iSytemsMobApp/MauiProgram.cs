using Microsoft.Extensions.Logging;
using ZXing.Net.Maui.Controls;

namespace C4iSytemsMobApp;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
            .UseBarcodeReader() // Register ZXing Barcode Scanner
            .ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});
        // Register HttpClient as a singleton service
        builder.Services.AddSingleton<HttpClient>();
        // Register LoginPage with HttpClient dependency
        builder.Services.AddTransient<LoginPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
