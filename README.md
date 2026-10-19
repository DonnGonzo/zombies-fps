# Zombies FPS

Co-op (1–4 players) round-based zombie survival shooter in Unity 6. Design plan: `zombies01.pdf` (Unity 6, Netcode for GameObjects host-authoritative, Relay + Lobby, ScriptableObject balance data).

## Current state: every gameplay system from the design doc, with procedural graphics

All of the design doc's gameplay phases (0-6) are in: co-op, rounds to 500, the map, power-ups, the upgrade machine, six zombie types and bosses, missions, XP and the spawn shop, perks, the story and voice lines. Graphics are built in code (procedural textures, primitive-based models, custom shaders) and the voices are placeholder "blips" with subtitles. A plain-language feature list is in the project files as `feature-breakdown.pdf`.

**Easter egg step 1 (the Lantern trial) and music**
- Doctor Ignatius Morrow, who built the Lantern before Vale took it over, talks through the Lantern Core's intercoms. With every player who isn't bled out standing in the Lantern Core, anyone can touch the reactor (E). The four doorways seal with energy fields (players can't pass; shots and zombies can), the reactor glows and throws lightning at four new tesla coils, the ceiling and the floor, and 50 runners rise from the floor where bolts land, on top of the round. They don't count toward ending it. Kill all 50 and the fields drop, the reactor stays lit and everyone gets 2,500 points. The HUD shows the runners left; the pause menu tracks Doctor Morrow's steps once found
- Steps live in `Data/EasterEggData.cs` (one done, four locked placeholders). A new step is a new entry there; a new kind of step is a case in `Net/EasterEgg.cs`. The fields are `World/Lockdown.cs`, the reactor look `World/ReactorFx.cs`, the lab's new dressing (coils, specimen tanks, chalkboard, intercom horns, conduit crown) `World/ZoneMap.Lab.cs`. Test console: `goto lantern`, then touch the reactor; `egg` finishes the step
- Doctor Morrow's lines play voice files from `Resources/Audio/Voice/Morrow/<line id>.ogg` when they exist (else the placeholder blips). The ones there now are synthesised placeholders (`tools/voice/make_placeholder_voice.sh`); real recordings with the same names replace them
**Easter egg steps 2 to 5 (the Mana Spark)**
- Step 2, Charge the Spark: the Archives (Library) are bigger and hold a monks' altar with four plinths. Four relics (Watch Chalice, Silver Bell, Brass Lamp, Star Tablet) lie in the Chapel and the Temple; pick them up (E) and set them on the plinths in any order, then touch the altar to start a 3-minute ritual. Zombies keep coming (capped, a bit weaker); the clock pauses while nobody is in the Archives. The charged spark comes back at the end
- Step 3, The Watchman's Trust: Sgt. Brannock appears at the Crane Yard only once steps 1 and 2 are done. Talk to him and an ambush of 100 zombies plus brutes, spitters, crawlers and a Warden comes for him. If he dies the step resets; if he lives he tells you about the ritual stones under the Greenhouse
- Step 4, The Rotbloom: place the spark on the binding stones in the Greenhouse. A boss rises (seed pods, moves around) with adds; kill it for the spark and its elemental heart
- Step 5, The Ember Below: the stones open a portal to the Sanctum (off the main map). Place spark and heart on the blood altar, Azhrakel rises and talks, then you choose at two stones. Fight him: Godblood boon (2x damage and health, faster reload, more stamina, +20% speed) and the Brand of Azhrakel gun. Serve him: you're sent to the Hollow Keep, Gen. Ashgrave stands from his throne, talks, then fights; Ember Oath boon (3x damage and health) and the Oathbreaker sword (replaces the knife: one hit to round 55, two to 100, three after). Boons last the rest of the match. Round spawns hold while anyone is in the arenas
- Code: `Data/EasterEggData.cs` (steps, lines, tuning), `Net/EasterEgg.cs`, `Net/NetPlayer.Egg.cs` (boons, sword, teleport), `World/ZoneMap.Egg.cs` (altar, relic spots, stones, Sanctum, Keep), `World/EggProps.cs`. Voices for Morrow, Brannock, Azhrakel and Ashgrave are placeholder espeak clips in `Resources/Audio/Voice/<speaker>/`
- Test console: `egg` finishes the current step (`egg serve` picks the general's ending at the choice), `relics` hands over all four relics, `goto sanctum`, `goto keep`
- Music (`Core/MusicPlayer.cs`, Settings > Sound > Music): a menu theme, a quiet ambience in matches and an epic track while an easter egg step runs, crossfading. All three are synthesised from scratch by `tools/music/make_music.py`, so they are ours to ship; any other tracks saved under the same names in `Resources/Audio/Music` replace them

**Graphics pass (built-in render pipeline, no art files)**
- `Shaders/`: `ZombiesFPS/Surface` (triplanar detail and normal maps, world space for the map and object space for anything that moves, plus grime), `Glow` (HDR unlit, so it blooms), `NightSky` (skybox), `Decal` and `Hidden/ZombiesFPS/Post`. Each has a material in `Resources/Materials` so builds include it
- `Core/SurfaceTextures.cs` generates tileable textures per `Surface` (concrete, brick, metal, rust, wood, tile, plaster, asphalt, dirt, grass, fabric, skin, rubber); `Greybox.Lit(color, surface, moving)` makes the material
- `Core/GraphicsSetup.cs`: moonlight, trilight ambient, fog, night sky, a realtime reflection probe on High, and quality levels; `Core/PostFx.cs`: bloom, ACES tone mapping, grade, vignette, grain, brightness. Settings > Graphics has quality, brightness, bloom and film grain
- Map dressing in `World/ZoneMap.Dressing.cs` (zone materials, wall skins, colliderless decor merged per material and cell, lamps, skyline); detailed zombies and teammates with procedural animation; gun and shield models after Ivan's TenWeapons pack in `Weapons/GunArt.cs`, with bought attachments shown on the gun (meshes in `Resources/Guns`, made by `tools/tenweapons/build_guns.py`; `Weapons/GunModels.cs` builds the old primitive guns if they are missing); pooled decals in `Weapons/Decals.cs`
- An editor script switches the project to Linear colour space once, which the lighting is tuned for

**Zombies that climb, bullet penetration, a new layout, the story to round 50, upgraded guns**
- Zombies reach you on every high place. Parkour blocks are now 3 m square so zombies keep a path onto them, and a zombie standing under a player it has no path up to (a crate, a block, a ledge whose ladder is behind a shut door) scrambles up the side after a moment. High spots with a ladder or a jump up are still reached the long way round
- Bullet penetration: some guns' bullets go through zombies and keep going until they run out of punch or hit a wall or prop. Each zombie takes a bit of the damage out of the bullet; a brute counts as two zombies and a boss stops it dead. Vanta .50 sniper goes through 5 (keeps 90% each time), Ion Lance 3, Scrap Cannon 4, Brennan DMR 2, Talon LMG and Hatchet .44 one each. Every upgrade machine tier adds more (Mk II and Mk III +1, Mk IV +2), so upgraded guns of any kind punch through. Tracers run to where the bullet finally stops, and a piercing shot leaves a heavier streak
- New map layout: the twelve zones are no longer a grid of equal squares. The Chapel is a cross, the Barracks a long hall, the Depot a wide warehouse, the Loading Dock and Archives L-shaped, the Pump Station a T, the Lantern Core a stepped octagon reached through a short passage, and the Crane Yard is the biggest open space. There are walled-off gaps between some zones, so the outline is ragged too. Same 17 doors and prices; the map screen draws the real shapes
- The story now runs from round 1 and finishes at round 50: Setup (1-10), Rising stakes (11-25), the Twist (26-40, Vale unmasked at round 28) and the Endgame (41-50). Clearing round 50 plays the finale and shows STORY COMPLETE. The game carries on after that (the win is still round 500): the Trader takes over the radio and posts bounties, six new well-paid missions that only appear after the story (two of every three jobs), including Skewer, a new kind of mission for kills made with bullets that already went through another zombie
- Upgraded guns sound different and glow: each machine tier has its own gunshot (Mk II a cold zap, Mk III a crackling electric arc, Mk IV a deep plasma boom with an echo), glowing strips and a muzzle ring in the tier's colour (ice blue, violet, molten orange) on your gun and on teammates' guns, and tracers in the same colour

**Bigger map, climbing and buildables**
- The map is four times bigger: twelve zones of different shapes (see above), and every pair of neighbouring zones is joined by a door (17 doors, 750 near the start up to 2,250 at the far side), so there are lots of ways round. Roughly, north up:

  | Greenhouse | Archives | Lantern Core | Crane Yard |
  |---|---|---|---|
  | **Barracks** | **Chapel** | **Garage** | **Pump Station** |
  | **Courtyard** | **Gate Yard** (start) | **Depot** | **Loading Dock** |

- Every zone has something to climb: stairs up to a watch platform, choir loft, mezzanine, pump house, loading dock and catwalk; parkour block runs in the Garage, Courtyard and Crane Yard; and ladders in the Depot, Barracks, Loading Dock (up the truck), Pump Station, Lantern Core, Archives and Greenhouse
- Ladders: walk into one facing it to climb, S to go down, jump to let go (from the top, just drop off; there is no fall damage). Zombies climb ladders and jump the parkour gaps too, so nowhere is safe for long
- Six high spots can only be reached by climbing: Depot Loft, Garage Roof, Water Tower, Container Stack, Records Loft and Greenhouse Roof
- New mission type, high ground: King of the Hill (round 6, full ammo) and Signal Boost (round 15, power-up reroll) light the signal beacon on one of the high spots; climb up and hold it
- Build stations on the Garage Roof and the Greenhouse Roof build three things from parts hidden around the map. Each part can be in one of three places, picked fresh every game, many of them up on the high spots. Parts anyone finds count for the whole squad:
  - Scrap Cannon (barrel, coil, grip): a slow, explosive cannon whose shots punch through four zombies. Once built, everyone can take one each per game
  - Aegis Shield (armour plate, view module, power core, brace kit: four parts): carried on your arm, it takes 90% of hits from in front (not acid) until it has soaked 1,500 damage. When it breaks you can take a new one from the next round
  - Buddy Bot (chassis, rotor, brain): a drone that follows whoever switched it on and shoots the nearest zombie it can see for two minutes, then breaks and stays offline for five rounds. Its kills count as yours
- Map screen (M): every zone and its name, which are open, the doors and their prices, where you and your squad are and which way you face, the high spots, build stations, shop, upgrade machine and current missions, and which parts are still missing
- Test console: `doors` opens every door, `parts` finds every part, `goto crane` or `goto high 2` teleports you

**Story, NPCs and voice (Phase 6)**
- The Dispatcher talks to the squad over the radio in four acts, all within the first 50 rounds: Setup (rounds 1-10), Rising stakes (11-25), the Twist (26-40, the Dispatcher turns out to be Director Vale and every generator you powered fed the Lantern) and the Endgame (41-50, the lift to the surface), with about 30 calls along the way and a finale when round 50 is cleared. After that the Trader has the radio, with calls at rounds 51, 60, 75, 100, 150, 200, 300, 400, 499 and on winning. The radio also comments on new mission sets, finished missions, boss rounds, blackouts and game overs
- The Trader stands behind the spawn shop counter and tells a bit of lore each time you open the shop, different for each act
- Pick one of four characters in the main menu: Marlow (ex-cop), Dee (mechanic), Dr. Hale (facility scientist) and Rook (courier). Each has lines for 16 events (round start and end, going down, being revived, a teammate down, buying doors, guns and perks, the upgrade machine, low ammo, power-ups, bosses, milestone rounds 50/100/250/500, blackouts, missions), and two players standing together between rounds trade banter
- Lines show as subtitles (Settings, Accessibility) and play placeholder speech blips at each character's pitch; radio calls always show. The pause menu has a "story so far" card
- Two sabotage missions join the pool in the Twist: Cut the Feed (round 26) and The Defector (round 28)

**Perks, armour and rerolls**
- Eight perk machines around the map (placeholder names): Ironhide Tonic 2,500 (double health), Slick Fingers 3,000 (reload 50% faster), Second Wind 1,500 (revive twice as fast; solo, get yourself up once), Hair Trigger 2,000 (fire 33% faster), Fleet Foot 2,000 (move 12% faster), Steady Hand 2,000 (40% less spread), Hazard Ward 2,500 (acid and slow immune, 15% less damage), Bloodline 4,000 (kills heal you, faster regen)
- You lose every perk when you go down. Power-ups stay
- Armour station: plates soak 50 damage each, up to 3. 750 each, plus 25 per round, capped at 5,000
- The Fortune Shrine sells power-up rerolls: 3,000 plus 100 per round, capped at 20,000

**Melee and throwables**
- Melee (V): a quick knife swing, stronger on the head, scales with the round and staggers what it doesn't kill
- Frag grenades (G, start with 2, carry 4), throwing knives (T, 3/3, instant-kill early on, sticks in walls) and concussion bangs (C, 1/2, stuns zombies for 3 s then slows them). Supplies top up every round and fill up with full ammo. Damage scales with zombie health so they stay useful late

**More guns and rounds**
- New wall guns: Mauler 12 shotgun (pellets), Kestrel AR and Vanta .50 sniper (bullets go through zombies; see bullet penetration above). The Ion Lance is a rare prototype weapon only from The Prototype mission
- Every 25th round is a blackout: the lights drop, runners flood in, and every kill pays double
- Protect missions: Safe Passage (round 5) and The Prototype (round 25) put a survivor with a flare in an open zone. Zombies go for them; stay close and keep them alive. They show on screen through walls

**Profile, settings and UI**
- Prestige: at level 30 you can prestige from the main menu (10 ranks, Ember to Master). Your level resets, your camos stay, and prestige camos unlock at P1, P3, P5, P7 and P10. Your rank shows next to your name
- XP to the next level shows in the menu and in game
- Settings (main menu and pause menu): rebind every key, mouse sensitivity, invert Y, field of view, master, effects and voice volume, subtitles, reduced camera shake, colour-blind colours, tips, HUD size
- A first-time tips system explains each thing the first time you meet it
- New look for the main menu, HUD, shop, power-up cards, pause and game-over screens
- Test console on the host (` or F2): `round 250`, `points 50000`, `xp 10000`, `spawn brute 5`, `boss`, `gun lance`, `perks`, `ammo`, `powerup emp`, `god`, `clear`, `help`

**XP, levels and the spawn shop (Phase 5, part B)**
- Everyone earns XP each run: kills (more for headshots and bosses), every round cleared, and every mission finished. Your level shows under your points and next to your name in the squad list
- Levels carry over between runs: total XP is saved on each player's own PC. Your level unlocks attachments in the shop, camos and prestige titles. Unlocked attachments still have to be bought with points in each match
- Camos and titles are picked in the main menu (Camos and title) before a match and can't be changed in game. Camos: Factory, then Olive Drab (level 3) up to Dark Matter (level 50), one pick per gun, and teammates see yours. Titles show next to your name: Rookie, Survivor (5), Scavenger (10), Veteran (15) up to Undying (50)
- The spawn shop is the stall in the Gate Yard. Press E there to open it, then a number key to fit an attachment to the gun in your hands, paid with points. Your level unlocks them:
  - Level 1 Red-Dot Sight, 2 Extended Mag, 4 Foregrip, 6 Suppressor, 9 Fast Mag, 12 Marksman Scope, 16 Heavy Barrel, 20 Hollow-Core Rounds, 25 Full-Auto Conversion
- One attachment per gun slot (optic, magazine, underbarrel, muzzle, barrel, ammo, trigger): a new optic replaces the old one. Bought attachments show on the gun (yours and teammates'), and the scope gives a full scope view when aiming. Attachments stay on the gun until you replace it or the run ends

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
- The upgrade machine (purple glow, in the Lantern Core) upgrades the gun in your hands: Mk II 5,000 (x2 damage, x1.5 ammo), Mk III 10,000 (x2.6, chain hits jump to a nearby zombie), Mk IV 15,000 (x3.3, chain plus explosive rounds). Each player can use it once every 5 rounds. An upgraded gun comes out full
- Owned power-ups show above the health bar; the health bar follows Thick Skin

**Map, doors and wall guns (Phase 3)**
- Twelve zones joined by 17 doors (see "Bigger map" above). Opening a door opens the zone behind it and its zombie windows. Zombies come from the open windows nearest the players
- Wall guns, drawn in chalk on the walls: buy a gun once, then buy ammo for it at about half price. Walk up and press E
  - Rook-9 Carbine ammo (Gate Yard, Barracks) 250
  - Hatchet .44 revolver (Depot) 750
  - Wren-7 SMG (Courtyard) 1,000
  - Mauler 12 shotgun (Chapel) 1,250
  - Brennan DMR (Garage) 1,500
  - Kestrel AR (Pump Station) 2,000
  - Vanta .50 sniper (Crane Yard) 2,250
  - Talon LMG (Lantern Core) 2,500
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
- Test mode on the host: `=` next round, `]` jump 10 rounds, `F1` god mode (no damage, endless ammo) for checking round 50 and beyond

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

Every keyboard and mouse key can be changed in Settings, Controls. Defaults:

| Action | Key |
|---|---|
| Move / sprint / jump | WASD / Shift / Space |
| Fire / aim / reload | Left mouse / right mouse / R |
| Buy (doors, wall guns, ammo, perks, armour, upgrade), pick up parts, use a build station | E |
| Revive a downed teammate | Hold E |
| Melee / grenade / throwing knife / concussion bang | V / G / T / C |
| Switch weapon | Q / mouse wheel / 1, 2 |
| Pick a power-up / redraw the cards (with a reroll) | 1 / 2 / 3 / 4 |
| Open the spawn shop / buy an attachment | E at the stall / 1-9 |
| Climb a ladder / climb down / let go | Walk into it / S / Space |
| Map | M |
| Pause menu (resume, settings, story, leave) / toggle help | Esc / H |
| Test mode (host): next round / jump 10 rounds / god mode / +5,000 points / power-up offer / spawn a boss / console | `=` / `]` / `F1` / `P` / `O` / `B` / `` ` `` or F2 |
| Restart after game over (host) | Enter |

A gamepad works too, with the Input System backend (sticks, triggers, south = jump, west = reload, north = buy / revive, east = switch weapon, right stick click = melee, RB = grenade, LB = knife, Select = bang, Start = pause).

## Tuning

Select an asset in `Assets/_Project/Data` and edit it in the Inspector, even during Play mode:

- `Gun_Rook9`, `Gun_Hatchet44`, `Gun_Wren7`, `Gun_BrennanDMR`, `Gun_TalonLMG`: damage, headshot multiplier, fire rate, magazine, reload times, spread, recoil, ADS zoom, and the model's length and colour
- `PriceTable`: door costs, upgrade machine costs, each wall gun's price and ammo price, and the reward guns (Ion Lance, Scrap Cannon)
- `Gun_ScrapCannon`: the buildable cannon's stats
- `ShopData`: attachments (unlock level, price, stat multipliers), camos and titles with their unlock levels, the XP curve and XP rewards, and whether levels are saved between runs
- `MissionData`: each mission's objective, target and reward (base plus growth per round), how often sets refresh, generator radius
- `UpgradeData`: each power-up's max bonus and half level, how often offers come, empowered levels, machine tiers, chain and explosive damage
- `Zombie_Walker`, `Zombie_Runner`, `Zombie_Crawler`, `Zombie_Brute`, `Zombie_Spitter`, `Zombie_Boss`: health multiplier, speeds per tier, attack damage/range/wind-up, stagger, points, first round and spawn share, alive limit, look, armour and weak point, leg-hit slow, acid spit
- `PerkData`: each perk's name, price, colour and effect size, armour plates and the reroll shrine prices
- `EquipmentData`: melee damage and range, each throwable's starting count, carry limit, refill, damage, radius, fuse and stun
- `RoundData`: blackout rounds, boss rounds and boss bonus, zombie count `base + a·round^0.85`, live cap, spawn pacing, intermission length, starting points and round bonus, health curve (linear → steep to round 30 → soft cap → late-game growth), speed tier rounds

Run the tests (story and voice tables, prestige, perks, explosions, blackout rounds, round formulas, live cap, spawn pacing, round bonus, the door network, high spots, part spots, ladders, the map key, prices, power-up curves, machine windows, zombie type arrival, boss rounds, mission packing, refresh rounds and targets, XP curve, attachment slots and stats, camo and title unlocks) from **Window → General → Test Runner → EditMode**.

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

- Unity Relay + Lobby join codes, so friends can join without port forwarding, and Vivox voice chat. Both need the project linked to a Unity Cloud project (Edit → Project Settings → Services).
- Real art, animation and recorded voices to replace the greybox and the speech blips. Moving to URP at the same time.
- An autoplay bot for overnight balance runs to rounds 100, 250 and 500. The test console and god mode cover quick checks for now.
