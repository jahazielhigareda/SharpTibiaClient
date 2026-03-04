// SharpTibiaServer — Host entry point (Phase 11)
// All library projects target net8.0; C# 12 is the default language version.
using mtanksl.OpenTibia.Common;
using mtanksl.OpenTibia.Data.InMemory;
using mtanksl.OpenTibia.Game;
using mtanksl.OpenTibia.GameData;
using mtanksl.OpenTibia.Network;
using mtanksl.OpenTibia.Plugins;

// -----------------------------------------------------------------------
// Banner
// -----------------------------------------------------------------------
Console.Title = "SharpTibiaServer";
Logger.Info("SharpTibiaServer starting…");

// -----------------------------------------------------------------------
// Configuration (minimal; replace with config.json parsing in Phase 12)
// -----------------------------------------------------------------------
const int LoginPort = 7171;
const int GamePort  = 7172;
const string DataDirectory   = "data";
const string PluginsDirectory = "plugins";

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
    Id = 1, AccountId = 1, Name = "Admin", World = "SharpTibia",
    Level = 8, Health = 185, MaxHealth = 185, Mana = 90, MaxMana = 90,
    Capacity = 400, Experience = 4200, PosX = 1000, PosY = 1000, PosZ = 7
});

// -----------------------------------------------------------------------
// Load game data files (Tibia.dat / Tibia.spr)
// -----------------------------------------------------------------------
using var gameData = new TibiaGameData(DataDirectory);
gameData.Load();

// -----------------------------------------------------------------------
// Initialise game engine
// -----------------------------------------------------------------------
using var engine = new GameEngine(unitOfWork);

// -----------------------------------------------------------------------
// Load plugins
// -----------------------------------------------------------------------
var pluginLoader = new PluginLoader(PluginsDirectory);
foreach (IPlugin plugin in pluginLoader.LoadPlugins())
    Logger.Info($"  Plugin loaded: {plugin.Name} v{plugin.Version}");

// -----------------------------------------------------------------------
// Start network listeners (login on 7171, game on 7172)
// -----------------------------------------------------------------------
using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Logger.Info("Shutdown requested…");
    cts.Cancel();
};

// Login server (stub handler — full implementation in Phase 12)
var loginServer = new TcpServer(LoginPort, async client =>
{
    Logger.Debug($"Login connection from {client.Client.RemoteEndPoint}");
    await using var conn = new Connection(client);
    // TODO: implement full 8.6 login handshake
});

// Game server (stub handler)
var gameServer = new TcpServer(GamePort, async client =>
{
    Logger.Debug($"Game connection from {client.Client.RemoteEndPoint}");
    await using var conn = new Connection(client);
    // TODO: implement full 8.6 game protocol loop
});

Logger.Info($"Login server listening on port {LoginPort}");
Logger.Info($"Game  server listening on port {GamePort}");

// Run both servers concurrently until Ctrl+C
await Task.WhenAll(
    loginServer.StartAsync(cts.Token),
    gameServer .StartAsync(cts.Token)
);

Logger.Info("Server shut down gracefully.");
