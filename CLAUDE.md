# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

"SkyFighter" — a 2D vertical-scrolling shooter built in **Godot 4.7 with C# (.NET 8)**, GL Compatibility renderer, 800×600 viewport. UI text is Chinese.

## Build & Run

- Build: `dotnet build` from the project root (outputs to `.godot/mono/temp/bin/Debug/SkyFighter.dll`). The Godot editor also rebuilds automatically when you press Play.
- Run: normally launch the game through the Godot editor (snap `/snap/bin/godot-4`). There are **no tests** and **no lint config** beyond `.editorconfig`.
- CLI/headless runs on this machine require `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` (the .NET runtime can't find a usable libicu here), e.g.:
  `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 godot --headless --path . --quit-after 120`
- The root-level `sky-fighter.tscn` is an unused artifact; the main scene is `scenes/main.tscn` (`run/main_scene`). The repo is not under git.

## Architecture

Scenes live in `scenes/`, one C# script per entity in `scripts/`. Assets (SVG) in `assets/`. This is a Godot 4 `partial class` C# project — every script extends a Godot node type and is attached to a scene root.

**Entity pattern:** root `CharacterBody2D` with a `CollisionShape2D` (`BodyShape`) + a child `Area2D` named `Hitbox` (`HitboxShape`) used for contact detection. **Exception: bullets.**

### Group-based container pattern (the core convention)

`Main.cs` registers container nodes into groups in `_Ready`:
`Enemies→enemies`, `Obstacles→obstacles`, `PlayerBullets→player_bullets`, `EnemyBullets→enemy_bullets`, `HealthPacks→health_packs`, `BulletPacks→bullet_packs`, `Effects→effects`.

- **Finding a spawn container:** `tree.GetFirstNodeInGroup("player_bullets")` etc. (used by `Player.cs`, `Enemy.cs`, `ExplosionFx.cs`).
- **Iterating live entities:** `Enemy` and `Obstacle` add **themselves** to their group in their own `_Ready` (`AddToGroup`). This means the `enemies`/`obstacles` groups contain **both the container and the individual entities** — when iterating, always filter by concrete type (`node is Enemy`, `node is Obstacle`) to skip the container. `Player` adds itself to group `player` for the enemy→player ammo-grant lookup.

### Damage & hit detection — read before touching

- **Bullets (player & enemy) use a child `ShapeCast2D` named `Cast`, not an Area2D.** Godot's `Area2D.body_entered` discretely samples and reliably misses fast bullets (the player bullet at 520 px/s passed through enemies entirely). `Bullet.cs` calls `_cast.ForceShapecastUpdate()` every physics frame and is the **single authority that applies damage**: `Enemy.Hit(1)`, `Obstacle.Destroy()`, `Player.Hit(1)`. The cast `CollisionMask` is set in `_Ready`: player bullet = 18, enemy bullet = 17 (also hits asteroids).
- The player/enemy `Hitbox` Area2D handlers were deliberately stripped of bullet damage to avoid double-counting. The player's Hitbox only handles direct contact (`Enemy` body → 1 damage, `Obstacle` → instant death); the enemy's Hitbox only handles `Obstacle` → instant death. Do **not** re-add bullet damage to these Area2D handlers.
- Asteroids: player bullets and enemy bullets both destroy them (`Obstacle.Destroy()`); any aircraft touching an asteroid dies instantly.

### Collision layers (bit mask → layer)

Player=1, Enemy=2, PlayerBullet=4, EnemyBullet=8, Obstacle=16, HealthPack=32, BulletPack=64. Masks you'll see: player Hitbox 18 (enemy+obstacle), enemy Hitbox 53 (player+bullet+healthpack+obstacle), enemy body mask 2 (enemies physically block each other), enemy bullet cast 17 (player+obstacle), pickup Hitbox 1 (player only — both HealthPack and BulletPack).

### Enemy behavior

State machine `ENTER → IDLE` (`Enemy.cs`). ENTER descends to a random `TargetY` (or transitions early if physically blocked below, `IsOnFloor()`). IDLE movement = obstacle **dodge** (hysteresis on `_dodgeDir`, highest priority) else **roam** (random target X, `RoamIntervalMin/Max`) + **separation** from teammates (`GetNodesInGroup("enemies")`). Enemies have `collision_mask = 2` so they physically cannot pass through each other; spawn points in `Main.cs` are chosen to avoid overlapping existing enemies. Fires downward via a `Timer`. Killing an enemy grants no ammo — the only resupply is picking up a bullet pack.

### Game flow & systems

- **Start/end screens:** a `UI` CanvasLayer (`process_mode = 3`, Always — so buttons work while paused) contains `StartScreen` and `EndScreen` Controls. `Main.cs` pauses the tree until "开始游戏" is pressed; on player death it shows the end screen; "再来一局" calls `GetTree().ReloadCurrentScene()` to restart.
- **Pickups:** `HealthPack.cs` (+`HealAmount` HP) and `BulletPack.cs` (+`AmmoAmount` ammo) are near-identical fallers: `Hitbox` mask 1 so only the player body triggers them, then `QueueFree()`. `Main` spawns them on their own timers into the `HealthPacks`/`BulletPacks` containers.
- **Ammo:** player starts with `InitialAmmo` (30), each shot costs 1. **Running out of ammo is not a loss** — firing just fails until a bullet pack is picked up. `Player` has no `OutOfAmmo` signal and no failure path; the only way the game ends is the player dying. The only resupply is `BulletPack` (`AmmoAmount` 15, spawned every `BulletPackSpawnInterval` 4.5s).
- **HUD:** plain `Label`s in `main.tscn` (`HUD` = HP top-left, `AmmoHUD` = ammo top-right), refreshed each frame by `Main._Process`/`UpdateHud()`.
- **Explosions:** `Explosion.cs` builds a 4-frame `SpriteFrames` at runtime (`process_mode = Always` so it plays out after game-over pause); `ExplosionFx.cs` is a static helper that parents the effect under the `effects` container.

### Input

Defined in `project.godot` under `[input]`: `move_left` (A / ←), `move_right` (D / →), `fire` (Space, held to shoot). All tuned numbers are `@export` on the scripts so they're adjustable from the Inspector.
