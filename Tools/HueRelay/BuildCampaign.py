"""Deterministic offline campaign authoring and exact settled-chain verification."""
from __future__ import annotations
import argparse
import heapq
import json
import random
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DIRECTIONS = {'u': (0, -1), 'd': (0, 1), 'l': (-1, 0), 'r': (1, 0)}
CHAPTERS = [
    ('First Connections', '认识方向与同色连锁', 5, 6, 4, 8, 3, 2, 1, 4),
    ('Read the Routes', '识别转折与简单阻挡', 5, 7, 8, 11, 3, 2, 1, 3),
    ('Three Colors', '加入第三种颜色和多色顺序', 6, 8, 11, 14, 3, 1, 1, 3),
    ('Crossed Paths', '交叉射线与分支释放', 7, 9, 14, 17, 2, 1, 1, 2),
    ('Find the Gate', '寻找关键入口和门控箭头', 7, 9, 17, 20, 2, 1, 1, 2),
    ('Plan the Order', '避免提前拆掉连锁桥梁', 8, 10, 20, 23, 2, 1, 0, 2),
    ('Layered Locks', '分层阻挡与多段连锁', 8, 11, 23, 25, 2, 1, 0, 1),
    ('Chain Strategy', '在多个入口之间选择顺序', 9, 12, 25, 27, 1, 1, 0, 1),
    ('Precision Play', '更密集布局和有限容错', 9, 12, 27, 29, 1, 0, 0, 1),
    ('Master Relay', '综合运用连锁并挑战标准最短解', 9, 12, 29, 30, 1, 0, 0, 0),
]

def peel_order(blockers):
    mask = (1 << len(blockers)) - 1
    result = []
    while mask:
        previous = mask
        for i, blocked in enumerate(blockers):
            if mask & (1 << i) and not (blocked & mask):
                result.append(i)
                mask &= ~(1 << i)
        if previous == mask:
            return None
    return result

def generate_layout(rng, w, h, count, chapter):
    occupied = {}
    ray_owners = [0] * (w * h)
    arrows, blockers = [], []
    max_length = 4 if chapter < 3 else 5
    straight_bias = .82 if chapter < 3 else .63
    for attempt in range(count * 150):
        if len(arrows) == count:
            return arrows, blockers
        x, y = rng.randrange(w), rng.randrange(h)
        if (x, y) in occupied:
            continue
        name = rng.choice(tuple(DIRECTIONS))
        dx, dy = DIRECTIONS[name]
        ray = []
        rx, ry = x + dx, y + dy
        while 0 <= rx < w and 0 <= ry < h:
            ray.append((rx, ry)); rx += dx; ry += dy
        ray_set = set(ray)
        bx, by = x - dx, y - dy
        if not (0 <= bx < w and 0 <= by < h) or (bx, by) in occupied:
            continue
        cells = [(x, y), (bx, by)]
        used = set(cells)
        length = (rng.choices((2, 3, 4, 5), (4, 4, 2, 1))[0]
                  if chapter >= 6 else rng.randint(2, max_length))
        if len(occupied) > w*h*.62:
            length = rng.randint(2, 3)
        previous = (-dx, -dy)
        for _ in range(length - 2):
            choices = [(ex, ey) for ex, ey in DIRECTIONS.values()
                       if 0 <= bx+ex < w and 0 <= by+ey < h
                       and (bx+ex, by+ey) not in occupied
                       and (bx+ex, by+ey) not in used and (bx+ex, by+ey) not in ray_set]
            if not choices:
                break
            ex, ey = previous if previous in choices and rng.random() < straight_bias else rng.choice(choices)
            bx += ex; by += ey
            cells.append((bx, by)); used.add((bx, by)); previous = ex, ey
        bit = 1 << len(arrows)
        graph = list(blockers)
        affected = 0
        for cx, cy in cells:
            affected |= ray_owners[cy*w+cx]
        for i in range(len(arrows)):
            if affected & (1 << i): graph[i] |= bit
        blocked = 0
        for cell in ray:
            if cell in occupied: blocked |= 1 << occupied[cell]
        graph.append(blocked)
        if peel_order(graph) is None:
            continue
        i = len(arrows)
        cells.reverse()
        arrows.append({'c': 'B', 'd': name, 'cells': [{'x': cx, 'y': cy} for cx, cy in cells]})
        blockers = graph
        for cell in cells: occupied[cell] = i
        for cx, cy in ray: ray_owners[cy*w+cx] |= bit
    return None

