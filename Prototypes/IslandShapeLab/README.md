# Island Shape Lab

An independent Python island and resource generation experiment. It has no Unity dependencies and does not read or modify the game. Worker simulation, actual buildings and age rewards remain outside this experiment.

## Run

Requires Python 3.9 or newer with Tkinter. No pip packages are required. From this directory:

```powershell
python island_shapes.py
```

Generate creates a new starting island using the displayed parameters. Expand adds one section. Previous/Next seed makes comparison reproducible; Undo restores the previous geometry, resources and random state. Save SVG exports the current grid. All parameter edits take effect on Generate. The view automatically fits the island; the pale outline marks the most recent section. Colours identify biomes by default; disable Biome colours to colour individual sections instead.

## Biomes and resources

The viewer now includes resources. Select Next biome before expanding. Toggle Resources, Biome colours, Access reservations and Bridge preview to inspect the layers. Undo restores both geometry and population; Save SVG exports the visible layers. Hover a cell for details.

Resource placement uses a separate random stream: biome choices and resource retries do not change the approved island shapes. Earlier resources never move. The initial Grasslands section contains a reserved 2×2 house footprint, at least two berry bushes and two trees, and no higher-level resources.

| Profile | Minimum naturally open | Mountain cap | River cap | Object cap |
| --- | --- | --- | --- | --- |
| Starting Grasslands | 80% | 0% | 0% | 20% |
| Grasslands | 65% | 0% | 20% | 15% |
| Stone desert | 45% | 35% | 5% | 15% |
| Forest | 50% | 0% | 15% | 35% |

Caps round down to whole cells. These are maximum budgets, not quotas: unsuitable features are omitted and their allowance remains open. Naturally open excludes natural features, but includes the reserved house footprint. Grasslands has sparse berries and trees; Stone desert has solid mountain patches and stones; Forest favours clustered trees with some berries, optional dens and occasional stones. Forest has no mountains. Natural wheat is never generated.

Trees, berries, stones and both dens occupy one cell. Mountains and rivers block movement. Rivers are ordered one-cell-wide paths with no nonconsecutive orthogonal contact. This first version uses **underground endpoints only**: sea outlets still need a rule for expansions that bury their coast.

Access reservations protect a clearing, section connections, future coastal connections and resource approaches. All free land and resource approaches must connect after building the marked potential bridges; the starting section requires no bridges. Bridge preview only illustrates those crossings. It does not generate built bridges. Mine and mill markers reserve provisional approaches, not validated building footprints. Worker circulation and building-size checks remain future work.

Letters: T tree, B berry, S stone, D boar den, F fox den, M mountain, E underground endpoint, H house footprint. Yellow outlines mark reserved cells, gold dots mine/mill approaches, and cyan marks potential crossings.

Harvest eligibility is testable metadata: basic workers or the matching specialist can harvest tier-one sources, other assigned workers cannot. Farmers receive twice the berries; tired workers cannot harvest. No production worker simulation is implemented here.

## First design rule

Forbid small or deep water pockets, allow broad shallow bays. Initial tunable defaults:

- Minimum bay width: **4 cells**.
- Maximum bay depth/width: **0.75**.
- Minimum continuous attachment: **3 cell edges**.
- Minimum horizontal and vertical land run: **3 cells** in the combined island; **2 cells** in the new section alone.
- Each expansion must contain at least one **3×3** block of new land.
- No detached land or enclosed water.
- Earlier land and section ownership never change. Shore repairs belong to the new section.

Bay measurements are an axis-aligned grid approximation: water bracketed by land is a bay slice; its width is the gap between those banks. Depth is the distance to land closing a perpendicular end. Slices with no closed end are still checked for width. This is deliberately conservative, and is not a proof against worker trapping. The directional land-run check likewise does not prove every possible diagonal neck has the requested clearance.

## Generation baseline

1. Build compact candidate masses with varied proportions and independent corner cuts, including uncut corners and deeper cuts. Budget extra cells for clipping. Choose quarter-turn orientation independently of width sampling. Validate finished land instead of preallocating equal corner margins. This is a controlled baseline, not a final organic-shore algorithm.
2. Position a mass outward from a sampled coast cell, with partial overlap. Vary the candidate mass size to compensate for land lost to overlap.
3. Add land to fill prohibited pockets and holes in the combined island. Reject repairs that exceed the area budget.
4. Require the new section to be connected, have a broad join and usable interior, and pass thin-feature checks.
5. Give each distinct sampled shape family one entry, grouping translations and rotations together. Starting islands choose uniformly among up to 24 valid families, then uniformly among that family's distinct quarter-turn orientations. Expansions choose uniformly among up to ten valid families; repeated proposals can improve a family's placement using overall compactness, but cannot increase its selection weight. An attached section cannot be freely rotated after selection, so its final orientation remains constrained by the existing coast. Stop after 240 attempts; report failure instead of silently accepting bad geometry or widening the area band.

Selection is uniform within the sampled pool, not over every mathematically possible shape. The candidate generator still determines which shapes can enter that pool. Reflection is not grouped with rotation, so mirrored asymmetric shapes remain separate families.

Start area defaults to **36**, expansion area to **48**. Both have a hard **±10%** tolerance, rounded inward to whole cells: **33–39** for the default start and **44–52** new cells per default expansion. Bounds apply after corner cuts, overlap and all shore repairs. The viewer shows the accepted range beside the newest section's actual count. All areas within the band are equally acceptable; exact target counts receive no selection bonus. Changing the seed or parameters changes generation. Nothing in this prototype commits the game to a square starting footprint, a particular biome system or a production implementation.

## Repeatable checks and exports

Add `--resources` to PNG exports for biome content. `--biomes Grasslands "Stone desert" Forest` selects the repeating expansion sequence (also the default). Add `--reservations` to show reserved access and potential bridge sites, or `--bridges` to illustrate those crossings. The shape script's command-line SVG export remains geometry-only; use Save SVG in the viewer for resource layers.

```powershell
python -m unittest -v
python island_shapes.py --check 100 --seed 0 --steps 8
python island_shapes.py --seed 12 --steps 5 --svg example.svg
python island_shapes.py --check 30 --area 64 --bay-width 5 --bay-depth 0.5
python export_examples.py --seeds 0 1 2 3 4 5 --sections 12 --output island-examples.png
python island_resources.py --check 30 --steps 6
python export_examples.py --resources --seeds 0 1 2 3 4 5 --sections 4 --output biome-examples.png
```

`export_examples.py` saves a PNG contact sheet with a shared cell scale and one colour per section, without installing any extra packages. `--sections` includes the starting section; use 1 for starting islands only. Defaults reproduce six seeds (0–5) with 12 total sections each. It also accepts `--start-area`, `--area`, `--land-width`, `--bay-width` and `--bay-depth`. Colours repeat after twelve sections; the size labels list the first twelve section counts.

The sweep reports seeds that fail and exits nonzero on failure, including any section outside its area band. Fixtures separately exercise broad/shallow and narrow/deep bays, rotation, holes, spurs, disconnected land, scattered contacts, area limits across multiple sizes, reproducibility and preservation. Variety tests check that duplicate proposals and rotations cannot add selection weight, larger asymmetric corner cuts can survive validation, and a fixed seed sample does not collapse to one family or orientation. A seed reproduces the same expansion sequence only with the same parameters and algorithm version. The revised sampling changes layouts from the earlier prototype, even with the same seed.

Visual review is still necessary: look for overly rectangular silhouettes, stair-stepped diagonals, awkward joins, excessive coast length and thin-looking regions that pass the grid checks. A bouncing-worker test is a future experiment; this version makes no circulation guarantee.
