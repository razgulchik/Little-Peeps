using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The island in a save: each committed section as SectionSave and back. The SHAPE and the generation
    // PLAN of every zone (IslandSectionContent: where its rivers, mountains, kept passages and the pier lane
    // are) — not its seed and the choices made. Replaying those would rebuild a different island as soon as a
    // biome's tuning changes how its rolls are spent, and the structures of the save would then stand on land
    // that is not there. The plan matters as much as the land: every future zone is planned around the old
    // ones (IslandContent.Populate reads them).
    //
    // Pure C#, so the round trip is tested offline.
    public static class IslandSnapshot
    {
        public static SectionSave Save(IslandSection section)
        {
            var save = new SectionSave();
            Write(section.Cells, save.cells);

            var c = section.Content;
            if (c == null) return save;

            save.hasContent = true;
            save.biome = c.biome != null ? c.biome.id : null;
            save.houseFootprint = Save(c.houseFootprint);
            save.pierFootprint = Save(c.pierFootprint);
            foreach (var f in c.extraFootprints) save.extraFootprints.Add(Save(f));

            foreach (var cell in IslandShape.Sorted(c.objects.Keys))
            {
                var rule = c.objects[cell];
                save.objects.Add(new ObjectSave
                {
                    x = cell.x, y = cell.y,
                    def = rule != null && rule.def != null ? rule.def.id : null,
                });
            }

            Write(c.mountains, save.mountains);
            foreach (var cell in c.river) { save.river.Add(cell.x); save.river.Add(cell.y); }
            Write(c.house, save.house);
            save.houseOriginX = c.houseOrigin.x;
            save.houseOriginY = c.houseOrigin.y;
            Write(c.pier, save.pier);
            save.pierOriginX = c.pierOrigin.x;
            save.pierOriginY = c.pierOrigin.y;
            Write(c.extras, save.extras);
            foreach (var cell in c.extraOrigins) { save.extraOrigins.Add(cell.x); save.extraOrigins.Add(cell.y); }
            Write(c.clearing, save.clearing);
            Write(c.reserved, save.reserved);
            foreach (var cell in IslandShape.Sorted(c.bridges.Keys))
            {
                var (a, b) = c.bridges[cell];
                save.bridges.AddRange(new[] { cell.x, cell.y, a.x, a.y, b.x, b.y });
            }
            Write(c.mineAccess, save.mineAccess);
            Write(c.millAccess, save.millAccess);
            return save;
        }

        // The section's cells and its content, or false with the reason. `biomeById` finds the zone's biome
        // profile; a biome that is gone fails the section — its plan cannot be read without it, and a saved
        // island without one of its zones would not be the island the structures were placed on.
        public static bool TryLoad(SectionSave save, Func<string, IslandBiome> biomeById,
                                   out HashSet<Vector2Int> cells, out IslandSectionContent content, out string problem)
        {
            cells = Read(save.cells);
            content = null;
            problem = null;

            if (cells.Count == 0)
            {
                problem = "a section with no cells";
                return false;
            }
            if (!save.hasContent) return true;

            var biome = biomeById?.Invoke(save.biome);
            if (biome == null)
            {
                problem = $"its biome '{save.biome}' is no longer in the game";
                return false;
            }

            var extraFootprints = new List<Footprint>();
            foreach (var f in save.extraFootprints) extraFootprints.Add(Load(f));

            content = new IslandSectionContent(biome, cells, Load(save.houseFootprint), Load(save.pierFootprint),
                                               extraFootprints);

            // A rule that is gone leaves its cell as a planned object all the same: the plan only needs the
            // cell (it is blocked); the rule is what the generator placed there, and the save has that.
            foreach (var o in save.objects)
                content.objects[new Vector2Int(o.x, o.y)] = RuleFor(biome, o.def);

            content.mountains.UnionWith(Read(save.mountains));
            content.river.AddRange(ReadOrdered(save.river));
            content.house.UnionWith(Read(save.house));
            content.houseOrigin = new Vector2Int(save.houseOriginX, save.houseOriginY);
            content.pier.UnionWith(Read(save.pier));
            content.pierOrigin = new Vector2Int(save.pierOriginX, save.pierOriginY);
            content.extras.UnionWith(Read(save.extras));
            content.extraOrigins.AddRange(ReadOrdered(save.extraOrigins));
            content.clearing.UnionWith(Read(save.clearing));
            content.reserved.UnionWith(Read(save.reserved));
            for (int i = 0; i + 5 < save.bridges.Count; i += 6)
            {
                var b = save.bridges;
                content.bridges[new Vector2Int(b[i], b[i + 1])] =
                    (new Vector2Int(b[i + 2], b[i + 3]), new Vector2Int(b[i + 4], b[i + 5]));
            }
            content.mineAccess.UnionWith(Read(save.mineAccess));
            content.millAccess.UnionWith(Read(save.millAccess));
            return true;
        }

        private static IslandObjectRule RuleFor(IslandBiome biome, string defId)
        {
            if (biome.objects == null || string.IsNullOrEmpty(defId)) return null;
            foreach (var rule in biome.objects)
                if (rule != null && rule.def != null && rule.def.id == defId) return rule;
            return null;
        }

        private static FootprintSave Save(Footprint footprint)
        {
            var save = new FootprintSave { width = footprint.Size.x, height = footprint.Size.y };
            bool full = true;
            var mask = new List<bool>(footprint.Size.x * footprint.Size.y);
            for (int y = 0; y < footprint.Size.y; y++)
                for (int x = 0; x < footprint.Size.x; x++)
                {
                    bool on = footprint.Contains(x, y);
                    mask.Add(on);
                    full &= on;
                }
            if (!full) save.mask = mask;
            return save;
        }

        private static Footprint Load(FootprintSave save)
        {
            if (save == null || save.width <= 0 || save.height <= 0) return default;

            var size = new Vector2Int(save.width, save.height);
            bool[] mask = save.mask != null && save.mask.Count == save.width * save.height ? save.mask.ToArray() : null;
            return new Footprint(size, mask);
        }

        private static void Write(IEnumerable<Vector2Int> cells, List<int> into)
        {
            foreach (var cell in IslandShape.Sorted(cells)) { into.Add(cell.x); into.Add(cell.y); }
        }

        private static HashSet<Vector2Int> Read(List<int> flat)
        {
            var cells = new HashSet<Vector2Int>();
            if (flat == null) return cells;
            for (int i = 0; i + 1 < flat.Count; i += 2) cells.Add(new Vector2Int(flat[i], flat[i + 1]));
            return cells;
        }

        private static List<Vector2Int> ReadOrdered(List<int> flat)
        {
            var cells = new List<Vector2Int>();
            if (flat == null) return cells;
            for (int i = 0; i + 1 < flat.Count; i += 2) cells.Add(new Vector2Int(flat[i], flat[i + 1]));
            return cells;
        }
    }
}
