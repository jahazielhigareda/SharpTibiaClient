using mtanksl.OpenTibia.Common;
using mtanksl.OpenTibia.Data.InMemory;
using mtanksl.OpenTibia.Game;
using mtanksl.OpenTibia.Game.Common;
using mtanksl.OpenTibia.Game.Scripts;
using mtanksl.OpenTibia.Plugins;

namespace mtanksl.OpenTibia.Tests;

// ---------------------------------------------------------------------------
// Phase 12 validation: C# scripting system (replaces NLua)
// ---------------------------------------------------------------------------

/// <summary>
/// Tests for the concrete <see cref="PluginRegistry"/> — registration and dispatch.
/// </summary>
public class PluginRegistryTests
{
    private static readonly FakeContext Ctx = new();

    [Fact]
    public async Task RegisterCreatureScript_DispatchLoginAsync_InvokesScript()
    {
        var registry = new PluginRegistry();
        var tracker  = new TrackingCreatureScript();
        registry.RegisterCreatureScript(tracker);

        var player = new Player("Test") { Health = 100, MaxHealth = 100 };
        await registry.DispatchLoginAsync(Ctx, player);

        Assert.True(tracker.LoginCalled);
    }

    [Fact]
    public async Task RegisterCreatureScript_DispatchLogoutAsync_InvokesScript()
    {
        var registry = new PluginRegistry();
        var tracker  = new TrackingCreatureScript();
        registry.RegisterCreatureScript(tracker);

        var player = new Player("Test") { Health = 100, MaxHealth = 100 };
        await registry.DispatchLogoutAsync(Ctx, player);

        Assert.True(tracker.LogoutCalled);
    }

    [Fact]
    public async Task RegisterCreatureScript_DispatchDeathAsync_InvokesScript()
    {
        var registry = new PluginRegistry();
        var tracker  = new TrackingCreatureScript();
        registry.RegisterCreatureScript(tracker);

        var player = new Player("DeadPlayer") { Health = 0, MaxHealth = 100 };
        await registry.DispatchDeathAsync(Ctx, player);

        Assert.True(tracker.DeathCalled);
    }

    [Fact]
    public async Task RegisterSpell_DispatchSpellAsync_InvokesMatchingScript()
    {
        var registry = new PluginRegistry();
        var spell    = new HealingSpellScript(healAmount: 50);
        registry.RegisterSpell("exura", spell);

        var player = new Player("Healer") { Health = 50, MaxHealth = 150 };
        await registry.DispatchSpellAsync(Ctx, player, "exura");

        // Player should be healed
        Assert.Equal(100, player.Health);
    }

    [Fact]
    public async Task RegisterSpell_UnknownWord_DoesNothing()
    {
        var registry = new PluginRegistry();
        registry.RegisterSpell("exura", new HealingSpellScript(50));

        var player = new Player("Healer") { Health = 50, MaxHealth = 150 };
        int hpBefore = player.Health;

        // "exura gran" not registered — should be a no-op
        await registry.DispatchSpellAsync(Ctx, player, "exura gran");

        Assert.Equal(hpBefore, player.Health);
    }

    [Fact]
    public async Task RegisterNpc_DispatchNpcSayAsync_InvokesMatchingScript()
    {
        var registry = new PluginRegistry();
        var tracker  = new TrackingNpcScript();
        registry.RegisterNpc("Greeter", tracker);

        var player = new Player("Adventurer");
        var npc    = new Npc("Greeter");

        await registry.DispatchNpcSayAsync(Ctx, player, "hi", npc);

        Assert.True(tracker.SayCalled);
        Assert.Equal("hi",   tracker.LastWords);
        Assert.Equal("Greeter", tracker.LastNpcName);
    }

    [Fact]
    public async Task MultipleCreatureScripts_AllInvokedOnLogin()
    {
        var registry = new PluginRegistry();
        var s1 = new TrackingCreatureScript();
        var s2 = new TrackingCreatureScript();
        registry.RegisterCreatureScript(s1);
        registry.RegisterCreatureScript(s2);

        var player = new Player("Test");
        await registry.DispatchLoginAsync(Ctx, player);

        Assert.True(s1.LoginCalled);
        Assert.True(s2.LoginCalled);
    }
}

/// <summary>
/// Tests for the built-in <see cref="DefaultCreatureScript"/>.
/// </summary>
public class DefaultCreatureScriptTests
{
    [Fact]
    public async Task OnLogin_ReturnsCompletedPromise()
    {
        var script = new DefaultCreatureScript();
        var player = new Player("Hero");
        var promise = script.OnLogin(new FakeContext(), player);
        await promise.AsTask(); // should not throw
    }

