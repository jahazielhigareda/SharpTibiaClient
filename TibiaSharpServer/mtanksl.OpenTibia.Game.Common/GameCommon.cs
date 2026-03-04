using mtanksl.OpenTibia.Common;

namespace mtanksl.OpenTibia.Game.Common;

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
// Script interfaces (Phase 12 will provide C# implementations)
// ---------------------------------------------------------------------------

/// <summary>Script that runs when a player uses an item.</summary>
public interface IActionScript
{
    Promise OnUse(IContext context, Player player, Item item);
}

/// <summary>Script that runs on NPC dialogue.</summary>
public interface INpcScript
{
    Promise OnSay(IContext context, Player player, string words);
}

/// <summary>Scripts bound to creature lifecycle events.</summary>
public interface ICreatureScript
{
    Promise OnLogin(IContext context, Player player);
    Promise OnLogout(IContext context, Player player);
    Promise OnDeath(IContext context, Creature creature);
}

/// <summary>Script for spell execution.</summary>
public interface ISpellScript
{
    Promise OnCast(IContext context, Player caster, string words);
}

/// <summary>Script for global server events (startup, shutdown, daily reset).</summary>
public interface IGlobalEventScript
{
    Promise OnStartup(IContext context);
    Promise OnShutdown(IContext context);
}
