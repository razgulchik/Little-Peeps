"""Standalone island shape experiment. Python 3.9+, standard library only.

Run without arguments for the Tk grid viewer; --check 100 for a seed sweep.
Coordinates are stable. Every accepted expansion only adds cells.
"""
import argparse
from collections import deque
from dataclasses import dataclass
import math
from pathlib import Path
import random
import time


DIRS = ((1, 0), (-1, 0), (0, 1), (0, -1))
COLORS = ("#8dcf9b", "#f1c27d", "#8ebee8", "#d7a7d9", "#e99c91", "#b6cb77", "#a6a3e3", "#78c9c0")


def adjacent(cell):
    x, y = cell
    return [(x + dx, y + dy) for dx, dy in DIRS]


def bounds(cells):
    return (min(x for x, y in cells), min(y for x, y in cells),
            max(x for x, y in cells), max(y for x, y in cells))


def connected(cells):
    if not cells:
        return False
    seen = {next(iter(cells))}
    queue = deque(seen)
    while queue:
        for p in adjacent(queue.popleft()):
            if p in cells and p not in seen:
                seen.add(p)
                queue.append(p)
    return len(seen) == len(cells)


def holes(cells):
    x0, y0, x1, y1 = bounds(cells)
    x0, y0, x1, y1 = x0 - 1, y0 - 1, x1 + 1, y1 + 1
    ocean = {(x0, y0)}
    queue = deque(ocean)
    while queue:
        for x, y in adjacent(queue.popleft()):
            if x0 <= x <= x1 and y0 <= y <= y1 and (x, y) not in cells and (x, y) not in ocean:
                ocean.add((x, y))
                queue.append((x, y))
    return {(x, y) for x in range(x0, x1 + 1) for y in range(y0, y1 + 1)
            if (x, y) not in cells and (x, y) not in ocean}


def runs(values):
    values = sorted(values)
    if not values:
        return
    start = previous = values[0]
    for value in values[1:]:
        if value != previous + 1:
            yield start, previous
            start = value
        previous = value
    yield start, previous


def narrow_land(cells, width):
    """Conservative axis-aligned feature test; also rejects skinny tip cells."""
    for axis in (0, 1):
        lines = {}
        for p in cells:
            lines.setdefault(p[axis], []).append(p[1 - axis])
        for values in lines.values():
            if any(b - a + 1 < width for a, b in runs(values)):
                return True
    return False


def pocket_cells(cells, min_bay, depth_ratio):
    """Grid approximation, not a physics guarantee.

    A bay slice is water bracketed by land on opposite sides. Reject slices
    narrower than min_bay. If land closes one perpendicular end, distance
    to that end may be at most depth_ratio * slice width. Through-channels
    without a closed end are subject only to the width rule.
    """
    x0, y0, x1, y1 = bounds(cells)
    result = set()
    for axis in (0, 1):
        lines = {}
        for p in cells:
            lines.setdefault(p[axis], []).append(p[1 - axis])
        for line, values in lines.items():
            spans = list(runs(values))
            for (_, end), (start, _) in zip(spans, spans[1:]):
                width = start - end - 1
                for value in range(end + 1, start):
                    p = (line, value) if axis == 0 else (value, line)
                    if width < min_bay:
                        result.add(p)
                        continue
                    depths = []
                    for direction in (-1, 1):
                        distance = 1
                        while True:
                            q = (p[0] + direction * distance, p[1]) if axis == 0 else (p[0], p[1] + direction * distance)
                            if not (x0 <= q[0] <= x1 and y0 <= q[1] <= y1):
                                break
                            if q in cells:
                                depths.append(distance)
                                break
                            distance += 1
                    if depths and min(depths) > width * depth_ratio:
                        result.add(p)
    return result


@dataclass(frozen=True)
class Rules:
    start_area: int = 36
    section_area: int = 48
    min_width: int = 3
    min_bay: int = 4
    depth_ratio: float = 0.75
    attempts: int = 240

    @staticmethod
    def area_limits(target):
        """Hard +/-10% bounds, rounded inward to whole cells."""
        allowance = target // 10
        return target - allowance, target + allowance

    def validate(self):
        if self.min_width < 2 or self.min_bay < 2 or self.depth_ratio <= 0:
            raise ValueError("Widths must be at least 2 and bay depth ratio must be positive.")
        if min(self.start_area, self.section_area) < (self.min_width + 2) ** 2:
            raise ValueError("Area targets must be at least (minimum land width + 2) squared.")
        if self.attempts < 1:
            raise ValueError("Candidate attempts must be positive.")


