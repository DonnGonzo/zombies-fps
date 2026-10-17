# Zombies FPS

Co-op (1–4 players) round-based zombie survival shooter in Unity 6. Design plan: `zombies01.pdf` (Unity 6, Netcode for GameObjects host-authoritative, Relay + Lobby, ScriptableObject balance data).

## Current state: Phase 5 (missions, XP and the spawn shop), on top of Phases 0-4

A first-person greybox co-op zombie game for up to 4 players: Netcode for GameObjects with a host-authoritative model, a round-based loop, and now a map to buy your way through.

**XP, levels and the spawn shop (Phase 5, part B)**
- Everyone earns XP each run: kills (more for headshots and bosses), every round cleared, and every mission finished. Your level shows under your points and next to your name in the squad list
- Levels carry over between runs: total XP is saved on each player's own PC. Your level unlocks attachments in the shop, camos and prestige titles. Unlocked attachments still have to be bought with points in each match
- Camos and titles are picked in the main menu (Camos and title) before a match and can't be changed in game. Camos: Factory, then Olive Drab (level 3) up to Dark Matter (level 50), one pick per gun, and teammates see yours. Titles show next to your name: Rookie, Survivor (5), Scavenger (10), Veteran (15) up to Undying (50)
- The spawn shop is the stall in the Gate Yard. Press E there to open it, then a number key to fit an attachment to the gun in your hands, paid with points. Your level unlocks them:
  - Level 1 Red-Dot Sight, 2 Extended Mag, 4 Foregrip, 6 Suppressor, 9 Fast Mag, 12 Marksman Scope, 16 Heavy Barrel, 20 Hollow-Core Rounds, 25 Full-Auto Conversion
- One attachment per gun slot (optic, magazine, underbarrel, muzzle, barrel, ammo, trigger): a new optic replaces the old one. Attachments stay on the gun until you replace it or the run ends

