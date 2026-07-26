using System;
using UnityEngine;

namespace LittlePeeps
{
    // Which meaning a hover tint carries. The colour lives in PlacementVisuals; the tool only says what
    // the player is about to do, so no call site has to know that "will be sold" happens to be red.
    public enum HoverStyle
    {
        Sell,   // the structure under the cursor will be sold
        Move,   // the structure under the cursor can be grabbed
    }

    // Everything the build mode DRAWS that is not a real placed structure: the ghost preview, the faint
    // territory halo, and the recolouring of real structures for hover and drag.
    //
    // Split out of PlacementController so the tools decide WHAT is targeted and this class decides how it
    // LOOKS — no tool touches a SpriteRenderer. It owns three visual subjects:
    //   - the GHOST: a neutralised clone of the real prefab that follows the cursor before anything is built;
    //   - the TERRITORY halo: a scaled quad showing the footprint+border the ghost would claim;
    //   - the HOVER and HELD tints: real structures already in the scene, recoloured and then restored.
    // Ghost and halo are objects this class creates and destroys; hover and held targets are NOT — they are
    // borrowed, so their original colours are remembered and put back.
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
        [SerializeField] private Color moveHoverColor = new Color(0.4f, 1f, 0.4f, 0.6f);   // tint of a grabbable structure under the cursor in idle Move mode

        [Header("Territory halo (ghost)")]
        [SerializeField] private Color territoryValidColor = new Color(0.4f, 1f, 0.4f, 0.18f);
        [SerializeField] private Color territoryInvalidColor = new Color(1f, 0.4f, 0.4f, 0.18f);
        [SerializeField] private string territorySortingLayer = "Ground";   // same layer as the grid overlay (above the grass)
        [SerializeField] private int territorySortingOrder = 1001;

        // Needed for CenterOnFootprint, which the builder also uses — sharing it is what makes the preview
        // land exactly where the real structure will.
        private StructureSystem structureSystem;

        private GameObject ghost;
        private SpriteRenderer[] ghostRenderers;       // every renderer to tint (the prefab's sprites for a cell ghost, 2 poses for a fence)
        private DualVisual ghostVisual;                // non-null when the ghost is an edge structure (fence)
        private DualVisual ghostRowVisual;             // non-null when the cell ghost interlocks by row (forest)

        // Faint square showing the footprint+border the ghost would claim (Place and Move).
        private GameObject territoryGhost;
        private SpriteRenderer territoryRenderer;
        private static Sprite squareSprite;   // shared 1x1 white sprite the territory quad is scaled from

        // The structure OR fence currently tinted as the hover target. A cell structure and a fence are
        // never both hovered (the edge wins by precedence), so one target covers both.
        private readonly TintTarget hoverTint = new();

        // The structure OR fence lifted off the grid and following the cursor. Its transform and pose
        // switch are captured on pick-up so the drag can be posed without handing the object back out.
        private readonly TintTarget heldTint = new();
        private Transform heldRoot;
        private DualVisual heldVisual;

        public bool HasGhost => ghost != null;
        public bool HasHeld => heldTint.Active;

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

        // Cell ghost: instantiate the real prefab so the preview matches the placed structure 1:1 —
        // including any sprite offset hand-tuned inside the prefab (placement centers the ROOT, so the
        // sprite child sits exactly where it will once built). Then neutralize it: disable every
        // behaviour (so Spawner/ResourceSource/Structure don't spawn units, register, or log) and every
        // collider/rigidbody so it's purely visual. Centered on the footprint each frame (PoseCellGhost).
        private void BuildCellGhost(StructureDef def)
        {
            ghost = UnityEngine.Object.Instantiate(def.prefab);
            ghost.name = "PlacementGhost";

            foreach (var mb in ghost.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
            foreach (var col in ghost.GetComponentsInChildren<Collider2D>(true)) col.enabled = false;
            foreach (var rb in ghost.GetComponentsInChildren<Rigidbody2D>(true)) rb.simulated = false;

            ghostRenderers = ghost.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var r in ghostRenderers) r.sortingOrder += 1;   // draw above the placed structures

            ghostRowVisual = ghost.GetComponent<DualVisual>();   // forest: the preview interlocks by row too
        }

        // Edge ghost (fence): instantiate the real prefab so the preview matches 1:1 (both poses), then
        // neutralize it — disable colliders + the Structure behaviour so it's purely visual. The active
        // pose and tint are set every frame in PoseEdgeGhost.
        private void BuildEdgeGhost(StructureDef def)
        {
            ghost = UnityEngine.Object.Instantiate(def.prefab);
            ghost.name = "PlacementGhost";
            ghostVisual = ghost.GetComponent<DualVisual>();

            foreach (var col in ghost.GetComponentsInChildren<Collider2D>(true)) col.enabled = false;
            if (ghost.TryGetComponent<Structure>(out var s)) s.enabled = false;

            ghostRenderers = ghost.GetComponentsInChildren<SpriteRenderer>(true);   // both poses (incl. inactive)
            foreach (var r in ghostRenderers) r.sortingOrder += 1;                  // draw above the real fences
        }

