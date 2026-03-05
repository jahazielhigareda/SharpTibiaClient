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

// ---------------------------------------------------------------------------
// Concrete plugin registry — implements IPluginRegistry and provides
// dispatch methods so the GameEngine can invoke the right scripts.
// ---------------------------------------------------------------------------

/// <summary>
/// Thread-safe collection of registered C# scripts.
/// Scripts are registered once at startup and dispatched on the game thread.
/// Implements both <see cref="IPluginRegistry"/> (for registration) and
/// <see cref="IScriptDispatcher"/> (for dispatch by the game engine).
/// </summary>
public sealed class PluginRegistry : IPluginRegistry, IScriptDispatcher
{
    // --- per-type storage -------------------------------------------------
    private readonly Dictionary<ushort, IActionScript>        _actions       = new();
    private readonly Dictionary<ushort, IAmmunitionScript>    _ammunition    = new();
    private readonly List<ICreatureScript>                    _creatureScripts = new();
    private readonly List<IGlobalEventScript>                 _globalEvents  = new();
    private readonly Dictionary<string, IMonsterAttackScript> _monsterAttacks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ushort, IMovementScript>      _movements     = new();
    private readonly Dictionary<string, INpcScript>           _npcs          = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IRaidScript>          _raids         = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ushort, IRuneScript>          _runes         = new();
    private readonly Dictionary<string, ISpellScript>         _spells        = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ITalkActionScript>    _talkActions   = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ushort, IWeaponScript>        _weapons       = new();

    // --- IPluginRegistry --------------------------------------------------
    public void RegisterAction(ushort itemTypeId, IActionScript script)          => _actions[itemTypeId]         = script;
    public void RegisterAmmunition(ushort itemTypeId, IAmmunitionScript script)  => _ammunition[itemTypeId]      = script;
    public void RegisterCreatureScript(ICreatureScript script)                   => _creatureScripts.Add(script);
    public void RegisterGlobalEvent(IGlobalEventScript script)                   => _globalEvents.Add(script);
    public void RegisterMonsterAttack(string name, IMonsterAttackScript script)  => _monsterAttacks[name]        = script;
    public void RegisterMovement(ushort itemTypeId, IMovementScript script)      => _movements[itemTypeId]       = script;
    public void RegisterNpc(string npcName, INpcScript script)                   => _npcs[npcName]               = script;
    public void RegisterRaid(string raidName, IRaidScript script)                => _raids[raidName]             = script;
    public void RegisterRune(ushort itemTypeId, IRuneScript script)              => _runes[itemTypeId]           = script;
    public void RegisterSpell(string words, ISpellScript script)                 => _spells[words]               = script;
    public void RegisterTalkAction(string words, ITalkActionScript script)       => _talkActions[words]          = script;
    public void RegisterWeapon(ushort itemTypeId, IWeaponScript script)          => _weapons[itemTypeId]         = script;

    // --- read-only views for testing / introspection ----------------------
    public IReadOnlyDictionary<ushort, IActionScript>        Actions        => _actions;
    public IReadOnlyList<ICreatureScript>                    CreatureScripts => _creatureScripts;
    public IReadOnlyDictionary<string, ISpellScript>         Spells         => _spells;
    public IReadOnlyDictionary<string, INpcScript>           Npcs           => _npcs;

    // --- dispatch helpers -------------------------------------------------

    public async Task DispatchLoginAsync(IContext ctx, Player player)
    {
        foreach (var script in _creatureScripts)
            await script.OnLogin(ctx, player).AsTask();
    }

    public async Task DispatchLogoutAsync(IContext ctx, Player player)
    {
        foreach (var script in _creatureScripts)
            await script.OnLogout(ctx, player).AsTask();
    }

    public async Task DispatchDeathAsync(IContext ctx, Creature creature)
    {
        foreach (var script in _creatureScripts)
            await script.OnDeath(ctx, creature).AsTask();
    }

    public async Task DispatchSpellAsync(IContext ctx, Player caster, string words)
    {
        if (_spells.TryGetValue(words, out var script))
            await script.OnCast(ctx, caster, words).AsTask();
    }

    public async Task DispatchNpcSayAsync(IContext ctx, Player player, string words, Npc npc)
    {
        if (_npcs.TryGetValue(npc.Name, out var script))
            await script.OnSay(ctx, player, words, npc).AsTask();
    }

    public async Task DispatchActionAsync(IContext ctx, Player player, Item item, Tile tile, Item? targetItem)
    {
        if (_actions.TryGetValue(item.TypeId, out var script))
            await script.OnUse(ctx, player, item, tile, targetItem).AsTask();
    }

    public async Task DispatchStartupAsync(IContext ctx)
    {
        foreach (var script in _globalEvents)
            await script.OnStartup(ctx).AsTask();
    }

    public async Task DispatchShutdownAsync(IContext ctx)
    {
        foreach (var script in _globalEvents)
            await script.OnShutdown(ctx).AsTask();
    }
}

// ---------------------------------------------------------------------------
// Plugin discovery via DLL scanning
// ---------------------------------------------------------------------------

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

