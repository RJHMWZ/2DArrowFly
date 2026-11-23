"""Rebalance levels 11-100 offline, preserving the first ten puzzles and gameplay rules.

No runtime generation or ads are introduced. Every board has a certified solution.
"""
from __future__ import annotations
import argparse
import copy
import json
import random
from pathlib import Path
import BuildCampaign as author

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / 'Assets/HueRelay/Resources/HueRelay/Data/Levels.json'
# title, design goal, width, height, arrows min/max, hints, erasers, extra, slack
PROFILES = [
    ('First Connections', '保留原入门布局和步数', 5, 6, 4, 8, 1, 1, 0, 4),
    ('Read the Routes', '提前引入三色和有代价的出手顺序', 6, 8, 10, 14, 1, 0, 0, 2),
    ('Three Colors', '交叉阻挡并收紧容错', 7, 9, 14, 19, 1, 0, 0, 1),
    ('Crossed Paths', '必须保留同色连锁桥梁', 8, 10, 19, 24, 1, 0, 0, 0),
    ('Find the Gate', '多层门控和精确顺序', 9, 12, 24, 28, 1, 0, 0, 0),
    ('Plan the Order', '密集多色依赖', 9, 12, 28, 30, 1, 0, 0, 0),
    ('Layered Locks', '多个看似可行的入口', 9, 12, 30, 30, 1, 0, 0, 0),
    ('Chain Strategy', '更高的顺序选择代价', 9, 12, 30, 30, 1, 0, 0, 0),
    ('Precision Play', '连锁与阻挡的组合规划', 9, 12, 30, 30, 1, 0, 0, 0),
    ('Master Relay', '严格最短路线挑战', 9, 12, 30, 30, 1, 0, 0, 0),
]
MIN_GAP = [0, .8, 1.6, 2.1, 2.6, 3.1, 3.6, 4.1, 4.6, 5.1]
MAX_CLEAR_RATE = [1, .85, .65, .5, .35, .25, .20, .15, .10, .05]

def board_for(level):
    owners = {(c['x'],c['y']):i for i,a in enumerate(level['a']) for c in a['cells']}
    blockers = []
    for a in level['a']:
        x,y=a['cells'][-1]['x'],a['cells'][-1]['y']
        dx,dy=author.DIRECTIONS[a['d']]; blocked=0
        x+=dx; y+=dy
        while 0<=x<level['w'] and 0<=y<level['h']:
            if (x,y) in owners: blocked |= 1 << owners[x,y]
            x+=dx; y+=dy
        blockers.append(blocked)
    return author.Board(level['a'],blockers)

def measure(level, samples=128):
    board=board_for(level)
    rng=random.Random(level['seed'] ^ 0xC04123)
    lengths=[len(board.rollout(rng)) for _ in range(samples)]
    return dict(randomClearRate=round(sum(n<=level['moves'] for n in lengths)/samples,4),
                randomMeanMoves=round(sum(lengths)/samples,3), rolloutSamples=samples)

def validate(level):
    author.valid_shape(level)
    board=board_for(level); mask=board.full
    assert level['moves']>=level['par']==len(level['solution'])
    for selected in level['solution']:
        assert selected in board.free(mask), (level['id'],selected)
        mask=board.chain(mask,selected)
    assert mask==0, level['id']