    [Fact]
    public async Task OnLogout_ReturnsCompletedPromise()
    {
        var script = new DefaultCreatureScript();
        var player = new Player("Hero");
        var promise = script.OnLogout(new FakeContext(), player);
        await promise.AsTask();
    }

    [Fact]
    public async Task OnDeath_ReturnsCompletedPromise()
    {
        var script = new DefaultCreatureScript();
        var creature = new Player("Ghost");
        var promise = script.OnDeath(new FakeContext(), creature);
        await promise.AsTask();
    }
}

/// <summary>
/// Tests for <see cref="HealingSpellScript"/>.
/// </summary>
public class HealingSpellScriptTests
{
    [Fact]
    public void Constructor_NegativeHeal_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HealingSpellScript(-1));
    }

    [Fact]
    public async Task OnCast_HealsUpToMax()
    {
        var spell  = new HealingSpellScript(healAmount: 100);
        var player = new Player("Hero") { Health = 50, MaxHealth = 200 };

        await spell.OnCast(new FakeContext(), player, "exura").AsTask();

        Assert.Equal(150, player.Health);
    }

    [Fact]
    public async Task OnCast_DoesNotExceedMaxHealth()
    {
        var spell  = new HealingSpellScript(healAmount: 999);
        var player = new Player("FullHp") { Health = 190, MaxHealth = 200 };

        await spell.OnCast(new FakeContext(), player, "exura vita").AsTask();

        Assert.Equal(200, player.Health); // capped at MaxHealth
    }

    [Fact]
    public async Task OnCast_AlreadyAtFullHealth_NoChange()
    {
        var spell  = new HealingSpellScript(healAmount: 50);
        var player = new Player("FullHp") { Health = 200, MaxHealth = 200 };

        await spell.OnCast(new FakeContext(), player, "exura").AsTask();

        Assert.Equal(200, player.Health);
    }
}

/// <summary>
/// Tests for <see cref="GreeterNpcScript"/>.
/// </summary>
public class GreeterNpcScriptTests
{
    [Fact]
    public async Task OnSay_CompletesSuccessfully()
    {
        var script = new GreeterNpcScript("Welcome!");
        var player = new Player("Adventurer");
        var npc    = new Npc("Sage");

        var promise = script.OnSay(new FakeContext(), player, "hello", npc);
        await promise.AsTask(); // should not throw
    }
}

/// <summary>
/// Tests for <see cref="Tile"/> entity.
/// </summary>
public class TileTests
{
    [Fact]
    public void Tile_AddAndRemoveItem()
    {
        var tile = new Tile(new Position(100, 200, 7));
        var item = new Item { TypeId = 101, Count = 1 };

        tile.AddItem(item);
        Assert.Single(tile.Items);

        tile.RemoveItem(item);
        Assert.Empty(tile.Items);
    }

    [Fact]
    public void Tile_Position_IsCorrect()
    {
        var pos  = new Position(10, 20, 3);
        var tile = new Tile(pos);
        Assert.Equal(pos, tile.Position);
    }

    [Fact]
    public void Tile_Ground_CanBeSetAndRetrieved()
    {
        var tile   = new Tile(new Position(50, 50, 7));
        var ground = new Item { TypeId = 102, IsStackable = false };

        tile.Ground = ground;

        Assert.NotNull(tile.Ground);
        Assert.Equal(102, tile.Ground!.TypeId);
    }

    [Fact]
    public void Tile_Ground_DefaultsToNull()
    {
        var tile = new Tile(new Position(0, 0, 0));
        Assert.Null(tile.Ground);
    }
}

/// <summary>
/// Tests for <see cref="Npc"/> entity.
/// </summary>
public class NpcTests
{
    [Fact]
    public void Npc_NameIsSet()
    {
        var npc = new Npc("Oracle");
        Assert.Equal("Oracle", npc.Name);
    }

    [Fact]
    public void Npc_DialogueTopic_DefaultsEmpty()
    {
        var npc = new Npc("Oracle");
        Assert.Equal("", npc.DialogueTopic);
    }

    [Fact]
    public void Npc_IsCreature()
    {
        var npc = new Npc("Oracle");
        Assert.IsAssignableFrom<Creature>(npc);
    }
}

