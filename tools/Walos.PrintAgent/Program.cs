using System.Net;
using Walos.PrintAgent.Api;
using Walos.PrintAgent.Security;
using Walos.PrintAgent.Storage;
using Walos.PrintAgent.Tray;

namespace Walos.PrintAgent;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var stateDirectory = AgentPaths.GetStateDirectory();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
            ApplicationName = typeof(Program).Assembly.FullName,
            ContentRootPath = AppContext.BaseDirectory
        });

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = PrintAgentApi.MaxPayloadBytes;
            options.Listen(IPAddress.Loopback, PrintAgentApi.Port);
        });

        PrintAgentApi.ConfigureServices(builder.Services, builder.Configuration, stateDirectory);
        var app = builder.Build();
        PrintAgentApi.ConfigurePipeline(app);

        // Keep the entry thread in STA. Awaiting before Application.Run can resume on an
        // MTA pool thread because WinForms has not installed its synchronization context yet.
        app.StartAsync().GetAwaiter().GetResult();
        try
        {
            var pairing = app.Services.GetRequiredService<PairingService>();
            Application.Run(new PrintAgentTrayContext(app, pairing));
        }
        finally
        {
            app.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
