"""Offline ElevenLabs production. Run --estimate, then --generate [--limit N].

Source script is exported by Unity's Zombies FPS > Voice menu. Cache successful
requests before conversion so interrupted runs do not purchase the same audio twice.
API credentials stay in the environment or ignored .env, never in Unity assets.
"""
import argparse, concurrent.futures, hashlib, json, os, subprocess, threading
import urllib.request, urllib.error
from fix_ogg_granules import repair
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CACHE = ROOT / 'Temp/elevenlabs'
CONFIG = json.loads((ROOT/'tools/voice/elevenlabs_cast.json').read_text())
LINES = json.loads((ROOT/'tools/voice/dialogue.json').read_text(encoding='utf-8-sig'))['lines']
LOCK = threading.Lock()

def key():
    value = os.environ.get('ELEVENLABS_API_KEY')
    env = ROOT/'.env'
    if not value and env.exists():
        for line in env.read_text().splitlines():
            if line.strip().startswith('ELEVENLABS_API_KEY='):
                value = line.split('=',1)[1].strip().strip('\"\'')
    if not value: raise RuntimeError('Set ELEVENLABS_API_KEY in the environment or ignored .env.')
    return value

def get(route):
    req=urllib.request.Request('https://api.elevenlabs.io/v1/'+route, headers={'xi-api-key':key()})
    with urllib.request.urlopen(req, timeout=60) as response: return json.load(response)

def payload(line):
    cast=CONFIG['cast'][line['speaker']]
    return dict(text=line['text'], model_id=CONFIG['model_id'],
        voice_settings=dict(stability=cast['stability'], similarity_boost=0.75,
            style=cast['style'], use_speaker_boost=True, speed=1.0))

def cache_paths(line):
    cast=CONFIG['cast'][line['speaker']]
    digest=hashlib.sha256(json.dumps([cast['voice_id'],payload(line)],sort_keys=True).encode()).hexdigest()
    return CACHE/(digest+'.mp3'), CACHE/(digest+'.json')

def duration(path):
    return float(subprocess.check_output(['ffprobe','-v','error','-show_entries','format=duration','-of','default=nw=1:nk=1',str(path)],text=True))

def generate(line):
    raw, metadata=cache_paths(line)
    cast=CONFIG['cast'][line['speaker']]
    if not raw.exists():
        req=urllib.request.Request('https://api.elevenlabs.io/v1/text-to-speech/'+cast['voice_id']+'?output_format=mp3_44100_192',
            data=json.dumps(payload(line)).encode(),headers={'xi-api-key':key(),'Content-Type':'application/json'})
        # No automatic paid retries. A failed request is reported for inspection.
        try:
            with urllib.request.urlopen(req, timeout=180) as response:
                data=response.read()
                info={'request_id':response.headers.get('request-id'),
                      'characters':int(response.headers.get('x-character-count',len(line['text'])))}
        except urllib.error.HTTPError as e:
            raise RuntimeError(f"{line['path']}: HTTP {e.code}: {e.read().decode()[:300]}") from None
        if len(data)<1000: raise RuntimeError('Unexpectedly short audio response: '+line['path'])
        raw.with_suffix('.tmp').write_bytes(data)
        raw.with_suffix('.tmp').replace(raw)
        metadata.write_text(json.dumps(info))
    dest=ROOT/'Assets/_Project/Resources'/(line['path']+'.ogg')
    dest.parent.mkdir(parents=True, exist_ok=True)
    filters=['highpass=f=75']
    if line['speaker']=='Morrow': filters += ['highpass=f=170','lowpass=f=6500']
    if line['speaker']=='Azhrakel': filters += ['asetrate=39690','aresample=44100','atempo=1.08','aecho=0.7:0.4:95:0.16']
    filters += ['loudnorm=I=-19:TP=-2:LRA=9','aresample=44100']
    temp=dest.with_suffix('.pending.ogg')
    subprocess.run(['ffmpeg','-v','error','-y','-i',str(raw),'-af',','.join(filters),'-ac','1','-c:a','libvorbis','-q:a','5',str(temp)],check=True)
    repair(temp)  # FFmpeg 8 can overstate the length; Unity then warns the clip is truncated
    seconds=duration(temp)
    if not 0.2 < seconds < 120: raise RuntimeError('Invalid audio duration: '+line['path'])
    temp.replace(dest)
    report={**line,'voice_id':cast['voice_id'],'voice_name':cast['voice_name'],'model':CONFIG['model_id'],
        'duration':round(seconds,3),'source_sha256':hashlib.sha256(raw.read_bytes()).hexdigest(),
        'audio_sha256':hashlib.sha256(dest.read_bytes()).hexdigest()}
    with LOCK: print(f"OK {line['speaker']} {dest.stem} {seconds:.1f}s",flush=True)
    return report

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--estimate',action='store_true'); parser.add_argument('--generate',action='store_true')
    parser.add_argument('--limit',type=int)
    args=parser.parse_args()
    sub=get('user/subscription')
    remaining=sub['character_limit']-sub['character_count']
    lines=LINES[:args.limit] if args.limit else LINES
    needed=sum(len(l['text']) for l in lines if not cache_paths(l)[0].exists())
    print(json.dumps({'lines':len(lines),'characters':sum(len(l['text']) for l in lines),
        'uncached_characters':needed,'remaining_credits':remaining,'speakers':sorted(set(l['speaker'] for l in lines))}),flush=True)
    if not args.generate: return
    if needed > min(remaining,CONFIG['max_credits']): raise RuntimeError('Batch exceeds available/authorized credit budget.')
    available={v['voice_id'] for v in get('voices')['voices']}
    assert all(c['voice_id'] in available for c in CONFIG['cast'].values()), 'Cast contains an unavailable voice'
    CACHE.mkdir(parents=True,exist_ok=True)
    results=[]
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        for result in pool.map(generate,lines): results.append(result)
    manifest={'model':CONFIG['model_id'],'lines':results,'subscription_after':{
        k:v for k,v in get('user/subscription').items() if k in ('character_count','character_limit','tier')}}
    (ROOT/'tools/voice/recordings.json').write_text(json.dumps(manifest,indent=2)+'\n')
    print('COMPLETE '+str(len(results))+' recordings',flush=True)

if __name__=='__main__': main()