class Board:
    def __init__(self, arrows, blockers):
        self.arrows, self.blockers = arrows, blockers
        self.n = len(arrows)
        self.full = (1 << self.n) - 1
        self.colors = [a['c'] for a in arrows]
        self.color_masks = {c: sum(1 << i for i, col in enumerate(self.colors) if col == c) for c in set(self.colors)}

    def free(self, mask):
        return [i for i, b in enumerate(self.blockers) if mask & (1 << i) and not (mask & b)]

    def chain(self, mask, i):
        queue = [i]
        for k in queue:
            mask &= ~(1 << k)
            for j, blocked in enumerate(self.blockers):
                if mask & (1 << j) and blocked & (1 << k) and not (blocked & mask) and self.colors[j] == self.colors[k]:
                    queue.append(j)
        return mask

    def mandatory(self, mask):
        # An arrow with no remaining blocker of its own color cannot be auto-launched.
        return sum(1 for i, b in enumerate(self.blockers) if mask & (1 << i)
                   and not (b & mask & self.color_masks[self.colors[i]]))

    def rollout(self, rng, weighted=False):
        mask, path = self.full, []
        while mask:
            choices = [(i, self.chain(mask, i)) for i in self.free(mask)]
            if not choices: raise ValueError('Deadlocked board')
            if weighted:
                item = rng.choices(choices, weights=[(mask.bit_count()-m.bit_count())**2 for _, m in choices])[0]
            else:
                item = rng.choice(choices)
            i, mask = item
            path.append(i)
        return path

    def exact_solution(self, rng, budget=40000):
        rolls = [self.rollout(rng, k >= 20) for k in range(32)]
        solution = min(rolls, key=len)
        upper = len(solution)
        lower = self.mandatory(self.full)
        if lower == upper:
            return solution, rolls, 0
        heap = [(lower, self.n, 0, self.full)]
        cost = {self.full: 0}
        parent = {}
        expanded = 0
        while heap:
            bound, _, g, mask = heapq.heappop(heap)
            if bound >= upper: return solution, rolls, expanded
            if cost.get(mask) != g: continue
            expanded += 1
            if expanded > budget: return None
            actions = sorted(((self.chain(mask, i), i) for i in self.free(mask)), key=lambda x: x[0].bit_count())
            for after, i in actions:
                ng = g + 1
                if cost.get(after, self.n+1) <= ng: continue
                h = self.mandatory(after)
                if ng + h >= upper: continue
                cost[after] = ng; parent[after] = (mask, i)
                if after == 0:
                    solution = []
                    current = 0
                    while current != self.full:
                        current, selected = parent[current]
                        solution.append(selected)
                    solution.reverse(); upper = len(solution)
                else:
                    heapq.heappush(heap, (ng+h, after.bit_count(), ng, after))
        return solution, rolls, expanded