**Missions (Phase 5, part A)**
- Three optional missions are handed out on round 1, then a fresh set on rounds 10, 20, 30 and so on. An unfinished set just goes away when it refreshes. They're listed top right; progress is shared by the squad and every player gets the reward
- Objective types: kill N zombies, get N headshot kills, kill N of one type (runners, crawlers, brutes, spitters, once they've shown up), hold a generator (stand next to the lit generator in an open zone while the round is on), and clear N rounds in a row with nobody going down
- Targets and rewards grow with the round. Rewards are points plus, for some missions, full ammo for everyone or a power-up reroll (press 4 on the power-up cards to redraw them)

**Zombie types and bosses (Phase 4, part B)**
- New types join the mix as the rounds climb, each ramping in over a few rounds (a banner names them on the round they arrive):
  - Runner from round 8: fast, low health
  - Crawler from round 12: low to the ground and hard to hit; its hits slow you for 2 s (at most 8 alive)
  - Brute from round 20: big, slow, hits for 70; armour halves body damage, so aim for the head (at most 4 alive)
  - Spitter from round 30: lobs acid where you stand when it can see you; the puddle burns for 4 s, so keep moving (at most 3 alive)
- Every 10th round a Warden boss joins the horde: 20x zombie health (+50% per extra player), armoured body, and a glowing weak point on its back that takes triple damage. Round 50 brings 2, round 100 brings 3, up to 4. Its health bar shows at the top of the screen, and killing it pays every player 500

**Power-ups and the upgrade machine (Phase 4, part A)**
- After every 5th round up to round 50 each player picks 1 of 3 power-ups with 1 / 2 / 3 (d-pad left / up / right). They stack: picking one again levels it up, with diminishing returns so stacking never runs away. Offers that pile up (left unpicked, or skipped over with the test keys) wait in a queue and come up one after another
  - Hollow Point (damage, up to +300%), Quick Hands (reload speed), Thick Skin (max health), Light Feet (move speed), Deadeye (headshot damage), Deep Pockets (magazine and reserve), Medic (revive speed), Bounty (kill points)
- Every 25th round the offer is empowered: it is drawn from power-ups you already own and adds 2 levels
- The upgrade machine (purple glow, in the Chapel) upgrades the gun in your hands: Mk II 5,000 (x2 damage, x1.5 ammo), Mk III 10,000 (x2.6, chain hits jump to a nearby zombie), Mk IV 15,000 (x3.3, chain plus explosive rounds). Each player can use it once every 5 rounds. An upgraded gun comes out full
- Owned power-ups show above the health bar; the health bar follows Thick Skin

**Map, doors and wall guns (Phase 3)**
- Six zones in a 3 x 2 grid, joined by six doors into one loop, so there is always a way round to retreat:

  | Barracks | Chapel | Garage |
  |---|---|---|
  | **Courtyard** | **Gate Yard** (start) | **Depot** |

- Doors cost 750 (next to the start), 1,000, then 1,250. Opening one opens the zone behind it and its zombie windows. Zombies come from the open windows nearest the players
- Wall guns, drawn in chalk on the walls: buy a gun once, then buy ammo for it at about half price. Walk up and press E
  - Rook-9 Carbine ammo (Gate Yard, Barracks) 250
  - Hatchet .44 revolver (Depot) 750
  - Wren-7 SMG (Courtyard) 1,000
  - Brennan DMR (Garage) 1,500
  - Talon LMG (Chapel) 2,500
- Two weapon slots. A third gun replaces the one in your hands. Switch with Q, the mouse wheel or 1 / 2
- Every price is in one asset, `Data/PriceTable`. Each gun is its own `GunData` asset
- Doors and buys are checked and paid for on the host; everyone sees doors open together

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
- Greybox world with cover, boarded spawn windows and a NavMesh baked at runtime (now the six-zone map above)
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
| Buy (door, wall gun, ammo, upgrade) | E |
| Pick a power-up / redraw the cards (with a reroll) | 1 / 2 / 3 / 4 |
| Open the spawn shop / buy an attachment | E at the stall / 1-9 |
| Switch weapon | Q / mouse wheel / 1, 2 |
| Revive a downed teammate | Hold E |
| Test mode (host): next round / jump 10 rounds / god mode / +5,000 points / power-up offer / spawn a boss | `=` / `]` / `G` / `P` / `O` / `B` |
| Free the mouse (shows Leave) / toggle help | Esc / H |
| Restart after game over (host) | Enter |

A gamepad works too (sticks, triggers, south = jump, west = reload, north = buy / revive, east = switch weapon).

## Tuning

Select an asset in `Assets/_Project/Data` and edit it in the Inspector, even during Play mode:

- `Gun_Rook9`, `Gun_Hatchet44`, `Gun_Wren7`, `Gun_BrennanDMR`, `Gun_TalonLMG`: damage, headshot multiplier, fire rate, magazine, reload times, spread, recoil, ADS zoom, and the model's length and colour
- `PriceTable`: door costs, upgrade machine costs, and each wall gun's price and ammo price
- `ShopData`: attachments (unlock level, price, stat multipliers), camos and titles with their unlock levels, the XP curve and XP rewards, and whether levels are saved between runs
- `MissionData`: each mission's objective, target and reward (base plus growth per round), how often sets refresh, generator radius
- `UpgradeData`: each power-up's max bonus and half level, how often offers come, empowered levels, machine tiers, chain and explosive damage
- `Zombie_Walker`, `Zombie_Runner`, `Zombie_Crawler`, `Zombie_Brute`, `Zombie_Spitter`, `Zombie_Boss`: health multiplier, speeds per tier, attack damage/range/wind-up, stagger, points, first round and spawn share, alive limit, look, armour and weak point, leg-hit slow, acid spit
- `RoundData`: boss rounds and boss bonus, zombie count `base + a·round^0.85`, live cap, spawn pacing, intermission length, starting points and round bonus, health curve (linear → steep to round 30 → soft cap → late-game growth), speed tier rounds

Run the tests (round formulas, live cap, spawn pacing, round bonus, the door loop, prices, power-up curves, machine windows, zombie type arrival, boss rounds, mission packing, refresh rounds and targets, XP curve, attachment slots and stats, camo and title unlocks) from **Window → General → Test Runner → EditMode**.

## Layout

```
Assets/_Project/
  Data/            ScriptableObject balance assets
  Resources/Net/   Network prefabs (player, zombie, round manager). Bodies are built in code on spawn
  Scenes/          Prototype.unity
  Scripts/Runtime/ Core (bootstrap + menu, input, audio), World (zone map, doors), Net (players, rounds, map state), Player, Weapons, Zombies, Combat, UI, Data
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
- Perks: 8 buyable perks, lost when you go down (in the design doc, not scheduled yet).
- Phase 6: the Dispatcher radio NPC, story beats every 25 rounds, and player voice lines.
