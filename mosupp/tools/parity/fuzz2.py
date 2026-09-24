import random
random.seed(99)
def num(): return random.choice(['0','-0','1e3','.5','5.','+3','nan','inf','-inf','abc','','  7 ','1_0','0x10', str(random.uniform(-1e4,1e4)), str(random.randint(-200000,200000)), '2147483648', '-2147483649','131073'])
for k in range(300):
    L=[]
    if random.random()<0.9: L.append(random.choice(['osu file format v14','osu file format v5','osu file format v7','osu file format vX','﻿osu file format v12','garbage header','']))
    L += ['[General]', random.choice(['StackLeniency: 0.3','StackLeniency:nan','Mode: 0','Mode: 00','StackLeniency: 1e1']), '[Difficulty]']
    for key in ['HPDrainRate','CircleSize','OverallDifficulty','ApproachRate','SliderMultiplier','SliderTickRate']:
        if random.random()<0.8: L.append(f"{key}:{random.choice([num(), str(random.uniform(0,11))])}")
    L += ['[Events]', '2,1000,5000', f'Break,{num()},{num()}', '//comment', '3,100,1,2,3']
    L += ['[TimingPoints]']
    t=0
    for _ in range(random.randint(1,12)):
        L.append(f"{random.choice([t, num()])},{random.choice(['300','-100','-50','nan','-nan','0','-0','500',num()])},{random.choice(['4','0','-1',''])},2,0,50,{random.choice(['1','0','','2'])},{random.choice(['0','1','9','x',''])}")
        t+=random.randint(0,5000)
    L += ['[HitObjects]']
    tt=0
    for i in range(random.randint(0,400)):
        tt+=random.choice([0,1,10,50,100,200,500, -5])
        x,y=random.randint(-600,1200),random.randint(-600,1200)
        r=random.random()
        if r<0.35: L.append(f"{x},{y},{tt},{random.choice(['1','5','17','1 '])},{random.choice(['0','2','x'])},{random.choice(['0:0:0:0:','1:2:3:4:file.wav','','a:b'])}")
        elif r<0.85:
            kinds=['L','P','B','C','B3','X','','p']
            pts='|'.join(random.choice([f"{random.randint(-700,1300)}:{random.randint(-700,1300)}", f"{x}:{y}", f"{random.randint(0,512)}:{random.randint(0,384)}", random.choice(kinds), '5', '1:2:3']) for _ in range(random.randint(0,9)))
            kind=random.choice(kinds)
            L.append(f"{x},{y},{tt},{random.choice(['2','6','2'])},0,{kind}|{pts},{random.choice(['1','2','0','-3','9001','3','40'])},{random.choice(['',num(),'100','0','-50','99999'])},{random.choice(['0|2|8','','x|y'])},0:0|0:0,0:0:0:0:")
        elif r<0.95: L.append(f"256,192,{tt},{random.choice(['8','12'])},0,{random.choice([tt+random.randint(-100,20000), num()])},0:0:0:0:")
        else: L.append(random.choice(['garbage','1,2','1,2,3,4','1,2,3,128,0,500:0:0:0:0:','1,2,3,0,0']))
    sep=random.choice(['\n','\r\n'])
    open(f'/home/claude/fuzz2/g{k:03d}.osu','w',encoding='utf-8').write(sep.join(L)+sep)
out=[]
import glob
for f in sorted(glob.glob('/home/claude/fuzz2/*.osu')):
    for m in ['L:0','L:128','L:208','Z:RX,CL','L:1034']:
        out.append('\t'.join([f,m,'-',random.choice(['-','98']),'-','-','-',random.choice(['-','1']),'-','-','-','-','-','-','-','-','-','-']))
open('/home/claude/harness/fuzz2_cases.tsv','w').write('\n'.join(out)+'\n')