def build(source,seed):
    original=json.loads(source.read_text(encoding='utf-8'))
    levels=copy.deepcopy(original['levels'][:10])
    for level in levels:
        level['design'].update(measure(level))
    signatures={author.geometry_signature(l) for l in levels}
    author.CHAPTERS=PROFILES
    for chapter in range(1,10):
        spec=PROFILES[chapter]
        for slot in range(10):
            number=chapter*10+slot+1
            count=round(spec[4]+(spec[5]-spec[4])*slot/9)
            # Increase order loss within each chapter, not just visual board size.
            gap_target=MIN_GAP[chapter]+slot*.035
            options=[]
            for attempt in range(6000):
                c=author.candidate(seed+number*10000+attempt,chapter,count,palette='ABC',search_budget=8000)
                if c is None: continue
                design=c['design']
                if design['orderGap']<gap_target or design['depth']<min(3+chapter//2,7): continue
                if len({a['c'] for a in c['a']})!=3: continue
                signature=author.geometry_signature(c)
                if signature in signatures: continue
                stats=measure(c,64)
                if stats['randomClearRate']>MAX_CLEAR_RATE[chapter]: continue
                # Structural score is an audit measure, not a promise that every
                # individual puzzle is harder for every player than the last.
                score=count*.5+design['orderGap']*10+design['depth']*.3+design['turns']*.03-spec[9]*5
                c.update(id=number,title=f'{spec[0]} {slot+1:02}',chapter=chapter+1)
                design.update(stats,structureScore=round(score,3))
                options.append(c)
                if len(options)>=5: break
            if not options:
                raise RuntimeError(f'No verified board for {number}; gap>={gap_target}')
            level=min(options,key=lambda l:l['design']['structureScore'])
            level['difficulty']=round(levels[9]['difficulty']+(100-levels[9]['difficulty'])*(number-10)/90,3)
            validate(level)
            levels.append(level); signatures.add(author.geometry_signature(level))
            print(f"L{number}: arrows={count}, moves={level['par']}+{spec[9]}, gap={level['design']['orderGap']}, "
                  f"random clear={level['design']['randomClearRate']:.1%}, score={level['design']['structureScore']}",flush=True)
    for before,after in zip(original['levels'][:10],levels[:10]):
        for key in ('a','w','h','par','moves','solution','guidedChains'):
            assert before.get(key)==after.get(key), (before['id'],key)
    for level in levels:
        validate(level)
        for key in ('hints','erasers','extraMoves'): level.pop(key,None)
    return levels

def report(levels,seed):
    lines=['# Hue Relay：100 关难度调整', '',
        '前 10 关保留布局、标准解、教学和步数。道具已改为全局持久库存，首次启动每种赠送 3 个，跨关卡和重试不重置；初始数量及广告奖励数量统一配置于 Assets/HueRelay/Resources/HueRelay/Data/PowerUps.asset。关卡数据不再配置道具配额。', '',
        '第 11 关起重选三色布局，更早增加箭头数、阻挡依赖及顺序选择代价；31 关起步数上限等于已证明的标准最短解。所有关卡可不使用道具通关，箭头上限维持 30。', '',
        '难度字段是关卡进度标尺，不是玩家通关率。下表的随机通关率是离线合法随机点击、等待连锁完成后的采样结果，不代表真人或并行点击通关率；“三天”目标仍须用玩家试玩、每关耗时和重试数据校准。', '',
        '| 关卡 | 棋盘 | 箭头 | 最短步数 | 上限 | 依赖深度 | 顺序损失 | 随机通关率（64 次；前十128次） |',
        '|---|---|---|---|---|---|---|---|']
    for l in levels:
        d=l['design']
        lines.append(f"| {l['id']} | {l['w']}×{l['h']} | {len(l['a'])} | {l['par']} | {l['moves']} | "
                     f"{d['depth']} | {d['orderGap']:.2f} | {d['randomClearRate']:.1%} |")
    lines+=['','生成：`python Tools/HueRelay/RebalanceCampaign.py --seed '+str(seed)+'`。固定种子、首十关保留和几何去重检查；新关卡使用现有 A* 求解器证明最短解。',
        '关卡版本改为 `hue100_v2`，避免将旧布局最高分混入新布局。旧最高分数据保留，不删除。',
        '此前 Campaign100Preview.png 为旧版预览，本次不作为新关卡截图。','']
    (ROOT/'Tools/HueRelay/Campaign100.md').write_text('\n'.join(lines),encoding='utf-8')

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--seed',type=int,default=2026112308)
    parser.add_argument('--source',type=Path,default=DATA)
    args=parser.parse_args()
    levels=build(args.source,args.seed)
    # Do not touch shipped data until all 100 levels pass validation.
    DATA.write_text('{\n  "campaignId":"hue100_v2",\n  "levels":[\n'
        +',\n'.join('    '+json.dumps(l,separators=(',',':')) for l in levels)+'\n  ]\n}\n',encoding='utf-8')
    report(levels,args.seed)

if __name__=='__main__': main()
