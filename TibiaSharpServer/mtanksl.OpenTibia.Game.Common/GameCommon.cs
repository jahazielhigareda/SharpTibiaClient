using mtanksl.OpenTibia.Common;

namespace mtanksl.OpenTibia.Game.Common;

// ---------------------------------------------------------------------------
// Phase 12 additions: map entity types used by script interfaces
// ---------------------------------------------------------------------------

/// <summary>
/// A single map tile that may hold a ground item and stacked items.
/// Passed to movement and action scripts.
/// </summary>
public sealed class Tile
{
    private readonly List<Item> _items = new();

    public Tile(Position position) { Position = position; }

    public Position            Position { get; }
    public Item?               Ground   { get; set; }
    public IReadOnlyList<Item> Items    => _items;

    public void AddItem(Item item)    => _items.Add(item);
    public void RemoveItem(Item item) => _items.Remove(item);
}

/// <summary>An NPC creature that can hold a dialogue topic.</summary>
public sealed class Npc : Creature
{
    public Npc(string name) : base(name) { }

    public string DialogueTopic { get; set; } = "";
}

// ---------------------------------------------------------------------------
// IPluginRegistry — defined here so Game, Plugins, and Host all share the
// same contract without a circular dependency.
// ---------------------------------------------------------------------------

/// <summary>
/// Registry that plugins (and the host) use to bind C# scripts to game hooks.
/// </summary>
public interface IPluginRegistry
{
    void RegisterAction(ushort itemTypeId, IActionScript script);
    void RegisterAmmunition(ushort itemTypeId, IAmmunitionScript script);
    void RegisterCreatureScript(ICreatureScript script);
    void RegisterGlobalEvent(IGlobalEventScript script);
    void RegisterMonsterAttack(string attackName, IMonsterAttackScript script);
    void RegisterMovement(ushort itemTypeId, IMovementScript script);
    void RegisterNpc(string npcName, INpcScript script);
    void RegisterRaid(string raidName, IRaidScript script);
    void RegisterRune(ushort itemTypeId, IRuneScript script);
    void RegisterSpell(string words, ISpellScript script);
    void RegisterTalkAction(string words, ITalkActionScript script);
    void RegisterWeapon(ushort itemTypeId, IWeaponScript script);
}

/// <summary>
/// Dispatcher that the game engine uses to invoke registered C# scripts.
/// Separated from <see cref="IPluginRegistry"/> so <c>GameEngine</c>
/// does not depend on the <c>Plugins</c> assembly.
/// </summary>
public interface IScriptDispatcher
{
    Task DispatchLoginAsync(IContext ctx, Player player);
    Task DispatchLogoutAsync(IContext ctx, Player player);
    Task DispatchDeathAsync(IContext ctx, Creature creature);
    Task DispatchSpellAsync(IContext ctx, Player caster, string words);
    Task DispatchNpcSayAsync(IContext ctx, Player player, string words, Npc npc);
    Task DispatchActionAsync(IContext ctx, Player player, Item item, Tile tile, Item? targetItem);
    Task DispatchStartupAsync(IContext ctx);
    Task DispatchShutdownAsync(IContext ctx);
}

/// <summary>
/// Execution context passed to every game script / command handler.
/// Provides access to the game state and a way to schedule follow-up actions.
/// </summary>
public interface IContext
{
    IGameState GameState { get; }
    void       Schedule(TimeSpan delay, Action action);
}

/// <summary>
/// The live game state accessible to scripts.
/// </summary>
public interface IGameState
{
    IReadOnlyDictionary<uint, Player> Players  { get; }
    IReadOnlyDictionary<uint, Creature> Creatures { get; }
}

// ---------------------------------------------------------------------------
// Core entity types
// ---------------------------------------------------------------------------

/// <summary>Base class for anything that exists on the Tibia map.</summary>
public abstract class Creature
{
    private static uint _nextId = 1;

    protected Creature(string name)
    {
        Id   = _nextId++;
        Name = name;
    }

    public uint   Id        { get; }
    public string Name      { get; set; }
    public int    Health    { get; set; } = 100;
    public int    MaxHealth { get; set; } = 100;
    public Position Position { get; set; }

    public float HealthPercent => (float)Health / MaxHealth;
}

/// <summary>A connected player character.</summary>
public sealed class Player : Creature
{
    public Player(string name) : base(name) { }

    public int    Level      { get; set; } = 1;
    public int    Mana       { get; set; } = 55;
    public int    MaxMana    { get; set; } = 55;
    public int    Capacity   { get; set; } = 400;
    public long   Experience { get; set; }
    public bool   Premium    { get; set; }
}

/// <summary>A monster or NPC.</summary>
public sealed class Monster : Creature
{
    public Monster(string name) : base(name) { }
    public int    BaseExperience { get; set; }
}

/// <summary>
/// A single item on the map or in a container.
/// </summary>
public sealed class Item
{
    public ushort TypeId  { get; init; }
    public int    Count   { get; set; } = 1;
    public bool   IsStackable { get; init; }
}

// ---------------------------------------------------------------------------
// Script interfaces (Phase 12 — full C# implementations replace Lua scripts)
// ---------------------------------------------------------------------------

/// <summary>Script that runs when a player uses an item on a tile or another item.</summary>
public interface IActionScript
{
    Promise OnUse(IContext context, Player player, Item item, Tile tile, Item? targetItem);
}

/// <summary>Script that runs when a player fires ammunition (arrows, bolts).</summary>
public interface IAmmunitionScript
{
    Promise OnFire(IContext context, Player player, Item ammunition, Creature target);
}

/// <summary>Scripts bound to creature lifecycle events.</summary>
public interface ICreatureScript
{
    Promise OnLogin(IContext context, Player player);
    Promise OnLogout(IContext context, Player player);
    Promise OnDeath(IContext context, Creature creature);
}

/// <summary>Script for global server events (startup, shutdown, daily reset).</summary>
public interface IGlobalEventScript
{
    Promise OnStartup(IContext context);
    Promise OnShutdown(IContext context);
}

/// <summary>Script for a named monster attack pattern.</summary>
public interface IMonsterAttackScript
{
    Promise OnAttack(IContext context, Monster attacker, Creature target);
}

/// <summary>Script that runs when a creature steps onto a tile.</summary>
public interface IMovementScript
{
    Promise OnStepIn(IContext context, Creature creature, Tile tile);
    Promise OnStepOut(IContext context, Creature creature, Tile tile);
}

/// <summary>Script that runs on NPC dialogue.</summary>
public interface INpcScript
{
    Promise OnSay(IContext context, Player player, string words, Npc npc);
}

/// <summary>Script for a scheduled raid event.</summary>
public interface IRaidScript
{
    Promise OnRaidStart(IContext context);
}

/// <summary>Script that runs when a player uses a rune item.</summary>
public interface IRuneScript
{
    Promise OnUse(IContext context, Player player, Item rune, Creature target);
}

/// <summary>Script for spell execution.</summary>
public interface ISpellScript
{
    Promise OnCast(IContext context, Player caster, string words);
}

/// <summary>Script bound to a player talk-action keyword (e.g. "!online").</summary>
public interface ITalkActionScript
{
    Promise OnSay(IContext context, Player player, string words);
}

/// <summary>Script that runs when a player attacks with a weapon item.</summary>
public interface IWeaponScript
{
    Promise OnAttack(IContext context, Player attacker, Item weapon, Creature target);
}