def normalized(cells):
    """Translation-independent, stable ordering for comparisons and seeded selection."""
    x0, y0, _, _ = bounds(cells)
    return tuple(sorted((x - x0, y - y0) for x, y in cells))


def orientations(cells):
    """Unique quarter-turn orientations; rotations do not get extra lottery entries."""
    result = set()
    for _ in range(4):
        result.add(normalized(cells))
        cells = {(-y, x) for x, y in cells}
    return sorted(result)


def silhouette_key(cells):
    return orientations(cells)[0]


def orient_mass(rng, cells):
    chosen = rng.choice(orientations(cells))
    _, _, x1, y1 = bounds(chosen)
    return {(x - (x1 + 1) // 2, y - (y1 + 1) // 2) for x, y in chosen}


def make_mass(rng, area, width):
    """A compact, asymmetric chamfered rectangle; no shoreline noise."""
    # Reserve some area for the cells removed by corner clipping.
    envelope_area = area + max(4, round(area * 0.12))
    w = max(width + 2, round(math.sqrt(envelope_area) * rng.uniform(0.85, 1.3)))
    h = max(width + 2, round(envelope_area / w))
    # Each corner may stay square or cut deeper. Do not divide the available
    # margin equally between corners: validate the actual finished land instead.
    cut_limit = max(1, min(w, h) - width)
    cuts = [rng.randint(0, cut_limit) for _ in range(4)]
    if not any(cuts):
        cuts[rng.randrange(4)] = 1
    cells = {(x - w // 2, y - h // 2) for x in range(w) for y in range(h)
            if x + y >= cuts[0] and w - 1 - x + y >= cuts[1]
            and x + h - 1 - y >= cuts[2] and w - 1 - x + h - 1 - y >= cuts[3]}
    # Orientation is independent of width sampling. A candidate may still fail
    # validation or attachment, but horizontal forms have no proposal advantage.
    return orient_mass(rng, cells)


def repair(cells, rules, limit):
    cells = set(cells)
    if len(cells) > limit:
        return None
    for _ in range(16):
        fill = holes(cells) | pocket_cells(cells, rules.min_bay, rules.depth_ratio)
        if not fill:
            return cells
        cells.update(fill)
        if len(cells) > limit:
            return None
    return None


def broad_join(old, added, width):
    """Require a continuous straight shared boundary, not a count of scattered contacts."""
    for dx, dy in DIRS:
        lines = {}
        for x, y in added:
            if (x + dx, y + dy) in old:
                line, value = (x, y) if dx else (y, x)
                lines.setdefault(line, []).append(value)
        if any(b - a + 1 >= width for values in lines.values() for a, b in runs(values)):
            return True
    return False


class Island:
    def __init__(self, seed=1, rules=None):
        self.seed = seed
        self.rules = rules or Rules()
        self.rules.validate()
        self.rng = random.Random(seed)
        self.sections = []
        self.last_attempts = 0
        self.last_fill = 0
        lower, upper = self.rules.area_limits(self.rules.start_area)
        candidates = set()
        for _ in range(self.rules.attempts):
            initial = make_mass(self.rng, self.rules.start_area, self.rules.min_width)
            if (lower <= len(initial) <= upper and not narrow_land(initial, self.rules.min_width)
                    and connected(initial) and not holes(initial)
                    and not pocket_cells(initial, self.rules.min_bay, self.rules.depth_ratio)):
                candidates.add(silhouette_key(initial))
            if len(candidates) >= 24:
                break
        if not candidates:
            raise ValueError("Could not generate a start with these rules.")
        # Every distinct sampled family has one entry, regardless of area,
        # duplicate frequency or how many rotations were proposed.
        self.sections = [orient_mass(self.rng, self.rng.choice(sorted(candidates)))]

    @property
    def cells(self):
        return set().union(*self.sections)

    def expand(self):
        old = self.cells
        rules = self.rules
        lower, upper = rules.area_limits(rules.section_area)
        candidates = {}
        # Sorted traversal makes seed reproduction independent of set iteration order.
        coast = [(p, d) for p in sorted(old) for d in DIRS
                 if (p[0] + d[0], p[1] + d[1]) not in old]
        for attempt in range(rules.attempts):
            # Attachment consumes overlapping cells; vary the mass budget to compensate.
            mass_area = round(rules.section_area * self.rng.uniform(1.0, 1.35))
            mass = make_mass(self.rng, mass_area, rules.min_width)
            (x, y), (dx, dy) = self.rng.choice(coast)
            x0, y0, x1, y1 = bounds(mass)
            depth = (x1 - x0 + 1) if dx else (y1 - y0 + 1)
            offset = max(1, depth // 2 - 1)
            cx, cy = x + dx * offset, y + dy * offset
            proposal = old | {(a + cx, b + cy) for a, b in mass}
            combined = repair(proposal, rules, len(old) + upper)
            if combined is None:
                continue
            added = combined - old
            if not lower <= len(added) <= upper:
                continue
            if not connected(added) or not broad_join(old, added, rules.min_width):
                continue
            if narrow_land(combined, rules.min_width) or narrow_land(added, 2):
                continue
            # A section needs usable interior, not just a ring or ribbon of new cells.
            if not any(all((a + i, b + j) in added for i in range(rules.min_width)
                           for j in range(rules.min_width)) for a, b in added):
                continue
            bx0, by0, bx1, by1 = bounds(combined)
            box_area = (bx1 - bx0 + 1) * (by1 - by0 + 1)
            aspect = max(bx1 - bx0 + 1, by1 - by0 + 1) / min(bx1 - bx0 + 1, by1 - by0 + 1)
            # Prefer compact overall growth, without requiring a rectangular outline.
            score = len(combined) / box_area - 0.10 * aspect
            key = silhouette_key(added)
            if key not in candidates or score > candidates[key][0]:
                # Keep a compact placement for each family; duplicate proposals
                # improve placement at most, never its probability of selection.
                candidates[key] = (score, added, len(combined - proposal))
            if len(candidates) >= 10:
                break
        self.last_attempts = attempt + 1
        if not candidates:
            raise ValueError("No valid expansion found. Try another seed, larger area, or looser rules.")
        _, added, self.last_fill = candidates[self.rng.choice(sorted(candidates))]
        self.sections.append(added)
        return added


def export_svg(island, path, world=None, reservations=False, bridges=False, biome_colors=True):
    from island_resources import BIOME_COLORS, FEATURE_COLORS, SYMBOLS
    x0, y0, x1, y1 = bounds(island.cells)
    size, pad = 22, 30
    width, height = (x1 - x0 + 1) * size + pad * 2, (y1 - y0 + 1) * size + pad * 2 + 30
    parts = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}">',
             '<rect width="100%" height="100%" fill="#142d40"/>',
             f'<text x="20" y="22" fill="white" font-family="sans-serif" font-size="14">Seed {island.seed} · {len(island.sections)-1} expansions · {len(island.cells)} cells</text>']
    for i, section in enumerate(island.sections):
        color = BIOME_COLORS[world.contents[i].biome] if world and biome_colors else COLORS[i % len(COLORS)]
        for x, y in sorted(section):
            parts.append(f'<rect x="{pad+(x-x0)*size}" y="{pad+30+(y1-y)*size}" width="{size}" height="{size}" fill="{color}" stroke="#244658" stroke-width="0.7"/>')
    if world:
        for (x, y), kind in world.features().items():
            px, py = pad+(x-x0)*size, pad+30+(y1-y)*size
            inset = 1 if kind in ('mountain', 'river', 'house') else 4
            parts.append(f'<rect x="{px+inset}" y="{py+inset}" width="{size-2*inset}" height="{size-2*inset}" fill="{FEATURE_COLORS[kind]}"/>')
            if kind != 'river':
                parts.append(f'<text x="{px+size/2}" y="{py+size/2+4}" text-anchor="middle" fill="white" font-family="sans-serif" font-size="11">{SYMBOLS[kind]}</text>')
        for c in world.contents:
            if reservations:
                for x, y in c.reserved-c.house:
                    parts.append(f'<rect x="{pad+(x-x0)*size+2}" y="{pad+30+(y1-y)*size+2}" width="{size-4}" height="{size-4}" fill="none" stroke="#fff0a8"/>')
                for x, y in c.mine_access | c.mill_access:
                    parts.append(f'<circle cx="{pad+(x-x0+0.5)*size}" cy="{pad+30+(y1-y+0.5)*size}" r="3" fill="#ffcf54"/>')
            for x, y in c.endpoints:
                parts.append(f'<text x="{pad+(x-x0+0.5)*size}" y="{pad+30+(y1-y+0.5)*size+4}" text-anchor="middle" fill="white" font-size="12">E</text>')
            for (x, y), banks in c.bridges.items():
                px, py = pad+(x-x0)*size, pad+30+(y1-y)*size
                horizontal = banks[0][1] == banks[1][1]
                if reservations or bridges:
                    parts.append(f'<rect x="{px if horizontal else px+size/3}" y="{py+size/3 if horizontal else py}" width="{size if horizontal else size/3}" height="{size/3 if horizontal else size}" fill="{"#c69a61" if bridges else "#a8ecf5"}"/>')
    parts.append('</svg>')
    Path(path).write_text('\n'.join(parts), encoding='utf-8')


def check_seeds(count, steps, rules, first_seed=0):
    started = time.perf_counter()
    failures = []
    completed = 0
    for seed in range(first_seed, first_seed + count):
        try:
            island = Island(seed, rules)
            for step in range(steps + 1):
                land = island.cells
                assert connected(land), "disconnected island"
                assert not holes(land), "enclosed water"
                assert not pocket_cells(land, rules.min_bay, rules.depth_ratio), "shore pocket"
                assert not narrow_land(land, rules.min_width), "thin land"
                assert sum(map(len, island.sections)) == len(land), "overlapping sections"
                target = rules.start_area if step == 0 else rules.section_area
                lower, upper = rules.area_limits(target)
                assert lower <= len(island.sections[-1]) <= upper, "section area outside +/-10%"
                if step == steps:
                    break
                old_sections = [set(s) for s in island.sections]
                old = land
                added = island.expand()
                assert old_sections == island.sections[:-1], "modified existing section"
                assert old <= island.cells, "removed land"
                assert connected(added), "disconnected addition"
                assert broad_join(old, added, rules.min_width), "narrow join"
                completed += 1
        except (AssertionError, ValueError) as exc:
            failures.append(f"seed {seed}: {exc}")
    print(f"{count} seeds, {completed} expansions, {len(failures)} failures, {time.perf_counter()-started:.2f}s")
    for failure in failures:
        print(failure)
    return not failures


def viewer(seed, rules):
    import tkinter as tk
    from tkinter import filedialog, ttk
    from island_resources import ResourceWorld, BIOMES, BIOME_COLORS, FEATURE_COLORS, SYMBOLS

    root = tk.Tk()
    root.title("Island Shape Lab — biomes and resources")
    root.geometry("1100x800")
    toolbar = ttk.Frame(root, padding=8)
    toolbar.pack(fill='x')
    values = {}
    fields = [('Seed', seed), ('Start area', rules.start_area), ('Section area', rules.section_area),
              ('Land width', rules.min_width), ('Bay width', rules.min_bay), ('Bay depth/width', rules.depth_ratio)]
    for label, value in fields:
        ttk.Label(toolbar, text=label).pack(side='left', padx=(7, 3))
        values[label] = tk.StringVar(value=str(value))
        ttk.Entry(toolbar, textvariable=values[label], width=6).pack(side='left')
    layers = ttk.Frame(root, padding=(8, 0, 8, 8))
    layers.pack(fill='x')
    ttk.Label(layers, text='Next biome:').pack(side='left', padx=6)
    next_biome = tk.StringVar(value='Grasslands')
    ttk.Combobox(layers, textvariable=next_biome, values=BIOMES, state='readonly', width=15).pack(side='left')
    show_resources = tk.BooleanVar(value=True)
    show_biomes = tk.BooleanVar(value=True)
    show_reserved = tk.BooleanVar(value=False)
    show_bridges = tk.BooleanVar(value=False)
    for label, variable in [('Resources', show_resources), ('Biome colours', show_biomes),
                            ('Access reservations', show_reserved), ('Bridge preview', show_bridges)]:
        ttk.Checkbutton(layers, text=label, variable=variable, command=lambda: draw()).pack(side='left', padx=5)
    actions = ttk.Frame(root, padding=(8, 0, 8, 8))
    actions.pack(fill='x')
    status = tk.StringVar()
    ttk.Label(root, textvariable=status, padding=8, wraplength=1050).pack(side='bottom', fill='x')
    ttk.Label(root, text="T Tree | B Berry | S Stone | D Boar den | F Fox den | M Mountain | E Underground river end | H House footprint\n"
              "Yellow: reserved access; gold dots: mine/mill access; cyan: potential bridge. Bridge preview does not build anything.", padding=8).pack(side='bottom', fill='x')
    canvas = tk.Canvas(root, background='#142d40', highlightthickness=0)
    canvas.pack(fill='both', expand=True)
    island = None
    world = None
    hit_cells = {}

    def draw(event=None):
        canvas.delete('all')
        hit_cells.clear()
        if island is None:
            return
        x0, y0, x1, y1 = bounds(island.cells)
        cw, ch = max(200, canvas.winfo_width()), max(200, canvas.winfo_height())
        size = min(38, (cw-80)/(x1-x0+3), (ch-80)/(y1-y0+3))
        ox, oy = cw/2-(x0+x1+1)*size/2, ch/2+(y0+y1+1)*size/2
        for index, section in enumerate(island.sections):
            color = BIOME_COLORS[world.contents[index].biome] if show_biomes.get() else COLORS[index % len(COLORS)]
            for x, y in section:
                item = canvas.create_rectangle(ox+x*size, oy-(y+1)*size, ox+(x+1)*size, oy-y*size,
                                               fill=color, outline='#244658')
                hit_cells[item] = (index, (x, y))
        def mark(item, p):
            hit_cells[item] = (next(i for i, c in enumerate(world.contents) if p in c.land), p)
        if show_resources.get():
            for (x, y), kind in world.features().items():
                px, py = ox+x*size, oy-(y+1)*size
                inset = size * (0.06 if kind in ('mountain', 'river', 'house') else 0.18)
                mark(canvas.create_rectangle(px+inset, py+inset, px+size-inset, py+size-inset,
                                             fill=FEATURE_COLORS[kind], outline=''), (x, y))
                if size >= 12 and kind != 'river':
                    mark(canvas.create_text(px+size/2, py+size/2, text=SYMBOLS[kind], fill='#483c2b' if kind == 'house' else 'white',
                                            font=('Arial', max(7, int(size*0.35)), 'bold')), (x, y))
            for c in world.contents:
                for x, y in c.endpoints:
                    mark(canvas.create_text(ox+(x+0.5)*size, oy-(y+0.5)*size, text='E', fill='white',
                                            font=('Arial', max(7, int(size*0.35)), 'bold')), (x, y))
                if show_reserved.get():
                    for x, y in c.reserved-c.house:
                        mark(canvas.create_rectangle(ox+x*size+2, oy-(y+1)*size+2, ox+(x+1)*size-2, oy-y*size-2,
                                                     outline='#fff0a8'), (x, y))
                    for x, y in c.mine_access | c.mill_access:
                        px, py = ox+(x+0.5)*size, oy-(y+0.5)*size
                        mark(canvas.create_oval(px-3, py-3, px+3, py+3, fill='#ffcf54', outline=''), (x, y))
                if show_reserved.get() or show_bridges.get():
                    for (x, y), banks in c.bridges.items():
                        px, py = ox+x*size, oy-(y+1)*size
                        horizontal = banks[0][1] == banks[1][1]
                        mark(canvas.create_rectangle(px if horizontal else px+size/3, py+size/3 if horizontal else py,
                                                     px+size if horizontal else px+2*size/3, py+2*size/3 if horizontal else py+size,
                                                     fill='#c69a61' if show_bridges.get() else '#a8ecf5', outline=''), (x, y))
        # Distinguish the newest section even when palette colours eventually repeat.
        newest = island.sections[-1]
        for x, y in newest:
            for dx, dy in DIRS:
                if (x+dx, y+dy) in newest:
                    continue
                if dx:
                    px = ox+(x+(dx > 0))*size
                    canvas.create_line(px, oy-y*size, px, oy-(y+1)*size, fill='#fff0c9', width=2)
                else:
                    py = oy-(y+(dy > 0))*size
                    canvas.create_line(ox+x*size, py, ox+(x+1)*size, py, fill='#fff0c9', width=2)
        target = island.rules.start_area if len(island.sections) == 1 else island.rules.section_area
        lower, upper = island.rules.area_limits(target)
        status.set(f"Seed {island.seed}  |  {len(island.sections)-1} expansions  |  {len(island.cells)} cells  |  "
                   f"Newest: {len(newest)} cells (allowed {lower}–{upper})  |  "
                   f"{world.contents[-1].biome}: {world.contents[-1].open_cells}/{len(newest)} naturally open  |  Hover a cell for details")

    def inspect(event):
        for item in reversed(canvas.find_overlapping(event.x, event.y, event.x, event.y)):
            if item not in hit_cells:
                continue
            index, p = hit_cells[item]
            c = world.contents[index]
            feature = world.features().get(p, 'open land')
            tags = []
            if p in c.reserved: tags.append('reserved access')
            if p in c.bridges: tags.append('bridge candidate; river blocked until built')
            if p in c.mine_access: tags.append('provisional mine approach')
            if p in c.mill_access: tags.append('provisional mill approach')
            if p in c.endpoints: tags.append('underground endpoint')
            if p in c.den_exits.values(): tags.append('den exit')
            status.set(f'Section {index}: {c.biome} | cell {p} | {feature} | ' + ', '.join(tags))
            break

    def generate(delta=0):
        nonlocal island, world
        try:
            new_seed = int(values['Seed'].get()) + delta
            config = Rules(int(values['Start area'].get()), int(values['Section area'].get()),
                           int(values['Land width'].get()), int(values['Bay width'].get()), float(values['Bay depth/width'].get()))
            candidate = ResourceWorld(new_seed, config)
            world, island = candidate, candidate.island
            values['Seed'].set(str(new_seed))
            draw()
        except ValueError as exc:
            status.set(str(exc))

    def expand():
        if island is None:
            return
        root.config(cursor='watch')
        root.update_idletasks()
        try:
            world.expand(next_biome.get())
            draw()
        except ValueError as exc:
            status.set(str(exc))
        finally:
            root.config(cursor='')

    def undo():
        if world is None or not world.history:
            return
        world.undo()
        draw()

    def save():
        if island is None:
            return
        path = filedialog.asksaveasfilename(defaultextension='.svg', filetypes=[('SVG image', '*.svg')],
                                          initialfile=f'island-{island.seed}-{len(island.sections)-1}.svg')
        if path:
            export_svg(island, path, world if show_resources.get() else None,
                       show_reserved.get(), show_bridges.get(), show_biomes.get())
            status.set(f"Saved {path}")

    for label, command in [('Generate', generate), ('Previous seed', lambda: generate(-1)),
                           ('Next seed', lambda: generate(1)), ('Expand', expand), ('Undo expansion', undo), ('Save SVG', save)]:
        ttk.Button(actions, text=label, command=command).pack(side='left', padx=3)
    canvas.bind('<Configure>', draw)
    canvas.bind('<Motion>', inspect)
    generate()
    root.mainloop()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--seed', type=int, default=1)
    parser.add_argument('--check', type=int, metavar='SEEDS')
    parser.add_argument('--steps', type=int, default=6)
    parser.add_argument('--svg', type=Path, help='Export the selected seed after --steps expansions; no GUI')
    parser.add_argument('--start-area', type=int, default=36)
    parser.add_argument('--area', type=int, default=48)
    parser.add_argument('--land-width', type=int, default=3)
    parser.add_argument('--bay-width', type=int, default=4)
    parser.add_argument('--bay-depth', type=float, default=0.75)
    args = parser.parse_args()
    rules = Rules(args.start_area, args.area, args.land_width, args.bay_width, args.bay_depth)
    rules.validate()
    if args.steps < 0 or (args.check is not None and args.check < 1):
        parser.error('Steps must be nonnegative and check count must be positive.')
    if args.check is not None:
        raise SystemExit(0 if check_seeds(args.check, args.steps, rules, args.seed) else 1)
    if args.svg:
        island = Island(args.seed, rules)
        for _ in range(args.steps):
            island.expand()
        export_svg(island, args.svg)
    else:
        viewer(args.seed, rules)


if __name__ == '__main__':
    main()
