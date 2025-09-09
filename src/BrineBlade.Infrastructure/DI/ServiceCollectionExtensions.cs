using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using BrineBlade.Infrastructure.Content;
using BrineBlade.Infrastructure.Persistence;
using BrineBlade.Infrastructure.Services;
using BrineBlade.Services.Abstractions;

namespace BrineBlade.Infrastructure.DI;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all engine services and content/persistence singletons.
    /// Idempotent: safe to call more than once.
    /// </summary>
    public static IServiceCollection AddBrineBladeEngine(
        this IServiceCollection services,
        string contentRoot,
        string saveRoot)
    {
        // Ensure expected folders exist
        Directory.CreateDirectory(saveRoot);

        // Stores & persistence
        services.TryAddSingleton<IContentStore>(_ => new JsonContentStore(contentRoot));
        services.TryAddSingleton<ISaveGameService>(_ => new JsonSaveGameService(saveRoot));

        // Catalogs
        services.TryAddSingleton(_ => new ClassCatalog(contentRoot));
        services.TryAddSingleton(_ => new SpecCatalog(contentRoot));
        services.TryAddSingleton(_ => new ItemCatalog(contentRoot));

        // Enemy catalog: register concrete AND map interface to the SAME instance
        services.TryAddSingleton<EnemyCatalog>(_ => new EnemyCatalog(contentRoot));
        services.TryAddSingleton<IEnemyCatalog>(sp => sp.GetRequiredService<EnemyCatalog>());

        // RNG (needed by CombatService and anywhere else that needs randomness)
        services.TryAddSingleton<IRandom, DefaultRandom>();

        // Services (rules layer)
        services.TryAddSingleton<ICombatService, CombatService>();
        services.TryAddSingleton<IDialogueService, DialogueService>();
        services.TryAddSingleton<IInventoryService, InventoryService>();
        services.TryAddSingleton<IWorldSimService, WorldSimService>();
        services.TryAddSingleton<IClockService, ClockService>();

        return services;
    }
}
