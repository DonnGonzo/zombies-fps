# Zombies FPS

Co-op (1–4 players) round-based zombie survival shooter in Unity 6. Design plan: `zombies01.pdf` (Unity 6, Netcode for GameObjects host-authoritative, Relay + Lobby, ScriptableObject balance data).

## Current state: Phase 2 (round loop), on top of Phases 0 and 1

A first-person greybox slice you can play with up to 4 friends, using Netcode for GameObjects with a host-authoritative model, and now a real round-based game loop.

**Round loop (Phase 2)**
- One host-owned `RoundManager` runs the game: intermission, round active, game over
- Each round spawns a budget of zombies (`6 + 2.2·round^0.85`, more with more players). Never more than the live cap alive at once (24 solo, +4 per extra player, 40 max). The rest spawn as others die, faster each round
- Zombie health and speed follow the round curves in `RoundData`, up to round 500
- 8 seconds before round 1, 10 seconds between rounds. Round start and round cleared banners and sounds
- Points: everyone starts with 500. Hits, kills and headshot kills pay, and clearing a round pays everyone a bonus (100 + 10 per round). Nothing to spend them on until Phase 3 (doors, wall guns, ammo)
- Players who bled out come back when the round is cleared. Game over when nobody is left standing, showing the round you reached
- Zombies come from a network object pool (no Instantiate mid-round). A zombie that gets stuck far from everyone is recycled and spawns again
- Test mode on the host: `=` next round, `]` jump 10 rounds, `G` god mode (no damage, endless ammo) for checking round 50 and beyond

**Co-op (Phase 1)**
- Host/Join menu: one player hosts, the others join by IP address and port (default 7777)
- Synced players with names, colours and name tags, and a squad list with health and status
- The host owns every zombie (NavMesh AI, health, attacks). Clients see them through NetworkTransform plus hit, swing and death events
- Shots are predicted locally (tracer, muzzle flash, impact) and resolved on the host. Hitmarkers come back from the host
- The host owns health, regen, downs, 30 s bleed-out, revives (hold E for 3 s near a downed teammate), points, kills and headshots
- The host presses Enter after a game over to restart for everyone

**Gameplay slice (Phase 0)**
- Walled greybox yard with cover, a raised platform, boarded spawn windows and a NavMesh baked at runtime
- First-person controller: walk, sprint, jump, aim-walk, mouse look, recoil and camera shake
- One hitscan automatic rifle (Rook-9 Carbine): magazine + reserve, reload, aim down sights, spread/bloom, tracers, muzzle flash, impact debris, hitmarkers (white hit, gold headshot, red kill)
- Walker zombie: chases the nearest standing player, telegraphed swing you can back out of, hit flash, stagger, falls away from the shot, headshot kills pop the head
- Placeholder sound effects synthesised in code (no audio assets yet)
- `GunData`, `ZombieData` and `RoundData` ScriptableObjects in `Assets/_Project/Data` hold the tuning numbers
- Edit Mode tests for the round 1-500 difficulty formulas

## Open it

1. Unity Hub → **Add → Add project from disk** → pick this folder. Open with Unity 6 (6000.x). The first import takes a few minutes.
2. If Unity asks to enable the new Input System backends, click **Yes** (the editor restarts). Either input backend works.
3. Open `Assets/_Project/Scenes/Prototype.unity` (or menu **Zombies FPS → Open Prototype Scene**) and press Play.
4. Click **Host game** to play (solo is just hosting with nobody else joining), then click the Game view to capture the mouse.

## Test co-op on one PC

1. **File → Build Profiles** (or Build Settings) → **Build And Run** a Windows build.
2. Press Play in the editor and click **Host game**.
3. In the built game, leave the address as `127.0.0.1` and click **Join game**.

To play with friends on another PC, they join your LAN IP (shown on the menu). Over the internet, you forward port 7777 for now. Relay join codes come next, so nobody has to forward ports.

If the scene is ever missing or broken, **Zombies FPS → Rebuild Prototype Scene** recreates it.

## Controls

| Action | Key |
|---|---|
| Move / sprint / jump | WASD / Shift / Space |
| Fire / aim / reload | Left mouse / right mouse / R |
| Revive a downed teammate | Hold E |
| Test mode (host): next round / jump 10 rounds / god mode | `=` / `]` / `G` |
| Free the mouse (shows Leave) / toggle help | Esc / H |
| Restart after game over (host) | Enter |

A gamepad works too (sticks, triggers, south = jump, west = reload).

## Tuning

Select an asset in `Assets/_Project/Data` and edit it in the Inspector, even during Play mode:

- `Gun_Rook9`: damage, headshot multiplier, fire rate, magazine, reload times, spread, recoil, ADS zoom
- `Zombie_Walker`: speeds per tier, attack damage/range/wind-up, stagger, points
- `RoundData`: zombie count `base + a·round^0.85`, live cap, spawn pacing, intermission length, starting points and round bonus, health curve (linear → steep to round 30 → soft cap → late-game growth), speed tier rounds

Run the tests (round formulas, live cap, spawn pacing, round bonus) from **Window → General → Test Runner → EditMode**.

## Layout

```
Assets/_Project/
  Data/            ScriptableObject balance assets
  Resources/Net/   Network prefabs (player, zombie, round manager). Bodies are built in code on spawn
  Scenes/          Prototype.unity
  Scripts/Runtime/ Core (bootstrap + menu, arena, input, audio), Net (players, director), Player, Weapons, Zombies, Combat, UI, Data
  Scripts/Editor/  Menu helpers
  Tests/EditMode/  Round formula tests
```

The prototype builds its scene from code (`PrototypeBootstrap`) so there are no hand-made prefabs while the design is still moving. Pieces move into prefabs once they stop changing.

## Decisions so far

- First person.
- Built-in render pipeline for the greybox; switch to URP once real art starts.
- Placeholder IMGUI HUD; the real HUD is UI Toolkit later.
- Names are original placeholders (no Call of Duty names).
- Movement is owner-authoritative (each player moves themselves). The host decides everything that costs health or money, which is enough for co-op. Server-side movement validation can come later if cheating matters.

## Next

- Unity Relay + Lobby join codes, so friends can join without port forwarding. This needs the project linked to a Unity Cloud project (Edit → Project Settings → Services).
- An autoplay bot for overnight balance runs to rounds 100, 250 and 500 (planned for Phase 2 in the design doc).
- Phase 3: a real map blockout with 5-6 zones, doors, wall guns and ammo purchases to spend points on.
