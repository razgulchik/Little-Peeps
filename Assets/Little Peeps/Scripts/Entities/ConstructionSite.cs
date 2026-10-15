using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // How a construction site looks until it has art of its own: the building as a pale blueprint over a
    // faint patch of ground. One look for every site, authored on StructureSystem.
    [Serializable]
    public class ConstructionSiteLook
    {
        [Tooltip("Multiplies the building's own sprites in the blueprint. Alpha = how see-through it is.")]
        public Color blueprintColor = new Color(1f, 1f, 1f, 0.45f);

        [Tooltip("The patch of ground under the site, one square per footprint cell.")]
        public Color groundColor = new Color(1f, 0.85f, 0.55f, 0.25f);

        public string groundSortingLayer = "Ground";   // same layer as the grid overlay and the territory halo
        public int groundSortingOrder = 1000;          // just under the halo (1001), so a ghost over a site still reads

        [Tooltip("A fence's site: the width of the strip along its edge, in cells.")]
        [Range(0.05f, 1f)] public float fenceThickness = 0.25f;
    }

    // A structure that has been paid for but does not stand yet. It already holds its place — its cells
    // (or edge) on the grid, its slot in the run's count and price — and it is already a wall, but a wall
    // with one exception: the units that were standing on the spot when it was placed walk through it,
    // until each of them is out. Nobody else gets in. Once nobody is inside, the real building is put up
    // in its place (StructureSystem.CompleteSite), so a building never appears on top of anyone.
    //
    // "Lets out, never lets in" is done by naming the exceptions, not by guessing directions: every
    // insider gets Physics2D.IgnoreCollision against the wall, and gets the wall back the moment its body
    // has fully left the area. Who is inside is plain geometry (ConstructionArea) over the live units, not
    // a physics query.
    //
    // Animals are kinematic: no wall stops them, only the cells they refuse to step onto. So for them the
    // same "out, never in" comes from the grid — a site's cells refuse animals (StructureInstance.
    // UnderConstruction, see AnimalSpawner.IsBlocked), and one already standing on a refused cell may
    // always walk off it — and a cell site waits until no animal stands on its spot either. Without that
    // wait a house went up around a grazing animal, and every step it then tried led into the house.
    //
    // Built entirely in code by Create — there is no prefab to wire — and swapped for the real prefab on
    // completion; the StructureInstance stays the same object throughout.
    [RequireComponent(typeof(Structure))]
    public class ConstructionSite : MonoBehaviour
    {
        // How far past the area's edge a body must be before the wall closes behind it, so a unit that has
        // just slipped out is not caught by the wall it is still brushing.
        private const float ExitMargin = 0.05f;

        private static Sprite square;   // shared 1x1 white sprite the ground squares are scaled from

        private readonly List<Rect> area = new();
        private readonly List<Unit> insiders = new();
        private Collider2D wall;
        private SpawnSystem spawns;
        private bool clearOfAnimals;
        private Action onReady;

        // Put a site on the island: a static wall over `rects` (merged into one collider, so units bounce off
        // a site as cleanly as off a building), the blueprint and the ground patch. `poseBlueprint` places
        // and poses the throwaway copy of the prefab the blueprint is read from — anchor, row, river side,
        // fence direction — the same way the finished building will be. `clearOfAnimals`: wait for the
        // animals on the spot too (a cell site; a fence never stops an animal, so its site does not wait).
        // `onReady` runs once, when nobody is inside any more. The object is assembled INACTIVE and woken
        // whole, so every Awake (CollisionTarget caching its colliders, among them) sees the finished
        // hierarchy rather than whatever existed at the moment its component was added.
        public static Structure Create(StructureDef def, Vector2 rootPosition, IReadOnlyList<Rect> rects,
                                       Action<GameObject> poseBlueprint, ConstructionSiteLook look,
                                       SpawnSystem spawns, bool clearOfAnimals, Action onReady)
        {
            var go = new GameObject($"Site ({def.name})");
            go.SetActive(false);
            go.transform.position = rootPosition;

            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            for (int i = 0; i < rects.Count; i++)
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.size = rects[i].size;
                box.offset = rects[i].center - rootPosition;
                box.compositeOperation = Collider2D.CompositeOperation.Merge;
            }
            var wall = go.AddComponent<CompositeCollider2D>();
            wall.geometryType = CompositeCollider2D.GeometryType.Polygons;

            var structure = go.AddComponent<Structure>();
            structure.def = def;
            var site = go.AddComponent<ConstructionSite>();

            AddGround(go.transform, rects, look);
            AddBlueprint(go.transform, def, poseBlueprint, look);

            go.SetActive(true);
            wall.GenerateGeometry();
            site.Begin(wall, rects, spawns, clearOfAnimals, onReady);
            return structure;
        }

        private void Begin(Collider2D wall, IReadOnlyList<Rect> rects, SpawnSystem spawns, bool clearOfAnimals,
                           Action onReady)
        {
            this.wall = wall;
            this.spawns = spawns;
            this.clearOfAnimals = clearOfAnimals;
            this.onReady = onReady;
            area.Clear();
            for (int i = 0; i < rects.Count; i++) area.Add(rects[i]);

            // Before the next physics step: the site is placed from a click (Update), and every FixedUpdate of
            // the next frame — the simulation's included — comes after this, so no insider ever touches the wall.
            AdoptUnitsInside();
        }

        // Physics time, like the units: a frozen screen (perk pick, age transition) holds the site as it is.
        private void FixedUpdate()
        {
            for (int i = insiders.Count - 1; i >= 0; i--)
            {
                var unit = insiders[i];
                if (unit == null || !unit.IsOnField)
                {
                    // Off the island (despawned in a puff, the run ending) — there is no body here to let
                    // through any more. Give the wall back only to a body that still exists in the physics.
                    if (unit != null && unit.Body != null && unit.Body.isActiveAndEnabled)
                        Physics2D.IgnoreCollision(unit.Body, wall, false);
                    insiders.RemoveAt(i);
                    continue;
                }

                if (ConstructionArea.Overlaps(area, unit.BodyCenter, unit.BodyRadius + ExitMargin)) continue;

                Physics2D.IgnoreCollision(unit.Body, wall, false);   // out: from now on it is a wall to this unit too
                insiders.RemoveAt(i);
            }
            if (insiders.Count > 0) return;

            // Empty by the books — unless someone stepped onto the spot past the wall: a house next door
            // launching through a side the site does not close (a field's site, say, keeps the field's
            // passable verdict on the grid). Such a unit is let out like the rest before anything is built.
            AdoptUnitsInside();
            if (insiders.Count > 0) return;
            if (clearOfAnimals && AnAnimalStandsInside()) return;   // it walks off on its own — the cells refuse only newcomers

            var ready = onReady;
            onReady = null;   // once: the building replaces this object, but never twice even if it lingers a frame
            ready?.Invoke();
        }

        // Every live unit whose body touches the area and is not yet let through becomes an insider.
        private void AdoptUnitsInside()
        {
            if (spawns == null || wall == null) return;

            var units = spawns.ActiveUnits;
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit == null || !unit.IsOnField || unit.Body == null || insiders.Contains(unit)) continue;
                if (!ConstructionArea.Overlaps(area, unit.BodyCenter, unit.BodyRadius)) continue;

                Physics2D.IgnoreCollision(unit.Body, wall, true);
                insiders.Add(unit);
            }
        }

        // Whether an animal stands on the spot: its feet (the transform, where the grid reads it too) on any
        // area rect.
        private bool AnAnimalStandsInside()
        {
            var animals = AnimalWander.Alive;
            for (int i = 0; i < animals.Count; i++)
                if (animals[i] != null && ConstructionArea.Overlaps(area, animals[i].transform.position, 0f))
                    return true;
            return false;
        }

        // One faint square per area rect, on the ground layer.
        private static void AddGround(Transform parent, IReadOnlyList<Rect> rects, ConstructionSiteLook look)
        {
            var root = new GameObject("Ground").transform;
            root.SetParent(parent, false);

            for (int i = 0; i < rects.Count; i++)
            {
                var cell = new GameObject("Cell");
                cell.transform.SetParent(root, false);
                cell.transform.position = rects[i].center;
                cell.transform.localScale = new Vector3(rects[i].width, rects[i].height, 1f);

                var r = cell.AddComponent<SpriteRenderer>();
                r.sprite = Square();
                r.color = look.groundColor;
                r.sortingLayerName = look.groundSortingLayer;
                r.sortingOrder = look.groundSortingOrder;
            }
        }

        // The building's visible sprites, copied as plain renderers and tinted. Read from a copy of the
        // prefab made under an INACTIVE holder, which therefore never wakes — no Awake, no Start, no shadow,
        // nothing registers anywhere — and is thrown away once read. Only plain renderers end up under the
        // site, so nothing of the building's behaviour (a market's visit zone, a house's door) can answer
        // for the site.
        private static void AddBlueprint(Transform parent, StructureDef def, Action<GameObject> pose,
                                         ConstructionSiteLook look)
        {
            if (def.prefab == null) return;

            var holder = new GameObject("Blueprint Source");
            holder.SetActive(false);
            var copy = Instantiate(def.prefab, holder.transform);
            pose?.Invoke(copy);

            var root = new GameObject("Blueprint").transform;
            root.SetParent(parent, false);

            foreach (var source in copy.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (source.sprite == null || !source.enabled || !ShownWithin(source.transform, copy.transform))
                    continue;

                var go = new GameObject(source.name);
                go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                go.transform.localScale = source.transform.lossyScale;   // the site itself is never scaled

                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = source.sprite;
                r.flipX = source.flipX;
                r.flipY = source.flipY;
                r.drawMode = source.drawMode;
                if (source.drawMode != SpriteDrawMode.Simple) r.size = source.size;
                r.sharedMaterial = source.sharedMaterial;
                r.sortingLayerID = source.sortingLayerID;
                r.sortingOrder = source.sortingOrder;
                r.spriteSortPoint = source.spriteSortPoint;
                r.maskInteraction = source.maskInteraction;
                r.color = source.color * look.blueprintColor;
            }

            Destroy(holder);
        }

        // Whether `t` would be visible were the copy active: every object from it up to the copy's root is
        // switched on. (activeInHierarchy is false for all of them — the holder is off on purpose.)
        private static bool ShownWithin(Transform t, Transform root)
        {
            for (; t != null && t != root; t = t.parent)
                if (!t.gameObject.activeSelf) return false;
            return root.gameObject.activeSelf;
        }

        private static Sprite Square()
        {
            if (square == null)
                square = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return square;
        }
    }
}
