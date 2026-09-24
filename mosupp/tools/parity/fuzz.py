import random, os
random.seed(7)
os.makedirs('/home/claude/fuzz', exist_ok=True)
def rnd_pt():
    return f"{random.randint(-50,560)}:{random.randint(-50,430)}"
for k in range(400):
    ver = random.choice([3,4,5,6,7,8,9,10,11,12,13,14,14,14])
    lines = [f"osu file format v{ver}", "", "[General]", f"StackLeniency: {random.choice([0.2,0.5,0.7,1.0])}", "Mode: 0", "",
             "[Difficulty]", f"HPDrainRate:{random.uniform(0,10):.1f}", f"CircleSize:{random.uniform(0,10):.1f}",
             f"OverallDifficulty:{random.uniform(0,10):.1f}"]
    if random.random() < 0.8: lines.append(f"ApproachRate:{random.uniform(0,10):.1f}")
    lines += [f"SliderMultiplier:{random.choice([0.4,1.0,1.4,1.8,2.2,3.6])}", f"SliderTickRate:{random.choice([0.5,1,2,3,4])}", "", "[Events]"]
    t = random.randint(0, 2000)
    lines.append("0,0,\"bg.jpg\",0,0")
    lines += ["", "[TimingPoints]"]
    tp_t = 0
    bl = random.choice([250, 300, 333.33, 400, 500, 187.5])
    lines.append(f"{tp_t},{bl},4,2,0,50,1,0")
    for _ in range(random.randint(0, 15)):
        tp_t += random.randint(500, 20000)
        if random.random() < 0.3:
            lines.append(f"{tp_t},{random.choice([200,250,300,400,166.67])},4,2,0,50,1,{random.choice([0,1])}")
        else:
            lines.append(f"{tp_t},{-random.choice([25,50,75,100,133.33,200,400,1000,5000])},4,2,0,50,0,{random.choice([0,1])}")
    lines += ["", "[HitObjects]"]
    n = random.randint(2, 900)
    x, y = random.randint(0,512), random.randint(0,384)
    stacky = random.random() < 0.3
    for i in range(n):
        t += random.choice([random.randint(40, 400), 75, 150, 150, 300, 0 if random.random()<0.02 else 10])
        if not (stacky and random.random() < 0.5):
            x, y = random.randint(0,512), random.randint(0,384)
        r = random.random()
        if r < 0.5:
            lines.append(f"{x},{y},{t},1,0,0:0:0:0:")
        elif r < 0.93:
            kind = random.choice("LPBCBP")
            npts = {'L': random.randint(1,4), 'P': random.choice([2,2,2,3]), 'B': random.randint(1,8), 'C': random.randint(1,6)}[kind]
            pts = [rnd_pt() for _ in range(npts)]
            if kind == 'B' and random.random() < 0.4 and len(pts) > 2:
                pts.insert(random.randint(1,len(pts)-1), pts[random.randint(0,len(pts)-1)])  # red anchor
            if random.random() < 0.1: kind = 'B' + str(random.randint(2,4))
            reps = random.choice([1,1,1,2,3,5])
            length = random.choice(['', f"{random.uniform(10,600):.2f}", f"{random.uniform(10,600):.2f}", "0"])
            line = f"{x},{y},{t},2,0,{kind}|{'|'.join(pts)},{reps}"
            if length != '': line += f",{length}"
            lines.append(line)
            t += random.randint(100, 1500)
        else:
            e = t + random.randint(200, 5000)
            lines.append(f"256,192,{t},12,0,{e},0:0:0:0:")
            t = e
    open(f'/home/claude/fuzz/f{k:03d}.osu','w').write('\n'.join(lines)+'\n')
