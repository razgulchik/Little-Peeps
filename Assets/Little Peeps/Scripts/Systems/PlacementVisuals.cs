using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Everything the build mode DRAWS that is not a real placed structure: the ghost preview, the faint
    // territory halo, and the recolouring of a real structure about to be sold.
    //
    // Split out of PlacementController so the tools decide WHAT is targeted and this class decides how it
    // LOOKS — no tool touches a SpriteRenderer. It owns three visual subjects:
    //   - the GHOST: a neutralised clone of the real prefab that follows the cursor before anything is built;
    //   - the TERRITORY halo: one quad per cell of the footprint+border the ghost would claim;
    //   - the HOVER tint: a real structure already in the scene, recoloured and then restored.
    // Ghost and halo are objects this class creates and destroys; the hover target is NOT — it is
    // borrowed, so its original colours are remembered and put back. Drop shadows (SpriteShadow) follow
    // one rule throughout: a shadow belongs to what stands on the ground. The ghost never has one, and no
    // tint ever recolours one.
    //
    // A plain [Serializable] class, not a MonoBehaviour: the colours stay inspector-authored on the
    // PlacementController that owns it, but none of the drawing code sits in the controller any more.
    [Serializable]
    public class PlacementVisuals
    {
        [Header("Ghost tint")]
        [SerializeField] private Color validColor = new Color(0.4f, 1f, 0.4f, 0.6f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.4f, 0.4f, 0.6f);
        [SerializeField] private Color sellHoverColor = new Color(1f, 0.4f, 0.4f, 0.6f);   // tint of the structure under the cursor in Sell mode

        [Header("Territory halo (ghost)")]
        [SerializeField] private Color territoryValidColor = new Color(0.4f, 1f, 0.4f, 0.18f);
        [SerializeField] private Color territoryInvalidColor = new Color(1f, 0.4f, 0.4f, 0.18f);
        [SerializeField] private string territorySortingLayer = "Ground";   // same layer as the grid overlay (above the grass)
        [SerializeField] private int territorySortingOrder = 1001;

        // Needed for AnchorOnFootprint, which the builder also uses — sharing it is what makes the preview
        // land exactly where the real structure will.
        private StructureSystem structureSystem;

        private GameObject ghost;
        private SpriteRenderer[] ghostRenderers;       // every renderer to tint (the prefab's sprites for a cell ghost, 2 poses for a fence)
        private DualVisual ghostVisual;                // non-null when the ghost is an edge structure (fence)

        // Faint tiles showing the footprint+border the ghost would claim: one quad per
        // claimed cell under a common parent, pooled — a shaped footprint needs the halo to follow its
        // shape, which one scaled square cannot.
        private GameObject territoryGhost;
        private readonly List<SpriteRenderer> territoryTiles = new();
        private static Sprite squareSprite;   // shared 1x1 white sprite every territory tile is scaled from

        // The structure OR fence currently tinted as the hover target. A cell structure and a fence are
        // never both hovered (the edge wins by precedence), so one target covers both.
        private readonly TintTarget hoverTint = new();

        public bool HasGhost => ghost != null;

        // Called by PlacementController.Begin, before any drawing happens.
        public void Init(StructureSystem structureSystem)
        {
            this.structureSystem = structureSystem;
        }

        // --- ghost ------------------------------------------------------------------------------------

        // Build the preview for `def`, edge or cell. Does nothing for a def with no prefab.
        public void BuildGhost(StructureDef def)
        {
            ClearGhost();
            if (def == null || def.prefab == null) return;

            if (def.placement == PlacementKind.Edge) BuildEdgeGhost(def);
            else BuildCellGhost(def);
        }

        // Cell ghost: the neutralised prefab (SpawnGhost), centered on the footprint each frame
        // (PoseCellGhost), where it also takes the look of the spot — a forest's row, a watermill's side.
        private void BuildCellGhost(StructureDef def)
        {
            ghost = SpawnGhost(def);
        }

        // Edge ghost (fence): the neutralised prefab (SpawnGhost), both poses collected for the tint; the
        // active pose and position are set every frame in PoseEdgeGhost.
        private void BuildEdgeGhost(StructureDef def)
        {
            ghost = SpawnGhost(def);
            ghostVisual = ghost.GetComponent<DualVisual>();
        }

        // Instantiate the real prefab so the preview matches the placed structure 1:1 — including any
        // sprite offset hand-tuned inside the prefab (placement centers the ROOT, so the sprite child sits
        // exactly where it will once built) — then neutralise it so it is purely visual: every behaviour
        // off (so Spawner/ResourceSource/Structure don't spawn units, register, or log), every collider and
        // rigidbody off. "Every behaviour" is a contract other code leans on: a disabled behaviour never
        // Starts, so whatever a view builds at Start — a SpriteShadow's shadow, for one — is simply absent
        // from the ghost. DualVisual.Show still works on a disabled component, so the pose switch is
        // unaffected. Collects every renderer (inactive poses included) for the tint.
        private GameObject SpawnGhost(StructureDef def)
        {
            var go = UnityEngine.Object.Instantiate(def.prefab);
            go.name = "PlacementGhost";

            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
            foreach (var col in go.GetComponentsInChildren<Collider2D>(true)) col.enabled = false;
            foreach (var rb in go.GetComponentsInChildren<Rigidbody2D>(true)) rb.simulated = false;

            ghostRenderers = go.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var r in ghostRenderers) r.sortingOrder += 1;   // draw above the placed structures
            return go;
        }

        // Put the cell ghost on `origin`, give it the spot's look (row, river side), and tint it by validity.
        public void PoseCellGhost(IslandGrid grid, Vector2Int origin, Footprint footprint, bool valid)
        {
            if (ghost == null) return;

            // Same anchoring and the same placement visual the builder uses, so the preview matches the
            // placed structure exactly.
            structureSystem.AnchorOnFootprint(ghost.transform, origin, footprint.Size);
            StructureSystem.ApplyPlacementVisual(ghost, grid, origin, footprint);
            TintGhost(valid);
        }

        // Put the edge ghost on `edge`'s midpoint, show the matching pose, and tint it by validity.
        public void PoseEdgeGhost(IslandGrid grid, Edge edge, bool valid)
        {
            if (ghost == null) return;

            ghost.transform.position = grid.EdgeToWorld(edge);
            if (ghostVisual != null) ghostVisual.Show(edge.horizontal);
            TintGhost(valid);
        }

        private void TintGhost(bool valid) => Tint(ghostRenderers, valid ? validColor : invalidColor);

        // Destroy the ghost and hide the halo that goes with it.
        public void ClearGhost()
        {
            if (ghost != null) UnityEngine.Object.Destroy(ghost);
            ghost = null;
            ghostRenderers = null;
            ghostVisual = null;
            HideTerritory();
        }

        // --- territory halo ---------------------------------------------------------------------------

        // Show the faint footprint+border halo at `origin`, tinted by validity, so the area the ghost
        // would claim follows the cursor. One tile per claimed cell (Footprint.Claims — the same sweep CanPlace makes), so the halo shows exactly
        // what would be claimed, notch and all.
        public void ShowTerritory(IslandGrid grid, Vector2Int origin, Footprint footprint, int border, bool valid)
        {
            EnsureTerritory();

            float cs = grid.CellSize;
            var color = valid ? territoryValidColor : territoryInvalidColor;
            Vector2Int s = footprint.Size;
            int used = 0;

            for (int x = -border; x < s.x + border; x++)
                for (int y = -border; y < s.y + border; y++)
                {
                    if (!footprint.Claims(x, y, border)) continue;

                    var tile = TerritoryTile(used++);
                    Vector2 c = grid.GridToWorld(new Vector2Int(origin.x + x, origin.y + y));
                    tile.transform.position = new Vector3(c.x, c.y, 0f);
                    tile.transform.localScale = new Vector3(cs, cs, 1f);
                    tile.color = color;
                    tile.gameObject.SetActive(true);
                }

            for (int i = used; i < territoryTiles.Count; i++) territoryTiles[i].gameObject.SetActive(false);
            territoryGhost.SetActive(true);
        }

        public void HideTerritory()
        {
            if (territoryGhost != null) territoryGhost.SetActive(false);
        }

        private void EnsureTerritory()
        {
            if (territoryGhost != null) return;
            territoryGhost = new GameObject("PlacementTerritory");
            territoryTiles.Clear();   // a parent gone (scene unload) took its tiles with it
        }

        // The i-th pooled tile, created on first use. Tiles are never destroyed while the halo lives;
        // ShowTerritory switches off the ones a smaller footprint does not need.
        private SpriteRenderer TerritoryTile(int i)
        {
            while (territoryTiles.Count <= i)
            {
                var go = new GameObject("Tile");
                go.transform.SetParent(territoryGhost.transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = SquareSprite();
                r.sortingLayerName = territorySortingLayer;
                r.sortingOrder = territorySortingOrder;
                territoryTiles.Add(r);
            }
            return territoryTiles[i];
        }

        // A shared 1x1 white sprite (centered pivot, 1 px/unit) every territory tile is scaled from.
        private static Sprite SquareSprite()
        {
            if (squareSprite == null)
                squareSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return squareSprite;
        }

        // --- hover tint (a real structure under the cursor) ---------------------------------------------

        // Tint the structure/fence rooted at `root` so it reads as "will be sold". Callers only do this
        // when the target CHANGES, so the GetComponentsInChildren inside stays off the per-frame path.
        public void SetHover(Component root)
        {
            hoverTint.Capture(root);
            hoverTint.Retint(sellHoverColor);
        }

        // Put the hover target's real colours back and forget it.
        public void ClearHover() => hoverTint.Restore();

        // Drop the hover target WITHOUT restoring — for when it is being destroyed (Sell), so there is
        // nothing left to colour back.
        public void ForgetHover() => hoverTint.Forget();

        // Color every renderer in the set (skipping nulls). Used to tint the multi-renderer place ghost,
        // which is destroyed rather than restored. (A hovered structure uses TintTarget instead, which
        // also remembers the originals so the tint can be undone.)
        private static void Tint(SpriteRenderer[] renderers, Color color)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].color = color;
        }

        // One tinted structure — the hover target — bundling its renderers with the colors they had before
        // the tint, so the tint can be applied and undone as a unit. Works the same for a cell structure
        // (a forest is many trees) and a fence (its two poses): every renderer is taken includeInactive so
        // a hidden visual root is covered too.
        private sealed class TintTarget
        {
            private SpriteRenderer[] renderers;
            private Color[] originalColors;

            public bool Active => renderers != null;

            // Snapshot every renderer under `root` — except drop shadows — and the color it currently has
            // (no tint applied yet — call Retint to color them). A tint replaces the colour outright, and
            // a shadow turned green or red reads as a second copy of the building; left out, it stays
            // black under any tint. Replaces any previous capture without restoring it, so callers
            // restore/forget first.
            public void Capture(Component root)
            {
                var all = root.GetComponentsInChildren<SpriteRenderer>(true);
                var kept = new List<SpriteRenderer>(all.Length);
                foreach (var r in all) if (!SpriteShadow.IsShadow(r)) kept.Add(r);
                renderers = kept.ToArray();
                originalColors = new Color[renderers.Length];
                for (int i = 0; i < renderers.Length; i++) originalColors[i] = renderers[i].color;
            }

            // Color all captured renderers (skipping destroyed ones). Cheap to call every frame for the
            // drag's valid/invalid tint; does not touch the remembered originals.
            public void Retint(Color color)
            {
                if (renderers == null) return;
                for (int i = 0; i < renderers.Length; i++)
                    if (renderers[i] != null) renderers[i].color = color;
            }

            // Put the original colors back (skipping destroyed renderers) and forget the target.
            public void Restore()
            {
                if (renderers == null) return;
                for (int i = 0; i < renderers.Length; i++)
                    if (renderers[i] != null) renderers[i].color = originalColors[i];
                Forget();
            }

            // Forget the target WITHOUT restoring — for when the structure is being destroyed (Sell), so
            // there is nothing to color back.
            public void Forget()
            {
                renderers = null;
                originalColors = null;
            }
        }
    }
}
