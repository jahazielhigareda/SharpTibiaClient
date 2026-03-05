# SharpTibiaClient — Roadmap: Visual & Functional Parity with otclientv8

## Overview

This roadmap guides the SharpTibiaClient project from its current state (completed .NET 8 / Raylib-cs port) toward a client that is **visually and functionally equivalent** to [`jahazielhigareda/otclientv8`](https://github.com/jahazielhigareda/otclientv8).

The otclientv8 reference is used **as a feature specification only** — the C# implementation is a clean-room port; no C++ or Lua code is carried forward.

> **Stack:** C# · .NET 8 · Raylib-cs · Tibia protocol 8.6 · TibiaSharpServer (OpenTibia).
>
> **Key constraint:** The previous XNA → Raylib migration (Phases 1–14) is complete. All remaining work builds on the live C# + Raylib-cs codebase.

---

## Repositories at a Glance

| Repository | Role | Status |
|------------|------|--------|
| `jahazielhigareda/SharpTibiaClient` | C# client (this repo) | Active — functional base ready |
| `jahazielhigareda/otclientv8` | Visual reference (C++/Lua, read-only) | Reference only |
| `jahazielhigareda/TibiaSharpServer` | OpenTibia 8.6 server | Active — server-side partner |

---

## Completed Work (Phases 1–14)

The following foundational migration from XNA 4.0 to .NET 8 + Raylib-cs has been done:

| Phase | What Was Done |
|-------|--------------|
| 1 | SDK-style `.csproj`, target `net8.0`, removed legacy GUIDs and COM references |
| 2 | Added `Raylib-cs` NuGet package, removed all XNA assembly references |
| 3 | Replaced `Microsoft.Xna.Framework.Game` with Raylib-based game loop (`InitWindow`, `BeginDrawing`) |
| 4 | Removed XNA Content Pipeline; assets loaded at runtime via `Raylib.LoadFont/LoadTexture` |
| 5 | Replaced `SpriteBatch` rendering with `Raylib.DrawTexturePro` / `DrawTextEx` |
| 6 | Replaced XNA mouse/keyboard input with Raylib input API |
| 7 | Replaced WinForms `DebugWindow` with console logging (`Log.cs`) |
| 8 | Built Tibia 8.6 protocol: RSA, XTEA, Adler32, `LoginConnection`, `GameConnection`, packet dispatcher |
| 9 | Integrated `Tibia.spr` sprite loader with Raylib `LoadTextureFromImage` |
| 10 | Implemented: `LoginPanel`, `MinimapPanel`, `BattlePanel`, `ShopPanel`, `HotbarPanel`, `ChatPanel`, `InventoryPanel`, `SkillPanel`, `ContainerPanel`, `VIPPanel` |
| 11 | TibiaSharpServer library projects migrated from `netstandard2.0` to `net8.0` |
| 12 | TibiaSharpServer: began NLua-to-C# scripting replacement plan |
| 13 | Fixed .NET 8 breaking changes (`Color`, `Rectangle`, `Vector2` ambiguities, nullable analysis) |
| 14 | Cross-platform build verified; game UI panels hidden during login screen |

---

## otclientv8 Module Reference Map

The table below maps every relevant otclientv8 Lua module to its C# equivalent phase in this roadmap:

| otclientv8 Module | C# Panel / Class | Phase |
|-------------------|-----------------|-------|
| `modules/client_background` | `LoginBackground` (fullscreen bg) | 15 |
| `modules/client_entergame` | `LoginPanel` (rewritten) | 15 |
| `modules/client_entergame` (characterlist) | `CharacterListWindow` | 16 |
| `modules/client_topmenu` | `TopMenuBar` | 17 |
| `modules/client_options` | `OptionsDialog` | 23 |
| `modules/game_interface` | `GameDesktop` layout (rewrite) | 18 |
| `modules/game_inventory` | `InventoryPanel` (rewrite) | 19 |
| `modules/game_minimap` | `MinimapPanel` (rewrite) | 20 |
| `modules/game_console` / `game_channels` | `ChatPanel` (rewrite) | 21 |
| `modules/game_battle` | `BattlePanel` (rewrite) | 19 |
| `modules/game_skills` | `SkillPanel` (rewrite) | 19 |
| `modules/game_containers` | `ContainerPanel` (rewrite) | 19 |
| `modules/game_viplist` | `VIPPanel` (rewrite) | 19 |
| `modules/game_npctrade` | `NpcTradeWindow` | 22 |
| `modules/game_playertrade` | `PlayerTradeWindow` | 22 |
| `modules/game_outfit` | `OutfitDialog` | 22 |
| `modules/game_playerdeath` | `DeathDialog` | 22 |
| `modules/game_questlog` | `QuestLogWindow` | 22 |
| `modules/game_textmessage` | `TextMessageOverlay` | 22 |
| `modules/game_spelllist` / `game_cooldown` | `SpellCooldownBar` | 22 |
| `modules/game_hotkeys` | `HotkeyManager` | 23 |
| `modules/game_modaldialog` | `ModalDialog` | 23 |
| `modules/game_walking` | Walking prediction / client-side move | 24 |
| `modules/client_styles` / `client_background` | `UISkin` (Tibia textures) | 25 |

---

## New Phases (15–25)

---

### Phase 15 — Login Protocol Fix and Connection Debug

**Goal:** Make the login actually connect to `TibiaSharpServer` reliably and show the real character list.

**Complexity: High | Estimated effort: 8–16 hours**

The current `LoginConnection.cs` builds and sends the 8.6 login packet, but several issues prevent it from working against a live TibiaSharpServer:

#### 15a — RSA Key Alignment

The client must encrypt using the **same RSA modulus as the server's public key**. TibiaSharpServer uses the well-known OpenTibia RSA modulus. If the modulus hardcoded in the client's `Rsa.cs` does not match the server, the server will silently drop the connection.

- [ ] Read `mtanksl.OpenTibia.Security/Rsa.cs` from the TibiaSharpServer repo and copy the exact modulus + exponent into the client's `Rsa.cs`
- [ ] Verify the RSA block format: `block[0] = 0x00` (leading zero, required by PKCS#1 v1.5 for the BigInteger approach)
- [ ] Confirm the client uses raw BigInteger encryption matching the server's expected scheme, **not** `RSA.Create().Encrypt()` with PKCS#1 padding — the OT protocol uses a custom raw modular exponentiation, not standard padding

#### 15b — Login Packet Format Verification

Compare the client's `BuildLoginPacket` byte layout with what TibiaSharpServer's `LoginPacketReader` expects:

- [ ] Verify byte order of 2-byte packet length prefix (little-endian)
- [ ] Verify Adler32 checksum covers the correct bytes (payload only, not the length prefix)
- [ ] Verify account name is sent as: `uint16 length + ASCII bytes` (not null-terminated)
- [ ] Verify password is sent as: `uint16 length + ASCII bytes`
- [ ] Verify XTEA key block (4 × `uint32` LE) is placed at bytes 1–16 of the RSA block (byte 0 = `0x00`)
- [ ] Add detailed logging of sent/received bytes to diagnose mismatch

#### 15c — Response Parsing Fix

- [ ] Handle the case where the server sends only a character list (no MOTD packet 0x14) — do not throw when packet type is directly `0x64`
- [ ] Handle error packet type `0x0A` gracefully — display the error string in the UI status label
- [ ] Handle the case where the server sends a MOTD followed by the character list in the **same TCP segment** (no extra read needed)
- [ ] Fix `ParseCharacterList`: in protocol 8.6 the account name in the response packet uses `uint16` length prefix; verify parsing does not over-read or under-read

#### 15d — Game Server Connection Fix

- [ ] Implement `LivePacketStream.ConnectAsync` sending the game login packet (type `0x0A`) including: XTEA key, account name, character name, OS, client version
- [ ] Enable XTEA decryption on all subsequent packets from the game server after the login acknowledgment
- [ ] Verify the game server's expected packet format by cross-referencing `mtanksl.OpenTibia.Network` packet readers

#### 15e — Error UX in LoginPanel

- [ ] Display all login errors (wrong password, server offline, etc.) in the status label with a readable color-coded message
- [ ] Show a spinner / "Connecting…" state that blocks repeated button presses

**Validation Checkpoint:**
- [ ] Connect button sends valid 8.6 login packet; server does not drop connection
- [ ] Character list is received and displayed with correct character name and world name
- [ ] Entering game loads the player into the world (map description packet is received)
- [ ] Walking, looking, and saying text work end-to-end

---

### Phase 16 — Login Screen Visual Overhaul (client_entergame)

**Goal:** Replace the plain textbox form with a Tibia-style login dialog matching `otclientv8/modules/client_entergame`.

**Complexity: Medium | Estimated effort: 8–12 hours**

The otclientv8 login screen has:
- A fullscreen animated/static **background** (Tibia art)
- A floating titled dialog window: **"Enter Game"**
- Fields: Account name, Password, (optional) Token
- **Server selector**: dropdown with preset servers + "Custom" option
- **Custom host/port** inputs (shown when "Custom" is selected)
- **Remember password** checkbox
- **Login** button (full-width, centered)
- Status label showing server online player count in green

C# implementation plan:

- [ ] Create `LoginBackground.cs` — draws a fullscreen image (`Content/background.png`) or a solid gradient as the Tibia map background; displayed behind the login dialog
- [ ] Redesign `LoginPanel.cs` to use the Tibia-style dialog layout:
  - Titled frame: "Enter Game" (styled as a `UIFrame` with `UIElementType.Window`)
  - `UILabel` + `UITextbox` for Account name
  - `UILabel` + `UITextbox` (password mode) for Password
  - `UILabel` + `UITextbox` for Token (optional, hidden by default)
  - Server selector: `UIComboBox` or a row of preset `UIButton`s + custom host/port textboxes; toggle visibility based on selection
  - `UICheckbox` for "Remember password" (persist to `settings.json`)
  - Full-width `UIButton` for "Login"
  - `UILabel` for server info (player count, online status) in green/red
- [ ] Implement `UIComboBox.cs` — dropdown list widget (dropdown opens below, items are `UILabel` rows, click closes and selects)
- [ ] Implement `UICheckbox.cs` — clickable toggle with check mark
- [ ] Persist account name / password to `%APPDATA%/SharpTibiaClient/settings.json` (encrypted with DPAPI or a simple XOR for non-Windows portability) when "Remember password" is checked; load on startup
- [ ] Add server presets list (name + host + port) stored in `servers.json` in the data directory

**Validation Checkpoint:**
- [ ] Login dialog looks visually comparable to the otclientv8 `enterGame` window
- [ ] Server dropdown switches between preset and custom host/port inputs
- [ ] Remember password persists credentials between sessions
- [ ] Login button triggers the real 8.6 login flow from Phase 15

---

### Phase 17 — Character Selection Window (client_entergame characterlist)

**Goal:** Implement a separate `CharacterListWindow` dialog matching `otclientv8/modules/client_entergame/characterlist.otui`.

**Complexity: Medium | Estimated effort: 4–8 hours**

In otclientv8, after login the character list is a **separate floating window** (not embedded in the login form):
- Title: "Character List"
- Scrollable `TextList` showing each character row: **name** (left) | **world** (right)
- Focused row is highlighted in white, others gray
- **Account Status** line at the bottom (e.g., "Free Account" or "Premium Account")
- **Auto reconnect** toggle button (left-aligned, bottom)
- **Ok** and **Cancel** buttons (right-aligned, bottom)
- Keyboard: Up/Down arrows move selection; Enter logs in; Escape cancels

- [ ] Create `CharacterListWindow.cs` as a `UIFrame` (titled floating window)
- [ ] Implement a scrollable character list (`UIScrollbar` + list of `CharacterRowButton` items)
- [ ] Show character name on the left, world name on the right of each row
- [ ] Highlight focused row (selected row brightened); support keyboard Up/Down navigation
- [ ] Show "Account Status: Free Account" or "Account Status: Premium Account" based on premium days from login response
- [ ] Add "Auto Reconnect" toggle button — when enabled, automatically re-login and re-select the last character on disconnect
- [ ] "Ok" button triggers game server connection; "Cancel" returns to login dialog
- [ ] Close the login `LoginPanel` when `CharacterListWindow` is shown; restore it on cancel

**Validation Checkpoint:**
- [ ] Character list shows all returned characters with correct name and world
- [ ] Selecting a character and pressing Ok starts the game connection
- [ ] Cancel returns to the login dialog cleanly
- [ ] Premium status is shown correctly

---

### Phase 18 — Top Menu Bar (client_topmenu)

**Goal:** Add a top-of-screen menu bar matching `otclientv8/modules/client_topmenu`.

**Complexity: Low–Medium | Estimated effort: 4–6 hours**

The otclientv8 top menu contains small icon buttons:
- **Help** — opens help page / keybind list
- **Bug Report** — opens bug report dialog
- **Options** — opens options dialog (Phase 23)
- **Log Out** — disconnects from game and returns to login screen
- **Exit** — closes the client

- [ ] Create `TopMenuBar.cs` as a `UIView` docked to the top of the window, height ~22 px
- [ ] Add icon buttons: Help, Options, Log Out, Exit (using text labels if icons are not yet available)
- [ ] Wire "Log Out" to disconnect the active `ClientState`, dispose `LivePacketStream`, and show `LoginPanel` again
- [ ] Wire "Exit" to `Raylib.WindowShouldClose` flag or `Environment.Exit(0)`
- [ ] Show server name and character name in the top bar after login
- [ ] Integrate with `GameDesktop`: top bar is always visible while in-game; hidden on login screen

**Validation Checkpoint:**
- [ ] Top menu renders at the top of the game window
- [ ] Log Out disconnects and returns to login screen cleanly
- [ ] Exit closes the application

---

### Phase 19 — Game HUD Layout Overhaul (game_interface)

**Goal:** Restructure the main game HUD to match the otclientv8 `game_interface` layout.

**Complexity: High | Estimated effort: 16–24 hours**

The otclientv8 HUD has:
- **Center**: full-height `GameMapPanel` (the rendered Tibia world)
- **Right panel** (`GameSidePanel`): stacked miniwindows (Inventory, Skills, Battle list, VIP, containers)
- **Bottom** `ChatPanel` (tabbed channels), with a resizable `Splitter` between map and chat
- **Top bar** (game_topbar): attack/follow mode buttons and experience bar
- No left panel in desktop mode

#### 19a — GameDesktop Layout

- [ ] Rewrite `GameDesktop.LayoutSubviews()` to place `GameFrame` (map) filling center
- [ ] Place `GameSidebar` on the **right** only (remove any left sidebar), width 198 px (matching otclientv8)
- [ ] Place `ChatPanel` at the **bottom**, resizable via a horizontal `Splitter`
- [ ] Implement `Splitter.cs` — a draggable divider that resizes its adjacent panels
- [ ] Place `TopMenuBar` at the top of the screen (always above everything)
- [ ] Remove the hard-coded pixel offsets in favor of anchor-based layout

#### 19b — GameSidebar (MiniWindow Container)

- [ ] Redesign `GameSidebar.cs` as a scrollable vertical stack of collapsible "miniwindow" panels (matching otclientv8's `UIMiniWindowContainer`)
- [ ] Each miniwindow has a title bar (click to collapse/expand), close button, and drag handle
- [ ] Fixed panel order: Inventory → Skills → Battle → VIP (same default order as otclientv8)
- [ ] Containers open as additional miniwindows below the fixed panels

#### 19c — InventoryPanel Overhaul

Match `otclientv8/modules/game_inventory`:

- [ ] Render standard 10 Tibia equipment slots in a fixed 2-column grid:
  - Row 1: Head, Necklace
  - Row 2: Backpack, Armor
  - Row 3: Right hand, Left hand
  - Row 4: Legs, Feet
  - Row 5: Ring, Ammo
- [ ] Each slot is a 34×34 px `ItemButton` with the item sprite drawn inside
- [ ] Empty slots show the slot icon (head shape, ring shape, etc.)
- [ ] Right-click context menu: Use, Move, Look, Trade, Drop
- [ ] Drag-and-drop between slots and open container windows

#### 19d — SkillPanel Overhaul

Match `otclientv8/modules/game_skills`:

- [ ] Show: Health (red bar), Mana (blue bar), Experience (yellow bar), Level, Experience to next level
- [ ] Show skill rows: Magic Level, Shielding, Distance, Sword, Axe, Club, Fist Fighting — each with skill value and a colored progress bar
- [ ] Show speed, capacity, food, stamina (stamina in green/yellow/red by time remaining)
- [ ] Show soul points

#### 19e — BattlePanel Overhaul

Match `otclientv8/modules/game_battle`:

- [ ] Show visible creatures in a scrollable list
- [ ] Each row: creature name + health bar + skull/party icons
- [ ] Click to attack; right-click for creature info
- [ ] Filter buttons: Players, NPCs, Monsters, Party

#### 19f — ContainerPanel Overhaul

Match `otclientv8/modules/game_containers`:

- [ ] Container window: title = container name, close button
- [ ] Grid of item slots (4 columns, variable rows) with `ItemButton` items
- [ ] Show item count badge when count > 1
- [ ] Drag-and-drop items between containers and inventory

**Validation Checkpoint:**
- [ ] HUD layout visually resembles the otclientv8 game interface
- [ ] Inventory shows equipped items with correct sprites
- [ ] Skills panel shows real values from the server
- [ ] Battle list shows nearby creatures
- [ ] Containers show correct items

---

### Phase 20 — Minimap Panel Overhaul (game_minimap)

**Goal:** Implement a functional minimap matching `otclientv8/modules/game_minimap`.

**Complexity: High | Estimated effort: 8–12 hours**

The otclientv8 minimap:
- Renders a 2D overhead view of the current floor's tiles
- Colors each tile based on its ground item (dark green = grass, grey = stone, blue = water, etc.)
- Shows the player as a white/yellow dot at center; other players as colored dots; monsters as red dots
- Has floor up/down buttons and current floor label
- Has zoom in/out buttons
- Shows a crosshair cursor; click to set a waypoint marker

- [ ] Render minimap into a `RenderTexture2D` each frame (or when dirty)
- [ ] Color tiles: read ground `ItemType` from `TibiaGameData` and map type IDs to map colors using a lookup table
- [ ] Draw player dot (white) at the center of the minimap; draw other visible creatures as colored dots
- [ ] Implement floor navigation buttons (F1–F8 shortcuts + UI buttons)
- [ ] Implement zoom levels (1:1, 2:1, 4:1 — scroll wheel)
- [ ] Click on minimap → set a cross marker (route waypoint — cosmetic only in this phase)

**Validation Checkpoint:**
- [ ] Minimap shows the correct floor layout after walking around
- [ ] Player dot moves with the player
- [ ] Floor up/down switches the visible floor

---

### Phase 21 — Chat System Overhaul (game_console, game_channels)

**Goal:** Implement a tab-based chat system matching `otclientv8/modules/game_console` and `game_channels`.

**Complexity: High | Estimated effort: 12–16 hours**

The otclientv8 chat system:
- Tab bar at the top of the chat panel: **Local**, **Trade**, **Help**, **Battle Log**, etc.
- Each tab has a scrollable message history with colored lines (say = white, yell = red, whisper = orange, npc = yellow, server = blue, etc.)
- Input bar at the bottom with "Say as:" dropdown (say/whisper/yell/battle chat/npc)
- Right-click on a message opens a context menu (copy text, add to VIP, etc.)
- Channels can be opened/closed; channels list button opens the channel list dialog

#### 21a — Tab Bar and Per-Channel History

- [ ] Redesign `ChatPanel.cs` with a `UITabFrame`-style tab bar
- [ ] Built-in channels: Local (Say), Trade, Help, Battle Log (system messages)
- [ ] Each channel keeps a scrollable list of `ChatMessage` structs (color, sender, text, timestamp)
- [ ] Auto-scroll to bottom on new message; scroll wheel to scroll up

#### 21b — Message Formatting and Colors

| Message Type | Color |
|---|---|
| Say | White |
| Whisper | Light gray |
| Yell | Red |
| NPC | Yellow |
| Private message | Light green |
| Server notification | Blue |
| Monster say | Orange |
| Guild | Light purple |
| Party | Dark green |

- [ ] Map incoming `0xB4` (text message) packet subtypes to the correct channel and color

#### 21c — Say Input Bar

- [ ] `UITextbox` at the bottom of chat panel; press Enter to send
- [ ] Dropdown for message mode: Say (`0x01`), Whisper (`0x02`), Yell (`0x03`), Private (`0x04`), Channel (`0x05`)
- [ ] Send outgoing say packet `0x96` with mode and text

#### 21d — Channel List Dialog

- [ ] "Open Channel" button shows a list of available channels (received from packet `0x96–0x97`)
- [ ] Double-click a channel to join; clicking X on the tab closes the channel

**Validation Checkpoint:**
- [ ] Say, whisper, yell messages sent to server and shown in chat
- [ ] Incoming NPC speech and server messages shown in correct color/tab
- [ ] Chat scroll works correctly
- [ ] Tab switching works; each tab preserves its message history

---

### Phase 22 — Extended Game Dialogs (game_outfit, game_playerdeath, game_questlog, game_npctrade, game_playertrade, game_textmessage, game_spelllist)

**Goal:** Implement the remaining in-game dialog windows referenced in otclientv8.

**Complexity: High | Estimated effort: 20–32 hours**

#### 22a — Outfit Changer Dialog (game_outfit)

- [ ] Triggered by server packet `0xD1` (open outfit window)
- [ ] Shows current outfit with look type, head/body/legs/feet color pickers (0–132 palette)
- [ ] Addons checkboxes (Addon 1, Addon 2)
- [ ] Forward/Back buttons to cycle through available outfits
- [ ] OK button sends `0xD3` (set outfit) packet

#### 22b — Player Death Dialog (game_playerdeath)

- [ ] Triggered by server packet `0x28` (player death)
- [ ] Shows: "You are dead" message, penalty (unfair fight reduction), OK/Reconnect button
- [ ] OK returns to character selection window

#### 22c — Quest Log Window (game_questlog)

- [ ] Triggered by server packets `0xCA` (quest list) and `0xCB` (quest detail)
- [ ] Left panel: scrollable list of quests (name, completed/in-progress)
- [ ] Right panel: quest description and mission text
- [ ] Keyboard shortcut: Q (or via top menu)

#### 22d — NPC Trade Window (game_npctrade)

- [ ] Triggered by server packet `0x5F` (open NPC trade)
- [ ] Two tabs: Buy / Sell
- [ ] Buy: list of items for sale with name, weight, price; quantity spinner; Buy button
- [ ] Sell: list of player's eligible items; Sell button
- [ ] Filter/search field

#### 22e — Player Trade Window (game_playertrade)

- [ ] Triggered by server packet `0x7D` (trade initiate)
- [ ] Two panels: "Your offer" (left) and "Their offer" (right) — each shows up to 5 items
- [ ] Accept / Close buttons
- [ ] Sends `0x7D` (counter offer) and `0x7E` (accept trade) packets

#### 22f — Text Message Overlay (game_textmessage)

- [ ] Triggered by server packet `0xB4` (text message)
- [ ] Subtypes that show as overlays (center screen, timed fade-out):
  - Red: damage taken
  - Blue: experience gained
  - White: loot found
  - Green: healed
- [ ] Regular status messages route to the chat tab (Battle Log or Local)

#### 22g — Spell List and Cooldown Indicators (game_spelllist, game_cooldown)

- [ ] Triggered by server packets `0xA3` (cooldown group) and `0xA4` (spell cooldown)
- [ ] Show spell icons in the hotbar with a circular cooldown sweep overlay
- [ ] Spell list window: all known spells with name, mana cost, level requirement, words

**Validation Checkpoint:**
- [ ] Outfit dialog opens when triggered by server and correctly previews/saves outfit
- [ ] Death dialog appears and returns to character selection correctly
- [ ] Quest log loads and displays entries
- [ ] NPC trade and player trade windows open/close correctly
- [ ] Damage/heal text messages flash on screen
- [ ] Spell cooldowns display on the hotbar

---

### Phase 23 — Options, Hotkeys and Modal Dialogs (client_options, game_hotkeys, game_modaldialog)

**Goal:** Implement settings persistence, key binding, and the generic modal dialog system.

**Complexity: Medium | Estimated effort: 8–12 hours**

#### 23a — Options Dialog (client_options)

- [ ] Create `OptionsDialog.cs` — tabbed dialog with sections:
  - **Graphics**: vsync, show FPS, full-screen toggle, resolution
  - **Sound**: master volume (placeholder — no audio in Phase 23)
  - **Interface**: stretch map, show names, show HP bars
- [ ] Persist all options to `%APPDATA%/SharpTibiaClient/settings.json` on save
- [ ] Apply options in real-time (e.g., toggle FPS overlay, change render resolution)

#### 23b — Hotkey Manager (game_hotkeys)

- [ ] Create `HotkeyManager.cs` — maps `KeyboardKey` + modifier to an action
- [ ] Default bindings: F1–F12 execute hotbar slots; Arrow keys / WASD walk; Ctrl+T opens trade; Ctrl+K opens channel list; Ctrl+O opens options
- [ ] Rebindable via the Options dialog (Hotkeys tab)
- [ ] Persist bindings to `settings.json`

#### 23c — Modal Dialog (game_modaldialog)

- [ ] Create `ModalDialog.cs` — a generic centered dialog with a title, text body, and configurable buttons (OK / Cancel / Yes / No)
- [ ] Triggered by server packet `0xFA` (modal dialog)
- [ ] Sends `0xFA` response packet with the chosen button index
- [ ] Used for in-game quests (NPC interactions that need player choice)

**Validation Checkpoint:**
- [ ] Options dialog opens, saves, and applies settings correctly
- [ ] Default hotkeys work in-game
- [ ] Modal dialog appears and sends correct response packet

---

### Phase 24 — Client-Side Walking Prediction (game_walking)

**Goal:** Implement client-side walk prediction to eliminate input lag, matching `otclientv8/modules/game_walking`.

**Complexity: High | Estimated effort: 12–20 hours**

Without walk prediction the player appears to teleport one tile at a time as confirmed by the server. With prediction:
- The client moves the player sprite immediately on key press
- The server confirms or cancels the move
- If the server cancels (e.g., blocked tile), the client snaps back

- [ ] Track a client-side `PredictedPosition` separate from `ConfirmedPosition`
- [ ] On walk key press: advance `PredictedPosition` immediately; send walk packet to server
- [ ] Smooth tile-to-tile movement animation (lerp over the server beat interval, default 500 ms)
- [ ] On server move confirmation: advance `ConfirmedPosition`; if it diverges from `PredictedPosition`, snap and re-predict
- [ ] Handle diagonal walk (two packets: walk + turn) where supported
- [ ] Implement walk queue (buffer up to 10 pending steps)

**Validation Checkpoint:**
- [ ] Player movement feels smooth (no visible tile-jump lag at normal latency)
- [ ] Walking into a wall does not move the player
- [ ] Walk queue allows holding a key to walk continuously

---

### Phase 25 — Art Assets, Skin Textures, and Final Polish (client_styles)

**Goal:** Add proper Tibia-style UI artwork and apply it through the `UISkin` renderer to achieve full visual parity with otclientv8.

**Complexity: Medium | Estimated effort: 8–16 hours**

The otclientv8 uses texture atlas images for all UI chrome:
- `panel_map` — border frame around the game map
- `panel_side` — border frame for the right sidebar
- `panel_bottom2` — top bar background
- Button, scrollbar, textbox, window frame textures

- [ ] Source or create equivalent freely-licensed UI texture atlas (or draw simplified Tibia-style borders in code using `Raylib.DrawRectangleLinesEx`)
- [ ] Update `UISkin.cs` to load and render the atlas textures for: window frames, buttons, textboxes, scrollbars, tabs, separators
- [ ] Add the Tibia-style brown wood-panel border for the sidebar
- [ ] Add the Tibia-style dark gray border for the map panel
- [ ] Add proper button up/down/hover states from the texture atlas
- [ ] Replace the default monospace fallback font with a Verdana-equivalent (freely licensed) 11 px font
- [ ] Add the client logo / login background image (Tibia promotional art or a custom replacement)
- [ ] Add application icon (`Game.ico`) to the Raylib window

**Validation Checkpoint:**
- [ ] Login screen, game HUD, and all dialogs look visually close to otclientv8
- [ ] Buttons have correct hover and pressed states
- [ ] Window frames, scrollbars, and textboxes use the themed textures

---

## Phase Summary Table

| Phase | Scope | Description | Complexity | Est. Effort |
|-------|-------|-------------|------------|-------------|
| 1–14 | Both | XNA → .NET 8 + Raylib-cs migration | Various | ✅ **Done** |
| 15 | Client | Login protocol fix (RSA, packet format, response parsing) | High | 8–16 h |
| 16 | Client | Login screen visual overhaul (styled dialog, server selector, remember password) | Medium | 8–12 h |
| 17 | Client | Separate character list window + auto-reconnect | Medium | 4–8 h |
| 18 | Client | Top menu bar (Help, Options, Log Out, Exit) | Low–Med | 4–6 h |
| 19 | Client | Full HUD layout overhaul (inventory, skills, battle, containers, sidebar) | High | 16–24 h |
| 20 | Client | Minimap: color tiles, creature dots, floor nav, zoom | High | 8–12 h |
| 21 | Client | Chat: tabs, per-channel history, say bar, channel list | High | 12–16 h |
| 22 | Client | Outfit dialog, death screen, quest log, NPC trade, player trade, text messages, spell cooldowns | High | 20–32 h |
| 23 | Client | Options dialog, hotkey manager, modal dialog | Medium | 8–12 h |
| 24 | Client | Client-side walk prediction and smooth movement | High | 12–20 h |
| 25 | Client | UI skin textures, fonts, final visual polish | Medium | 8–16 h |
| **Total** | | | | **~108–174 h** |

---

## File-by-File Work Remaining

| File | Action | Phase |
|------|--------|-------|
| `CTC/Protocol/Rsa.cs` | Fix RSA modulus to match TibiaSharpServer | 15 |
| `CTC/Protocol/LoginConnection.cs` | Fix packet format, response parsing | 15 |
| `CTC/Protocol/LivePacketStream.cs` | Fix game login packet, XTEA decryption | 15 |
| `CTC/UI/Game/LoginPanel.cs` | Full rewrite: styled dialog + server selector | 16 |
| `CTC/UI/Game/LoginBackground.cs` | New: fullscreen background image | 16 |
| `CTC/UI/Framework/UIComboBox.cs` | New: dropdown selector widget | 16 |
| `CTC/UI/Framework/UICheckbox.cs` | New: checkbox widget | 16 |
| `CTC/UI/Game/CharacterListWindow.cs` | New: separate character list dialog | 17 |
| `CTC/UI/Game/TopMenuBar.cs` | New: top menu bar | 18 |
| `CTC/UI/Game/GameDesktop.cs` | Rewrite layout (anchor-based, splitter) | 19 |
| `CTC/UI/Framework/Splitter.cs` | New: draggable resize splitter | 19 |
| `CTC/UI/Game/GameSidebar.cs` | Rewrite as MiniWindow container | 19 |
| `CTC/UI/Game/InventoryPanel.cs` | Rewrite: 10-slot equipment grid | 19 |
| `CTC/UI/Game/SkillPanel.cs` | Rewrite: bars + skill rows | 19 |
| `CTC/UI/Game/BattlePanel.cs` | Rewrite: scrollable creature list | 19 |
| `CTC/UI/Game/ContainerPanel.cs` | Rewrite: grid layout, drag-drop | 19 |
| `CTC/UI/Game/MinimapPanel.cs` | Rewrite: tile colors, creature dots, zoom | 20 |
| `CTC/UI/Game/ChatPanel.cs` | Rewrite: tabs, per-channel history, say bar | 21 |
| `CTC/UI/Game/OutfitDialog.cs` | New: outfit changer | 22 |
| `CTC/UI/Game/DeathDialog.cs` | New: player death screen | 22 |
| `CTC/UI/Game/QuestLogWindow.cs` | New: quest log | 22 |
| `CTC/UI/Game/NpcTradeWindow.cs` | New: NPC trade | 22 |
| `CTC/UI/Game/PlayerTradeWindow.cs` | New: player trade | 22 |
| `CTC/UI/Game/TextMessageOverlay.cs` | New: animated damage/heal text | 22 |
| `CTC/UI/Game/SpellCooldownBar.cs` | New: spell cooldown sweep on hotbar | 22 |
| `CTC/UI/Game/OptionsDialog.cs` | New: options / settings dialog | 23 |
| `CTC/Game/HotkeyManager.cs` | New: key binding system | 23 |
| `CTC/UI/Game/ModalDialog.cs` | New: generic modal from server packet | 23 |
| `CTC/Game/WalkPredictor.cs` | New: client-side walk prediction | 24 |
| `CTC/UI/Framework/UISkin.cs` | Add texture atlas rendering | 25 |
| `CTC/Content/` | Add UI texture atlas + Verdana-style font | 25 |

---

## Notes

- **otclientv8 Lua/C++ code** is a feature specification reference only. None of it is compiled or run. C# is the only implementation language.
- **Protocol accuracy** must be verified against the live `TibiaSharpServer`. Always cross-reference packet readers in `mtanksl.OpenTibia.Network` before implementing new packet handlers on the client.
- **Tibia assets** (`.dat`, `.spr`, skin textures) are not redistributable. For development, use the Tibia 8.6 client files locally. For the public repository, provide only the parsing code; document that players must supply their own `Tibia.dat` and `Tibia.spr`.
- **Free asset fallback**: if real Tibia assets are unavailable, placeholder colored rectangles are acceptable for development milestones. The skinning system must be designed to swap them out trivially.
- **Test coverage**: add an xUnit test project (`CTC.Tests`) to cover: RSA/XTEA/Adler32 round-trips; login packet byte layout; character list response parsing; UI layout math.
- **CI**: configure GitHub Actions to run `dotnet build` and `dotnet test` on every push. Failure blocks merge.
