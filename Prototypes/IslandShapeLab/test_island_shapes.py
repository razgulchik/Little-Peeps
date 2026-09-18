"""Geometry fixtures and reproducibility checks; no third-party packages."""
import unittest
from collections import Counter
from unittest.mock import patch

from island_shapes import (Island, Rules, bounds, broad_join, connected, holes, make_mass,
                           narrow_land, orientations, pocket_cells, repair, silhouette_key)


def rectangle(x, y, w, h):
    return {(a, b) for a in range(x, x + w) for b in range(y, y + h)}


class ShapeTests(unittest.TestCase):
    def test_duplicates_and_rotations_do_not_increase_selection_weight(self):
        exact = rectangle(0, 0, 6, 6)
        other = rectangle(0, 0, 7, 6) - {(0, 0), (6, 0), (0, 5), (6, 5)}
        rotated = {(-y + 20, x - 30) for x, y in other}
        picked_areas = set()
        for seed in range(20):
            with patch('island_shapes.make_mass', side_effect=[exact, other]):
                plain = Island(seed, Rules(attempts=2))
            with patch('island_shapes.make_mass', side_effect=[exact] * 8 + [rotated, other]):
                repeated = Island(seed, Rules(attempts=10))
            self.assertEqual(plain.cells, repeated.cells)
            picked_areas.add(len(plain.cells))
        self.assertEqual(picked_areas, {36, 38})

    def test_shape_identity_ignores_translation_and_rotation(self):
        land = rectangle(0, 0, 7, 6) - {(0, 0), (0, 1), (1, 0)}
        self.assertEqual(silhouette_key(land), silhouette_key({(-y + 10, x - 17) for x, y in land}))
        self.assertEqual(len(orientations(land)), 4)

    def test_deeper_asymmetric_corner_cuts_can_pass_width_checks(self):
        from unittest.mock import Mock
        rng = Mock()
        rng.uniform.return_value = 1.0
        rng.randint.side_effect = [2, 0, 1, 0]
        rng.choice.side_effect = lambda options: options[0]
        mass = make_mass(rng, 36, 3)
        self.assertEqual(len(mass), 38)
        self.assertFalse(narrow_land(mass, 3))
        self.assertTrue(connected(mass))
        self.assertFalse(holes(mass))

    def test_seed_sample_has_shape_and_orientation_variety(self):
        families = Counter()
        horizontal = vertical = 0
        for seed in range(200):
            land = Island(seed).cells
            families[silhouette_key(land)] += 1
            x0, y0, x1, y1 = bounds(land)
            horizontal += x1 - x0 > y1 - y0
            vertical += y1 - y0 > x1 - x0
        self.assertGreaterEqual(len(families), 15)
        self.assertLess(max(families.values()), 40)
        self.assertTrue(0.35 < horizontal / (horizontal + vertical) < 0.65)

    def test_area_limits_round_inward(self):
        self.assertEqual(Rules.area_limits(36), (33, 39))
        self.assertEqual(Rules.area_limits(48), (44, 52))
        self.assertEqual(Rules.area_limits(25), (23, 27))

    def test_start_rejects_both_undersized_and_oversized_candidates(self):
        with patch('island_shapes.make_mass', side_effect=[rectangle(0, 0, 5, 5),
                   rectangle(0, 0, 7, 6), rectangle(0, 0, 6, 6)]):
            island = Island(1, Rules(attempts=3))
        self.assertEqual(len(island.cells), 36)

    def test_area_bands_across_sizes_include_shore_repairs(self):
        for target in (25, 36, 48, 64, 100):
            for seed in range(3):
                with self.subTest(target=target, seed=seed):
                    island = Island(seed, Rules(start_area=target, section_area=target))
                    for step in range(5):
                        section = island.sections[0] if step == 0 else island.expand()
                        # Independent assertion: actual new area, including repair, is within 10%.
                        self.assertLessEqual(abs(len(section) - target) * 10, target)

    def test_repair_rejects_oversized_mass_even_without_pockets(self):
        self.assertIsNone(repair(rectangle(0, 0, 6, 6), Rules(), 35))

    def test_broad_shallow_bay_is_preserved(self):
        land = rectangle(0, 0, 12, 10) - rectangle(4, 7, 4, 3)
        self.assertFalse(pocket_cells(land, 4, 0.75))
        self.assertEqual(repair(land, Rules(), 120), land)

    def test_narrow_bay_is_filled(self):
        land = rectangle(0, 0, 12, 10) - rectangle(4, 7, 3, 3)
        self.assertTrue(pocket_cells(land, 4, 0.75))
        self.assertEqual(repair(land, Rules(), 120), rectangle(0, 0, 12, 10))

    def test_deep_bay_is_filled(self):
        land = rectangle(0, 0, 12, 10) - rectangle(4, 3, 4, 7)
        self.assertTrue(pocket_cells(land, 4, 0.75))
        self.assertEqual(repair(land, Rules(), 120), rectangle(0, 0, 12, 10))

    def test_bay_rules_apply_after_rotation(self):
        land = rectangle(0, 0, 12, 10) - rectangle(4, 3, 4, 7)
        for _ in range(4):
            self.assertTrue(pocket_cells(land, 4, 0.75))
            fixed = repair(land, Rules(), 120)
            self.assertIsNotNone(fixed)
            self.assertFalse(pocket_cells(fixed, 4, 0.75))
            land = {(-y, x) for x, y in land}

    def test_enclosed_water_is_filled(self):
        missing = rectangle(4, 4, 3, 3)
        land = rectangle(0, 0, 12, 12) - missing
        self.assertEqual(holes(land), missing)
        self.assertEqual(repair(land, Rules(), 144), rectangle(0, 0, 12, 12))

    def test_repair_respects_area_budget_and_input(self):
        land = rectangle(0, 0, 12, 10) - rectangle(4, 3, 4, 7)
        original = set(land)
        self.assertIsNone(repair(land, Rules(), len(land)))
        self.assertEqual(land, original)

    def test_diagonal_contact_is_not_connectivity(self):
        self.assertFalse(connected({(0, 0), (1, 1)}))

    def test_thin_spur_is_rejected(self):
        land = rectangle(0, 0, 6, 6) | rectangle(6, 2, 3, 1)
        self.assertTrue(narrow_land(land, 3))
        self.assertFalse(narrow_land(rectangle(0, 0, 6, 6), 3))

    def test_join_requires_contiguous_boundary(self):
        old = rectangle(0, 0, 6, 6)
        self.assertFalse(broad_join(old, {(6, 0), (6, 2), (6, 4)}, 3))
        self.assertTrue(broad_join(old, rectangle(6, 1, 4, 3), 3))

    def test_reproducible_sequence_and_unchanged_old_land(self):
        a, b = Island(481), Island(481)
        self.assertEqual(a.sections, b.sections)
        for _ in range(8):
            prior = [set(s) for s in a.sections]
            self.assertEqual(a.expand(), b.expand())
            self.assertEqual(prior, a.sections[:-1])

    def test_impossible_configuration_explains_failure(self):
        with self.assertRaisesRegex(ValueError, 'Area targets'):
            Island(1, Rules(start_area=9))


if __name__ == '__main__':
    unittest.main()
