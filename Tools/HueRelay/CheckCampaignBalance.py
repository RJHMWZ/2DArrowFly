"""Offline route checks and independent random-policy comparison, without spending items."""
import argparse
import json
import random
from pathlib import Path
from RebalanceCampaign import DATA, MIN_GAP, board_for, validate
from BuildCampaign import geometry_signature

def stats(levels,seed,samples):
    result=[]
    for level in levels:
        board=board_for(level)
        rng=random.Random(seed+level['id'])
        lengths=[len(board.rollout(rng)) for _ in range(samples)]
        greedy=[]
        for _ in range(128):
            mask=board.full; moves=0
            while mask:
                choices=[board.chain(mask,i) for i in board.free(mask)]
                remaining=min(m.bit_count() for m in choices)
                mask=rng.choice([m for m in choices if m.bit_count()==remaining]); moves+=1
            greedy.append(moves)
        result.append(dict(id=level['id'],mean=sum(lengths)/samples,
                           clear=sum(n<=level['moves'] for n in lengths)/samples,
                           loss=sum(lengths)/samples-level['par'],
                           greedy=sum(n<=level['moves'] for n in greedy)/len(greedy)))
    return result

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--before',type=Path,help='Optional previous campaign JSON for preservation/comparison checks.')
    args=parser.parse_args()
    before=json.loads(args.before.read_text(encoding='utf-8'))['levels'] if args.before else None
    levels=json.loads(DATA.read_text(encoding='utf-8'))['levels']
    assert len(levels)==100
    for old,new in zip(before[:10] if before else [],levels[:10]):
        for key in ('w','h','a','par','solution','guidedChains') + (('moves',) if new['id'] < 4 else ()):
            assert old.get(key)==new.get(key),(new['id'],key)
    signatures=set()
    for i,l in enumerate(levels):
        validate(l)
        signature=geometry_signature(l)
        assert signature not in signatures,l['id']
        signatures.add(signature)
        assert all(key not in l for key in ('hints','erasers','extraMoves')), 'Inventory must be global.'
        if i>=3:
            assert l['moves']==l['par']<len(l['a']), (l['id'], 'Budget must require chains.')
        if i>=10:
            assert len({a['c'] for a in l['a']})==3
            assert l['design']['orderGap']>=MIN_GAP[i//10]+(i%10)*.035,l['id']
    old_stats=stats(before,0xA02123,512) if before else None
    new_stats=stats(levels,0xA02123,512)
    print('PASS: 100 unique valid routes; inventory removed from level data; increasing order-loss selection thresholds.'
          + (' First ten layouts/routes and first three teaching budgets preserved.' if before else ''))
    print('Independent 512-rollout legal random policy per level; these are NOT human pass rates:')
    print('Levels | old/new arrows avg | old/new order loss avg | old/new random clear rate | old/new greedy clear rate (128 trials)'
          if before else 'Levels | arrows avg | order loss avg | random clear rate | greedy clear rate (128 trials)')
    for start in range(0,100,10):
        b=new_stats[start:start+10]
        a=old_stats[start:start+10] if old_stats else b
        mean=lambda rows,key:sum(r[key] for r in rows)/len(rows)
        new_count=sum(len(l['a']) for l in levels[start:start+10])/10
        old_count=sum(len(l['a']) for l in before[start:start+10])/10 if before else new_count
        if before:
            print(f'{start+1}-{start+10} | {old_count:.1f}/{new_count:.1f} | '
                  f'{mean(a,"loss"):.2f}/{mean(b,"loss"):.2f} | {mean(a,"clear"):.1%}/{mean(b,"clear"):.1%} | '
                  f'{mean(a,"greedy"):.1%}/{mean(b,"greedy"):.1%}')
        else:
            print(f'{start+1}-{start+10} | {new_count:.1f} | {mean(b,"loss"):.2f} | '
                  f'{mean(b,"clear"):.1%} | {mean(b,"greedy"):.1%}')
        if before and start>=10 and any(x['a']!=y['a'] for x,y in zip(before[start:start+10],levels[start:start+10])):
            assert mean(b,'loss')>mean(a,'loss')
            assert mean(b,'clear')<mean(a,'clear')

if __name__=='__main__':main()
