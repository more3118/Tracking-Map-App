using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Hosting;
using Syncfusion.Maui.Toolkit.Hosting;

namespace Tracking_Map_App
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .UseMauiMaps()
                .ConfigureSyncfusionToolkit()
                .ConfigureMauiHandlers(handlers =>
                {
#if IOS || MACCATALYST
                    handlers.AddHandler<Microsoft.Maui.Controls.CollectionView, Microsoft.Maui.Controls.Handlers.Items2.CollectionViewHandler2>();
#endif
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
                builder.Logging.AddDebug();
                builder.Services.AddLogging(configure => configure.AddDebug());
#endif

            // Location tracking services only
            builder.Services.AddSingleton<LocationDatabaseService>();
            builder.Services.AddSingleton<LocationTrackingService>();
            builder.Services.AddSingleton<LocationTrackingPageModel>();
            builder.Services.AddSingleton<LocationTrackingPage>();

            return builder.Build();
        }
    }
}
