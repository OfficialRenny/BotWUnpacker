using BotwUnpacker.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BotwUnpacker;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBotwUnpackerServices(this IServiceCollection services)
    {
        services
            .AddSingleton<IConfiguration>(JsonConfiguration.CreateConfigurationContainer())
            .AddSingleton<MainWindow>()
            .AddTransient<PaddingTool>()
            .AddTransient<CompareTool>();
        
        return services;
    }
}