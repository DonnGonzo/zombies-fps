# Credits

Third-party art in this project that asks to be credited.

- **Character speech:** generated with ElevenLabs using the account's available voices. Cast, synthesis settings and recording provenance are documented in `tools/voice/elevenlabs_cast.json` and `tools/voice/recordings.json`.

- **Supplied model packs:** chapel bench, gothic window, hospital bed, prison cell, two trucks, eight guns (including the MP5), hazmat zombie, Dans zombie (the Stalker), slot machine, hazard table, animated oak, animated old mage, magic ring, sci-fi capsule and hex sphere were imported from the user's local model archives. `tools/imported_models/sources.json` records archive names, hashes and original model paths. Author and license information was not included in the supplied archive metadata; this provenance entry does not assign a new license to the art.

- **Yuan-ti** (the Sunken Temple's guardian, Ssethra): "D&D Heroes (2003) - Yuan-ti" by Ziggertron on Sketchfab,
  licensed under Creative Commons Attribution 4.0 (https://creativecommons.org/licenses/by/4.0/). Converted to the
  game's own format by `tools/yuanti/pack_yuanti.py` (re-scaled, turned to face +Z, clips resampled); otherwise unchanged.

- **Zombie voices** (`Assets/_Project/Resources/Audio/Zombie`): cut, pitched and levelled from CC0 (public domain)
  recordings on OpenGameArt by `tools/zombievox/pack_zombie_vox.py`, which lists every source cut. No attribution
  is required; credit is given anyway.
  - "Zombie moans" and "Hungry Ghoul" by Darsycho (https://opengameart.org/content/zombie-moans,
    https://opengameart.org/content/hungry-ghoul), CC0.
  - "Ghost Monster Voice Moaning & Growling" by qubodup
    (https://opengameart.org/content/ghost-monster-voice-moaning-growling), CC0, pitched down.
  - "Monster Sound Pack, Volume 1" by Ogrebane, made for Summoning Wars (https://opengameart.org/node/4017), CC0.

- **Blackjack table** (`Assets/_Project/Resources/CC0/Casino`): a round table with three chairs that the project
  owner (Ivan) generated with Hunyuan3D-2 and supplied for the casino. Packed by
  `tools/casino/pack_blackjack_table.py`; hashes of the source OBJ and texture are in `tools/imported_models/sources.json`.