        // Put the cell ghost on `origin`, lap it to the row, and tint it by validity.
        public void PoseCellGhost(Vector2Int origin, Vector2Int size, bool valid)
        {
            if (ghost == null) return;

            // Same centering the builder uses, so the preview matches the placed structure exactly.
            structureSystem.CenterOnFootprint(ghost.transform, origin, size);
            if (ghostRowVisual != null) ghostRowVisual.Show((origin.y & 1) == 0);   // forest: preview the row's layout
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
            ghostRowVisual = null;
            HideTerritory();
        }

        // --- territory halo ---------------------------------------------------------------------------

        // Show the faint footprint+border halo at `origin`, tinted by validity. Used by both Place (new
        // structure) and Move (held structure) so the claimed area follows the cursor.
        public void ShowTerritory(IslandGrid grid, Vector2Int origin, Vector2Int size, int border, bool valid)
        {
            EnsureTerritory();

            float cs = grid.CellSize;
            Vector2 center = grid.OriginToWorldCenter(origin, size);   // footprint center = territory center (border is symmetric)

            territoryGhost.transform.position = new Vector3(center.x, center.y, 0f);
            territoryGhost.transform.localScale = new Vector3((size.x + 2 * border) * cs, (size.y + 2 * border) * cs, 1f);
            territoryRenderer.color = valid ? territoryValidColor : territoryInvalidColor;
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
            territoryRenderer = territoryGhost.AddComponent<SpriteRenderer>();
            territoryRenderer.sprite = SquareSprite();
            territoryRenderer.sortingLayerName = territorySortingLayer;
            territoryRenderer.sortingOrder = territorySortingOrder;
        }

        // A shared 1x1 white sprite (centered pivot, 1 px/unit) the territory quad is scaled from.
        private static Sprite SquareSprite()
        {
            if (squareSprite == null)
                squareSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return squareSprite;
        }

        // --- hover tint (a real structure under the cursor) ---------------------------------------------

        // Tint the structure/fence rooted at `root` so it reads as the hover target. Callers only do this
        // when the target CHANGES, so the GetComponentsInChildren inside stays off the per-frame path.
        public void SetHover(Component root, HoverStyle style)
        {
            hoverTint.Capture(root);
            hoverTint.Retint(style == HoverStyle.Sell ? sellHoverColor : moveHoverColor);
        }

        // Put the hover target's real colours back and forget it.
        public void ClearHover() => hoverTint.Restore();

        // Drop the hover target WITHOUT restoring — for when it is being destroyed (Sell), so there is
        // nothing left to colour back.
        public void ForgetHover() => hoverTint.Forget();

        // --- held tint + pose (the structure being dragged) ---------------------------------------------

        // Take over the object being dragged: remember its renderers' real colours, its transform, and its
        // pose switch if it has one. The caller must restore any hover tint FIRST, so the colours captured
        // here are the real ones and not the green grabbable hint.
        public void CaptureHeld(Component root)
        {
            heldTint.Capture(root);
            heldRoot = root.transform;
            heldVisual = root.GetComponent<DualVisual>();
        }

        // Drag pose for a cell structure: centered on `origin`, re-lapped to its new row, tinted by validity.
        public void PoseHeldCell(Vector2Int origin, Vector2Int size, bool valid)
        {
            if (heldRoot == null) return;

            structureSystem.CenterOnFootprint(heldRoot, origin, size);
            StructureSystem.ApplyRowVisual(heldRoot.gameObject, origin.y);   // re-lap a dragged forest
            heldTint.Retint(valid ? validColor : invalidColor);
        }

        // Drag pose for a fence: snapped to `edge`'s midpoint, showing the matching pose, tinted by validity.
        public void PoseHeldEdge(IslandGrid grid, Edge edge, bool valid)
        {
            if (heldRoot == null) return;

            heldRoot.position = grid.EdgeToWorld(edge);
            if (heldVisual != null) heldVisual.Show(edge.horizontal);
            heldTint.Retint(valid ? validColor : invalidColor);
        }

        // Put the dragged object's real colours back and let go of it.
        public void ReleaseHeld()
        {
            heldTint.Restore();
            heldRoot = null;
            heldVisual = null;
            HideTerritory();
        }

        // Color every renderer in the set (skipping nulls). Used to tint the multi-renderer place ghost,
        // which is destroyed rather than restored. (A hovered/dragged structure uses TintTarget instead,
        // which also remembers the originals so the tint can be undone.)
        private static void Tint(SpriteRenderer[] renderers, Color color)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].color = color;
        }

        // One tinted structure — a hover target or the dragged structure — bundling its renderers with the
        // colors they had before the tint, so the tint can be applied and undone as a unit. Works the same
        // for a cell structure (a forest is many trees) and a fence (its two poses): every renderer is
        // taken includeInactive so a hidden visual root is covered too. Shared by hoverTint and heldTint.
        private sealed class TintTarget
        {
            private SpriteRenderer[] renderers;
            private Color[] originalColors;

            public bool Active => renderers != null;

            // Snapshot every renderer under `root` and the color it currently has (no tint applied yet —
            // call Retint to color them). Replaces any previous capture without restoring it, so callers
            // restore/forget first.
            public void Capture(Component root)
            {
                renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
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
