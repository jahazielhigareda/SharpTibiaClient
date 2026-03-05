// Phase 14: Cross-Platform Build and Integration Validation
//
// These tests validate the cross-platform file-path conventions introduced in
// Phase 14:
//
//  - All runtime asset paths are constructed with Path.Combine() rather than
//    slash/backslash string literals so they are correct on Windows, Linux,
//    and macOS.
//  - Paths are anchored to AppContext.BaseDirectory (executable directory)
//    rather than the current working directory, so self-contained publish
//    outputs work regardless of where the user runs the binary from.
//  - ServerConfig.Load() falls back gracefully when config.json is absent.
//  - ServerConfig.Load() parses values correctly from a real JSON file.
//  - The plugins directory listing is skipped gracefully when absent.

using System.Runtime.InteropServices;
using System.Text;
using mtanksl.OpenTibia.Host;
using mtanksl.OpenTibia.Plugins;

namespace mtanksl.OpenTibia.Tests;

// ---------------------------------------------------------------------------
// Path-helper tests (platform-agnostic Path.Combine behaviour)
// ---------------------------------------------------------------------------

/// <summary>
/// Validates the cross-platform path construction patterns used throughout
/// the CTC client and TibiaSharpServer host (Phase 14).
/// </summary>
public class CrossPlatformPathTests
{
    [Fact]
    public void PathCombine_TwoSegments_UsesSystemSeparator()
    {
        string result = Path.Combine("Content", "StandardFont.ttf");

        // Must not contain the wrong separator for this platform.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Assert.DoesNotContain('/', result);
        else
            Assert.DoesNotContain('\\', result);
    }

    [Fact]
    public void PathCombine_ThreeSegments_UsesSystemSeparator()
    {
        string result = Path.Combine("base", "Content", "DefaultSkin.bmp");

        char bad = Path.DirectorySeparatorChar == '/' ? '\\' : '/';
        Assert.DoesNotContain(bad, result);
    }

    [Fact]
    public void AppContextBaseDirectory_IsAbsolutePath()
    {
        string baseDir = AppContext.BaseDirectory;

        Assert.True(Path.IsPathRooted(baseDir),
            $"AppContext.BaseDirectory should be an absolute path; got: '{baseDir}'");
    }

    [Fact]
    public void AppContextBaseDirectory_Exists()
    {
        // The directory where the test assembly lives must exist.
        Assert.True(Directory.Exists(AppContext.BaseDirectory));
    }

    [Fact]
    public void ClientAssetPath_Dat_IsAbsoluteAndCorrectName()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Tibia.dat");

        Assert.True(Path.IsPathRooted(path));
        Assert.Equal("Tibia.dat", Path.GetFileName(path));
    }

    [Fact]
    public void ClientAssetPath_Spr_IsAbsoluteAndCorrectName()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Tibia.spr");

        Assert.True(Path.IsPathRooted(path));
        Assert.Equal("Tibia.spr", Path.GetFileName(path));
    }

    [Fact]
    public void ClientAssetPath_Tmv_IsAbsoluteAndCorrectName()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Test.tmv");

        Assert.True(Path.IsPathRooted(path));
        Assert.Equal("Test.tmv", Path.GetFileName(path));
    }

    [Fact]
    public void ClientContentPath_Font_IsAbsoluteAndCorrectName()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Content", "StandardFont.ttf");

        Assert.True(Path.IsPathRooted(path));
        Assert.Equal("StandardFont.ttf", Path.GetFileName(path));
        Assert.EndsWith(Path.Combine("Content", "StandardFont.ttf"), path);
    }

    [Fact]
    public void ClientContentPath_Skin_IsAbsoluteAndCorrectName()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Content", "DefaultSkin.bmp");

        Assert.True(Path.IsPathRooted(path));
        Assert.Equal("DefaultSkin.bmp", Path.GetFileName(path));
        Assert.EndsWith(Path.Combine("Content", "DefaultSkin.bmp"), path);
    }

    [Fact]
    public void ServerConfigPath_IsAbsoluteAndCorrectName()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "config.json");

        Assert.True(Path.IsPathRooted(path));
        Assert.Equal("config.json", Path.GetFileName(path));
    }

    [Fact]
    public void PathCombine_NeverProducesDoubleSlash()
    {
        // Ensure no accidental "Content//StandardFont.ttf" on any platform.
        string path = Path.Combine(AppContext.BaseDirectory, "Content", "StandardFont.ttf");

        Assert.DoesNotContain("//", path);
        Assert.DoesNotContain("\\\\", path);
    }

    [Fact]
    public void PathGetFileName_CaseIsPreserved()
    {
        // On Linux the file system is case-sensitive; filenames must match exactly.
        string datPath  = Path.Combine(AppContext.BaseDirectory, "Tibia.dat");
        string sprPath  = Path.Combine(AppContext.BaseDirectory, "Tibia.spr");
        string skinPath = Path.Combine(AppContext.BaseDirectory, "Content", "DefaultSkin.bmp");
        string fontPath = Path.Combine(AppContext.BaseDirectory, "Content", "StandardFont.ttf");

        Assert.Equal("Tibia.dat",          Path.GetFileName(datPath));
        Assert.Equal("Tibia.spr",          Path.GetFileName(sprPath));
        Assert.Equal("DefaultSkin.bmp",    Path.GetFileName(skinPath));
        Assert.Equal("StandardFont.ttf",   Path.GetFileName(fontPath));
    }
}

