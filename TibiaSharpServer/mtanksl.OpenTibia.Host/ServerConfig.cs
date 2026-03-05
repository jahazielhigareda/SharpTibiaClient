using System.Text.Json;
using System.Text.Json.Serialization;

namespace mtanksl.OpenTibia.Host;

/// <summary>
/// Experience-rate stages configuration (from config.json).
/// </summary>
public sealed class ExperienceConfig
{
    [JsonPropertyName("stage1Multiplier")]
    public int Stage1Multiplier { get; init; } = 1;

    [JsonPropertyName("stage1MaxLevel")]
    public int Stage1MaxLevel { get; init; } = 8;
}

/// <summary>
/// Full server configuration loaded from <c>config.json</c>.
/// Missing keys fall back to sensible defaults.
///
/// NOTE: In the Tibia protocol, login and status servers intentionally share
/// port 7171.  They are distinguished by the first byte of the incoming
/// packet: <c>0x01</c> = login request, <c>0xFF</c> = status request.
/// </summary>
public sealed class ServerConfig
{
    [JsonPropertyName("loginPort")]
    public int LoginPort { get; init; } = 7171;

    [JsonPropertyName("gamePort")]
    public int GamePort { get; init; } = 7172;

    /// <summary>
    /// The IP address (or hostname) that the login server tells clients to connect to
    /// for the game server.  Defaults to localhost for development.
    /// </summary>
    [JsonPropertyName("gameServerIp")]
    public string GameServerIp { get; init; } = "127.0.0.1";

    /// <summary>
    /// Status port — defaults to the same value as LoginPort (7171) because
    /// the Tibia protocol multiplexes login and status on a single port.
    /// </summary>
    [JsonPropertyName("statusPort")]
    public int StatusPort { get; init; } = 7171;

    [JsonPropertyName("maxPlayers")]
    public int MaxPlayers { get; init; } = 1000;

    [JsonPropertyName("serverName")]
    public string ServerName { get; init; } = "SharpTibiaServer";

    [JsonPropertyName("dataDirectory")]
    public string DataDirectory { get; init; } = "data";

    [JsonPropertyName("pluginsDirectory")]
    public string PluginsDirectory { get; init; } = "plugins";

    [JsonPropertyName("experience")]
    public ExperienceConfig Experience { get; init; } = new();

    // -----------------------------------------------------------------------
    // Factory methods
    // -----------------------------------------------------------------------

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling         = JsonCommentHandling.Skip,
        AllowTrailingCommas         = true
    };

    /// <summary>
    /// Loads <c>config.json</c> from <paramref name="path"/>.
    /// Returns default values if the file does not exist.
    /// Throws <see cref="JsonException"/> if the file exists but is malformed.
    /// </summary>
    public static ServerConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"[Config] '{path}' not found — using defaults.");
            return new ServerConfig();
        }

        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ServerConfig>(json, _options)
               ?? new ServerConfig();
    }
}
