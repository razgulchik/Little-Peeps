"""Biome population layered over the unchanged shape generator.

Reservations are prototype access markers, not final mine/mill/bridge footprints.
Rivers currently have underground endpoints so expansion never buries a sea outlet.
"""
from collections import Counter
from dataclasses import dataclass, field
import random

from island_shapes import Island, DIRS, adjacent, connected, narrow_land, runs


@dataclass(frozen=True)
class Profile:
    open_percent: int
    mountain_percent: int
    river_percent: int
    object_percent: int

    def budgets(self, area):
        return tuple(area * p // 100 for p in
                     (self.mountain_percent, self.river_percent, self.object_percent))


BIOMES = ('Grasslands', 'Stone desert', 'Forest')
PROFILES = {
    'Starting Grasslands': Profile(80, 0, 0, 20),
    'Grasslands': Profile(50, 0, 35, 15),
    'Stone desert': Profile(45, 35, 5, 15),
    'Forest': Profile(45, 0, 20, 35),
}
BIOME_COLORS = {'Grasslands': '#a6ca82', 'Stone desert': '#d2bf94', 'Forest': '#769e70'}
FEATURE_COLORS = {'tree': '#235739', 'berry': '#ae365f', 'stone': '#858d98',
                  'boar_den': '#825235', 'fox_den': '#d47935', 'mountain': '#515c70',
                  'river': '#338ac3', 'house': '#eedab1'}
SYMBOLS = {'tree': 'T', 'berry': 'B', 'stone': 'S', 'boar_den': 'D', 'fox_den': 'F',
           'mountain': 'M', 'river': 'R', 'house': 'H'}


def harvest_multiplier(source, profession='basic', tired=False):
    specialist = {'berry': 'farmer', 'tree': 'lumberjack', 'stone': 'miner'}.get(source)
    if tired or specialist is None or profession not in ('basic', specialist):
        return 0
    return 2 if source == 'berry' and profession == 'farmer' else 1


@dataclass
class SectionContent:
    biome: str
    land: frozenset
    starting: bool = False
    objects: dict = field(default_factory=dict)
    mountains: set = field(default_factory=set)
    river: tuple = ()
    endpoints: dict = field(default_factory=dict)
    house: set = field(default_factory=set)
    clearing: set = field(default_factory=set)
    reserved: set = field(default_factory=set)
    bridges: dict = field(default_factory=dict)  # river cell -> two clear bank cells
    mine_access: set = field(default_factory=set)
    mill_access: set = field(default_factory=set)
    den_exits: dict = field(default_factory=dict)

    @property
    def profile(self):
        return PROFILES['Starting Grasslands' if self.starting else self.biome]

    @property
    def natural(self):
        return set(self.objects) | self.mountains | set(self.river)

    @property
    def blocked(self):
        return self.natural | self.house

    @property
    def open_cells(self):
        # The approved budget counts natural obstructions, not the starting house.
        return len(self.land) - len(self.natural)


def combined_blocked(contents):
    return set().union(*(c.blocked for c in contents)) if contents else set()


def planned_bridges(contents):
    return set().union(*(set(c.bridges) for c in contents)) if contents else set()


def access_ok(land, contents):
    """Potential connectivity after building the marked bridges, not current access."""
    walkable = land - combined_blocked(contents) | planned_bridges(contents)
    if not connected(walkable):
        return False
    return all(any(q in walkable for q in adjacent(p))
               for c in contents for p in c.objects)


def centre_order(cells):
    cx = sum(x for x, y in cells) / len(cells)
    cy = sum(y for x, y in cells) / len(cells)
    return sorted(cells, key=lambda p: ((p[0]-cx)**2 + (p[1]-cy)**2, p))


def squares(cells, size):
    return [{(x+i, y+j) for i in range(size) for j in range(size)}
            for x, y in centre_order(cells)
            if all((x+i, y+j) in cells for i in range(size) for j in range(size))]