def candidate(seed, chapter, count):
    title, goal, w, h, _, _, hints, erasers, extra, slack = CHAPTERS[chapter]
    rng = random.Random(seed)
    layout = generate_layout(rng, w, h, count, chapter)
    if layout is None: return None
    arrows, blockers = layout
    palette = 'BC' if chapter < 2 else 'ABC'
    order = peel_order(blockers)
    for i in order:
        blocked_colors = [arrows[j]['c'] for j in range(count) if blockers[i] & (1 << j)]
        arrows[i]['c'] = rng.choice(blocked_colors) if blocked_colors and rng.random() < .7 else rng.choice(palette)
    board = Board(arrows, blockers)
    solved = board.exact_solution(rng)
    if solved is None: return None
    path, rolls, expanded = solved
    depth = [0] * count
    for i in order:
        depth[i] = 1 + max((depth[j] for j in range(count) if blockers[i] & (1 << j)), default=0)
    turns = sum(sum((a['cells'][j]['x']-a['cells'][j-1]['x'], a['cells'][j]['y']-a['cells'][j-1]['y'])
                    != (a['cells'][j-1]['x']-a['cells'][j-2]['x'], a['cells'][j-1]['y']-a['cells'][j-2]['y'])
                    for j in range(2,len(a['cells']))) for a in arrows)
    edges = sum(m.bit_count() for m in blockers)
    free = len(board.free(board.full))
    gap = sum(len(p) for p in rolls[:20])/20 - len(path)
    first_chain = max(board.n-board.chain(board.full,i).bit_count() for i in board.free(board.full))
    multi = sum(b.bit_count() >= 2 for b in blockers)
    score = count*1.8 + max(depth)*2 + (1-free/count)*5 + gap*6 + turns/count*3 + multi*.35 + len(path)*.5 - slack*2 - hints*.4 - erasers*2 - extra*1.5
    return {'w':w, 'h':h, 'a':arrows, 'par':len(path), 'moves':len(path)+slack,
            'hints':hints, 'erasers':erasers, 'extraMoves':extra, 'solution':path,
            'seed':seed, 'difficulty':round(score,3),
            'design':{'depth':max(depth),'edges':edges,'multiBlockers':multi,'initialChoices':free,
                      'openingChain':first_chain,'turns':turns,'orderGap':round(gap,3),'searchStates':expanded}}

def valid_shape(level):
    occupied = set()
    for a in level['a']:
        cells = [(c['x'],c['y']) for c in a['cells']]
        assert len(cells) >= 2 and len(cells) == len(set(cells))
        for cell in cells:
            assert 0 <= cell[0] < level['w'] and 0 <= cell[1] < level['h'] and cell not in occupied
            occupied.add(cell)
        for p,q in zip(cells,cells[1:]): assert abs(p[0]-q[0])+abs(p[1]-q[1]) == 1
        assert (cells[-1][0]-cells[-2][0],cells[-1][1]-cells[-2][1]) == DIRECTIONS[a['d']]

def geometry_signature(level):
    """Ignore colors, arrow IDs and reflection/180-degree duplicates."""
    versions=[]
    for sx,sy in ((1,1),(-1,1),(1,-1),(-1,-1)):
        arrows=[]
        for a in level['a']:
            cells=tuple((c['x'] if sx==1 else level['w']-1-c['x'],
                         c['y'] if sy==1 else level['h']-1-c['y']) for c in a['cells'])
            arrows.append(cells)
        versions.append(tuple(sorted(arrows)))
    return (level['w'],level['h'],min(versions))

