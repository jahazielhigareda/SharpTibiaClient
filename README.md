# SharpTibiaClient

A Tibia 8.6 protocol-compatible client and server written entirely in C#, targeting .NET 8.
Built with [Raylib-cs](https://github.com/ChrisDill/Raylib-CSharp) for cross-platform 2D rendering.

---

## Requirements

| Tool | Version |
|------|---------|
| .NET SDK | 8.0 (LTS) — [download](https://dotnet.microsoft.com/download/dotnet/8.0) |
| OS | Windows 10/11 x64, Ubuntu 22.04+ x64, or macOS Ventura+ (arm64/x64) |

> **Note:** No additional native library installation is required — Raylib native
> binaries for all three platforms are bundled inside the `Raylib-cs` NuGet package.

---

## Building

### Client (`CTC/`)

```bash
# Debug build
dotnet build CTC/CTC.csproj

# Release build
dotnet build CTC/CTC.csproj -c Release
```

### Server (`TibiaSharpServer/`)

```bash
# Debug build (all projects)
dotnet build TibiaSharpServer/

# Release build
dotnet build TibiaSharpServer/ -c Release
```

---

## Running

### Client

Place your Tibia 8.6 `Tibia.dat`, `Tibia.spr`, and optionally a `Test.tmv` replay
file in the same directory as the compiled executable (i.e. `CTC/bin/Debug/net8.0/`
for development builds), then run:

```bash
dotnet run --project CTC/CTC.csproj
```

### Server

```bash
dotnet run --project TibiaSharpServer/mtanksl.OpenTibia.Host/mtanksl.OpenTibia.Host.csproj
```

Server configuration is read from `config.json` in the executable directory.
A default `config.json` is included and copied automatically to the output directory.

---

## Self-Contained Publishing (cross-platform)

The following commands produce a single-file, self-contained executable that
includes the .NET runtime — no SDK installation needed on the target machine.

### Client

```bash
# Windows x64
dotnet publish CTC/CTC.csproj -r win-x64 --self-contained -c Release

# Linux x64
dotnet publish CTC/CTC.csproj -r linux-x64 --self-contained -c Release

# macOS arm64 (Apple Silicon)
dotnet publish CTC/CTC.csproj -r osx-arm64 --self-contained -c Release
```

After publishing, copy `Tibia.dat`, `Tibia.spr` (and optionally `Test.tmv`) into
the same directory as the published executable.  The `Content/` folder (skin and
font assets) is published automatically alongside the executable.

### Server

```bash
# Windows x64
dotnet publish TibiaSharpServer/mtanksl.OpenTibia.Host/mtanksl.OpenTibia.Host.csproj \
    -r win-x64 --self-contained -c Release

# Linux x64
dotnet publish TibiaSharpServer/mtanksl.OpenTibia.Host/mtanksl.OpenTibia.Host.csproj \
    -r linux-x64 --self-contained -c Release
```

`config.json` is copied into the publish output automatically.

---

## Running Tests

```bash
dotnet test TibiaSharpServer/mtanksl.OpenTibia.Tests/
```

---

## Project Structure

```
SharpTibiaClient/
├── CTC/                        # Tibia client (Raylib-cs, net8.0)
│   ├── Client/                 # Game state model
│   ├── Game/                   # Renderer, effects, ClientState
│   ├── Protocol/               # Packet parsing, XTEA, RSA, Adler32
│   └── UI/                     # Framework (buttons, frames, ...) + Game panels
├── CTCContent/                 # Original XNA content project (reference only)
├── TibiaSharpServer/           # OpenTibia server (net8.0)
│   ├── mtanksl.OpenTibia.Host/ # Entry point + config
│   ├── mtanksl.OpenTibia.Game/ # Game engine
│   ├── mtanksl.OpenTibia.Network/ # TCP server
│   ├── mtanksl.OpenTibia.Plugins/ # C# scripting system
│   └── mtanksl.OpenTibia.Tests/   # xUnit test suite
├── roadmap.md                  # Full migration roadmap (Phases 1-14)
└── README.md
```

---

## Platform Notes

| Platform | Client | Server | Notes |
|----------|--------|--------|-------|
| Windows 10/11 x64 | Primary | Primary | |
| Ubuntu 22.04+ x64 | Secondary | Supported | Case-sensitive file paths -- use exact asset filenames |
| macOS Ventura+ arm64 | Tertiary | Not yet tested | Use `osx-arm64` RID for native Apple Silicon; use `osx-x64` + Rosetta 2 for Intel builds on Apple Silicon |

### Linux asset path note

Linux uses a case-sensitive file system.  Ensure your asset files are named exactly
`Tibia.dat`, `Tibia.spr`, `Content/DefaultSkin.bmp`, and `Content/StandardFont.ttf`
(matching the case used in the source code).

---

## Feature Support

- Tibia 8.6 protocol (login + game packets)
- Movie replay (`.tmv` files)
- Inventory, chat, animations, skill/stat display
- Cross-platform rendering via Raylib-cs
- Pure C# server-side scripting (no NLua/Lua dependency)
- In-memory data layer (swap for SQLite/MySQL/PostgreSQL via provider projects)