def reserve_future_gates(content, previous, land):
    """Any future three-edge join must contain at least one accessible land cell.

    Reserve gaps rather than clearing resources retroactively when land expands.
    The shape generator's minimum join can be larger than three, but never
    smaller under the standard rules; two-edge custom joins are handled too.
    """
    old_land = land - content.land
    old_walk = old_land - combined_blocked(previous) | planned_bridges(previous)
    for dx, dy in DIRS:
        lines = {}
        for x, y in sorted(land):
            if (x+dx, y+dy) not in land:
                line, value = (x, y) if dx else (y, x)
                lines.setdefault(line, []).append(value)
        for line, values in lines.items():
            for start, end in runs(values):
                for value in range(start, end):
                    # Reserve at least one free cell per pair. This also supports
                    # the existing configurable land-width minimum of two.
                    pair = [(line, v) if dx else (v, line) for v in (value, value+1)]
                    if any(p in old_walk or p in content.reserved-content.house for p in pair):
                        continue
                    candidates = [p for p in pair if p in content.land and p not in content.house]
                    if candidates:
                        content.reserved.add(candidates[0])


def river_banks(path, land, blocked):
    result = {}
    for i in range(1, len(path)-1):
        x, y = path[i]
        before, after = path[i-1], path[i+1]
        if before[0] == after[0] == x:
            banks = ((x-1, y), (x+1, y))
        elif before[1] == after[1] == y:
            banks = ((x, y-1), (x, y+1))
        else:
            continue
        if all(p in land and p not in blocked for p in banks):
            result[(x, y)] = banks
    return result


