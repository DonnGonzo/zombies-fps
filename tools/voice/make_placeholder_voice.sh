#!/usr/bin/env bash
# Placeholder voice for Doctor Morrow: every EggLine in Data/EasterEggData.cs spoken by espeak-ng and run
# through an old-intercom filter, saved as Resources/Audio/Voice/Morrow/<id>.ogg. Real recordings saved under
# the same names replace these. Needs espeak-ng and ffmpeg.
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
data="$root/Assets/_Project/Scripts/Runtime/Data/EasterEggData.cs"
out="$root/Assets/_Project/Resources/Audio/Voice/Morrow"
tmp="$(mktemp -d)"
mkdir -p "$out"
grep -oE 'new EggLine\("[^"]+", "[^"]+"\)' "$data" | sed -E 's/new EggLine\("([^"]+)", "([^"]+)"\)/\1\t\2/' |
while IFS=$'\t' read -r id text; do
  espeak-ng -v en-gb-x-rp+m3 -s 132 -p 28 -g 3 -w "$tmp/$id.wav" "$text"
  ffmpeg -nostdin -hide_banner -loglevel error -y -i "$tmp/$id.wav" \
    -af "asetrate=22050*0.93,aresample=44100,atempo=1.05,vibrato=f=4.5:d=0.06,highpass=f=160,lowpass=f=6000,aecho=0.8:0.55:55|120:0.22|0.12,loudnorm=I=-16:TP=-1.5" \
    -ac 1 -c:a libvorbis -q:a 4 "$out/$id.ogg"
  echo "$id"
done
rm -rf "$tmp"
