"""Content invariants, seeded preservation and prototype access checks."""
from collections import Counter
from copy import deepcopy
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

from island_shapes import Island, Rules, connected, export_svg
from island_resources import (BIOMES, PROFILES, ResourceWorld, SectionContent, access_ok,
                              harvest_multiplier, populate, validate_content)


class ResourceTests(unittest.TestCase):
    def test_approved_budgets_round_down_without_transferring_capacity(self):
        self.assertEqual(PROFILES['Starting Grasslands'].budgets(36), (0, 0, 7))
        self.assertEqual(PROFILES['Grasslands'].budgets(48), (0, 9, 7))
        self.assertEqual(PROFILES['Stone desert'].budgets(48), (16, 2, 7))
        self.assertEqual(PROFILES['Forest'].budgets(48), (0, 7, 16))

    def test_start_guarantees_food_wood_and_unbridged_house_access(self):
        for size in (25, 36, 64):
            for seed in range(10):
                with self.subTest(size=size, seed=seed):
                    world = ResourceWorld(seed, Rules(start_area=size))
                    world.validate()
                    c = world.contents[0]
                    self.assertEqual(c.biome, 'Grasslands')
                    self.assertEqual(set(c.objects.values()), {'tree', 'berry'})
                    counts = Counter(c.objects.values())
                    self.assertGreaterEqual(counts['tree'], 2)
                    self.assertGreaterEqual(counts['berry'], 2)
                    self.assertEqual(len(c.house), 4)
                    self.assertTrue(connected(world.walkable()))
                    self.assertFalse(c.river or c.mountains or c.den_exits)

    def test_biomes_do_not_change_shape_rng_and_old_content_survives_expansion(self):
        for seed in (0, 1, 2):
            shape = Island(seed)
            world = ResourceWorld(seed)
            for biome in BIOMES:
                prior = deepcopy(world.contents)
                shape.expand()
                world.expand(biome)
                self.assertEqual(shape.sections, world.island.sections)
                self.assertEqual(shape.rng.getstate(), world.island.rng.getstate())
                self.assertEqual(prior, world.contents[:-1])
                world.validate()

    def test_undo_and_regenerate_restore_exact_population(self):
        world = ResourceWorld(1)
        world.expand('Forest')
        snapshot = deepcopy(world.contents)
        world.undo()
        world.expand('Forest')
        self.assertEqual(world.contents, snapshot)

    def test_future_expansion_is_not_sealed_off_by_existing_forest(self):
        world = ResourceWorld(16)
        for biome in ('Grasslands', 'Stone desert', 'Forest', 'Grasslands', 'Stone desert', 'Forest'):
            world.expand(biome)
            world.validate()
        self.assertTrue(connected(world.walkable(with_bridges=True)))

    def test_population_failure_rolls_back_shape_content_and_rng(self):
        world = ResourceWorld(0)
        sections = deepcopy(world.island.sections)
        contents = deepcopy(world.contents)
        state = world.island.rng.getstate()
        with patch('island_resources.populate', side_effect=ValueError('test failure')):
            with self.assertRaisesRegex(ValueError, 'test failure'):
                world.expand('Forest')
        self.assertEqual(world.island.sections, sections)
        self.assertEqual(world.contents, contents)
        self.assertEqual(world.island.rng.getstate(), state)
        self.assertFalse(world.history)

    def test_mixed_biomes_respect_caps_allowed_features_and_reserved_access(self):
        for seed in range(6):
            world = ResourceWorld(seed)
            for biome in BIOMES * 2:
                world.expand(biome)
                world.validate()
                self.assertTrue(connected(world.walkable(with_bridges=True)))
                for c in world.contents:
                    self.assertFalse(c.natural & c.reserved)
                    # Independent budget and overlap checks in addition to validation.
                    self.assertGreaterEqual(c.open_cells / len(c.land), c.profile.open_percent / 100)
                    self.assertFalse(set(c.objects) & (c.mountains | set(c.river)))

    def test_river_blocks_movement_but_bridge_candidates_restore_access(self):
        # Explicit river cutting a rectangle into two halves.
        land = frozenset((x, y) for x in range(7) for y in range(7))
        path = tuple((3, y) for y in range(7))
        c = SectionContent('Grasslands', land, river=path,
                           bridges={(3, 3): ((2, 3), (4, 3))})
        self.assertFalse(connected(set(land) - c.blocked))
        self.assertTrue(access_ok(set(land), [c]))

    def test_generated_river_endpoints_and_banks_remain_unchanged_after_growth(self):
        world = ResourceWorld(1)
        world.expand('Grasslands')
        c = deepcopy(world.contents[1])
        self.assertTrue(c.river)
        self.assertEqual(set(c.endpoints.values()), {'underground'})
        self.assertTrue(c.mill_access and c.bridges)
        self.assertFalse(set(c.river) & world.walkable())
        self.assertTrue(set(c.bridges) <= world.walkable(with_bridges=True))
        world.expand('Forest')
        self.assertEqual(world.contents[1], c)
        world.validate()

    def test_small_desert_skips_river_instead_of_exceeding_budget(self):
        world = ResourceWorld(1)
        world.expand('Stone desert')
        self.assertFalse(world.contents[-1].river)

    def test_tier_one_harvest_eligibility(self):
        for source, specialist in [('berry', 'farmer'), ('tree', 'lumberjack'), ('stone', 'miner')]:
            self.assertEqual(harvest_multiplier(source, 'basic'), 1)
            self.assertEqual(harvest_multiplier(source, specialist), 2 if source == 'berry' else 1)
            for wrong in {'farmer', 'lumberjack', 'miner', 'hunter'} - {specialist}:
                self.assertEqual(harvest_multiplier(source, wrong), 0)
            self.assertEqual(harvest_multiplier(source, specialist, tired=True), 0)
        self.assertEqual(harvest_multiplier('wheat'), 0)

    def test_invalid_biome_does_not_advance_generation(self):
        world = ResourceWorld(1)
        state = world.island.rng.getstate()
        with self.assertRaises(ValueError):
            world.expand('Swamp')
        self.assertEqual(world.island.rng.getstate(), state)

    def test_population_svg_is_valid_and_includes_feature_colors(self):
        world = ResourceWorld(1)
        world.expand('Grasslands')
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'population.svg'
            export_svg(world.island, path, world, reservations=True, bridges=True)
            ET.parse(path)
            svg = path.read_text(encoding='utf-8')
            self.assertIn('#ae365f', svg)
            self.assertIn('#338ac3', svg)


if __name__ == '__main__':
    unittest.main()