def apply_guided_openings(levels):
    """Every permitted opening move launches at least two arrows; no singleton endgame."""
    layouts = [
        [('B','r',[(3,1),(4,1)]), ('B','r',[(0,1),(1,1)]),
         ('C','l',[(1,4),(0,4)]), ('C','l',[(4,4),(3,4)])],
        [('B','u',[(4,2),(4,1)]), ('B','r',[(1,2),(2,2)]),
         ('B','d',[(1,0),(1,1)]), ('B','l',[(3,0),(2,0)])],
        [('B','r',[(3,0),(4,0)]), ('B','u',[(3,3),(3,2)]),
         ('C','l',[(1,4),(0,4)]), ('C','d',[(1,1),(1,2)]),
         ('C','l',[(4,1),(3,1),(2,1)])]
    ]
    for level, layout, route in zip(levels[:3], layouts, [[0,2],[0],[2,0]]):
        level['a'] = [dict(c=c,d=d,cells=[dict(x=x,y=y) for x,y in cells]) for c,d,cells in layout]
        level.update(guidedChains=True, par=len(route), moves=len(route)+4, solution=route)
        valid_shape(level)
        owners = {(c['x'],c['y']):i for i,a in enumerate(level['a']) for c in a['cells']}
        blockers = []
        for i,a in enumerate(level['a']):
            x,y=a['cells'][-1]['x'],a['cells'][-1]['y'];dx,dy=DIRECTIONS[a['d']];mask=0
            x+=dx;y+=dy
            while 0<=x<level['w'] and 0<=y<level['h']:
                if (x,y) in owners: mask |= 1<<owners[x,y]
                x+=dx;y+=dy
            blockers.append(mask)
        board=Board(level['a'],blockers)
        pending=[board.full];seen=set()
        while pending:
            mask=pending.pop()
            if mask==0 or mask in seen: continue
            seen.add(mask)
            choices=[board.chain(mask,i) for i in board.free(mask) if mask.bit_count()-board.chain(mask,i).bit_count()>=2]
            assert choices, f"Guided dead end in level {level['id']}"
            pending.extend(choices)
        settled=board.full
        for i in route:
            after=board.chain(settled,i)
            assert i in board.free(settled) and settled.bit_count()-after.bit_count()>=2
            settled=after
        assert settled==0
        level['design'].update(edges=sum(b.bit_count() for b in blockers),
            multiBlockers=sum(b.bit_count()>1 for b in blockers), initialChoices=len(board.free(board.full)),
            openingChain=max(board.n-board.chain(board.full,i).bit_count() for i in board.free(board.full)),
            depth=max(_depth(i,blockers) for i in range(board.n)), turns=0)

def _depth(i,blockers):
    return 1+max((_depth(j,blockers) for j in range(len(blockers)) if blockers[i]&(1<<j)),default=0)

def write_campaign(levels,seed):
    apply_guided_openings(levels)
    # Keep the raw authoring score for auditing and expose a readable 1–100 scale.
    raw=[l['design'].get('structureScore',l['difficulty']) for l in levels]
    signatures=set()
    for l,score in zip(levels,raw):
        valid_shape(l)
        signature=geometry_signature(l)
        assert signature not in signatures, f"Duplicate geometry at {l['id']}"
        signatures.add(signature)
        l['design']['structureScore']=score
        l['difficulty']=round(1+99*(score-raw[0])/(raw[-1]-raw[0]),3)
    target=ROOT/'Assets/HueRelay/Resources/HueRelay/Data/Levels.json'
    text='{\n  "campaignId":"hue100_v1",\n  "levels":[\n'+',\n'.join('    '+json.dumps(l,separators=(',',':')) for l in levels)+'\n  ]\n}\n'
    target.write_text(text,encoding='utf-8',newline='\r\n')
    write_report(levels,seed)

