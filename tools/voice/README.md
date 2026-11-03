# Offline character voices

The game ships 334 ElevenLabs recordings for ten characters: Marlow, Dee, Dr. Hale, Rook, Director Vale (also the Dispatcher), the Trader, Doctor Morrow, Brannock, Azhrakel and Ashgrave. Voice IDs were selected from the authenticated account's available voices. `elevenlabs_cast.json` records the cast and synthesis settings; `recordings.json` records durations, content hashes and the returned account usage snapshot.

No ElevenLabs account, API key or online speech service is needed to play. The API key is only used by the offline production script, from `ELEVENLABS_API_KEY` or a local ignored `.env` file. Never place it in Assets or commit it.

## Rebuild dialogue

1. In Unity, select **Zombies FPS > Voice > Export dialogue script**. The export includes every survivor event, both sides of banter, mirror banter, radio story beats, mission reactions, Trader lore and Easter egg dialogue.
2. Run `python tools/voice/generate_elevenlabs.py --estimate` to check text counts and available credits.
3. Run `python tools/voice/generate_elevenlabs.py --generate` to produce the recordings. This spends ElevenLabs credits; the script caps each batch at 121,000 uncached characters and the available balance. It uses three concurrent requests and does not automatically retry paid generations.
4. Refresh Unity and run Edit Mode tests. `VoiceAndViewTests` checks the complete manifest and runtime survivor/banter lookup.

Successful source recordings are cached under ignored `Temp/elevenlabs` before processing, preventing duplicate purchases on reruns. Keep that cache if you intend to reprocess the audio. FFmpeg converts the source to mono 44.1 kHz Vorbis at approximately -19 LUFS; Morrow has a light intercom filter and Azhrakel a subtle lower register and echo. Other voices retain their natural performance. FFmpeg 8 can stamp a Vorbis file with a longer length than it holds, which Unity reports as "FSBTool WARNING: This source file was truncated"; the script runs `fix_ogg_granules.py` on every encode to correct it without re-encoding. Run `python tools/voice/fix_ogg_granules.py --check Assets` to scan existing clips, or without `--check` to repair them. `direction` in the cast file documents casting intent; it is not sent as spoken text.

Easter egg filenames stay unchanged to preserve existing triggers. Other dialogue uses a stable SHA-256 prefix of its exact subtitle text, under the character's folder. Editing dialogue therefore requires re-exporting and generating its new clip. Playback follows clip duration for subtitles and banter replies. Synthetic blips remain only as a fallback for future lines without recordings.

The former `make_placeholder_voice.sh` is retained for historical/reference use; running it with FORCE=1 would replace the production Easter egg voices with placeholders.
