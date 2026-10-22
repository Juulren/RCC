namespace Rcc.AiRelay;
public static class RelayProgram
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        // Loopback only: no router port or LAN listener is needed.
        builder.WebHost.ConfigureKestrel(server =>
        {
            server.Configure(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
            server.Listen(System.Net.IPAddress.Loopback, 5092);
        });
        builder.Logging.ClearProviders();
        RelayApp.Configure(builder);
        var app = builder.Build();
        RelayApp.Map(app);
        await app.RunAsync();
    }
}