/// <summary>
/// Tests for <see cref="GameEngine"/> script dispatch integration.
/// </summary>
public class GameEngineDispatchTests
{
    [Fact]
    public async Task LoginPlayerAsync_WithRegistry_DispatchesOnLogin()
    {
        var registry = new PluginRegistry();
        var tracker  = new TrackingCreatureScript();
        registry.RegisterCreatureScript(tracker);

        using var uow    = new InMemoryUnitOfWork();
        ((InMemoryPlayerRepository)uow.Players).Add(new mtanksl.OpenTibia.Data.Common.PlayerRecord
        {
            Id = 1, AccountId = 1, Name = "Hero",
            Level = 5, Health = 100, MaxHealth = 100, Mana = 50, MaxMana = 50,
            Capacity = 400, PosX = 100, PosY = 100, PosZ = 7
        });

        using var engine = new GameEngine(uow, registry);
        Player? player = await engine.LoginPlayerAsync("Hero");

        Assert.NotNull(player);
        Assert.Equal("Hero", player!.Name);
        Assert.True(tracker.LoginCalled);
    }

    [Fact]
    public async Task LoginPlayerAsync_NoRegistry_ReturnsPlayer()
    {
        using var uow = new InMemoryUnitOfWork();
        ((InMemoryPlayerRepository)uow.Players).Add(new mtanksl.OpenTibia.Data.Common.PlayerRecord
        {
            Id = 2, AccountId = 1, Name = "Solo",
            Level = 1, Health = 100, MaxHealth = 100, Mana = 55, MaxMana = 55,
            Capacity = 400, PosX = 100, PosY = 100, PosZ = 7
        });

        using var engine = new GameEngine(uow); // no registry
        Player? player = await engine.LoginPlayerAsync("Solo");

        Assert.NotNull(player);
        Assert.Equal("Solo", player!.Name);
    }

    [Fact]
    public async Task LoginPlayerAsync_UnknownName_ReturnsNull()
    {
        using var uow    = new InMemoryUnitOfWork();
        using var engine = new GameEngine(uow);
        Player? player   = await engine.LoginPlayerAsync("Nobody");
        Assert.Null(player);
    }

    [Fact]
    public async Task LogoutPlayerAsync_WithRegistry_DispatchesOnLogout()
    {
        var registry = new PluginRegistry();
        var tracker  = new TrackingCreatureScript();
        registry.RegisterCreatureScript(tracker);

        using var uow = new InMemoryUnitOfWork();
        ((InMemoryPlayerRepository)uow.Players).Add(new mtanksl.OpenTibia.Data.Common.PlayerRecord
        {
            Id = 3, AccountId = 1, Name = "Quitter",
            Level = 1, Health = 100, MaxHealth = 100, Mana = 55, MaxMana = 55,
            Capacity = 400, PosX = 100, PosY = 100, PosZ = 7
        });

        using var engine = new GameEngine(uow, registry);
        Player? player = await engine.LoginPlayerAsync("Quitter");
        Assert.NotNull(player);

        await engine.LogoutPlayerAsync(player!.Id);
        Assert.True(tracker.LogoutCalled);
    }
}

// ---------------------------------------------------------------------------
// Test helpers
// ---------------------------------------------------------------------------

/// <summary>Minimal no-op <see cref="IContext"/> for unit tests.</summary>
internal sealed class FakeContext : IContext
{
    public IGameState GameState { get; } = new FakeGameState();
    public void Schedule(TimeSpan delay, Action action) => action();
}

internal sealed class FakeGameState : IGameState
{
    public IReadOnlyDictionary<uint, Player>   Players   { get; } = new Dictionary<uint, Player>();
    public IReadOnlyDictionary<uint, Creature> Creatures { get; } = new Dictionary<uint, Creature>();
}

/// <summary>Records which lifecycle callbacks were invoked.</summary>
internal sealed class TrackingCreatureScript : ICreatureScript
{
    public bool LoginCalled  { get; private set; }
    public bool LogoutCalled { get; private set; }
    public bool DeathCalled  { get; private set; }

    public Promise OnLogin(IContext context, Player player)   { LoginCalled  = true; return Promise.Completed; }
    public Promise OnLogout(IContext context, Player player)  { LogoutCalled = true; return Promise.Completed; }
    public Promise OnDeath(IContext context, Creature creature) { DeathCalled = true; return Promise.Completed; }
}

/// <summary>Records NPC dialogue events.</summary>
internal sealed class TrackingNpcScript : INpcScript
{
    public bool   SayCalled   { get; private set; }
    public string LastWords   { get; private set; } = "";
    public string LastNpcName { get; private set; } = "";

    public Promise OnSay(IContext context, Player player, string words, Npc npc)
    {
        SayCalled   = true;
        LastWords   = words;
        LastNpcName = npc.Name;
        return Promise.Completed;
    }
}
