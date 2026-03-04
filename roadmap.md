# SharpTibiaClient — Migration Roadmap: .NET 8 + Raylib-cs

## Overview

This roadmap describes the complete, phase-by-phase plan to migrate **SharpTibiaClient** from its current stack (.NET Framework 4.0 / XNA 4.0 / x86-only) to a modern, cross-platform stack based on **.NET 8** and **[Raylib-cs](https://github.com/ChrisDill/Raylib-cs)** (C# bindings for Raylib). All implementation remains in **C# only** — no additional languages are required.

---

## Current State Analysis

### Technology Stack

| Component            | Current                              | Target                          |
|----------------------|--------------------------------------|---------------------------------|
| Runtime              | .NET Framework 4.0 (Client Profile)  | .NET 8                          |
| Graphics/Game Loop   | Microsoft XNA Framework 4.0          | Raylib-cs 6.x                   |
| Platform Target      | x86 only (32-bit Windows)            | AnyCPU (Windows / Linux / macOS)|
| Build System         | Visual Studio 2010 / MSBuild 4.0     | SDK-style `.csproj` / MSBuild   |
| Content Pipeline     | XNA Content Pipeline                 | Direct asset loading            |
| UI Debug Window      | System.Windows.Forms                 | Raylib GUI or Console           |
| UI Layout Files      | Custom XML (`ConnectDialog.xml`, `DefaultSkin.xml`) | Kept (custom XML parser)   |
| Fonts                | XNA `.spritefont` (XML)              | TTF/BMFont loaded via Raylib    |
| Project Type GUIDs   | XNA Game Studio project type         | Standard SDK-style project      |

### Legacy Dependencies to Remove

| Dependency                         | Reason to Remove                                              |
|------------------------------------|---------------------------------------------------------------|
| `Microsoft.Xna.Framework.*`        | XNA is abandoned; not available on .NET 8                     |
| `CTCContent.contentproj`           | XNA Content Pipeline; not compatible with .NET 8              |
| `EnvDTE`, `EnvDTE100`, `EnvDTE90`, `envdte80`, `envdte90a` | Visual Studio COM automation; irrelevant to a game client |
| `Microsoft.mshtml`                 | IE HTML DOM COM interop; irrelevant                           |
| `Accessibility` (COM)              | Unused COM reference                                          |
| `System.Windows.Forms` (debug only)| Replace with console or Raylib-based debug output            |
| `PresentationCore`, `PresentationFramework`, `System.Xaml`, `WindowsBase` | WPF references; unused in core logic |
| `Microsoft.Xna.Framework.GamerServices` | Xbox Live services; not needed                         |
| `NoStdLib=true` flag               | Required by old XNA tooling; not needed in .NET 8             |

### Key XNA APIs to Replace

| XNA Type / Namespace                          | Raylib-cs / .NET 8 Equivalent                        |
|-----------------------------------------------|------------------------------------------------------|
| `Microsoft.Xna.Framework.Game`                | Custom game loop using `Raylib.InitWindow()` / `Raylib.WindowShouldClose()` |
| `GraphicsDeviceManager`                       | `Raylib.SetConfigFlags()` / `Raylib.InitWindow()`    |
| `SpriteBatch` / `SpriteBatch.Draw()`          | `Raylib.DrawTexturePro()` / `Raylib.DrawTextureRec()` |
| `Texture2D` / `Texture2D.FromStream()`        | `Raylib.LoadTextureFromImage()` / `Raylib.LoadImage()` |
| `SpriteFont` / `SpriteBatch.DrawString()`     | `Raylib.LoadFont()` / `Raylib.DrawTextEx()`          |
| `Color` (XNA struct)                          | `Raylib_cs.Color` struct                             |
| `Rectangle` (XNA struct)                      | `Raylib_cs.Rectangle` struct                         |
| `Vector2` (XNA struct)                        | `System.Numerics.Vector2` (built into .NET 8)        |
| `ContentManager` / `Content.Load<T>()`        | Direct file loading via Raylib or `System.IO`        |
| `GameTime`                                    | `Raylib.GetFrameTime()` / `Raylib.GetTime()`         |
| `MouseState` / `Mouse.GetState()`             | `Raylib.GetMousePosition()` / `Raylib.IsMouseButtonDown()` |
| `KeyboardState` / `Keyboard.GetState()`       | `Raylib.IsKeyDown()` / `Raylib.GetKeyPressed()`      |
| `GraphicsDevice.Clear()`                      | `Raylib.ClearBackground()`                           |
| `RenderTarget2D`                              | `Raylib_cs.RenderTexture2D`                          |
| `Effect` / `BasicEffect`                      | Raylib shaders (`Raylib.LoadShader()`)               |
| `GamerServicesDispatcher`                     | Remove entirely                                      |

### Incompatible Project Features

- **XNA Project Type GUIDs** — Visual Studio can no longer load these without legacy extensions
- **XNA Content Pipeline** (`.contentproj`) — Build step is incompatible with SDK-style projects
- **x86-only Platform Target** — Raylib-cs ships as AnyCPU with native binaries for multiple platforms
- **`.NET Framework 4.0 Client Profile`** — Not supported in .NET 8 (full SDK only)
- **`NoStdLib=true`** — XNA build requirement that breaks .NET 8 compilation
- **`StandardFont.spritefont`** — XNA-specific font format; must be replaced with TTF + `Raylib.LoadFont()`

---

## Phase 1 — Project Modernization (Complexity: Low–Medium)

**Goal:** Migrate from legacy `.csproj` format to SDK-style, targeting .NET 8. Remove build-time blockers.

### Tasks

- [ ] Create a new SDK-style solution file (`SharpTibiaClient.sln`) using `dotnet new sln`
- [ ] Replace `CTC/CTC.csproj` with a new SDK-style `.csproj` targeting `net8.0`
- [ ] Set `<OutputType>Exe</OutputType>` (console app as starting point; window added via Raylib)
- [ ] Remove the `CTCContent/CTCContent.contentproj` from the solution (content pipeline replaced in Phase 3)
- [ ] Remove all XNA `ProjectTypeGuid` entries
- [ ] Remove `NoStdLib`, `NoConfig`, `DefineConstants` related to XNA
- [ ] Remove COM interop references: `EnvDTE*`, `Microsoft.mshtml`, `Accessibility`
- [ ] Remove WPF references: `PresentationCore`, `PresentationFramework`, `System.Xaml`, `WindowsBase`
- [ ] Change `<PlatformTarget>x86</PlatformTarget>` to `<PlatformTarget>AnyCPU</PlatformTarget>`
- [ ] Add `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` for modern C#
- [ ] Verify project loads and compiles (expect many errors from missing XNA usings — fixed in later phases)

### Validation Checkpoint

- [ ] `dotnet restore` completes without error
- [ ] `dotnet build` produces errors only from missing XNA types (not from project format issues)

### Estimated Complexity: **Low**

---

## Phase 2 — Add Raylib-cs and Remove XNA NuGet/Assembly References (Complexity: Low)

**Goal:** Add Raylib-cs as the replacement graphics package and remove all XNA assembly references.

### Tasks

- [ ] Add Raylib-cs via NuGet: `dotnet add package Raylib-cs` (latest stable — 6.x)
- [ ] Add `System.Numerics` reference if not implicit (built into .NET 8 BCL)
- [ ] Remove all `<Reference>` entries for `Microsoft.Xna.Framework.*` assemblies from `.csproj`
- [ ] Remove `<Reference>` entries for `System.Windows.Forms` (or keep if debug console is desired on Windows)
- [ ] Audit `using` statements across all `.cs` files for XNA namespaces:
  - `using Microsoft.Xna.Framework;`
  - `using Microsoft.Xna.Framework.Graphics;`
  - `using Microsoft.Xna.Framework.Input;`
  - `using Microsoft.Xna.Framework.Content;`
  - `using Microsoft.Xna.Framework.GamerServices;`
- [ ] Add `using Raylib_cs;` and `using System.Numerics;` where needed

### Validation Checkpoint

- [ ] `dotnet restore` downloads Raylib-cs successfully
- [ ] No XNA-related assembly references remain in `.csproj`

### Estimated Complexity: **Low**

---

## Phase 3 — Replace the Game Loop and Window Initialization (Complexity: Medium)

**Goal:** Replace `Microsoft.Xna.Framework.Game` with a Raylib-based game loop in `Game.cs`.

### Current Code (XNA)

```csharp
// Game.cs
public class Game : Microsoft.Xna.Framework.Game
{
    GraphicsDeviceManager _graphics;

    public Game()
    {
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 800;
        _graphics.SynchronizeWithVerticalRetrace = false;
        IsFixedTimeStep = false;
    }

    protected override void Initialize() { ... }
    protected override void LoadContent() { ... }
    protected override void Update(GameTime gameTime) { ... }
    protected override void Draw(GameTime gameTime) { ... }
}
```

### Target Code (Raylib-cs)

```csharp
// Game.cs
using Raylib_cs;

public class Game
{
    private GameDesktop _desktop;

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
            Raylib.ClearBackground(Raylib_cs.Color.Black); // Use qualified name to avoid System.Drawing.Color ambiguity
            Draw();
            Raylib.EndDrawing();
        }

        Unload();
        Raylib.CloseWindow();
    }

    private void Initialize() { /* former Initialize() body */ }
    private void LoadContent() { /* former LoadContent() body */ }
    private void Update() { /* former Update(GameTime) body, use Raylib.GetFrameTime() */ }
    private void Draw() { /* former Draw(GameTime) body */ }
    private void Unload() { /* dispose textures, fonts */ }
}
```

### Tasks

- [ ] Rewrite `Game.cs` — remove `Microsoft.Xna.Framework.Game` base class
- [ ] Replace `GraphicsDeviceManager` initialization with `Raylib.InitWindow()`
- [ ] Replace `IsFixedTimeStep = false` with `Raylib.SetTargetFPS(0)` (uncapped) or `Raylib.SetTargetFPS(60)`
- [ ] Replace `protected override Update(GameTime)` with `private void Update()` using `Raylib.GetFrameTime()`
- [ ] Replace `protected override Draw(GameTime)` with drawing inside `BeginDrawing()` / `EndDrawing()` block
- [ ] Update `Program.Main()` to instantiate `Game` and call `Run()` instead of `game.Run()`
- [ ] Remove `GamerServicesDispatcher.WindowHandle` and all gamer services code

### Validation Checkpoint

- [ ] Application window opens via Raylib without crash
- [ ] Empty black window renders and closes cleanly on `Escape` or window close button

### Estimated Complexity: **Medium**

---

## Phase 4 — Replace Content Pipeline and Asset Loading (Complexity: Medium)

**Goal:** Remove the XNA Content Pipeline (`CTCContent.contentproj`) and load all assets directly at runtime using Raylib and standard .NET I/O.

### Asset Inventory

| Asset File             | XNA Loading Method        | Raylib-cs Replacement                          |
|------------------------|---------------------------|------------------------------------------------|
| `DefaultSkin.bmp`      | `Content.Load<Texture2D>` | `Raylib.LoadImage()` + `Raylib.LoadTextureFromImage()` |
| `StandardFont.spritefont` | `Content.Load<SpriteFont>` | Replace with a TTF font + `Raylib.LoadFont()` |
| `Tibia.spr`            | Manual binary read (already) | Unchanged — already using `FileStream`       |
| `Tibia.dat`            | Manual binary read (already) | Unchanged — already using `FileStream`       |
| `Test.tmv`             | Manual binary read (already) | Unchanged — already using `FileStream`       |

### Tasks

- [ ] Delete `CTCContent/` directory (or retain only raw asset files)
- [ ] Copy `DefaultSkin.bmp` to a local `Content/` directory within `CTC/`
- [ ] Replace all `Content.Load<Texture2D>("DefaultSkin")` calls with:
  ```csharp
  Image img = Raylib.LoadImage("Content/DefaultSkin.bmp");
  Texture2D tex = Raylib.LoadTextureFromImage(img);
  Raylib.UnloadImage(img);
  ```
- [ ] Replace `StandardFont.spritefont` with a freely licensed TTF font (e.g., `Roboto.ttf` or `Arial.ttf`)
  and load via: `Font font = Raylib.LoadFont("Content/DefaultFont.ttf");`
- [ ] Add raw asset files (`Content/`) to `.csproj` with `CopyToOutputDirectory`:
  ```xml
  <ItemGroup>
    <Content Include="Content\**\*">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
  </ItemGroup>
  ```
- [ ] Remove `ContentManager`/`UIContext.Content` fields and replace with direct Raylib load calls

### Validation Checkpoint

- [ ] Application loads `DefaultSkin.bmp` texture without error
- [ ] Text renders correctly using the replacement TTF font
- [ ] No XNA content pipeline references remain

### Estimated Complexity: **Medium**

---

## Phase 5 — Replace Rendering Layer (`GameRenderer`, `UIContext`, `UISkin`) (Complexity: High)

**Goal:** Port all XNA rendering calls to Raylib-cs equivalents. This is the largest migration effort.

### `UIContext.cs` — Global Graphics Context

**Current (XNA):**
```csharp
public static class UIContext
{
    public static GraphicsDevice Graphics;
    public static ContentManager Content;
    public static SpriteBatch Batch;
    public static SpriteFont Font;

    public static void Initialize(GraphicsDevice gd, ContentManager content)
    {
        Graphics = gd;
        Batch = new SpriteBatch(gd);
        Content = content;
    }
}
```

**Target (Raylib-cs):**
```csharp
public static class UIContext
{
    public static Font Font;
    // No GraphicsDevice or SpriteBatch needed — Raylib uses immediate-mode rendering
}
```

### `UIView.cs` and All UI Components — Drawing Calls

| XNA Call                                          | Raylib-cs Equivalent                                         |
|---------------------------------------------------|--------------------------------------------------------------|
| `spriteBatch.Begin()`                             | (Implicit inside `BeginDrawing()` block)                     |
| `spriteBatch.End()`                               | (No equivalent needed)                                       |
| `spriteBatch.Draw(texture, destRect, srcRect, color)` | `Raylib.DrawTexturePro(texture, src, dst, origin, 0f, color)` |
| `spriteBatch.Draw(texture, position, color)`      | `Raylib.DrawTextureV(texture, position, color)`              |
| `spriteBatch.DrawString(font, text, pos, color)`  | `Raylib.DrawTextEx(font, text, pos, fontSize, spacing, color)` |
| `GraphicsDevice.SetRenderTarget(rt)`              | `Raylib.BeginTextureMode(renderTexture)`                     |
| `GraphicsDevice.SetRenderTarget(null)`            | `Raylib.EndTextureMode()`                                    |
| `new RenderTarget2D(gd, width, height)`           | `Raylib.LoadRenderTexture(width, height)`                    |
| `Color.White` / `Color.Black` (XNA)               | `Raylib_cs.Color.White` / `Raylib_cs.Color.Black` (use qualified name to avoid `System.Drawing.Color` ambiguity) |
| `new Color(r, g, b, a)` (XNA — `byte` values 0–255) | `new Raylib_cs.Color(r, g, b, a)` (also `byte` values 0–255; field order is identical: R, G, B, A) |
| `new Rectangle(x, y, w, h)` (XNA)                | `new Rectangle(x, y, w, h)` (Raylib_cs — same signature)    |
| `new Vector2(x, y)` (XNA)                        | `new Vector2(x, y)` (`System.Numerics`)                      |
| `Texture2D` (XNA)                                 | `Texture2D` (Raylib_cs)                                      |

### Tasks

- [ ] Rewrite `UIContext.cs`:
  - Remove `GraphicsDevice`, `SpriteBatch`, `ContentManager` fields
  - Keep `Font` field; load font via `Raylib.LoadFont()`
  - Remove `Initialize(GraphicsDevice, ContentManager)` — replace with `Initialize(string fontPath)`
- [ ] Rewrite `UISkin.cs`:
  - Replace `Texture2D` loaded via `ContentManager` with `Raylib.LoadTexture()`
  - Replace 9-grid drawing logic (XNA `spriteBatch.Draw`) with `Raylib.DrawTexturePro()` calls
- [ ] Rewrite `UIView.cs` — `Draw(SpriteBatch)` → `Draw()` (no parameter needed):
  - Replace all `spriteBatch.Draw*(...)` calls with Raylib equivalents
  - Clip/scissor regions: use `Raylib.BeginScissorMode()` / `Raylib.EndScissorMode()`
- [ ] Update all UIView subclasses (`UIButton`, `UILabel`, `UIFrame`, `UITabFrame`, `UIVirtualFrame`, `UIScrollbar`, `UIStackView`, `UIToggleButton`) to remove `SpriteBatch` parameter from `Draw()`
- [ ] Rewrite `GameRenderer.cs`:
  - Replace `SpriteBatch.Draw()` tile/sprite rendering with `Raylib.DrawTexturePro()`
  - Replace `RenderTarget2D` with `Raylib.LoadRenderTexture()`
  - Replace `GraphicsDevice.SetRenderTarget()` with `Raylib.BeginTextureMode()` / `Raylib.EndTextureMode()`
- [ ] Rewrite `GameCanvas.cs` (UI/Game):
  - Replace render target usage pattern with Raylib `RenderTexture2D`
- [ ] Rewrite `GameImage.cs`:
  - Replace `Texture2D` creation from raw bytes with `Raylib.LoadImageFromMemory()` + `Raylib.LoadTextureFromImage()`
- [ ] Update `ColorGradient.cs` — change `Microsoft.Xna.Framework.Color` to `Raylib_cs.Color`
- [ ] Update `Margin.cs` — change `Microsoft.Xna.Framework.Rectangle` to `Raylib_cs.Rectangle` (same fields)

### Validation Checkpoint

- [ ] Application renders UI skin without XNA types
- [ ] Text labels render correctly with Raylib font
- [ ] No `SpriteBatch` references remain in any file
- [ ] UI layout (buttons, frames, tabs) visually matches original

### Estimated Complexity: **High**

---

## Phase 6 — Replace Input Handling (Complexity: Low–Medium)

**Goal:** Replace XNA mouse and keyboard input with Raylib input API.

### Current Input Code (`Game.cs`, `UIView.cs`)

```csharp
// XNA
MouseState ms = Mouse.GetState();
if (ms.LeftButton == ButtonState.Pressed) { ... }
Point mousePos = new Point(ms.X, ms.Y);
```

### Target Input Code (Raylib-cs)

```csharp
// Raylib
Vector2 mousePos = Raylib.GetMousePosition();
if (Raylib.IsMouseButtonDown(MouseButton.Left)) { ... }
if (Raylib.IsMouseButtonPressed(MouseButton.Right)) { ... }
int wheel = (int)Raylib.GetMouseWheelMove();
```

### Tasks

- [ ] Replace all `Mouse.GetState()` calls with `Raylib.GetMousePosition()` / `Raylib.IsMouseButtonDown()`
- [ ] Replace all `MouseState.LeftButton == ButtonState.Pressed` patterns with `Raylib.IsMouseButtonDown(MouseButton.Left)`
- [ ] Replace scroll wheel detection (`MouseState.ScrollWheelValue`) with `Raylib.GetMouseWheelMove()`
- [ ] Replace keyboard state (`Keyboard.GetState()`, `Keys.*`) with `Raylib.IsKeyDown(KeyboardKey.*)` where used
- [ ] Update `UIView.HitTest()` and mouse event routing to use Raylib `Vector2` instead of XNA `Point`
- [ ] Remove `Microsoft.Xna.Framework.Input` usings from all files

### Validation Checkpoint

- [ ] Mouse click events route correctly to UI elements
- [ ] Scroll wheel works in `UIScrollbar` and `UIVirtualFrame`
- [ ] No XNA input types referenced in any file

### Estimated Complexity: **Low–Medium**

---

## Phase 7 — Replace DebugWindow (WinForms) (Complexity: Low)

**Goal:** Remove the WinForms-based `DebugWindow` dependency and replace with a platform-neutral alternative.

### Current Implementation

`DebugWindow.cs` spawns a secondary WinForms window to display `Log` output.

### Options

| Option | Description | Recommended |
|--------|-------------|-------------|
| **Console output** | Redirect `Log` events to `Console.WriteLine()` | ✅ Yes |
| **Raylib text overlay** | Draw log lines in Raylib window in debug builds | Optional |
| **Keep WinForms** | `System.Windows.Forms` works on .NET 8 Windows only | ❌ No (not cross-platform) |

### Tasks

- [ ] Delete or stub out `DebugWindow.cs`
- [ ] Update `Log.cs` to write to `Console.Error` or a `StreamWriter` log file instead of WinForms controls
- [ ] Remove `System.Windows.Forms` reference from `.csproj`
- [ ] Ensure `Program.cs` no longer spawns the WinForms debug window thread

### Validation Checkpoint

- [ ] Log output visible in terminal/console
- [ ] No `System.Windows.Forms` types referenced anywhere
- [ ] Application starts and exits cleanly on all target platforms

### Estimated Complexity: **Low**

---

## Phase 8 — Handle .NET 8 Breaking Changes (Complexity: Medium)

**Goal:** Address API surface changes and C# language evolution between .NET Framework 4.0 and .NET 8.

### Breaking Changes to Address

| Area | Change | Action Required |
|------|--------|-----------------|
| **GZipStream** | Constructor signature unchanged; `CompressionMode` still exists | No action needed |
| **BinaryReader / FileStream** | Unchanged in .NET 8 | No action needed |
| **System.Xml.Linq** | Unchanged | No action needed |
| **Reflection** | `Assembly.GetManifestResourceStream()` unchanged | No action needed |
| **`Thread` / threading** | `Thread.Abort()` throws `PlatformNotSupportedException` | Replace with `CancellationToken` if used |
| **Nullable reference types** | New warnings with `<Nullable>enable</Nullable>` | Annotate or suppress as needed |
| **`Color` struct ambiguity** | `System.Drawing.Color` vs `Raylib_cs.Color` | Use explicit aliases or `using Color = Raylib_cs.Color;` |
| **`Rectangle` struct ambiguity** | `System.Drawing.Rectangle` vs `Raylib_cs.Rectangle` | Use explicit alias: `using Rectangle = Raylib_cs.Rectangle;` |
| **`Vector2` struct** | XNA `Vector2` vs `System.Numerics.Vector2` | Use `System.Numerics.Vector2` everywhere |
| **`BitConverter`** | Unchanged in .NET 8 | No action needed |
| **`Encoding`** | Unchanged in .NET 8 | No action needed |
| **`AppDomain.SetupInformation`** | May be restricted | Remove if used |
| **COM interop** | DTE and mshtml COM references removed | Already covered in Phase 1 |
| **`NetworkStream` / `Socket`** | Minor API additions but backward compatible | Likely no changes needed |

### Tasks

- [ ] Add `using Color = Raylib_cs.Color;` alias at the top of all rendering files to avoid `System.Drawing.Color` conflicts
- [ ] Add `using Rectangle = Raylib_cs.Rectangle;` alias in files mixing XNA/drawing rectangles
- [ ] Remove `System.Drawing` reference if only used for `Color`/`Rectangle` (now provided by Raylib_cs)
- [ ] Audit for `Thread.Abort()` calls and replace with `CancellationToken` pattern
- [ ] Enable nullable analysis (`<Nullable>enable</Nullable>`) and suppress or fix warnings incrementally
- [ ] Run `dotnet build -warnaserror` to surface remaining issues

### Validation Checkpoint

- [ ] `dotnet build` produces zero errors and zero warnings (or known/accepted warnings only)
- [ ] No `System.Drawing` dependency remaining (unless explicitly needed)

### Estimated Complexity: **Medium**

---

## Phase 9 — Sprite Loading from `Tibia.spr` (Complexity: Medium)

**Goal:** Ensure the existing binary sprite loader (`TibiaGameData.cs`) integrates correctly with Raylib texture creation.

### Current Pattern

`TibiaGameData.cs` reads raw RGBA pixel data from `Tibia.spr` using `BinaryReader`. The raw bytes are then passed to an XNA `Texture2D` constructor.

### Target Pattern (Raylib-cs)

```csharp
// Create an Image from raw RGBA pixel data
Image img = new Image
{
    Data = /* pointer to pixel array */,
    Width = spriteWidth,
    Height = spriteHeight,
    Format = PixelFormat.UncompressedR8G8B8A8,
    Mipmaps = 1
};
Texture2D texture = Raylib.LoadTextureFromImage(img);
```

Or using `Raylib.LoadImageFromMemory()` if data is available as a managed byte array:

```csharp
// Using managed bytes (safe alternative)
unsafe
{
    fixed (byte* ptr = pixelData)
    {
        Image img = Raylib.LoadImageFromMemory(".raw", (sbyte*)ptr, pixelData.Length);
        // Set width/height/format manually
        Texture2D texture = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);
    }
}
```

### Tasks

- [ ] Audit `TibiaGameData.cs` and `GameImage.cs` for all `Texture2D` creation calls
- [ ] Replace XNA `Texture2D(GraphicsDevice, width, height)` + `SetData(pixels)` with Raylib image + texture pipeline
- [ ] Ensure sprite RGBA byte order matches Raylib's expected format (`PixelFormat.UncompressedR8G8B8A8`)
- [ ] Verify sprite transparency (alpha channel) renders correctly in Raylib
- [ ] Unload Raylib textures when `GameImage`/`GameSprite` objects are disposed

### Validation Checkpoint

- [ ] Tibia sprites load from `Tibia.spr` without corruption
- [ ] Sprite alpha/transparency renders correctly
- [ ] No memory leaks from unmanaged Raylib textures (all paired with `Raylib.UnloadTexture()`)

### Estimated Complexity: **Medium**

---

## Phase 10 — Cross-Platform Build and Runtime Validation (Complexity: Low)

**Goal:** Verify the migrated application builds and runs correctly on all target platforms.

### Target Platforms

| Platform     | Architecture | Status Goal |
|--------------|-------------|-------------|
| Windows 10/11 | x64         | ✅ Primary  |
| Linux (Ubuntu 22.04+) | x64 | ✅ Secondary |
| macOS (Ventura+) | arm64 / x64 | ✅ Tertiary |

### Tasks

- [ ] Ensure `.csproj` has no Windows-only conditional compilation (remove `#if WINDOWS` if blocking)
- [ ] Test `dotnet publish -r win-x64` → produces self-contained Windows executable
- [ ] Test `dotnet publish -r linux-x64` → produces self-contained Linux executable
- [ ] Test `dotnet publish -r osx-arm64` → produces self-contained macOS app
- [ ] Verify Raylib-cs native library is bundled automatically via NuGet (it is — Raylib-cs includes native binaries for all platforms)
- [ ] Verify file paths for assets use `Path.Combine()` and not Windows backslash literals
- [ ] Update `README.md` with updated build and run instructions

### Validation Checkpoint

- [ ] All three platform builds succeed
- [ ] Game window opens on each platform
- [ ] Asset loading works on each platform (case-sensitive paths on Linux)

### Estimated Complexity: **Low**

---

## Migration Summary Table

| Phase | Description                                   | Complexity | Effort Estimate |
|-------|-----------------------------------------------|------------|-----------------|
| 1     | Project file modernization (.NET 8 SDK-style) | Low        | 1–2 hours       |
| 2     | Add Raylib-cs, remove XNA references          | Low        | 1 hour          |
| 3     | Replace game loop and window initialization   | Medium     | 2–4 hours       |
| 4     | Replace content pipeline and asset loading    | Medium     | 2–3 hours       |
| 5     | Replace rendering layer (SpriteBatch → Raylib)| High       | 8–16 hours      |
| 6     | Replace input handling                        | Low–Medium | 2–4 hours       |
| 7     | Replace WinForms debug window                 | Low        | 1 hour          |
| 8     | Handle .NET 8 breaking changes                | Medium     | 2–4 hours       |
| 9     | Sprite loading from `Tibia.spr`               | Medium     | 2–4 hours       |
| 10    | Cross-platform build and runtime validation   | Low        | 2–3 hours       |
| **Total** |                                           |            | **~23–41 hours**|

---

## File-by-File Migration Reference

| File                              | Migration Required | Notes |
|-----------------------------------|--------------------|-------|
| `CTC/CTC.csproj`                  | ✅ Full rewrite     | SDK-style, .NET 8, remove XNA/COM refs |
| `CTC/Program.cs`                  | ✅ Minor            | Remove debug window, update `Game` call |
| `CTC/Game.cs`                     | ✅ Full rewrite     | Remove XNA `Game` base class, use Raylib |
| `CTC/DebugWindow.cs`              | ✅ Remove/replace   | Replace with console logging |
| `CTC/Common.cs`                   | 🔶 Minor            | Remove XNA/Drawing type aliases |
| `CTC/Game/GameRenderer.cs`        | ✅ Major            | SpriteBatch → Raylib.DrawTexturePro |
| `CTC/Game/GameCanvas.cs`          | ✅ Major            | RenderTarget2D → Raylib RenderTexture2D |
| `CTC/Game/GameImage.cs`           | ✅ Major            | Texture2D creation from raw bytes |
| `CTC/Game/GameSprite.cs`          | 🔶 Minor            | Update Texture2D type references |
| `CTC/Game/ClientState.cs`         | 🔶 Minor            | Update type references |
| `CTC/Game/ItemType.cs`            | 🔶 Minor            | Update type references |
| `CTC/Game/GameEffect.cs`          | 🔶 Minor            | Update type references |
| `CTC/Game/MagicEffect.cs`         | 🔶 Minor            | Update rendering calls |
| `CTC/Game/DistanceEffect.cs`      | 🔶 Minor            | Update rendering calls |
| `CTC/Game/AnimatedText.cs`        | 🔶 Minor            | Update font/draw calls |
| `CTC/Game/Log.cs`                 | ✅ Minor            | Remove WinForms event binding |
| `CTC/UI/Framework/UIContext.cs`   | ✅ Full rewrite     | Remove SpriteBatch/GraphicsDevice |
| `CTC/UI/Framework/UIView.cs`      | ✅ Major            | Remove SpriteBatch param from Draw() |
| `CTC/UI/Framework/UISkin.cs`      | ✅ Major            | Replace SpriteBatch 9-grid drawing |
| `CTC/UI/Framework/UIButton.cs`    | ✅ Major            | Remove SpriteBatch from Draw() |
| `CTC/UI/Framework/UILabel.cs`     | ✅ Major            | DrawString → Raylib.DrawTextEx |
| `CTC/UI/Framework/UIFrame.cs`     | ✅ Major            | Remove SpriteBatch from Draw() |
| `CTC/UI/Framework/UITabFrame.cs`  | ✅ Major            | Remove SpriteBatch from Draw() |
| `CTC/UI/Framework/UIVirtualFrame.cs` | ✅ Major         | Scissor mode → Raylib.BeginScissorMode |
| `CTC/UI/Framework/UIScrollbar.cs` | ✅ Major            | Remove SpriteBatch from Draw() |
| `CTC/UI/Framework/UIStackView.cs` | ✅ Major            | Remove SpriteBatch from Draw() |
| `CTC/UI/Framework/UIToggleButton.cs` | ✅ Major         | Remove SpriteBatch from Draw() |
| `CTC/UI/Framework/ColorGradient.cs` | 🔶 Minor          | XNA Color → Raylib_cs.Color |
| `CTC/UI/Framework/Margin.cs`      | 🔶 Minor            | XNA Rectangle → Raylib_cs.Rectangle |
| `CTC/UI/Framework/UIException.cs` | ✅ No change        | Pure C# |
| `CTC/UI/Game/GameDesktop.cs`      | ✅ Major            | SpriteBatch → Raylib, window init |
| `CTC/UI/Game/GameFrame.cs`        | ✅ Major            | Remove SpriteBatch from Draw() |
| `CTC/UI/Game/GameCanvas.cs`       | ✅ Major            | RenderTexture2D integration |
| `CTC/UI/Game/GameSidebar.cs`      | ✅ Major            | Remove SpriteBatch from Draw() |
| `CTC/UI/Game/ChatPanel.cs`        | ✅ Major            | DrawString → Raylib.DrawTextEx |
| `CTC/UI/Game/InventoryPanel.cs`   | ✅ Major            | Remove SpriteBatch from Draw() |
| `CTC/UI/Game/SkillPanel.cs`       | ✅ Major            | Remove SpriteBatch from Draw() |
| `CTC/UI/Game/ContainerPanel.cs`   | ✅ Major            | Remove SpriteBatch from Draw() |
| `CTC/UI/Game/ItemButton.cs`       | ✅ Major            | Sprite draw → Raylib |
| `CTC/UI/Game/VIPPanel.cs`         | ✅ Major            | Remove SpriteBatch from Draw() |
| `CTC/Protocol/TibiaGameData.cs`   | ✅ Medium           | Texture2D creation → Raylib |
| `CTC/Protocol/TibiaGameProtocol.cs` | ✅ No change      | Pure C# protocol logic |
| `CTC/Protocol/TibiaMovieStream.cs`  | ✅ No change      | Pure C# binary I/O |
| `CTC/Protocol/NetworkMessage.cs`    | ✅ No change      | Pure C# |
| `CTC/Protocol/PacketStream.cs`      | ✅ No change      | Pure C# |
| `CTC/Protocol/TibiaConstants.cs`    | ✅ No change      | Pure C# |
| `CTC/Protocol/TibiaGamePacketParserFactory.cs` | ✅ No change | Pure C# |
| `CTC/Client/ClientViewport.cs`    | ✅ No change        | Pure C# game state |
| `CTC/Client/ClientMap.cs`         | ✅ No change        | Pure C# |
| `CTC/Client/ClientPlayer.cs`      | ✅ No change        | Pure C# |
| `CTC/Client/ClientCreature.cs`    | ✅ No change        | Pure C# |
| `CTC/Client/ClientTile.cs`        | ✅ No change        | Pure C# |
| `CTC/Client/ClientThing.cs`       | ✅ No change        | Pure C# |
| `CTC/Client/ClientItem.cs`        | ✅ No change        | Pure C# |
| `CTC/Client/ClientContainer.cs`   | ✅ No change        | Pure C# |
| `CTC/Client/ClientChannel.cs`     | ✅ No change        | Pure C# |
| `CTC/Client/ClientOutfit.cs`      | ✅ No change        | Pure C# |
| `CTC/Client/MapPosition.cs`       | ✅ No change        | Pure C# |

**Legend:** ✅ Requires action  🔶 Minor updates only

---

## Recommended Tool Versions

| Tool                  | Version  |
|-----------------------|----------|
| .NET SDK              | 8.0 (LTS)|
| Raylib-cs (NuGet)     | 6.x      |
| Visual Studio         | 2022+    |
| VS Code + C# Dev Kit  | Latest   |
| Rider                 | 2024.x+  |

---

## Notes and Caveats

- **No XNA successor needed.** Raylib-cs provides a complete window management + 2D drawing + input API that covers all XNA use cases in this project.
- **MonoGame is NOT recommended** as an alternative to Raylib for this codebase: while it is the spiritual successor to XNA, it still targets a `Game`-class inheritance model and has content pipeline overhead. Raylib-cs is simpler for a 2D sprite renderer.
- **Native Raylib binaries** are bundled in the Raylib-cs NuGet package for Windows, Linux, and macOS. No manual native library installation is required.
- **Tibia protocol logic** (all files in `CTC/Protocol/` and `CTC/Client/`) requires **no changes** — it is pure C# with no framework dependencies.
- **RSA encryption** is noted as not implemented in the original codebase; this can be added using `System.Security.Cryptography.RSA` available in .NET 8.
- **Test projects:** The repository has no test projects. It is recommended to add a `SharpTibiaClient.Tests` project (using xUnit or NUnit) covering protocol parsing (`TibiaGameData`, `TibiaMovieStream`) as part of this migration, since these are the most testable components.
