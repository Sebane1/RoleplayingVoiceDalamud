using Dalamud.Game;
using Dalamud.IoC;
using Dalamud.Plugin.Services;

namespace RoleplayingVoiceDalamud.Services;

internal class Service {
    [PluginService] internal static IFramework Framework { get; set; } = null!;
    [PluginService] internal static IClientState ClientState { get; set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; set; } = null!;
    [PluginService] internal static ISigScanner SigScanner { get; set; } = null!;
    [PluginService] internal static ICondition Condition { get; set; } = null!;
    [PluginService] internal static IPluginLog PluginLog { get; set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get;  set; } = null!;
}