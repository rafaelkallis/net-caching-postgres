using JetBrains.Annotations;

using Microsoft.Extensions.DependencyInjection;

namespace RafaelKallis.Library.Implementation;

[PublicAPI]
public static class LibraryImplementationExtensions
{
    /// <summary>
    /// Adds library support to the application.
    /// </summary>
    public static IServiceCollection AddLibrary(this IServiceCollection services,
        string? configurationSectionName = null,
        Action<LibraryOptions>? configureOptions = null)
    {
        configurationSectionName ??= "Library";
        configureOptions ??= _ => { };

        services.AddOptions<LibraryOptions>()
            .BindConfiguration(configurationSectionName)
            .Configure(configureOptions)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IService, Service>();

        return services;
    }
}