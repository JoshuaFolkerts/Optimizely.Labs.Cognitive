using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Optimizely.Labs.ComputerVision;

public static class ComputeVisionExtensions
{
    public static IServiceCollection AddComputeVisionService(this IServiceCollection services)
    {
        return AddComputeVisionService(services, _ => { });
    }

    public static IServiceCollection AddComputeVisionService(this IServiceCollection services,
        Action<ComputeVisionOptions> setupAction)
    {
        services.AddOptions<ComputeVisionOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                setupAction(options);
                configuration.GetSection("Optimizely:Vision").Bind(options);
            });

        //services.TryAddSingleton<IComputeVisionService, ComputeVisionService>();

        return services;
    }
}