# SharpTibiaClient x TibiaSharpServer — Full Migration Roadmap

## Overview

This roadmap describes the complete, phase-by-phase plan to:

1. **Rebuild SharpTibiaClient** — replace the .NET Framework 4.0 / XNA 4.0 legacy codebase with a pure **C# + .NET 8 + [Raylib-cs](https://github.com/ChrisDill/Raylib-cs)** client targeting **Tibia protocol 8.6**, eliminating all C++ and Lua dependencies.
2. **Upgrade TibiaSharpServer** — modernize remaining `netstandard2.0` library projects to `net8.0`, eliminate NLua scripting, and replace it with a fully C# scripting layer.
3. **Align both systems on Tibia 8.6 protocol** — bridge the critical version mismatch (client at 7.4–7.6, server at 8.6) so both speak the same wire format end-to-end.

> **Critical constraint:** The `otclientv8` fork speaks **protocol 7.6** (C++17/Lua/OpenGL ES 2.0). `TibiaSharpServer` already implements **Tibia protocol 8.6** in C#. **No C++, no Lua** — the new unified stack is C# only.
>
> **Version notation:** Tibia 8.6 is equivalently written as 8.60 in some sources. This document uses **8.6** throughout.

---

## Repositories at a Glance

| Repository | Role | Language | Protocol | Status |
|------------|------|----------|----------|--------|
| `jahazielhigareda/SharpTibiaClient` | New C# client (this repo) | C# — .NET Framework 4.0 + XNA 4.0 | 7.4 | Legacy — full rebuild required |
| `jahazielhigareda/otclientv8` | Old client (reference only — abandon) | C++17 + Lua + OpenGL ES 2.0 | 7.6 | Abandoned — C# rebuild replaces this |
| `jahazielhigareda/TibiaSharpServer` | OpenTibia server | C# — .NET 8 host, netstandard2.0 libs | 8.6 | Partial — libraries need upgrade |

---

## Current State Analysis

### SharpTibiaClient (Client — This Repository)

**Stack:**
- Runtime: .NET Framework 4.0 (Client Profile), x86-only
- Graphics: Microsoft XNA Framework 4.0 (`Microsoft.Xna.Framework.*`)
- Content: XNA Content Pipeline (`CTCContent.contentproj`, `.spritefont`)
- Protocol: Tibia 7.4 (stream-based movie replay only; no live network)
- Build: Visual Studio 2010 solution, XNA Project Type GUIDs

**Legacy Dependencies to Remove:**

| Dependency | Reason |
|------------|--------|
| `Microsoft.Xna.Framework.*` (9 assemblies) | XNA is abandoned; incompatible with .NET 8 |
| `CTCContent.contentproj` | XNA Content Pipeline; cannot target .NET 8 |
| `EnvDTE`, `EnvDTE100`, `EnvDTE80`, `EnvDTE90`, `EnvDTE90a` | VS COM automation; irrelevant to a game client |
| `Microsoft.mshtml` | IE HTML DOM COM interop; irrelevant |
| `Accessibility` (COM) | Unused COM reference |
| `System.Windows.Forms` | Debug window only; not cross-platform |
| `PresentationCore`, `PresentationFramework`, `System.Xaml`, `WindowsBase` | WPF references; unused in core logic |
| `NoStdLib=true` flag | XNA tooling artifact; not needed in .NET 8 |
| XNA Project Type GUIDs | Visual Studio can no longer load these |

**Protocol Gap (7.4 to 8.6):** All packet parsers, login sequence, and game state logic must be rebuilt to match Tibia 8.6 wire format.

---

### otclientv8 (Old Client — Reference Only, Abandoned)

**Stack:**
- Language: C++17
- Scripting: Lua (all UI logic, modules, NPCs, updater, hotkeys, etc.)
- Graphics: OpenGL ES 2.0 (DirectX 9/11 via ANGLE on Windows)
- Audio: OpenAL
- VFS: PhysFS (compressed archive filesystem)
- Protocol: Tibia 7.6 (feature set, not full 8.6)
- Build: CMake (Windows/Linux/macOS/Android)

**Features to Port to C# (functional reference):**

| otclientv8 Lua Module | C# Equivalent to Implement |
|-----------------------|-----------------------------|
| `modules/client` | Login screen, character selection |
| `modules/game_interface` | Main game HUD |
| `modules/game_inventory` | Inventory panel |
| `modules/game_channels` | Chat channels |
| `modules/game_skills` | Skills/stats panel |
| `modules/game_containers` | Container/loot UI |
| `modules/game_viplist` | VIP list |
| `modules/game_minimap` | Minimap |
| `modules/game_console` | Chat console |
| `modules/game_shop` | NPC shop |
| `modules/game_battle` | Battle list |

> **None of the C++ or Lua code is carried forward.** The port is a clean-room C# reimplementation using `otclientv8` as a feature specification reference only.

---

### TibiaSharpServer (Server — TibiaSharpServer Repository)

**Stack:**
- Protocol: Tibia 8.6 (base; experimental 7.40–10.98 range)
- Security: RSA, XTEA, Adler32 — all in C# (`mtanksl.OpenTibia.Security`)
- Network: TCP sockets, custom framing (`mtanksl.OpenTibia.Network`)
- Scripting: **NLua 1.6.3** — Lua for actions, spells, NPCs, events, weapons, monsters (`mtanksl.OpenTibia.Game.Common`)
- Database: SQLite (default), MySQL, MSSQL, PostgreSQL, Oracle, In-Memory
- Host: `net8.0` (executable); all libraries: `netstandard2.0` with `LangVersion 11`
- File formats: `.dat`, `.spr`, `.otb`, `.otbm`, `.pic`, `.xml` parsers in `mtanksl.OpenTibia.FileFormats`
- Tests: xUnit-based test project (`mtanksl.OpenTibia.Tests`)

**Problems to Fix:**