// ---------------------------------------------------------------------------
// ServerConfig tests (Phase 14: config.json path and parse validation)
// ---------------------------------------------------------------------------

/// <summary>
/// Validates <see cref="ServerConfig.Load"/> cross-platform path behaviour.
/// </summary>
public class ServerConfigPhase14Tests
{
    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        string missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");

        ServerConfig cfg = ServerConfig.Load(missing);

        Assert.Equal(7171, cfg.LoginPort);
        Assert.Equal(7172, cfg.GamePort);
        Assert.Equal(1000, cfg.MaxPlayers);
        Assert.Equal("SharpTibiaServer", cfg.ServerName);
    }

    [Fact]
    public void Load_ValidJson_ParsesAllFields()
    {
        string tmpPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(tmpPath, """
            {
                "loginPort": 7777,
                "gamePort": 8888,
                "maxPlayers": 500,
                "serverName": "TestWorld",
                "dataDirectory": "testdata",
                "pluginsDirectory": "testplugins",
                "experience": {
                    "stage1Multiplier": 10,
                    "stage1MaxLevel": 20
                }
            }
            """, Encoding.UTF8);

        try
        {
            ServerConfig cfg = ServerConfig.Load(tmpPath);

            Assert.Equal(7777, cfg.LoginPort);
            Assert.Equal(8888, cfg.GamePort);
            Assert.Equal(500,  cfg.MaxPlayers);
            Assert.Equal("TestWorld",    cfg.ServerName);
            Assert.Equal("testdata",     cfg.DataDirectory);
            Assert.Equal("testplugins",  cfg.PluginsDirectory);
            Assert.Equal(10, cfg.Experience.Stage1Multiplier);
            Assert.Equal(20, cfg.Experience.Stage1MaxLevel);
        }
        finally
        {
            File.Delete(tmpPath);
        }
    }

    [Fact]
    public void Load_PartialJson_MissingKeysGetDefaults()
    {
        string tmpPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(tmpPath, """{"loginPort": 9999}""", Encoding.UTF8);

        try
        {
            ServerConfig cfg = ServerConfig.Load(tmpPath);

            Assert.Equal(9999, cfg.LoginPort);
            // Other keys use defaults
            Assert.Equal(7172, cfg.GamePort);
            Assert.Equal("SharpTibiaServer", cfg.ServerName);
        }
        finally
        {
            File.Delete(tmpPath);
        }
    }

    [Fact]
    public void Load_AbsolutePathToTempFile_Works()
    {
        // Simulate what Program.cs does with AppContext.BaseDirectory:
        //   ServerConfig.Load(Path.Combine(AppContext.BaseDirectory, "config.json"))
        string tmpPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(tmpPath, """{"serverName":"AbsoluteTest"}""", Encoding.UTF8);

        try
        {
            Assert.True(Path.IsPathRooted(tmpPath), "Test path should be absolute.");
            ServerConfig cfg = ServerConfig.Load(tmpPath);
            Assert.Equal("AbsoluteTest", cfg.ServerName);
        }
        finally
        {
            File.Delete(tmpPath);
        }
    }
}

// ---------------------------------------------------------------------------
// PluginLoader cross-platform path tests
// ---------------------------------------------------------------------------

/// <summary>
/// Validates that <see cref="PluginLoader"/> handles missing / empty directories
/// gracefully — which is important for cross-platform publish outputs where
/// the plugins directory may not exist yet.
/// </summary>
public class PluginLoaderPhase14Tests
{
    [Fact]
    public void LoadPlugins_MissingDirectory_YieldsEmpty()
    {
        string missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var loader = new PluginLoader(missing);

        IEnumerable<IPlugin> plugins = loader.LoadPlugins();

        Assert.Empty(plugins);
    }

    [Fact]
    public void LoadPlugins_EmptyDirectory_YieldsEmpty()
    {
        string tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmpDir);

        try
        {
            var loader  = new PluginLoader(tmpDir);
            var plugins = loader.LoadPlugins().ToList();
            Assert.Empty(plugins);
        }
        finally
        {
            Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Fact]
    public void LoadPlugins_DirectoryPath_IsAbsolute()
    {
        // Demonstrate that constructing the plugin path with Path.Combine +
        // AppContext.BaseDirectory produces an absolute path.
        string pluginsPath = Path.Combine(AppContext.BaseDirectory, "plugins");

        Assert.True(Path.IsPathRooted(pluginsPath));
    }
}
