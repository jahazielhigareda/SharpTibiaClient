// SharpTibiaServer — Host entry point (Phase 12)
// Phase 11: all library projects target net8.0, no LangVersion overrides.
// Phase 12: NLua removed; all scripts are pure C# classes; config.json replaces config.lua.
using mtanksl.OpenTibia.Common;
using mtanksl.OpenTibia.Data.InMemory;
using mtanksl.OpenTibia.Game;
using mtanksl.OpenTibia.Game.Scripts;
using mtanksl.OpenTibia.GameData;
using mtanksl.OpenTibia.Host;
using mtanksl.OpenTibia.Network;
using mtanksl.OpenTibia.Plugins;

// -----------------------------------------------------------------------
// Banner
// -----------------------------------------------------------------------
Console.Title = "SharpTibiaServer";
Logger.Info("SharpTibiaServer starting…");

// -----------------------------------------------------------------------
// Configuration — loaded from config.json (replaces config.lua)
// System.Text.Json is built into .NET 8; no external dependency needed.
// -----------------------------------------------------------------------
ServerConfig config = ServerConfig.Load("config.json");
Logger.Info($"Server name : {config.ServerName}");
Logger.Info($"Login port  : {config.LoginPort}");
Logger.Info($"Game port   : {config.GamePort}");
Logger.Info($"Max players : {config.MaxPlayers}");
Logger.Info($"XP stage 1  : x{config.Experience.Stage1Multiplier} up to level {config.Experience.Stage1MaxLevel}");

// -----------------------------------------------------------------------
// Bootstrap data layer (in-memory; swap for a real UoW in production)
// -----------------------------------------------------------------------
using var unitOfWork = new InMemoryUnitOfWork();

// Seed a test account + character so the server can be exercised immediately.
((InMemoryAccountRepository)unitOfWork.Accounts).Add(new mtanksl.OpenTibia.Data.Common.Account
{
    Id = 1, Name = "admin", Password = "admin", Premium = true, PremiumDays = 30
});
((InMemoryPlayerRepository)unitOfWork.Players).Add(new mtanksl.OpenTibia.Data.Common.PlayerRecord
{
    Id = 1, AccountId = 1, Name = "Admin", World = config.ServerName,
    Level = 8, Health = 185, MaxHealth = 185, Mana = 90, MaxMana = 90,
    Capacity = 400, Experience = 4200, PosX = 1000, PosY = 1000, PosZ = 7
});

// -----------------------------------------------------------------------
// Build the plugin registry with built-in C# scripts
// (Phase 12 — replaces Lua runtime / NLua dependency)
// -----------------------------------------------------------------------
var registry = new PluginRegistry();

// Creature lifecycle — login, logout, death
registry.RegisterCreatureScript(new DefaultCreatureScript());

// Built-in healing spells
registry.RegisterSpell("exura",      new HealingSpellScript(healAmount: 50));
registry.RegisterSpell("exura gran", new HealingSpellScript(healAmount: 150));
registry.RegisterSpell("exura vita", new HealingSpellScript(healAmount: 400));

// Sample NPC — the Greeter (in a real server this comes from an external plugin DLL)
registry.RegisterNpc("Greeter", new GreeterNpcScript("Hello, adventurer! Welcome to SharpTibiaServer."));

// -----------------------------------------------------------------------
// Discover and load external plugin DLLs from the plugins/ directory.
// SECURITY: Only load assemblies from trusted, administrator-controlled paths.
// -----------------------------------------------------------------------
var pluginLoader = new PluginLoader(config.PluginsDirectory);
foreach (IPlugin plugin in pluginLoader.LoadPlugins())
{
    plugin.Register(registry);
    Logger.Info($"  Plugin loaded: {plugin.Name} v{plugin.Version}");
}

// -----------------------------------------------------------------------
// Load game data files (Tibia.dat / Tibia.spr)
// -----------------------------------------------------------------------
using var gameData = new TibiaGameData(config.DataDirectory);
gameData.Load();

// -----------------------------------------------------------------------
// Initialise game engine with the script registry
// -----------------------------------------------------------------------
using var engine = new GameEngine(unitOfWork, registry);

// Dispatch global startup events
await registry.DispatchStartupAsync(engine.CreatePublicContext());

// -----------------------------------------------------------------------
// Start network listeners
// NOTE: Login (7171) and Status (7171) share a port — the first byte of each
// packet determines the packet type: 0x01 = login, 0xFF = status.
// -----------------------------------------------------------------------
using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Logger.Info("Shutdown requested…");
    cts.Cancel();
};

// Login server (stub handler — full protocol implementation is a separate concern)
var loginServer = new TcpServer(config.LoginPort, async client =>
{
    Logger.Debug($"Login connection from {client.Client.RemoteEndPoint}");
    await using var conn = new Connection(client);
    // TODO: implement full 8.6 login handshake + status packet dispatch
});

// Game server (stub handler)
var gameServer = new TcpServer(config.GamePort, async client =>
{
    Logger.Debug($"Game connection from {client.Client.RemoteEndPoint}");
    await using var conn = new Connection(client);
    // TODO: implement full 8.6 game protocol loop
});

Logger.Info($"Login / status server listening on port {config.LoginPort}");
Logger.Info($"Game  server listening on port {config.GamePort}");

// Run both servers concurrently until Ctrl+C
await Task.WhenAll(
    loginServer.StartAsync(cts.Token),
    gameServer .StartAsync(cts.Token)
);

// Dispatch global shutdown events
await registry.DispatchShutdownAsync(engine.CreatePublicContext());

Logger.Info("Server shut down gracefully.");