def add_mountains(rng, content, previous, land, cap):
    if cap < 4:
        return
    available = set(content.land) - content.reserved
    seeds = squares(available, 2)
    rng.shuffle(seeds)
    for seed in seeds[:48]:
        patch = set(seed)
        target = rng.randint(max(4, cap * 2 // 3), cap)
        while len(patch) < target:
            frontier = {q for p in patch for q in adjacent(p) if q in available and q not in patch}
            if not frontier:
                break
            # Strongly favour compact clusters; validate thickness after growing.
            ranked = sorted(frontier)
            weights = [1 + 5 * sum(q in patch for q in adjacent(p)) for p in ranked]
            patch.add(rng.choices(ranked, weights=weights, k=1)[0])
        if narrow_land(patch, 2):
            continue
        content.mountains = patch
        if not access_ok(land, previous + [content]):
            content.mountains = set()
            continue
        approaches = {q for p in patch for q in adjacent(p)
                      if q in content.land and q not in content.blocked}
        if not approaches:
            content.mountains = set()
            continue
        content.mine_access = set(centre_order(approaches)[:2])
        content.reserved.update(content.mine_access)
        return


def add_river(rng, content, previous, land, cap):
    if cap < 4 or rng.random() > {'Grasslands': 0.8, 'Forest': 0.65, 'Stone desert': 0.3}[content.biome]:
        return
    old_rivers = set().union(*(set(c.river) for c in previous)) if previous else set()
    available = set(content.land) - content.reserved - content.mountains
    available = {p for p in available if not any(q in old_rivers for q in adjacent(p))}
    if len(available) < 4:
        return
    for _ in range(100):
        target = rng.randint(4, cap)
        path = [rng.choice(sorted(available))]
        while len(path) < target:
            choices = [p for p in adjacent(path[-1]) if p in available and p not in path
                       and sum(q in path for q in adjacent(p)) == 1]
            if not choices:
                break
            weights = [3 if len(path) > 1 and (p[0]-path[-1][0], p[1]-path[-1][1]) ==
                       (path[-1][0]-path[-2][0], path[-1][1]-path[-2][1]) else 1 for p in choices]
            path.append(rng.choices(choices, weights=weights, k=1)[0])
        if len(path) < 4:
            continue
        content.river = tuple(path)
        blocked = combined_blocked(previous + [content])
        bank_options = river_banks(path, land, blocked)
        # Only reserve new-section bank sites: expansion must not alter old content.
        bank_options = {p: banks for p, banks in bank_options.items() if all(q in content.land for q in banks)}
        if not bank_options:
            content.river = ()
            continue
        content.bridges = dict(bank_options)
        if not access_ok(land, previous + [content]):
            content.river, content.bridges = (), {}
            continue
        # Keep one optional crossing or the minimum sampled set needed for connectivity.
        for p in sorted(bank_options):
            if len(content.bridges) == 1:
                break
            banks = content.bridges.pop(p)
            if not access_ok(land, previous + [content]):
                content.bridges[p] = banks
        mill_options = [q for p, banks in bank_options.items() if p not in content.bridges for q in banks]
        if not mill_options:
            content.river, content.bridges = (), {}
            continue
        content.mill_access = {rng.choice(sorted(set(mill_options)))}
        content.reserved.update(content.mill_access)
        content.reserved.update(q for banks in content.bridges.values() for q in banks)
        content.endpoints = {path[0]: 'underground', path[-1]: 'underground'}
        return


def place_object(rng, kind, content, previous, land, clustered=False):
    available = sorted(set(content.land) - content.reserved - content.blocked)
    rng.shuffle(available)
    peers = [p for p, k in content.objects.items() if k == kind]
    if peers:
        available.sort(key=lambda p: min(abs(p[0]-q[0])+abs(p[1]-q[1]) for q in peers), reverse=not clustered)
    for p in available:
        content.objects[p] = kind
        if not access_ok(land, previous + [content]):
            del content.objects[p]
            continue
        if kind.endswith('_den'):
            exits = [q for q in adjacent(p) if q in content.land and q not in content.blocked]
            if not exits:
                del content.objects[p]
                continue
            exit_cell = rng.choice(exits)
            content.den_exits[p] = exit_cell
            content.reserved.add(exit_cell)
        return True
    return False


def populate(seed, index, biome, section, previous):
    if biome not in BIOMES:
        raise ValueError(f'Unknown biome: {biome}')
    if index == 0 and biome != 'Grasslands':
        raise ValueError('The starting biome must be Grasslands.')
    land = set(section).union(*(set(c.land) for c in previous))
    old_land = land - section
    clearings = squares(section, 3)
    if not clearings:
        raise ValueError('Section has no 3x3 clearing; cannot populate it without changing shape.')
    for attempt in range(32):
        rng = random.Random(f'{seed}:resources:{index}:{biome}:{attempt}')
        content = SectionContent(biome, frozenset(section), starting=index == 0)
        content.clearing = set(clearings[attempt % len(clearings)])
        content.reserved.update(content.clearing)
        # Preserve every boundary cell that connects this section to older land.
        content.reserved.update(p for p in section if any(q in old_land for q in adjacent(p)))
        if content.starting:
            house_options = squares(content.clearing, 2)
            content.house = set(house_options[attempt % len(house_options)])
            content.reserved.update(q for p in content.house for q in adjacent(p) if q in section)
        reserve_future_gates(content, previous, land)
        if not access_ok(land, previous + [content]):
            continue
        mountain_cap, river_cap, object_cap = content.profile.budgets(len(section))
        add_mountains(rng, content, previous, land, mountain_cap)
        add_river(rng, content, previous, land, river_cap)
        if content.starting:
            kinds = ['berry', 'tree', 'berry', 'tree']
            if object_cap >= 5:
                kinds.append('berry')
            if object_cap >= 6:
                kinds.append('tree')
        else:
            target = rng.randint(max(1, object_cap * 2 // 3), object_cap) if object_cap else 0
            if biome == 'Stone desert':
                kinds = ['stone'] * target
            elif biome == 'Grasslands':
                kinds = ['berry' if n % 2 == 0 else 'tree' for n in range(target)]
            else:
                kinds = [kind for kind in ('boar_den', 'fox_den') if rng.random() < 0.4]
                kinds += ['berry'] * rng.randint(1, 2)
                if rng.random() < 0.45:
                    kinds.append('stone')
                kinds = kinds[:target] + ['tree'] * max(0, target-len(kinds))
        for kind in kinds:
            place_object(rng, kind, content, previous, land,
                         clustered=biome == 'Forest' or kind in ('berry', 'stone'))
        counts = Counter(content.objects.values())
        if content.starting and (counts['berry'] < 2 or counts['tree'] < 2):
            continue
        validate_content(content)
        return content
    raise ValueError(f'Could not populate section {index}; shape and previous resources were preserved.')


def validate_content(c):
    mountain_cap, river_cap, object_cap = c.profile.budgets(len(c.land))
    assert len(c.mountains) <= mountain_cap and len(c.river) <= river_cap and len(c.objects) <= object_cap
    assert c.open_cells * 100 >= len(c.land) * c.profile.open_percent
    assert c.natural <= c.land and c.reserved <= c.land and c.house <= c.reserved
    assert not (c.natural & c.reserved)
    assert len(c.natural) == len(c.mountains) + len(c.river) + len(c.objects)
    allowed = {'Grasslands': {'tree', 'berry'}, 'Stone desert': {'stone'},
               'Forest': {'tree', 'berry', 'stone', 'boar_den', 'fox_den'}}[c.biome]
    assert set(c.objects.values()) <= allowed
    assert not c.mountains or (c.biome == 'Stone desert' and connected(c.mountains) and not narrow_land(c.mountains, 2))
    if c.river:
        assert len(c.river) >= 4 and len(c.river) == len(set(c.river))
        assert set(c.endpoints) == {c.river[0], c.river[-1]}
        for i, p in enumerate(c.river):
            expected = set(c.river[max(0, i-1):i] + c.river[i+1:i+2])
            assert set(adjacent(p)) & set(c.river) == expected
        assert c.bridges and c.mill_access
        assert c.bridges.items() <= river_banks(c.river, c.land, c.blocked).items()
    assert c.mine_access <= c.reserved and c.mill_access <= c.reserved
    assert all(any(q in c.mountains for q in adjacent(p)) for p in c.mine_access)
    assert all(p in c.objects and c.objects[p].endswith('_den') and q in adjacent(p)
               and q in c.reserved and q not in c.blocked for p, q in c.den_exits.items())
    if c.starting:
        counts = Counter(c.objects.values())
        assert counts['berry'] >= 2 and counts['tree'] >= 2 and len(c.house) == 4
        assert not c.mountains and not c.river


class ResourceWorld:
    def __init__(self, seed=1, rules=None):
        self.island = Island(seed, rules)
        self.contents = [populate(seed, 0, 'Grasslands', self.island.sections[0], [])]
        self.history = []

    def expand(self, biome):
        if biome not in BIOMES:
            raise ValueError(f'Unknown biome: {biome}')
        state = self.island.rng.getstate(), self.island.last_attempts, self.island.last_fill
        old_count = len(self.island.sections)
        try:
            added = self.island.expand()
            content = populate(self.island.seed, old_count, biome, added, self.contents)
        except Exception:
            del self.island.sections[old_count:]
            self.island.rng.setstate(state[0])
            self.island.last_attempts, self.island.last_fill = state[1:]
            raise
        self.contents.append(content)
        self.history.append(state)
        return added

    def undo(self):
        if not self.history:
            return
        state, self.island.last_attempts, self.island.last_fill = self.history.pop()
        self.contents.pop()
        self.island.sections.pop()
        self.island.rng.setstate(state)

    def validate(self):
        assert len(self.contents) == len(self.island.sections)
        for section, c in zip(self.island.sections, self.contents):
            assert section == c.land
            validate_content(c)
        assert access_ok(self.island.cells, self.contents)
        rivers = [set(c.river) for c in self.contents]
        assert all(not any(q in rivers[j] for p in rivers[i] for q in adjacent(p))
                   for i in range(len(rivers)) for j in range(i))

    def features(self):
        result = {}
        for c in self.contents:
            result.update({p: 'mountain' for p in c.mountains})
            result.update({p: 'river' for p in c.river})
            result.update(c.objects)
            result.update({p: 'house' for p in c.house})
        return result

    def walkable(self, with_bridges=False):
        ground = self.island.cells - combined_blocked(self.contents)
        return ground | planned_bridges(self.contents) if with_bridges else ground


def main():
    import argparse
    from island_shapes import Rules
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', type=int, default=20)
    parser.add_argument('--steps', type=int, default=6)
    parser.add_argument('--seed', type=int, default=0)
    parser.add_argument('--area', type=int, default=48)
    parser.add_argument('--start-area', type=int, default=36)
    args = parser.parse_args()
    if args.check < 1 or args.steps < 0:
        parser.error('Check count must be positive; steps must be nonnegative.')
    features = Counter()
    failures = []
    for seed in range(args.seed, args.seed+args.check):
        try:
            world = ResourceWorld(seed, Rules(start_area=args.start_area, section_area=args.area))
            world.validate()
            for step in range(args.steps):
                world.expand(BIOMES[step % 3])
                world.validate()
            features.update(world.features().values())
        except (ValueError, AssertionError) as exc:
            failures.append(f'Seed {seed}: {type(exc).__name__}: {exc}')
    print(f'{args.check} seeds, {args.steps} expansions each: {len(failures)} failures. Features: {dict(features)}')
    for failure in failures:
        print(failure)
    raise SystemExit(1 if failures else 0)


if __name__ == '__main__':
    main()
