
using BrineBlade.Infrastructure.Content;
using BrineBlade.Infrastructure.Persistence;
using BrineBlade.Infrastructure.Services;
using BrineBlade.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace BrineBlade.Infrastructure.DI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBrineBladeEngine(this IServiceCollection s, string contentRoot, string saveRoot)
{
    // Stores & persistence
    s.AddSingleton<IContentStore>(_ => new JsonContentStore(contentRoot));
    s.AddSingleton<ISaveGameService>(_ => new JsonSaveGameService(saveRoot));

    // Catalogs
    s.AddSingleton(new ClassCatalog(contentRoot));
    s.AddSingleton(new SpecCatalog(contentRoot));
    s.AddSingleton(new ItemCatalog(contentRoot));
    s.AddSingleton<IEnemyCatalog>(_ => new EnemyCatalog(contentRoot));

    // Services
    s.AddSingleton<ICombatService, CombatService>();
    s.AddSingleton<IDialogueService, DialogueService>();
    s.AddSingleton<IInventoryService, InventoryService>();
    s.AddSingleton<IWorldSimService, WorldSimService>();
    s.AddSingleton<IClockService, ClockService>();
    return s;
}

}
