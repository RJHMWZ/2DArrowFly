"""Re-author only levels 4-11 with exact solutions and costly order choices."""
import copy
import json
import random
from pathlib import Path
import BuildCampaign as author
from RebalanceCampaign import DATA, board_for, measure, validate


def greedy_rate(level, samples=128):
    board = board_for(level)
    rng = random.Random(level['seed'] ^ 0x137A)
    wins = 0
    for _ in range(samples):
        mask, moves = board.full, 0
        while mask:
            choices = [board.chain(mask, i) for i in board.free(mask)]
            smallest = min(m.bit_count() for m in choices)
            mask = rng.choice([m for m in choices if m.bit_count() == smallest])
            moves += 1
        wins += moves <= level['moves']
    return wins / samples


def main():
    original = json.loads(DATA.read_text(encoding='utf-8'))
    levels = copy.deepcopy(original['levels'])
    signatures = {author.geometry_signature(l) for l in levels if not 4 <= l['id'] <= 11}
    author.CHAPTERS[1] = ('Chain Planning', 'Preserve chain bridges', 6, 8, 9, 10, 0, 0, 0, 0)
    for number in range(4, 12):
        count = 9 if number < 8 else 10
        best = None
        for attempt in range(12000):
            level = author.candidate(2026112400 + number * 20000 + attempt, 1, count, palette='ABC', search_budget=8000)
            if level is None or level['design']['depth'] < 4 or level['design']['orderGap'] < 1.5:
                continue
            if not 2 <= level['design']['initialChoices'] <= 5 or len({a['c'] for a in level['a']}) != 3:
                continue
            if not 3 <= level['par'] <= count - 3:
                continue
            signature = author.geometry_signature(level)
            if signature in signatures:
                continue
            stats = measure(level, 256)
            if stats['randomClearRate'] > .125:
                continue
            greedy = greedy_rate(level)
            if greedy > .25:
                continue
            level['design'].update(stats, greedyClearRate=greedy)
            best = level
            break
        if best is None:
            raise RuntimeError(f'No qualifying challenge for level {number}; shipped data is untouched.')
        old = levels[number - 1]
        best.update(id=number, chapter=old['chapter'], title=f'Chain Planning {number:02}', difficulty=old['difficulty'])
        for key in ('hints', 'erasers', 'extraMoves'):
            best.pop(key, None)
        validate(best)
        levels[number - 1] = best
        signatures.add(author.geometry_signature(best))
        print(f"L{number}: {count} arrows, {best['moves']} moves, depth {best['design']['depth']}, "
              f"random={stats['randomClearRate']:.1%}, greedy={greedy:.1%}, seed={best['seed']}", flush=True)
    for i, level in enumerate(levels):
        validate(level)
        if not 3 <= i <= 10:
            assert level == original['levels'][i]
    # Only replace lines for the intended boards; keep file GUID, campaign saves and other levels.
    lines = DATA.read_text(encoding='utf-8').splitlines(keepends=True)
    for i, line in enumerate(lines):
        if not line.lstrip().startswith('{"w"'):
            continue
        old = json.loads(line.strip().rstrip(','))
        if 4 <= old['id'] <= 11:
            lines[i] = '    ' + json.dumps(levels[old['id'] - 1], separators=(',', ':')) + ',\n'
    DATA.write_text(''.join(lines), encoding='utf-8')
    print('PASS: 4-11 replaced; all 100 routes valid; other levels and campaign save namespace preserved.', flush=True)


if __name__ == '__main__':
    main()
