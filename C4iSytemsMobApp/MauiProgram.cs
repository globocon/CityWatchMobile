//using Android.Net;
using C4iSytemsMobApp.Data;
using C4iSytemsMobApp.Data.DbServices;
using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.MapperProfile;
using C4iSytemsMobApp.Services;
using CommunityToolkit.Maui;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using System.IO;
using System.Net;
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
        builder.Services.AddTransient<AppUpgradePage>();
        builder.Services.AddSingleton<ICrowdControlServices, CrowdControlServices>();
        builder.Services.AddSingleton<IScannerControlServices, ScannerControlServices>();
        builder.Services.AddSingleton<ILogBookServices, LogBookServices>();
        builder.Services.AddSingleton<INfcService, NfcService>();
        builder.Services.AddSingleton<IGuardApiServices, GuardApiServices>();
        builder.Services.AddSingleton<IAppUpdateService, AppUpdateService>();
        builder.Services.AddSingleton<ICustomLogEntryServices, CustomLogEntryServices>();

        builder.Services.AddSingleton<ConnectivityListener>();
        builder.Services.AddSingleton<ISyncApiService, SyncApiService>();
        // Register AutoMapper
        builder.Services.AddAutoMapper(typeof(MappingProfile));

        // Register DbContext factory so each consumer gets a new context
        builder.Services.AddTransient<AppDbContext>();
        builder.Services.AddSingleton<IScanDataDbServices, ScanDataDbServices>(sp =>
        {
            // Provide factory to create new AppDbContext for each usage
            return new ScanDataDbServices(() => sp.GetRequiredService<AppDbContext>());
        });
        builder.Services.AddSingleton<SyncService>(sp =>
        {
            // Provide factory to create new AppDbContext for each usage
            return new SyncService(() => sp.GetRequiredService<AppDbContext>(), sp.GetRequiredService<ISyncApiService>());
        });


#if ANDROID
        builder.Services.AddSingleton<IVolumeButtonService, Platforms.Android.Services.VolumeButtonService>();
        builder.Services.AddSingleton<IDeviceInfoService, Platforms.Android.Services.DeviceInfoService>();
#elif IOS
        builder.Services.AddSingleton<IVolumeButtonService, Platforms.iOS.Services.VolumeButtonService>();
        builder.Services.AddSingleton<IDeviceInfoService, Platforms.iOS.Services.DeviceInfoService>();
#endif



#if DEBUG
        builder.Logging.AddDebug();
#endif
        // This line allows DI to create your App(LoginPage, IAppUpdateService)
        builder.Services.AddSingleton<App>();

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "app.db");
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Filename={dbPath}"));

        var app = builder.Build();

        // Start connectivity watcher
        var connListener = app.Services.GetService<ConnectivityListener>();

        return app;
    }
}
