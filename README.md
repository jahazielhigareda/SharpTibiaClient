# SharpTibiaClient

A **Tibia 8.6**-compatible client and OpenTibia server written entirely in **C# (.NET 8)**.
The client uses [Raylib-cs](https://github.com/ChrisDill/Raylib-cs) for cross-platform 2D
rendering. The server is based on the [TibiaSharpServer](https://github.com/jahazielhigareda/TibiaSharpServer)
OpenTibia implementation. No C++, no Lua, no XNA — pure C#.

> **Goal:** achieve full visual and functional parity with
> [`jahazielhigareda/otclientv8`](https://github.com/jahazielhigareda/otclientv8) — a
> battle-tested Tibia client — while keeping the entire stack in a single C# solution.
> See [`roadmap.md`](roadmap.md) for the complete phase-by-phase plan.

---

## Screenshot

![SharpTibiaClient in-game](https://github.com/user-attachments/assets/a0877f6c-706b-4237-b99b-4de4c5d9cf8c)

*Game view with tile renderer, inventory panel, skills panel and chat channels — running
inside Visual Studio on Windows.*

---

## Repositories

| Repository | Role | Stack |
|------------|------|-------|
| **SharpTibiaClient** (this repo) | Tibia 8.6 client | C# · .NET 8 · Raylib-cs |
| [`TibiaSharpServer`](https://github.com/jahazielhigareda/TibiaSharpServer) | OpenTibia 8.6 server | C# · .NET 8 · TCP |
| [`otclientv8`](https://github.com/jahazielhigareda/otclientv8) | Visual reference only | C++17 · Lua · OpenGL |

---

## Requirements

| Tool | Version |
|------|---------|
| .NET SDK | 8.0 LTS — [download](https://dotnet.microsoft.com/download/dotnet/8.0) |
| OS | Windows 10/11 x64 · Ubuntu 22.04+ x64 · macOS Ventura+ arm64/x64 |
| Tibia assets | `Tibia.dat` and `Tibia.spr` from the official **Tibia 8.60** client |

> **No extra native libraries needed.** Raylib native binaries for Windows, Linux, and
> macOS are bundled inside the `Raylib-cs` NuGet package automatically.

---

## Building

```bash
# Restore all NuGet packages (client + server)
dotnet restore

# Client only
dotnet build CTC/CTC.csproj

# Server only
dotnet build TibiaSharpServer/

# Everything (client + server)
dotnet build SharpTibiaClient.sln
```

---

## Running

### 1. Start the server

```bash
dotnet run --project TibiaSharpServer/mtanksl.OpenTibia.Host/mtanksl.OpenTibia.Host.csproj
```

The server listens on:

| Port | Purpose |
|------|---------|
| **7171** | Login server (also status server on the same port) |
| **7172** | Game server |

Configuration is read from `config.json` in the executable directory. A ready-to-use
default `config.json` is copied automatically on first build.

### 2. Place Tibia assets

Copy your official Tibia 8.60 asset files into the client output directory
(`CTC/bin/Debug/net8.0/` for a debug build):

```
CTC/bin/Debug/net8.0/
├── Tibia.dat      ← required
├── Tibia.spr      ← required
└── Test.tmv       ← optional (movie replay)
```

The `Content/` folder (skin bitmap and font) is copied automatically by the build.

### 3. Start the client

```bash
dotnet run --project CTC/CTC.csproj
```

Enter your account credentials in the login dialog, select a character, and click
**Enter Game**.

> **Default test account:** The server ships with a default account out of the box.
> Check `TibiaSharpServer/mtanksl.OpenTibia.Host/config.json` for the configured
> account name and password, or look for a `data/accounts/` directory if your server
> build uses file-based accounts. The default in-memory account is usually
> **account:** `1` / **password:** `1`.

---

## Self-Contained Publishing

These commands produce a single-file executable with the .NET runtime bundled — no SDK
needed on the target machine.

### Client

```bash
# Windows x64
dotnet publish CTC/CTC.csproj -r win-x64 --self-contained -c Release

# Linux x64
dotnet publish CTC/CTC.csproj -r linux-x64 --self-contained -c Release

# macOS Apple Silicon
dotnet publish CTC/CTC.csproj -r osx-arm64 --self-contained -c Release
```

After publishing, copy `Tibia.dat` and `Tibia.spr` into the same directory as the
published executable. The `Content/` folder is included automatically.

### Server

```bash
# Windows x64
dotnet publish TibiaSharpServer/mtanksl.OpenTibia.Host/mtanksl.OpenTibia.Host.csproj \
    -r win-x64 --self-contained -c Release

# Linux x64
dotnet publish TibiaSharpServer/mtanksl.OpenTibia.Host/mtanksl.OpenTibia.Host.csproj \
    -r linux-x64 --self-contained -c Release
```

---

## Running Tests

```bash
dotnet test TibiaSharpServer/mtanksl.OpenTibia.Tests/
```

---

## Project Structure

```
SharpTibiaClient/
├── CTC/                              # Tibia 8.6 client (Raylib-cs, net8.0)
│   ├── Client/                       # Pure game-state model (map, creatures, items…)
│   │   ├── ClientMap.cs
│   │   ├── ClientPlayer.cs
│   │   ├── ClientCreature.cs
│   │   ├── ClientOutfit.cs
│   │   ├── ClientItem.cs
│   │   ├── ClientContainer.cs
│   │   ├── ClientTile.cs
│   │   └── ClientViewport.cs
│   ├── Game/                         # Renderer, effects, game loop helpers
│   │   ├── GameRenderer.cs           # Tile + sprite rendering (Raylib.DrawTexturePro)
│   │   ├── GameImage.cs              # Raw RGBA → Raylib Texture2D
│   │   ├── GameSprite.cs             # Sprite sheet accessor
│   │   ├── ClientState.cs            # Running game state
│   │   ├── AnimatedText.cs           # Floating damage / heal text
│   │   ├── MagicEffect.cs
│   │   ├── DistanceEffect.cs
│   │   └── Log.cs                    # Console logging (no WinForms)
│   ├── Protocol/                     # Network and file-format layer
│   │   ├── LoginConnection.cs        # Tibia 8.6 login handshake (port 7171)
│   │   ├── GameConnection.cs         # Game server connection (port 7172)
│   │   ├── LivePacketStream.cs       # Live XTEA-encrypted TCP stream
│   │   ├── TibiaMovieStream.cs       # .tmv movie-replay stream
│   │   ├── NetworkMessage.cs         # Read/write helpers (LE int, string…)
│   │   ├── TibiaGameData.cs          # .dat + .spr parser (8.6 format)
│   │   ├── TibiaGameProtocol.cs      # Incoming packet dispatcher
│   │   ├── TibiaGamePacketParserFactory.cs
│   │   ├── TibiaConstants.cs         # Enums: slots, stats, skills, directions
│   │   ├── Rsa.cs                    # RSA-128 (OpenTibia public key)
│   │   ├── Xtea.cs                   # XTEA cipher
│   │   └── Adler32.cs                # Packet checksum
│   ├── UI/
│   │   ├── Framework/                # Reusable widget library (Raylib-backed)
│   │   │   ├── UIView.cs             # Base widget
│   │   │   ├── UIContext.cs          # Font + skin context
│   │   │   ├── UISkin.cs             # 9-grid skin renderer
│   │   │   ├── UIButton.cs
│   │   │   ├── UILabel.cs
│   │   │   ├── UITextbox.cs
│   │   │   ├── UIFrame.cs
│   │   │   ├── UITabFrame.cs
│   │   │   ├── UIScrollbar.cs
│   │   │   ├── UIVirtualFrame.cs
│   │   │   ├── UIStackView.cs
│   │   │   └── UIToggleButton.cs
│   │   └── Game/                     # In-game panels
│   │       ├── LoginPanel.cs         # Login dialog (host, account, password)
│   │       ├── GameDesktop.cs        # Root HUD container
│   │       ├── GameFrame.cs          # Map render area
│   │       ├── GameCanvas.cs         # RenderTexture wrapper
│   │       ├── GameSidebar.cs        # Right-side panel stack
│   │       ├── InventoryPanel.cs     # 10-slot equipment grid
│   │       ├── SkillPanel.cs         # Stats + skill bars
│   │       ├── ChatPanel.cs          # Tabbed chat channels
│   │       ├── BattlePanel.cs        # Creature list with HP bars
│   │       ├── MinimapPanel.cs       # 2D overhead minimap
│   │       ├── ContainerPanel.cs     # Container / bag windows
│   │       ├── VIPPanel.cs           # VIP list
│   │       ├── HotbarPanel.cs        # Action hotbar (10 slots)
│   │       ├── ShopPanel.cs          # NPC shop (buy/sell)
│   │       └── ItemButton.cs         # Sprite item slot
│   ├── Content/
│   │   ├── DefaultSkin.bmp           # UI skin texture atlas
│   │   └── StandardFont.ttf          # Verdana-compatible TTF font
│   ├── Game.cs                       # Raylib window + main game loop
│   └── Program.cs                    # Entry point
├── CTCContent/                       # Legacy XNA content project (archived — not built)
├── TibiaSharpServer/                 # OpenTibia 8.6 server (net8.0)
│   ├── mtanksl.OpenTibia.Host/       # Entry point + config.json
│   ├── mtanksl.OpenTibia.Game/       # Game engine (commands, actions, events)
│   ├── mtanksl.OpenTibia.Game.Common/# Interfaces + scripting contracts
│   ├── mtanksl.OpenTibia.Network/    # TCP listener, packet framing
│   ├── mtanksl.OpenTibia.Security/   # RSA, XTEA, Adler32 (server-side)
│   ├── mtanksl.OpenTibia.FileFormats/# .dat, .spr, .otb, .otbm parsers
│   ├── mtanksl.OpenTibia.Plugins/    # C# assembly plugin loader
│   ├── mtanksl.OpenTibia.Data*/      # Data providers (InMemory, SQLite, MySQL…)
│   └── mtanksl.OpenTibia.Tests/      # xUnit test suite
├── web/
│   └── interface.png                 # UI screenshot
├── roadmap.md                        # Phase-by-phase development plan (Phases 1–25)
└── README.md
```

---

## Feature Status

| Feature | Status |
|---------|--------|
| Tibia 8.6 login protocol (RSA + XTEA) | ✅ Implemented |
| Character list parsing | ✅ Implemented |
| Game server connection (XTEA-encrypted) | ✅ Implemented |
| Tile map rendering (`Tibia.dat` / `Tibia.spr`) | ✅ Implemented |
| Creature / player rendering with outfits | ✅ Implemented |
| Animated text (damage, healing) | ✅ Implemented |
| Magic effects and distance effects | ✅ Implemented |
| Inventory panel (10 equipment slots) | ✅ Implemented |
| Skills / stats panel | ✅ Implemented |
| Chat panel (tabbed channels) | ✅ Implemented |
| Battle list with health bars | ✅ Implemented |
| Minimap (2D overhead) | ✅ Implemented |
| Container / bag windows | ✅ Implemented |
| VIP list | ✅ Implemented |
| Hotbar (10 action slots) | ✅ Implemented |
| NPC shop dialog | ✅ Implemented |
| Movie replay (`.tmv` files) | ✅ Implemented |
| Cross-platform (Windows / Linux / macOS) | ✅ Verified |
| Login screen visual overhaul (otclientv8 parity) | 🔲 Phase 16 |
| Character list as separate window | 🔲 Phase 17 |
| Top menu bar | 🔲 Phase 18 |
| HUD layout overhaul (sidebar, splitter) | 🔲 Phase 19 |
| Minimap: tile colors + floor navigation | 🔲 Phase 20 |
| Chat: tab history + say-mode selector | 🔲 Phase 21 |
| Outfit / death / quest / trade dialogs | 🔲 Phase 22 |
| Options dialog + hotkey manager | 🔲 Phase 23 |
| Client-side walk prediction | 🔲 Phase 24 |
| Tibia-style UI skin textures | 🔲 Phase 25 |

> Phases 1–15 (XNA → .NET 8 + Raylib-cs migration and login protocol work) are complete
> and documented in [`roadmap.md`](roadmap.md). The ✅ rows above are the output of
> those phases. The 🔲 rows are the upcoming phases targeting visual and functional
> parity with `otclientv8`.

See [`roadmap.md`](roadmap.md) for the full plan with per-phase task checklists.

---

## Platform Notes

| Platform | Client | Server | Notes |
|----------|--------|--------|-------|
| Windows 10/11 x64 | ✅ Primary | ✅ Primary | |
| Ubuntu 22.04+ x64 | ✅ Secondary | ✅ Supported | File system is case-sensitive — asset filenames must match exactly |
| macOS Ventura+ arm64 | ✅ Tertiary | 🔲 Not yet tested | Use `osx-arm64` RID; for Intel Macs use `osx-x64` + Rosetta 2 |

**Linux / macOS asset filename note:** the code references `Tibia.dat`, `Tibia.spr`,
`Content/DefaultSkin.bmp`, and `Content/StandardFont.ttf` with that exact casing.
Match it or the files will not load.

---

## Tibia Asset Files

The Tibia `.dat` and `.spr` files are **copyrighted by CipSoft GmbH** and cannot be
distributed with this repository. To run the client you must supply your own copy of
the official Tibia **8.60** client and copy the files from it:

```
Tibia.dat   (≈ 500 KB)
Tibia.spr   (≈ 16 MB)
```

The official 8.60 client can be found on archive sites or through the
[Tibia.com download history](https://www.tibia.com).

---

## Contributing

1. **Fork** this repository and create a branch from `main`.
2. Check [`roadmap.md`](roadmap.md) to pick an open phase or task.
3. Keep changes focused — one phase or sub-task per pull request.
4. Run `dotnet build` and `dotnet test` from the repository root before opening a PR.
5. Include a screenshot for any UI change.

---

## License

This project is provided for educational purposes. Tibia® is a registered trademark
of CipSoft GmbH. This client is not affiliated with or endorsed by CipSoft GmbH.
