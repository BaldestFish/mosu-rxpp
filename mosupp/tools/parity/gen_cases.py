import glob, os, random, sys
random.seed(42)
maps = []
# usage: python3 gen_cases.py full|quick <dir with .osu files> [more dirs...] > cases.tsv
dirs = sys.argv[2:]
for f in sorted(sum((glob.glob(os.path.join(d, '**', '*.osu'), recursive=True) for d in dirs), [])):
    try:
        txt = open(f, 'rb').read().decode('utf-8', 'replace')
    except Exception:
        continue
    mode = '0'
    for line in txt.splitlines():
        if line.startswith('Mode'):
            mode = line.split(':',1)[1].strip()[:1]
            break
    if mode == '0':
        maps.append(f)
mods_list = ['L:0','L:128','L:136','L:192','L:208','L:130','L:1152','L:1224','L:320','L:24','L:8192','L:584',
             'Z:RX,DT=1.3','Z:RX,CL','Z:RX,CL=false','Z:RX,DA=ar:9.5;cs:4.2;od:8','Z:RX,MR','Z:RX,MR=2','Z:TC,RX','Z:RX,MG=0.5','Z:BL,RX','Z:RX,HR,NC=1.2','Z:RX,HD=true,FL','Z:RX,DF=5,FL','Z:HD,DT,CL']
full = sys.argv[1] == 'full'
out = []
def row(path, mods, clock='-', acc='-', n300='-', n100='-', n50='-', miss='-', combo='-', lazer='-', passed='-', large='-', ends='-', small='-', legacy='-', ar='-', od='-', cs='-'):
    out.append('\t'.join(map(str,[path,mods,clock,acc,n300,n100,n50,miss,combo,lazer,passed,large,ends,small,legacy,ar,od,cs])))
for m in maps:
    ms = mods_list if full else mods_list[:3]
    for mods in ms:
        row(m, mods)
        row(m, mods, acc=97.53, miss=3, combo=random.randint(0, 800))
        row(m, mods, n100=random.randint(0,40), n50=random.randint(0,10), miss=random.randint(0,5), combo=random.randint(0,1500))
        row(m, mods, acc=95.0, lazer='false', miss=2, legacy=random.randint(1000000, 90000000), combo=random.randint(100,900))
        row(m, mods, passed=random.randint(1, 400), acc=99.1)
        row(m, mods, acc=93.2, n300=random.randint(0,600), large=random.randint(0,30), ends=random.randint(0,200), small=random.randint(0,100))
    row(m, 'L:128', clock=1.37, ar=10.3, od=9.1, cs=4.4, acc=98.0)
    row(m, 'L:192', ar=9.0, od=7.5, acc=99.0, miss=1)
print('\n'.join(out))
print(len(maps), file=sys.stderr)
