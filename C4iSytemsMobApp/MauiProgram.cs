using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Services;
using CommunityToolkit.Maui;
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
            // Initialize the .NET MAUI Community Toolkit by adding the below line of code
            .UseMauiCommunityToolkit()   // 👈 Add this
            .UseMauiCommunityToolkitMediaElement()      // 👈 Required
                                                        // Initialize the .NET MAUI Community Toolkit MediaElement by adding the below line of code
            //.UseMauiCommunityToolkitMediaElement()
            .UseBarcodeReader() // Register ZXing Barcode Scanner
            .ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("Arlrdbd.ttf", "ArielRoundBold");
                fonts.AddFont("fa-solid-900.ttf", "FontAwesome");
                fonts.AddFont("fa-regular-400.ttf", "FontAwesomeRegular");
                fonts.AddFont("fa-brands-400.ttf", "FontAwesomeBrands");
            });
        // Register HttpClient as a singleton service
        builder.Services.AddSingleton<HttpClient>();

        // Register LoginPage with HttpClient dependency
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddSingleton<ICrowdControlServices, CrowdControlServices>();
        builder.Services.AddSingleton<INfcService, NfcService>();

#if ANDROID
        builder.Services.AddSingleton<IVolumeButtonService, Platforms.Android.Services.VolumeButtonService>();
#elif IOS
        builder.Services.AddSingleton<IVolumeButtonService, Platforms.iOS.Services.VolumeButtonService>();
#endif



#if DEBUG
        builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
