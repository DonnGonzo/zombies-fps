# Zombies FPS

Co-op (1–4 players) round-based zombie survival shooter in Unity 6. Design plan: `zombies01.pdf` (Unity 6, Netcode for GameObjects host-authoritative, Relay + Lobby, ScriptableObject balance data).

## Current state: Phase 0 prototype

An offline, first-person greybox slice whose goal is the Phase 0 exit criterion: **shooting one zombie feels good.**

- Walled greybox yard with cover, a raised platform, boarded spawn windows and a NavMesh baked at runtime
- First-person controller: walk, sprint, jump, aim-walk, mouse look, recoil and camera shake
- One hitscan automatic rifle (Rook-9 Carbine): magazine + reserve, reload, aim down sights, spread/bloom, tracers, muzzle flash, impact debris, hitmarkers (white hit, gold headshot, red kill)
- NavMesh zombie (Walker): chases, telegraphed swing you can back out of, white hit flash, stagger, falls away from the shot, headshot kills pop the head
- Pooled zombies and effects (nothing is instantiated mid-fight)
- Placeholder sound effects synthesised in code, so there are no audio assets yet
- Player health with regen, damage vignette, death screen and restart
- `GunData`, `ZombieData` and `RoundData` ScriptableObjects in `Assets/_Project/Data` hold all the tuning numbers
- Edit Mode tests for the round 1–500 difficulty formulas

## Open it

1. Unity Hub → **Add → Add project from disk** → pick this folder. Open with Unity 6 (6000.x). The first import takes a few minutes.
2. If Unity asks to enable the new Input System backends, click **Yes** (the editor restarts). Either input backend works.
3. Open `Assets/_Project/Scenes/Prototype.unity` (or menu **Zombies FPS → Open Prototype Scene**) and press Play.
4. Click the Game view to capture the mouse.

If the scene is ever missing or broken, **Zombies FPS → Rebuild Prototype Scene** recreates it.

## Controls

| Action | Key |
|---|---|
| Move / sprint / jump | WASD / Shift / Space |
| Fire / aim / reload | Left mouse / right mouse / R |
| Fewer / more zombies alive | `[` / `]` |
| Test round down / up (zombie health and speed) | `-` / `=` |
| Free the mouse / toggle help | Esc / H |
| Restart after dying | Enter |

A gamepad works too (sticks, triggers, south = jump, west = reload).

## Tuning

Select an asset in `Assets/_Project/Data` and edit it in the Inspector, even during Play mode:

- `Gun_Rook9`: damage, headshot multiplier, fire rate, magazine, reload times, spread, recoil, ADS zoom
- `Zombie_Walker`: speeds per tier, attack damage/range/wind-up, stagger, points
- `RoundData`: zombie count `base + a·round^0.85`, live cap, health curve (linear → steep to round 30 → soft cap → late-game growth), speed tier rounds

Run the tests from **Window → General → Test Runner → EditMode**.

## Layout

```
Assets/_Project/
  Data/            ScriptableObject balance assets
  Scenes/          Prototype.unity
  Scripts/Runtime/ Core (bootstrap, arena, input, audio), Player, Weapons, Zombies, Combat, UI, Data
  Scripts/Editor/  Menu helpers
  Tests/EditMode/  Round formula tests
```

The prototype builds its scene from code (`PrototypeBootstrap`) so there are no hand-made prefabs while the design is still moving. Pieces move into prefabs once they stop changing.

## Decisions taken for Phase 0

- First person.
- Built-in render pipeline for the greybox; switch to URP once real art starts.
- Placeholder IMGUI HUD; the real HUD is UI Toolkit later.
- Names are original placeholders (no Call of Duty names).

## Next: Phase 1 (networking spine)

Lobby + Relay join-by-code, synced players, host-owned zombies and health, using Netcode for GameObjects. The gun's raycast moves to the host; clients keep the effects.
