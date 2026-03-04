using mtanksl.OpenTibia.Game.Common;

namespace mtanksl.OpenTibia.Plugins;

/// <summary>
/// Marker interface for all server-side plugins.
/// A plugin registers scripts (action, NPC, spell, etc.) with the game engine
/// during <see cref="Register"/>.
/// </summary>
public interface IPlugin
{
    string Name    { get; }
    string Version { get; }

    /// <summary>
    /// Called once at server startup.  Register all scripts here.
    /// </summary>
    void Register(IPluginRegistry registry);
}

/// <summary>
/// Registry that plugins use to hook their scripts into the game engine.
/// </summary>
public interface IPluginRegistry
{
    void RegisterAction(ushort itemTypeId, IActionScript script);
    void RegisterCreatureScript(ICreatureScript script);
    void RegisterSpell(string words, ISpellScript script);
    void RegisterGlobalEvent(IGlobalEventScript script);
    void RegisterNpc(string npcName, INpcScript script);
}

/// <summary>
/// Discovers and loads plugins from a directory of DLL files.
///
/// SECURITY NOTE: <c>Assembly.LoadFrom()</c> does not sandbox or verify
/// signatures.  Only load assemblies from trusted, administrator-controlled
/// directories.  In production, validate each DLL against an allow-list or
/// use <c>AssemblyLoadContext</c> isolation.
/// </summary>
public sealed class PluginLoader
{
    private readonly string _pluginsDirectory;

    public PluginLoader(string pluginsDirectory)
    {
        _pluginsDirectory = pluginsDirectory;
    }

    /// <summary>
    /// Enumerates all <c>*.dll</c> files in <see cref="_pluginsDirectory"/>,
    /// loads each assembly, and yields every concrete type implementing
    /// <see cref="IPlugin"/>.
    /// </summary>
    public IEnumerable<IPlugin> LoadPlugins()
    {
        if (!Directory.Exists(_pluginsDirectory))
            yield break;

        foreach (string dll in Directory.GetFiles(_pluginsDirectory, "*.dll"))
        {
            System.Reflection.Assembly asm;
            try
            {
                asm = System.Reflection.Assembly.LoadFrom(dll);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[PluginLoader] Failed to load '{dll}': {ex.Message}");
                continue;
            }

            foreach (Type type in asm.GetTypes())
            {
                if (!typeof(IPlugin).IsAssignableFrom(type) || type.IsAbstract)
                    continue;

                IPlugin? plugin = null;
                try { plugin = (IPlugin?)Activator.CreateInstance(type); }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(
                        $"[PluginLoader] Could not instantiate '{type.FullName}': {ex.Message}");
                }

                if (plugin != null)
                    yield return plugin;
            }
        }
    }
}
