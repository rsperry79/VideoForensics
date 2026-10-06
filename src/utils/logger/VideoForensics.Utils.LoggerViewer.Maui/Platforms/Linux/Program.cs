using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace VideoForensics.Utils.LoggerViewer.Maui.Linux
{
    class Program : MauiProgram
    {
        static int Main(string[] args)
        {
            var app = MauiProgram.CreateMauiApp();
            return app.Services.GetRequiredService<IApplication>().Run(args);
        }
    }
}
