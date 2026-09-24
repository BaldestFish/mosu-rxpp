import sys, math
names = ("stars aim speed fl slider_factor speed_note_count aim_diff_slider_count aim_diff_strain speed_diff_strain aim_tw_slider speed_tw_slider "
         "ar great ok meh hp n_circles n_sliders n_large_ticks n_spinners max_combo nested_score legacy_base_mult max_legacy_combo_score | "
         "pp pp_aim pp_speed pp_acc pp_fl eff_miss speed_dev combo_miss score_miss aim_sb speed_sb zero | "
         "st_combo n300 n100 n50 miss large ends small").split()
names = [n for n in names if n != '|']
a = open(sys.argv[1]).read().splitlines()
b = open(sys.argv[2]).read().splitlines()
cases = open(sys.argv[3]).read().splitlines()
cases = [c for c in cases if c.strip() and not c.startswith('#')]
tol = float(sys.argv[4]) if len(sys.argv) > 4 else 1e-9
worst = {}
bad = 0
exact = 0
for i, (x, y) in enumerate(zip(a, b)):
    xs = [t for t in x.split() if t != '|']
    ys = [t for t in y.split() if t != '|']
    line_bad = False
    all_exact = True
    for j, (u, v) in enumerate(zip(xs, ys)):
        fu, fv = float(u), float(v)
        if math.isnan(fu) and math.isnan(fv): continue
        if fu == fv: continue
        all_exact = False
        if math.isnan(fu) or math.isnan(fv) or math.isinf(fu) or math.isinf(fv):
            rel = float('inf')
        else:
            rel = abs(fu - fv) / max(abs(fu), abs(fv), 1e-300)
        if rel > worst.get(names[j], (0,))[0]:
            worst[names[j]] = (rel, i, u, v)
        if rel > tol: line_bad = True
    if all_exact: exact += 1
    if line_bad:
        bad += 1
        if bad <= 8:
            print('MISMATCH case', i, cases[i][:160])
            for j, (u, v) in enumerate(zip(xs, ys)):
                if u != v and not (u in ('NaN','nan') and v in ('NaN','nan')): print('   ', names[j], u, v)
print(f'cases={len(a)} bit-exact={exact} bad(>{tol})={bad}')
for k, v in sorted(worst.items(), key=lambda kv: -kv[1][0])[:15]:
    print(f'  {k:24s} maxrel={v[0]:.3e} case={v[1]} rust={v[2]} cs={v[3]}')