def write_report(levels,seed):
    lines=['# Hue Relay：100 关初版设计','','所有关卡均提供不使用道具的标准连锁通关解。基准步数由离线 A* 求解器证明；难度分由箭头数量、依赖深度、多重阻挡、折线、随机出手的步数损失和道具余量共同评估，并映射到 1–100 分。','','[查看 Unity 实际渲染的代表关卡预览](Campaign100Preview.png)','','## 章节安排','','| 章节 | 关卡 | 主题 | 棋盘 | 箭头范围 | 提示 / 擦除 / 加步数 | 步数余量 |','|---|---|---|---|---|---|---|']
    for ch,spec in enumerate(CHAPTERS):
        lines.append(f'| {ch+1} | {ch*10+1}–{ch*10+10} | {spec[1]} | {spec[2]}×{spec[3]} | {spec[4]}–{spec[5]} | {spec[6]} / {spec[7]} / {spec[8]} | +{spec[9]} |')
    lines+=['','## 每关参数','','道具数量按每次进入/重试本关重置；加步数每次增加 3 步。`par` 是所有自动连锁结算后再出手的标准模型最短解，不限制玩家实际进行并行点击。难度分是初版结构评估，后续可结合试玩数据调节。','','| 关卡 | 棋盘 | 箭头 | 基准步数 | 步数上限 | 提示 | 擦除 | 加步数 | 依赖层数 | 顺序损失均值 | 难度分 |','|---|---|---|---|---|---|---|---|---|---|---|']
    for l in levels:
        lines.append(f"| {l['id']} | {l['w']}×{l['h']} | {len(l['a'])} | {l['par']} | {l['moves']} | {l['hints']} | {l['erasers']} | {l['extraMoves']} | {l['design']['depth']} | {l['design']['orderGap']:.2f} | {l['difficulty']:.2f} |")
    lines+=['','## 可复现与验收','','- 生成：`python Tools/HueRelay/BuildCampaign.py`。固定种子 '+str(seed)+'。','- 运行数据：`Assets/HueRelay/Resources/HueRelay/Data/Levels.json`；每关含坐标、规则、标准解和评估指标。','- 每关独立布局，不以旋转或换色复制凑数；坐标不重叠、路径连续、无自阻挡，箭头数不超过 30。','- 游戏第 100 关后进入原有无尽模式。TT 配置与 SDK 设置不参与本次修改。','']
    (ROOT/'Tools/HueRelay/Campaign100.md').write_text('\n'.join(lines),encoding='utf-8',newline='\r\n')

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--seed',type=int,default=20261118100)
    parser.add_argument('--probe',action='store_true')
    parser.add_argument('--refresh',action='store_true',help='Refresh normalization/report from existing authored levels.')
    args = parser.parse_args()
    started = time.monotonic()
    if args.refresh:
        levels=json.loads((ROOT/'Assets/HueRelay/Resources/HueRelay/Data/Levels.json').read_text(encoding='utf-8'))['levels']
        write_campaign(levels,args.seed)
        return
    if args.probe:
        for chapter in (0,3,6,9):
            values = []
            for k in range(12):
                c = candidate(args.seed+chapter*10000+k,chapter,CHAPTERS[chapter][5])
                if c: values.append((c['par'],c['difficulty'],c['design']))
            print(chapter+1,values,'seconds',round(time.monotonic()-started,2),flush=True)
        return
    levels=[]
    fingerprints=set()
    for chapter, spec in enumerate(CHAPTERS):
        for slot in range(10):
            number=chapter*10+slot+1
            count=round(spec[4]+(spec[5]-spec[4])*slot/9)
            options=[]
            for attempt in range(512):
                seed=args.seed+number*1000+attempt
                c=candidate(seed,chapter,count)
                if c is None:continue
                fingerprint=json.dumps(c['a'],sort_keys=True)
                if fingerprint in fingerprints:continue
                if chapter >= 3 and c['design']['depth'] < 3:continue
                if chapter >= 6 and c['design']['depth'] < 4:continue
                if chapter >= 8 and (c['design']['depth'] < 5 or c['design']['orderGap'] < .6):continue
                if chapter >= 2 and len({a['c'] for a in c['a']}) != 3:continue
                if chapter == 0 and slot >= 3 and c['design']['openingChain'] < 2:continue
                if c['design']['initialChoices'] < 2:continue
                if levels and c['difficulty'] <= levels[-1]['difficulty']+.05:continue
                if levels:
                    max_rise = 5.5 if count > len(levels[-1]['a']) or slot == 0 else 3.5
                    if c['difficulty'] > levels[-1]['difficulty'] + max_rise:continue
                options.append(c)
                if len(options)>=8:break
            if not options:raise RuntimeError(f'Cannot find a certified increasing-difficulty board for level {number} ({count} arrows).')
            level=min(options,key=lambda c:c['difficulty'])
            level.update(id=number,title=f'{spec[0]} {slot+1:02}',chapter=chapter+1)
            fingerprints.add(json.dumps(level['a'],sort_keys=True))
            valid_shape(level)
            levels.append(level)
            print(f"Level {number:03}: {count} arrows, par {level['par']}, moves {level['moves']}, difficulty {level['difficulty']}, depth {level['design']['depth']}, gap {level['design']['orderGap']}",flush=True)
    write_campaign(levels,args.seed)
    print('Saved 100 levels and design report in',round(time.monotonic()-started,2),'seconds.',flush=True)

if __name__=='__main__':main()
