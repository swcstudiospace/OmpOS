using OmpOS.EngineHost;

namespace OmpOS.App;

/// <summary>MacCatalyst shell seams (v1: no-op defaults; real platform backends are a later shell task).</summary>
public static partial class MauiProgram
{
    static partial void RegisterPlatformServices(IServiceCollection services)
    {
        services.AddSingleton<IWindowStateStore, NoOpWindowStateStore>();
        services.AddSingleton<ISingleInstanceGuard, NoOpSingleInstanceGuard>();
        services.AddSingleton<ISessionRecovery, NoOpSessionRecovery>();
    }
}
