using Microsoft.Extensions.DependencyInjection;
using Tailgrab;
using Tailgrab.Clients.OBS;
using Tailgrab.Clients.Office;
using Tailgrab.MCP;
using Tailgrab.MCP.Tools;

namespace Tailgrab.DependencyInjection;

/// <summary>
/// Configures and builds the dependency injection container for the application.
/// </summary>
public static class DiContainer
{
    /// <summary>
    /// Builds and returns the configured service provider.
    /// </summary>
    public static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        // Register OBSClient and OfficeClient as singletons (they don't depend on ServiceRegistry at construction)
        services.AddSingleton<OBSClient>();
        services.AddSingleton<OfficeClient>();

        // Register ServiceRegistry as singleton (depends on OBSClient and OfficeClient)
        services.AddSingleton<ServiceRegistry>(provider =>
        {
            var obsClient = provider.GetRequiredService<OBSClient>();
            var officeClient = provider.GetRequiredService<OfficeClient>();
            return new ServiceRegistry(obsClient, officeClient);
        });

        //// Register MCP Server with access to the service provider
        services.AddSingleton<McpServer>(provider =>
        {
            return new McpServer(provider);
        });

        return services.BuildServiceProvider();
    }
}
