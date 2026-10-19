#!/usr/bin/env bash
# Placeholder voices for the easter egg: every EggLine in Data/EasterEggData.cs spoken by espeak-ng and run
# through a filter for its speaker, saved as Resources/Audio/Voice/<Speaker>/<id>.ogg (with a Unity .meta).
#   Doctor Morrow: an old intercom.        Sergeant Brannock: a gravelly old soldier.
#   Azhrakel: deep, slow and doubled.      General Ashgrave: cold, in a stone hall.
# Real recordings saved under the same names replace these. Lines that already have a file are skipped;
# FORCE=1 redoes them all. Needs espeak-ng and ffmpeg.
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
data="$root/Assets/_Project/Scripts/Runtime/Data/EasterEggData.cs"
voices="$root/Assets/_Project/Resources/Audio/Voice"
tmp="$(mktemp -d)"

meta() {
  local guid
  guid="$(python3 -c 'import uuid; print(uuid.uuid4().hex)')"
  cat > "$1.meta" <<META
fileFormatVersion: 2
guid: $guid
AudioImporter:
  externalObjects: {}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 1
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 0.7
    conversionMode: 0
    preloadAudioData: 0
  platformSettingOverrides: {}
  forceToMono: 0
  normalize: 0
  loadInBackground: 1
  ambisonic: 0
  3D: 1
  userData: 
  assetBundleName: 
  assetBundleVariant: 
META
}

folder_meta() {
  [ -f "$1.meta" ] && return
  local guid
  guid="$(python3 -c 'import uuid; print(uuid.uuid4().hex)')"
  printf 'fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$guid" > "$1.meta"
}

# Lines as speaker<TAB>id<TAB>text; lines without a speaker are Doctor Morrow's.
grep -oE 'new EggLine\((EggSpeaker\.[A-Za-z]+, )?"[^"]+", "[^"]+"\)' "$data" |
  sed -E 's/new EggLine\("/new EggLine(EggSpeaker.Morrow, "/' |
  sed -E 's/new EggLine\(EggSpeaker\.([A-Za-z]+), "([^"]+)", "([^"]+)"\)/\1|\2|\3/' |
while IFS='|' read -r speaker id text; do
  case "$speaker" in
    Watchman) dir=Brannock; voice="en-us+m7"; speed=138; pitch=20
      filter="asetrate=22050*0.9,aresample=44100,atempo=1.08,highpass=f=90,lowpass=f=7000,acompressor=threshold=0.1:ratio=4,aecho=0.6:0.3:35:0.15" ;;
    God) dir=Azhrakel; voice="en+m1"; speed=112; pitch=0
      filter="asplit[a][b];[a]asetrate=22050*0.68,aresample=44100,atempo=1.3[low];[b]asetrate=22050*0.8,aresample=44100,atempo=1.1,volume=0.5[mid];[low][mid]amix=inputs=2:duration=longest,lowpass=f=4000,aecho=0.8:0.75:110|240:0.45|0.3" ;;
    General) dir=Ashgrave; voice="en-gb+m2"; speed=126; pitch=16
      filter="asetrate=22050*0.95,aresample=44100,atempo=1.02,highpass=f=110,lowpass=f=8000,aecho=0.8:0.6:70|150:0.3|0.18" ;;
    *) dir=Morrow; voice="en-gb-x-rp+m3"; speed=132; pitch=28
      filter="asetrate=22050*0.93,aresample=44100,atempo=1.05,vibrato=f=4.5:d=0.06,highpass=f=160,lowpass=f=6000,aecho=0.8:0.55:55|120:0.22|0.12" ;;
  esac
  out="$voices/$dir"
  mkdir -p "$out"
  folder_meta "$out"
  if [ -f "$out/$id.ogg" ] && [ "${FORCE:-0}" != 1 ]; then continue; fi
  espeak-ng -v "$voice" -s "$speed" -p "$pitch" -g 3 -w "$tmp/$id.wav" "$text"
  ffmpeg -nostdin -hide_banner -loglevel error -y -i "$tmp/$id.wav" \
    -filter_complex "$filter,loudnorm=I=-16:TP=-1.5" -ac 1 -c:a libvorbis -q:a 4 "$out/$id.ogg"
  [ -f "$out/$id.ogg.meta" ] || meta "$out/$id.ogg"
  echo "$dir/$id"
done
rm -rf "$tmp"