| Issue | Location | Impact |
|-------|----------|--------|
| `netstandard2.0` target on all library projects | All `*.csproj` except Host | Prevents use of .NET 8 APIs |
| **NLua 1.6.3** scripting dependency | `mtanksl.OpenTibia.Game.Common` | Violates "no Lua" constraint |
| `LangVersion 11` explicit override | `Game.Common.csproj` | Unnecessary once targeting `net8.0` (C# 12 is default) |
| Lua config file (`config.lua`) | `mtanksl.OpenTibia.GameData` | Must be replaced with C# configuration |

---

## Protocol 7.6 to 8.6: Key Differences

### Login Server Handshake

| Field | Protocol 7.6 | Protocol 8.6 |
|-------|-------------|-------------|
| Account identifier | `uint32` (numeric account number) | `string` (account name — variable length) |
| Password | `string` | `string` (unchanged) |
| Client version sent | `uint16` value 76 | `uint16` value 860 |
| RSA block size | 128 bytes | 128 bytes (same format, different RSA key accepted) |
| XTEA key exchange | Present | Present (unchanged format) |

### Login Server Response (Character List)

| Field | Protocol 7.6 | Protocol 8.6 |
|-------|-------------|-------------|
| Premium days | Not sent | `uint16` added in character list packet |
| Subscription type | Not present | `byte` added for premium account type |
| Character count | `byte` | `byte` (unchanged) |

### Game Server Login Packet (0x0A)

| Field | Protocol 7.6 | Protocol 8.6 |
|-------|-------------|-------------|
| Player ID | `uint32` | `uint32` |
| Server beat | `uint16` | `uint16` |
| Can report bugs | `byte` | `byte` |
| Premium account flag | Not present | `byte` added |

### Outfit System

| Feature | Protocol 7.6 | Protocol 8.6 |
|---------|-------------|-------------|
| Addons field | Not present | `byte` per outfit slot |
| Outfit packet (0x8E) | No addons byte | Addons byte added |

### Items (.dat Format)

| Feature | Protocol 7.6 | Protocol 8.6 |
|---------|-------------|-------------|
| `.dat` signature | `0x41360000` | `0x44380000` |
| Item property flags | Subset | Additional: `HookEast`, `HookSouth`, `Rotateable`, `HasLookType`, `Cloth`, `MarketItem` |

### New Packet Types in 8.6 (absent in 7.6)

| Packet ID | Direction | Description |
|-----------|-----------|-------------|
| `0x5F` | Server to Client | NPC trade open |
| `0x6D` | Server to Client | Container update item |
| `0xA3` | Server to Client | Spell cooldown group |
| `0xA4` | Server to Client | Spell cooldown (individual) |
| `0xCA` | Server to Client | Quest log list |
| `0xCB` | Server to Client | Quest log detail |

---

## Migration Phases

---

### Phase 1 — Project Modernization: SharpTibiaClient to .NET 8 SDK Style

**Goal:** Migrate from legacy `.csproj` format to SDK-style, targeting .NET 8.

**Complexity: Low | Estimated effort: 1–2 hours**

- [ ] Run `dotnet new sln -n SharpTibiaClient` to create a new SDK solution
- [ ] Create `CTC/CTC.csproj` using SDK-style format targeting `net8.0`; set `<OutputType>Exe</OutputType>`, `<PlatformTarget>AnyCPU</PlatformTarget>`
- [ ] Remove `CTCContent/CTCContent.contentproj` from solution (replaced in Phase 4)
- [ ] Remove all XNA `<ProjectTypeGuid>` entries
- [ ] Remove `<NoStdLib>`, `<NoConfig>`, `<XnaFrameworkVersion>`, `<XnaPlatform>` properties
- [ ] Remove all COM interop `<Reference>` entries: `EnvDTE*`, `Microsoft.mshtml`, `Accessibility`
- [ ] Remove WPF `<Reference>` entries: `PresentationCore`, `PresentationFramework`, `System.Xaml`, `WindowsBase`
- [ ] Add `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`

**Validation Checkpoint:**
- [ ] `dotnet restore` completes without error
- [ ] `dotnet build` produces errors only from missing XNA types (not from project format)

---

### Phase 2 — Add Raylib-cs, Strip XNA References

**Goal:** Add Raylib-cs as the replacement graphics package; remove all XNA assembly references.

**Complexity: Low | Estimated effort: 1 hour**

- [ ] Add Raylib-cs: `dotnet add CTC package Raylib-cs` (latest stable 6.x)
- [ ] Remove all `<Reference Include="Microsoft.Xna.Framework.*" />` entries from `.csproj`
- [ ] Remove `<Reference Include="System.Windows.Forms" />`
- [ ] Audit all `.cs` files for `using Microsoft.Xna.Framework*;` — mark for replacement
- [ ] Add `using Raylib_cs;` and `using System.Numerics;` where needed
- [ ] Add `using Color = Raylib_cs.Color;` alias to avoid `System.Drawing.Color` conflict
- [ ] Add `using Rectangle = Raylib_cs.Rectangle;` alias where needed

**Validation Checkpoint:**
- [ ] `dotnet restore` downloads Raylib-cs successfully
- [ ] No XNA assembly references remain in `.csproj`

---

### Phase 3 — Replace Game Loop and Window Initialization

**Goal:** Replace `Microsoft.Xna.Framework.Game` base class with a Raylib-based game loop.

**Complexity: Medium | Estimated effort: 2–4 hours**

**Before (XNA):**
```csharp
public class Game : Microsoft.Xna.Framework.Game
{
    GraphicsDeviceManager _graphics;
    public Game() {
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 800;
        IsFixedTimeStep = false;
    }
    protected override void Initialize() { ... }
    protected override void LoadContent() { ... }
    protected override void Update(GameTime gameTime) { ... }
    protected override void Draw(GameTime gameTime) { ... }
}
```

**After (Raylib-cs):**
```csharp
using Raylib_cs;
using Color = Raylib_cs.Color;

public class Game
{
    public void Run()
    {
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
        Raylib.InitWindow(1280, 800, "SharpTibiaClient");
        Raylib.SetTargetFPS(60);
        Initialize();
        LoadContent();
        while (!Raylib.WindowShouldClose())
        {
            Update();
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
            Draw();
            Raylib.EndDrawing();
        }
        Unload();
        Raylib.CloseWindow();
    }
    private void Initialize() { }
    private void LoadContent() { }
    private void Update() { /* use Raylib.GetFrameTime() instead of GameTime */ }
    private void Draw() { }
    private void Unload() { }
}
```

- [ ] Rewrite `Game.cs` — remove `Microsoft.Xna.Framework.Game` base class
- [ ] Replace `GraphicsDeviceManager` with `Raylib.InitWindow()`
- [ ] Replace `IsFixedTimeStep = false` with `Raylib.SetTargetFPS(0)` (uncapped) or `Raylib.SetTargetFPS(60)`
- [ ] Replace `Update(GameTime)` with `Update()` using `Raylib.GetFrameTime()`
- [ ] Wrap `Draw()` inside `Raylib.BeginDrawing()` / `Raylib.EndDrawing()` block
- [ ] Update `Program.Main()` to call `new Game().Run()`
- [ ] Remove all `GamerServicesDispatcher` references

**Validation Checkpoint:**
- [ ] Application window opens via Raylib without crash
- [ ] Empty black window renders and closes cleanly

---

### Phase 4 — Replace Content Pipeline and Asset Loading

**Goal:** Remove the XNA Content Pipeline; load all assets directly at runtime using Raylib.

**Complexity: Medium | Estimated effort: 2–3 hours**

| Asset | XNA Method | Replacement |
|-------|-----------|-------------|
| `DefaultSkin.bmp` | `Content.Load<Texture2D>` | `Raylib.LoadImage()` + `Raylib.LoadTextureFromImage()` |
| `StandardFont.spritefont` | `Content.Load<SpriteFont>` | Replace with TTF + `Raylib.LoadFont()` |
| `Tibia.spr` | Manual binary read (already) | Unchanged |
| `Tibia.dat` | Manual binary read (already) | Unchanged |

- [ ] Delete `CTCContent/`; copy `DefaultSkin.bmp` into `CTC/Content/`
- [ ] Replace all `Content.Load<Texture2D>()` calls with `Raylib.LoadImage()` + `Raylib.LoadTextureFromImage()`
- [ ] Replace `StandardFont.spritefont` with a freely licensed TTF font; load via `Raylib.LoadFont()`
- [ ] Add raw assets to `.csproj` with `CopyToOutputDirectory` = `PreserveNewest`
- [ ] Remove all `ContentManager` / `UIContext.Content` fields

**Validation Checkpoint:**
- [ ] Application loads `DefaultSkin.bmp` texture without error
- [ ] Text renders correctly with TTF replacement font
- [ ] No XNA content pipeline references remain

---

### Phase 5 — Replace Rendering Layer (SpriteBatch to Raylib)

**Goal:** Port all XNA `SpriteBatch` rendering calls to Raylib-cs equivalents.

**Complexity: High | Estimated effort: 8–16 hours**

**XNA to Raylib-cs API Mapping:**

| XNA Call | Raylib-cs Equivalent |
|----------|---------------------|
| `spriteBatch.Begin()` | Implicit inside `BeginDrawing()` block |
| `spriteBatch.End()` | (not needed) |
| `spriteBatch.Draw(tex, dst, src, color)` | `Raylib.DrawTexturePro(tex, src, dst, origin, 0f, color)` |
| `spriteBatch.Draw(tex, pos, color)` | `Raylib.DrawTextureV(tex, pos, color)` |
| `spriteBatch.DrawString(font, text, pos, color)` | `Raylib.DrawTextEx(font, text, pos, fontSize, spacing, color)` |
| `GraphicsDevice.SetRenderTarget(rt)` | `Raylib.BeginTextureMode(rt)` |
| `GraphicsDevice.SetRenderTarget(null)` | `Raylib.EndTextureMode()` |
| `new RenderTarget2D(gd, w, h)` | `Raylib.LoadRenderTexture(w, h)` |
| `Color.White` / `Color.Black` (XNA) | `Raylib_cs.Color.White` / `Raylib_cs.Color.Black` |
| `new Color(r, g, b, a)` (XNA, byte 0–255) | `new Raylib_cs.Color(r, g, b, a)` (same byte range, field order: R, G, B, A) |
| `new Rectangle(x, y, w, h)` (XNA) | `new Raylib_cs.Rectangle(x, y, w, h)` |
| `new Vector2(x, y)` (XNA) | `new System.Numerics.Vector2(x, y)` |
| Scissor/clip region | `Raylib.BeginScissorMode(x, y, w, h)` / `Raylib.EndScissorMode()` |

- [ ] Rewrite `UIContext.cs` — remove `GraphicsDevice`, `SpriteBatch`, `ContentManager`; keep `Font` via `Raylib.LoadFont()`
- [ ] Rewrite `UISkin.cs` — replace 9-grid `SpriteBatch.Draw` with `Raylib.DrawTexturePro()`
- [ ] Rewrite `UIView.cs` — change `Draw(SpriteBatch)` to `Draw()` (no parameter); replace all draw calls
- [ ] Update all UIView subclasses: `UIButton`, `UILabel`, `UIFrame`, `UITabFrame`, `UIVirtualFrame`, `UIScrollbar`, `UIStackView`, `UIToggleButton`
- [ ] Rewrite `GameRenderer.cs` — tile/sprite rendering with `Raylib.DrawTexturePro()`
- [ ] Rewrite `GameCanvas.cs` — `RenderTarget2D` to `Raylib.LoadRenderTexture()`
- [ ] Rewrite `GameImage.cs` — raw bytes to `Raylib.LoadImageFromMemory()` + `Raylib.LoadTextureFromImage()`
- [ ] Update `ColorGradient.cs` — XNA `Color` to `Raylib_cs.Color`
- [ ] Update `Margin.cs` — XNA `Rectangle` to `Raylib_cs.Rectangle`

**Validation Checkpoint:**
- [ ] UI skin renders without XNA types
- [ ] Text labels display correctly with Raylib font
- [ ] No `SpriteBatch` references remain anywhere
- [ ] UI layout (buttons, tabs, frames) visually matches original

---

### Phase 6 — Replace Input Handling

**Goal:** Replace XNA mouse/keyboard input with Raylib input API.

**Complexity: Low–Medium | Estimated effort: 2–4 hours**

| XNA Pattern | Raylib Replacement |
|-------------|-------------------|
| `Mouse.GetState()` | `Raylib.GetMousePosition()` |
| `ms.LeftButton == ButtonState.Pressed` | `Raylib.IsMouseButtonDown(MouseButton.Left)` |
| `ms.ScrollWheelValue` | `(int)Raylib.GetMouseWheelMove()` |
| `Keyboard.GetState().IsKeyDown(Keys.Enter)` | `Raylib.IsKeyDown(KeyboardKey.Enter)` |
| `MouseState.X / Y` | `Raylib.GetMouseX()` / `Raylib.GetMouseY()` |

- [ ] Replace all `Mouse.GetState()` / `MouseState` usages
- [ ] Replace all `Keyboard.GetState()` / `KeyboardState` usages
- [ ] Update `UIView.HitTest()` to use `Vector2` from `Raylib.GetMousePosition()`
- [ ] Replace mouse button state comparisons with `Raylib.IsMouseButtonPressed()` / `IsMouseButtonReleased()`
- [ ] Remove all `using Microsoft.Xna.Framework.Input;` directives

**Validation Checkpoint:**
- [ ] Mouse click events route correctly to UI elements
- [ ] Scroll wheel works in `UIScrollbar` and `UIVirtualFrame`
- [ ] No XNA input types referenced

---

### Phase 7 — Replace WinForms Debug Window

**Goal:** Remove `DebugWindow.cs` WinForms dependency; replace with cross-platform console logging.

**Complexity: Low | Estimated effort: 1 hour**

- [ ] Delete or stub out `DebugWindow.cs`
- [ ] Update `Log.cs` to write to `Console.Error` (or a `StreamWriter` file)
- [ ] Remove `System.Windows.Forms` reference from `.csproj`
- [ ] Update `Program.cs` — do not spawn debug window thread

**Validation Checkpoint:**
- [ ] Log output appears in terminal
- [ ] No `System.Windows.Forms` types referenced
- [ ] Application starts and exits cleanly

---

### Phase 8 — Protocol Upgrade: 7.4 to 8.6 (Client)

**Goal:** Rewrite the SharpTibiaClient protocol layer to speak Tibia 8.6, matching TibiaSharpServer.

**Complexity: High | Estimated effort: 16–24 hours**

This is the most impactful functional change. The existing protocol layer reads `.tmv` movie files only. Both security primitives and a live 8.6 network client must be built from scratch.

#### 8a — Security Layer (Client Side)

The server (`mtanksl.OpenTibia.Security`) already implements RSA, XTEA, and Adler32 in C#. The client must implement the same algorithms using the server as reference.

- [ ] Implement `Rsa.cs` (client-side) — use `System.Security.Cryptography.RSA` (built into .NET 8).
  The client encrypts its login block (XTEA session key + account name + password + padding) using the server's well-known OpenTibia RSA public key before sending:
  ```csharp
  // Client-side: encrypt 128-byte login block with server's RSA public key
  var rsa = RSA.Create();
  rsa.ImportParameters(new RSAParameters { Modulus = otPublicModulus, Exponent = otPublicExponent });
  byte[] encryptedBlock = rsa.Encrypt(loginBlock, RSAEncryptionPadding.Pkcs1);
  ```
- [ ] Implement `Xtea.cs` (client-side) — symmetric 8-byte block cipher; port from server reference
- [ ] Implement `Adler32.cs` (client-side) — checksum for packet integrity; port from server reference

#### 8b — Login Server Connection

- [ ] Implement `LoginConnection.cs` — `TcpClient`-based async TCP connection to port 7171
- [ ] Build login handshake: read 4-byte server challenge, build 128-byte RSA-encrypted block with XTEA key + account name + password + padding, send packet type `0x01` with client version `860`
- [ ] Send account identifier as a **length-prefixed `string`** (account name) — not as `uint32`; this is the key protocol change from 7.6 to 8.6 on the login packet
- [ ] Parse character list packet (8.6 format — account name string, premium days `uint16`, character entries)

#### 8c — Game Server Connection

- [ ] Implement `GameConnection.cs` — `TcpClient`-based async TCP connection to port 7172
- [ ] Implement game server handshake: send game login packet (type `0x0A`) with player name, XTEA key, character name; receive game server login response; enable XTEA decryption on subsequent packets

#### 8d — Incoming Packet Dispatcher (Server to Client)

Implement a dispatcher that reads the first byte of each decrypted packet and routes to the correct handler:

| Packet ID | Description |
|-----------|-------------|
| `0x0A` | Game server login |
| `0x14` | Map description |
| `0x15`–`0x18` | Move north/east/south/west |
| `0x1E`–`0x21` | Update/add/remove/transform tile thing |
| `0x6B`–`0x6D` | Container open/close/update |
| `0x78` | Player inventory |
| `0x82` | World light |
| `0x83` | Magic effect |
| `0x85` | Animated text |
| `0x86` | Distance effect |
| `0x8E` | Creature outfits |
| `0x96`–`0x97` | Channel list / open channel |
| `0xA1`–`0xA4` | Player stats, skills, cooldown group, cooldown |
| `0xB4` | Text message |
| `0xCA`–`0xCB` | Quest log list and detail |

#### 8e — Outgoing Packet Builders (Client to Server)

- [ ] Walk north/south/east/west (`0x65`–`0x68`)
- [ ] Turn directions (`0x6F`–`0x72`)
- [ ] Say/whisper/yell/channel (`0x96`)
- [ ] Attack creature (`0xA0`)
- [ ] Look at tile/item (`0x8C`)
- [ ] Use item (`0x82`), Move item (`0x78`), Open/close container (`0x86`–`0x87`)
- [ ] Logout (`0x14`)

#### 8f — `.dat` File Format Upgrade (7.4 to 8.6)

- [ ] Update `TibiaGameData.cs` signature check from `0x41360000` (7.4) to `0x44380000` (8.6)
- [ ] Parse additional item property flags: `HookEast` (`0x80`), `HookSouth` (`0x40`), `Rotateable` (`0x20`), `HasLookType` (`0x10`), `Cloth` (`0x08`)
- [ ] Update outfit packet parser to read the addons `byte` field (absent in 7.4, present in 8.6)

**Validation Checkpoint:**
- [ ] Client connects to `TibiaSharpServer` on `127.0.0.1:7171` with account `1` / password `1`
- [ ] Character list loads successfully
- [ ] Login to game world succeeds (player appears on map)
- [ ] Walking works in all 4 directions
- [ ] Chat messages are sent and received
- [ ] Inventory displays correct items

---

### Phase 9 — Sprite Loading: Tibia.spr to Raylib Textures

**Goal:** Ensure the binary sprite loader integrates with Raylib texture creation.

**Complexity: Medium | Estimated effort: 2–4 hours**

The existing `TibiaGameData.cs` reads raw RGBA pixel data from `Tibia.spr`; the XNA path called `Texture2D.SetData(pixels)`. The Raylib replacement:

```csharp
// Illustrative pseudo-code: Raylib Image.Data is void*; use unsafe cast from pinned byte array
unsafe
{
    fixed (byte* ptr = rgbaPixels)
    {
        Image img = new Image
        {
            Data = (void*)ptr,   // Raylib_cs Image.Data is void*
            Width = 32,
            Height = 32,
            Format = PixelFormat.UncompressedR8G8B8A8,
            Mipmaps = 1
        };
        Texture2D tex = Raylib.LoadTextureFromImage(img);
        // Texture is now GPU-resident; ptr/img can go out of scope
    }
}
```

- [ ] Audit all `Texture2D` creation calls in `TibiaGameData.cs` and `GameImage.cs`
- [ ] Replace XNA `Texture2D(GraphicsDevice, width, height)` + `SetData(pixels)` with Raylib `Image` + `LoadTextureFromImage()`
- [ ] Verify RGBA byte order matches `PixelFormat.UncompressedR8G8B8A8`
- [ ] Ensure sprite transparency (alpha channel) renders correctly
- [ ] Pair every `LoadTextureFromImage` with `Raylib.UnloadTexture()` on object disposal

**Validation Checkpoint:**
- [ ] Tibia sprites load from `Tibia.spr` without corruption
- [ ] Sprite alpha/transparency renders correctly on map
- [ ] No texture memory leaks

---

### Phase 10 — UI Feature Parity with otclientv8 Modules

**Goal:** Implement the game UI panels that correspond to the otclientv8 Lua modules, in pure C#.

**Complexity: High | Estimated effort: 20–40 hours**

Each panel below must be implemented as a C# class extending `UIView`. Reference `otclientv8` Lua modules for feature specification only; all implementation is pure C#.

| Panel | otclientv8 Module | Key Features |
|-------|-------------------|-------------|
| `LoginPanel` | `modules/client` | Server list, account/password fields, character list |
| `GameDesktop` | `modules/game_interface` | Main HUD container, hotbar, menus |
| `InventoryPanel` | `modules/game_inventory` | 10 equipment slots, drag-and-drop |
| `ChatPanel` | `modules/game_channels` | Tab channels, input box, say/whisper/yell |
| `SkillPanel` | `modules/game_skills` | Skills, stats, experience bar |
| `ContainerPanel` | `modules/game_containers` | Grid items, slots, weight |
| `VIPPanel` | `modules/game_viplist` | VIP list, online/offline status |
| `MinimapPanel` | `modules/game_minimap` | 2D overhead map with player dot |
| `BattlePanel` | `modules/game_battle` | Battle list, creature health bars |
| `ShopPanel` | `modules/game_shop` | NPC buy/sell dialog |

- [ ] Implement `LoginPanel.cs` — server selection, character list, connect button
- [ ] Implement `MinimapPanel.cs` — render overhead 2D minimap using `Raylib.DrawPixel()`
- [ ] Implement `BattlePanel.cs` — creature list with health bars and attack selection
- [ ] Implement `ShopPanel.cs` — NPC buy/sell items dialog
- [ ] Implement `HotbarPanel.cs` — 10-slot action bar with keyboard shortcuts

**Validation Checkpoint:**
- [ ] All panels render and respond to mouse/keyboard input
- [ ] Inventory drag-and-drop works between slots and containers
- [ ] Chat correctly routes messages to channels
- [ ] Minimap updates as player walks

---

### Phase 11 — TibiaSharpServer: Upgrade Libraries to .NET 8

**Goal:** Remove the `netstandard2.0` target from all server library projects; standardize on `net8.0`.

**Complexity: Low | Estimated effort: 2–3 hours**

All projects below currently target `netstandard2.0`. The host project already targets `net8.0`.

| Project | Current Target | New Target |
|---------|---------------|------------|
| `mtanksl.OpenTibia.Common` | `netstandard2.0` | `net8.0` |
| `mtanksl.OpenTibia.IO` | `netstandard2.0` | `net8.0` |
| `mtanksl.OpenTibia.Security` | `netstandard2.0` | `net8.0` |
| `mtanksl.OpenTibia.Network` | `netstandard2.0` | `net8.0` |
| `mtanksl.OpenTibia.FileFormats` | `netstandard2.0` | `net8.0` |
| `mtanksl.OpenTibia.Threading` | `netstandard2.0` | `net8.0` |
| `mtanksl.OpenTibia.Data.Common` | `netstandard2.0` | `net8.0` |
| `mtanksl.OpenTibia.Data` | `netstandard2.0` | `net8.0` |
| `mtanksl.OpenTibia.Data.InMemory` | `netstandard2.0` | `net8.0` |
| `mtanksl.OpenTibia.Data.MySql` | `netstandard2.0` | `net8.0` |
| `mtanksl.OpenTibia.Data.MsSql` | `netstandard2.0` | `net8.0` |
| `mtanksl.OpenTibia.Data.Oracle` | `netstandard2.0` | `net8.0` |
| `mtanksl.OpenTibia.Data.PostgreSql` | `netstandard2.0` | `net8.0` |
| `mtanksl.OpenTibia.Data.Sqlite` | `netstandard2.0` | `net8.0` |
| `mtanksl.OpenTibia.Game.Common` | `netstandard2.0` | `net8.0` |
| `mtanksl.OpenTibia.Game` | `netstandard2.0` (assumed) | `net8.0` |
| `mtanksl.OpenTibia.GameData` | `netstandard2.0` (assumed) | `net8.0` |
| `mtanksl.OpenTibia.Plugins` | `netstandard2.0` (assumed) | `net8.0` |

- [ ] In each library `.csproj`, change `<TargetFramework>netstandard2.0</TargetFramework>` to `<TargetFramework>net8.0</TargetFramework>`
- [ ] Remove explicit `<LangVersion>11</LangVersion>` overrides (C# 12 is default for .NET 8)
- [ ] Run `dotnet build` on the full solution to surface API compatibility issues

**Validation Checkpoint:**
- [ ] `dotnet build` succeeds for all server projects
- [ ] `dotnet test` passes all existing tests in `mtanksl.OpenTibia.Tests`
- [ ] Server starts and accepts connections

---

### Phase 12 — TibiaSharpServer: Replace NLua with C# Scripting

**Goal:** Remove NLua dependency from `mtanksl.OpenTibia.Game.Common`; replace Lua scripts with a pure C# plugin/scripting system.

**Complexity: High | Estimated effort: 24–40 hours**

This is the most significant server-side change. NLua provides the runtime for all Lua scripts. Replacing it requires a C# scripting architecture that preserves the same hook points.

#### 12a — Define C# Script Interfaces

Replace Lua callbacks with C# interfaces:

```csharp
public interface IActionScript
{
    Promise OnUse(IContext context, Player player, Item item, Tile tile, Item targetItem);
}

public interface INpcScript
{
    Promise OnSay(IContext context, Player player, string words, Npc npc);
}

public interface ICreatureScript
{
    Promise OnLogin(IContext context, Player player);
    Promise OnLogout(IContext context, Player player);
    Promise OnDeath(IContext context, Creature creature);
}
```

#### 12b — Plugin Discovery via Assembly Loading

```csharp
// NOTE: Only load assemblies from trusted, administrator-controlled directories.
// Assembly.LoadFrom() does not sandbox or verify signatures.
// In production, validate each DLL against an allow-list or use AssemblyLoadContext isolation.
public class PluginLoader
{
    public IEnumerable<IPlugin> LoadPlugins(string pluginsDirectory)
    {
        foreach (var dll in Directory.GetFiles(pluginsDirectory, "*.dll"))
        {
            var asm = Assembly.LoadFrom(dll);
            foreach (var type in asm.GetTypes().Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract))
                yield return (IPlugin)Activator.CreateInstance(type)!;
        }
    }
}
```

#### 12c — Migration Path for Each Lua Script Category

| Lua Script Category | C# Replacement Interface |
|--------------------|--------------------------|
| Actions (`actions/*.lua`) | `IActionScript` |
| Ammunition (`ammunitions/*.lua`) | `IAmmunitionScript` |
| Creature scripts (`creaturescripts/*.lua`) | `ICreatureScript` |
| Global events (`globalevents/*.lua`) | `IGlobalEventScript` |
| Monster attacks (`monsterattacks/*.lua`) | `IMonsterAttackScript` |
| Movements (`movements/*.lua`) | `IMovementScript` |
| NPCs (`npcs/*.lua`) | `INpcScript` |
| Raids (`raids/*.lua`) | `IRaidScript` |
| Runes (`runes/*.lua`) | `IRuneScript` |
| Spells (`spells/*.lua`) | `ISpellScript` |
| Talk actions (`talkactions/*.lua`) | `ITalkActionScript` |
| Weapons (`weapons/*.lua`) | `IWeaponScript` |

#### 12d — Configuration: Replace config.lua

```json
{
  "LoginPort": 7171,
  "GamePort": 7172,
  "StatusPort": 7171,
  "MaxPlayers": 1000,
  "ServerName": "SharpTibiaServer",
  "Experience": { "Stage1Multiplier": 5, "Stage1MaxLevel": 8 }
}
```

> **Note:** In the Tibia protocol, the login server and status server intentionally share port 7171. They are distinguished by the first byte of the incoming packet: `0x01` = login request, `0xFF` = status request. A single listener handles both packet types on this port, matching the `TibiaSharpServer` design.

- [ ] Remove `<PackageReference Include="NLua" Version="1.6.3" />` from `Game.Common.csproj`
- [ ] Define all script interfaces (`IActionScript`, `INpcScript`, `ICreatureScript`, etc.)
- [ ] Implement `PluginLoader.cs` using `Assembly.LoadFrom()` for DLL discovery
- [ ] Convert the most commonly used Lua scripts to C# classes (login, logout, death, say, spells first)
- [ ] Replace `config.lua` with `config.json` and `System.Text.Json` parsing
- [ ] Update `mtanksl.OpenTibia.Plugins` project — replace Lua plugin loader with C# assembly loader
- [ ] Run all existing unit tests to verify game behavior is unchanged

**Validation Checkpoint:**
- [ ] `NLua` package is not referenced by any project
- [ ] Server starts without requiring a Lua runtime
- [ ] Actions, spells, and NPC scripts execute correctly via C# implementations
- [ ] All tests in `mtanksl.OpenTibia.Tests` pass

---

### Phase 13 — .NET 8 Breaking Changes (Both Repositories)

**Goal:** Address API changes when upgrading from legacy targets to .NET 8.

**Complexity: Medium | Estimated effort: 2–4 hours each repo**

| Area | Change | Action |
|------|--------|--------|
| `Color` struct ambiguity (client) | `System.Drawing.Color` vs `Raylib_cs.Color` | Add `using Color = Raylib_cs.Color;` |
| `Rectangle` struct ambiguity (client) | `System.Drawing.Rectangle` vs `Raylib_cs.Rectangle` | Add `using Rectangle = Raylib_cs.Rectangle;` |
| `Vector2` struct (client) | XNA `Vector2` vs `System.Numerics.Vector2` | Use `System.Numerics.Vector2` everywhere |
| `Thread.Abort()` | Throws `PlatformNotSupportedException` on .NET 8 | Replace with `CancellationToken` pattern |
| `GZipStream` | Constructor unchanged | No action needed |
| `BinaryReader` / `FileStream` | Unchanged | No action needed |
| `System.Xml.Linq` | Unchanged | No action needed |
| Nullable reference types | New warnings with `<Nullable>enable</Nullable>` | Annotate or suppress incrementally |
| `RSACryptoServiceProvider` (legacy) | Works on .NET 8 but prefer `RSA.Create()` | Use `System.Security.Cryptography.RSA` |
| `netstandard2.0` APIs (server) | Some APIs not in netstandard2.0 now available natively | Review and adopt .NET 8 APIs |

- [ ] Add `using Color = Raylib_cs.Color;` in all client rendering files
- [ ] Add `using Rectangle = Raylib_cs.Rectangle;` in files mixing drawing rectangles
- [ ] Audit and replace any `Thread.Abort()` with `CancellationToken`
- [ ] Enable nullable analysis and fix or suppress warnings
- [ ] Run `dotnet build -warnaserror` on both solutions to surface issues

**Validation Checkpoint:**
- [ ] `dotnet build` produces zero errors on both solutions
- [ ] No `System.Drawing` dependency remains in the client

---

### Phase 14 — Cross-Platform Build and Integration Validation

**Goal:** Verify client and server build, run, and interoperate correctly on all target platforms.

**Complexity: Low | Estimated effort: 2–4 hours**

| Platform | Client | Server |
|----------|--------|--------|
| Windows 10/11 x64 | Primary | Primary |
| Linux (Ubuntu 22.04+) x64 | Secondary | Already supported |
| macOS (Ventura+) arm64/x64 | Tertiary | Not yet tested |

- [ ] Client: `dotnet publish -r win-x64 --self-contained` (Raylib native libs bundled automatically)
- [ ] Client: `dotnet publish -r linux-x64 --self-contained`
- [ ] Client: `dotnet publish -r osx-arm64 --self-contained`
- [ ] Server: verify `win-x64` and `linux-x64` publish targets still work after Phases 11–12
- [ ] Verify file paths in both projects use `Path.Combine()` (not Windows backslash literals)
- [ ] Test full connection: client on Windows connecting to server on Linux (or vice versa)
- [ ] Update `README.md` with build and run instructions

**Validation Checkpoint:**
- [ ] All three client platform builds succeed
- [ ] Client connects to server, character loads, walking and chat work
- [ ] Asset loading works on Linux (case-sensitive file paths verified)

---

## Phase Summary Table

| Phase | Scope | Description | Complexity | Effort |
|-------|-------|-------------|------------|--------|
| 1 | Client | SDK-style `.csproj`, target `net8.0` | Low | 1–2 h |
| 2 | Client | Add Raylib-cs, strip XNA references | Low | 1 h |
| 3 | Client | Game loop: XNA Game base class to Raylib `InitWindow` | Medium | 2–4 h |
| 4 | Client | Content pipeline to direct `Raylib.LoadTexture/LoadFont` | Medium | 2–3 h |
| 5 | Client | Full rendering: `SpriteBatch` to `Raylib.DrawTexturePro` | **High** | 8–16 h |
| 6 | Client | Input: XNA Mouse/Keyboard to Raylib input API | Low–Med | 2–4 h |
| 7 | Client | WinForms debug window to console logging | Low | 1 h |
| 8 | Client | Protocol upgrade: 7.4 to 8.6 (all packet handlers, security) | **High** | 16–24 h |
| 9 | Client | `Tibia.spr` raw bytes to `Raylib.LoadTextureFromImage` | Medium | 2–4 h |
| 10 | Client | UI feature parity: implement all game panels in C# | **High** | 20–40 h |
| 11 | Server | Library projects: `netstandard2.0` to `net8.0` | Low | 2–3 h |
| 12 | Server | Replace NLua with C# scripting system | **High** | 24–40 h |
| 13 | Both | .NET 8 breaking changes (Color, Rectangle, nullable) | Medium | 2–4 h each |
| 14 | Both | Cross-platform build and integration validation | Low | 2–4 h |
| **Total** | | | | **~85–150 h** |

---

## File-by-File Migration Reference (SharpTibiaClient)

| File | Action | Notes |
|------|--------|-------|
| `CTC/CTC.csproj` | Full rewrite | SDK-style, net8.0, remove XNA/COM refs |
| `CTC/Program.cs` | Minor | Remove debug window, update `Game` instantiation |
| `CTC/Game.cs` | Full rewrite | Remove XNA `Game` base, use Raylib loop |
| `CTC/DebugWindow.cs` | Delete | Replace with `Console.Error` in `Log.cs` |
| `CTC/Common.cs` | Minor | Remove XNA type aliases |
| `CTC/Game/GameRenderer.cs` | Major | `SpriteBatch` to `Raylib.DrawTexturePro` |
| `CTC/Game/GameCanvas.cs` | Major | `RenderTarget2D` to `Raylib.LoadRenderTexture` |
| `CTC/Game/GameImage.cs` | Major | Texture creation from raw bytes |
| `CTC/Game/GameSprite.cs` | Minor | Update `Texture2D` type references |
| `CTC/Game/ClientState.cs` | Minor | Update type references |
| `CTC/Game/ItemType.cs` | Minor | Add 8.6 item property flags |
| `CTC/Game/GameEffect.cs` | Minor | Update type references |
| `CTC/Game/MagicEffect.cs` | Minor | Update rendering calls |
| `CTC/Game/DistanceEffect.cs` | Minor | Update rendering calls |
| `CTC/Game/AnimatedText.cs` | Minor | Update font/draw calls |
| `CTC/Game/Log.cs` | Minor | Remove WinForms binding |
| `CTC/UI/Framework/UIContext.cs` | Full rewrite | Remove `SpriteBatch`/`GraphicsDevice` |
| `CTC/UI/Framework/UIView.cs` | Major | Remove `SpriteBatch` param from `Draw()` |
| `CTC/UI/Framework/UISkin.cs` | Major | Replace SpriteBatch 9-grid with Raylib |
| `CTC/UI/Framework/UIButton.cs` | Major | Remove `SpriteBatch` from `Draw()` |
| `CTC/UI/Framework/UILabel.cs` | Major | `DrawString` to `Raylib.DrawTextEx` |
| `CTC/UI/Framework/UIFrame.cs` | Major | Remove `SpriteBatch` from `Draw()` |
| `CTC/UI/Framework/UITabFrame.cs` | Major | Remove `SpriteBatch` from `Draw()` |
| `CTC/UI/Framework/UIVirtualFrame.cs` | Major | Scissor mode to `Raylib.BeginScissorMode` |
| `CTC/UI/Framework/UIScrollbar.cs` | Major | Remove `SpriteBatch` from `Draw()` |
| `CTC/UI/Framework/UIStackView.cs` | Major | Remove `SpriteBatch` from `Draw()` |
| `CTC/UI/Framework/UIToggleButton.cs` | Major | Remove `SpriteBatch` from `Draw()` |
| `CTC/UI/Framework/ColorGradient.cs` | Minor | XNA `Color` to `Raylib_cs.Color` |
| `CTC/UI/Framework/Margin.cs` | Minor | XNA `Rectangle` to `Raylib_cs.Rectangle` |
| `CTC/UI/Framework/UIException.cs` | No change | Pure C# |
| `CTC/UI/Game/GameDesktop.cs` | Major | `SpriteBatch` to Raylib, window init |
| `CTC/UI/Game/GameFrame.cs` | Major | Remove `SpriteBatch` from `Draw()` |
| `CTC/UI/Game/GameCanvas.cs` | Major | `RenderTexture2D` integration |
| `CTC/UI/Game/GameSidebar.cs` | Major | Remove `SpriteBatch` from `Draw()` |
| `CTC/UI/Game/ChatPanel.cs` | Major | `DrawString` to `Raylib.DrawTextEx` |
| `CTC/UI/Game/InventoryPanel.cs` | Major | Remove `SpriteBatch` from `Draw()` |
| `CTC/UI/Game/SkillPanel.cs` | Major | Remove `SpriteBatch` from `Draw()` |
| `CTC/UI/Game/ContainerPanel.cs` | Major | Remove `SpriteBatch` from `Draw()` |
| `CTC/UI/Game/ItemButton.cs` | Major | Sprite draw to Raylib |
| `CTC/UI/Game/VIPPanel.cs` | Major | Remove `SpriteBatch` from `Draw()` |
| `CTC/Protocol/TibiaGameData.cs` | Full rewrite | 7.4 `.dat` to 8.6; Texture2D to Raylib |
| `CTC/Protocol/TibiaGameProtocol.cs` | Full rewrite | 7.4 packet handlers to 8.6 |
| `CTC/Protocol/TibiaMovieStream.cs` | Delete/Archive | Replaced by live 8.6 network connection |
| `CTC/Protocol/NetworkMessage.cs` | Keep + Extend | Add XTEA decrypt, Adler32 verify |
| `CTC/Protocol/PacketStream.cs` | Rewrite | Live TCP stream replaces movie replay |
| `CTC/Protocol/TibiaConstants.cs` | Update | 7.4 constants to 8.6 constants |
| `CTC/Protocol/TibiaGamePacketParserFactory.cs` | Full rewrite | 8.6 packet ID to handler routing |
| `CTC/Client/ClientViewport.cs` | Keep + Extend | Add quest log, premium flag fields |
| `CTC/Client/ClientPlayer.cs` | Keep + Extend | Add addons field to outfit |
| `CTC/Client/ClientCreature.cs` | Keep + Extend | Add addons to creature outfit |
| `CTC/Client/ClientOutfit.cs` | Minor | Add `byte Addons` field (8.6) |
| All other `CTC/Client/*.cs` | No change | Pure C# game state |

**New files to create (Phase 8):**
- `CTC/Protocol/Rsa.cs`
- `CTC/Protocol/Xtea.cs`
- `CTC/Protocol/Adler32.cs`
- `CTC/Protocol/LoginConnection.cs`
- `CTC/Protocol/GameConnection.cs`

**New files to create (Phase 10):**
- `CTC/UI/Game/LoginPanel.cs`
- `CTC/UI/Game/MinimapPanel.cs`
- `CTC/UI/Game/BattlePanel.cs`
- `CTC/UI/Game/ShopPanel.cs`
- `CTC/UI/Game/HotbarPanel.cs`

---

## Recommended Tool Versions

| Tool | Version |
|------|---------|
| .NET SDK | 8.0 (LTS) |
| Raylib-cs (NuGet) | 6.x (latest stable) |
| Visual Studio | 2022 v17.8+ |
| VS Code + C# Dev Kit | Latest |
| Rider | 2024.x+ |
| xUnit | 2.7+ (for new client tests) |

---

## Notes and Caveats

- **No XNA successor needed.** Raylib-cs provides complete window management + 2D drawing + input API that covers all XNA use cases with a simpler immediate-mode model.
- **MonoGame is NOT recommended** as an XNA replacement — while the spiritual successor to XNA, it retains the `Game`-class inheritance model and content pipeline overhead. Raylib-cs is simpler for a 2D sprite renderer.
- **Native Raylib binaries** are bundled in the Raylib-cs NuGet package for Windows, Linux, and macOS. No manual native library installation is required.
- **Tibia protocol logic** (all files in `CTC/Client/`) requires no structural changes — the game state model is framework-agnostic and will be reused as-is.
- **RSA key in 8.6:** The standard OpenTibia RSA public key is hardcoded in the official 8.6 client and is well-known in the OT community. The `TibiaSharpServer` uses this same key in `mtanksl.OpenTibia.Security/Rsa.cs`. The client must encrypt using the matching public modulus.
- **NLua removal on the server** is a large undertaking because the existing Lua ecosystem covers 50+ scripts across 12 categories. Prioritize converting high-traffic scripts (login, logout, death, say commands) first; convert remaining scripts iteratively across sprints.
- **otclientv8 Lua/C++ source code** is used as a feature reference only — it is never compiled or run. The C# implementation is a clean-room port using the module list as a feature specification.
- **Test coverage:** The SharpTibiaClient currently has no tests. A new xUnit project should cover at minimum: XTEA encrypt/decrypt round-trips, Adler32 checksum validation, `.dat` 8.6 file parsing, and packet serialization/deserialization for the key incoming packet types.
