using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Tilemaps;
using Water2D;
using Object = UnityEngine.Object;

namespace LittlePeeps
{
    // The island rise itself: a committed section comes up out of the sea tile by tile, then the trees,
    // rocks and mountains on it pop up. Everything it shows is a function of one clock — the schedule says
    // where each tile is in its life, the profile says what that looks like — so any moment can be jumped
    // to, forwards or back (Seek), as easily as played into (Tick), and nothing here is a tween.
    //
    // A plain class, driven from outside, so that the game and the tuning window run the very same code:
    // IslandRisePlayer plays it on the game's island at an age transition, IslandRiseWindow on a sample
    // island of its own in Edit Mode. The island is an IslandDrawing either way.
    //
    // The grid never waits for this. The section is on the grid before the rise starts; what the rise holds
    // back is only how it is DRAWN. Its tiles start hidden (the caller's job) and each one is revealed as it
    // settles.
    //
    // A tile in flight is a stand-in: a pooled SpriteRenderer with the sprite of the ground tile the cell
    // will have at the moment it lands, so the swap to the real tile shows nothing. It is drawn:
    //   under water — on the island's layer below all the land and foam, over the water: the stand-in
    //                 plays the depth itself, dark and see-through. (The package's Below Water was tried
    //                 for this and dropped: it re-renders everything under the sea over the whole screen,
    //                 not a few cells, and our sea has nothing under it.)
    //   above water — on the island's layer just above the coast outline, under every structure.
    // After it lands the tile may still be wet; the drying goes on as the tilemap cell's own colour.
    //
    // A structure pops by its ROOT: switched on (if the caller left it off) when its pop starts, and
    // scaled from nothing by the profile's curve. The root sits at the bottom-centre of the footprint, so
    // everything grows up out of the ground. A den's animals, released in its Start, pop with it.
    //
    // Land already standing moves too: a new tile touching down makes the zone's landed tiles around it
    // bounce, and at the finale a breath runs across the new zone from the join. The land bends as one
    // sheet under both (IslandBend: the shader that draws it moves every corner of the grid on its own,
    // so no two tiles ever come apart), and the old island never moves — the sheet is pinned wherever it
    // touches it. What is drawn as part of the land (a river, a mountain) bends with it; what stands on it
    // rides up by the height under its root. Every lift, hop, bounce and breath can move in whole art
    // pixels (the profile's pixel snap).
    //
    // Around that, what goes off at a moment rather than lasting: bubbles over a cell until its tile breaks
    // the surface; a splash, a note and a camera kick as it does; a puff as it touches down; dust and a
    // sound as a structure pops; sparkles and a sound at the finale. Only forward play (Tick) sets these
    // off — a jump passes over the moments it skips, except the finale, which the skip lands on and plays.
    // Every effect is a profile slot (empty = off) run as a SharedEmitter; the notes climb a pentatonic
    // scale on a few voices of our own; the kick is a Cinemachine impulse, which needs a
    // CinemachineImpulseListener on the virtual camera to be felt.
    //
    // Time is whatever the caller hands to Tick: the game plays an age transition with the game paused, so
    // it passes unscaled time, and people and animals stand still while their island grows.
    public sealed class IslandRise
    {
        // Scale a popping root never goes below: a physics shape of zero size is a degenerate shape.
        private const float MinPopScale = 0.001f;
        private const int VoiceCount = 6;

        private sealed class Proxy
        {
            public GameObject gameObject;
            public Transform transform;
            public SpriteRenderer renderer;
            public Obstructor obstructor;   // null when the tiles don't push the water
        }

        private readonly Transform host;       // parent of everything this makes; its hide flags are copied
        private readonly bool pushWater;       // stand-ins in the air push on the sea (there is one)
        private readonly bool shakeCamera;     // kicks go to Cinemachine (Play Mode only)
        private readonly bool simulateEffects; // particles are stepped by hand (Edit Mode: nothing else does)

        private IslandRiseProfile profile;
        private IslandDrawing drawing;
        private IslandRiseSchedule schedule;
        private float clock;
        private float speed = 1f;

        // Per cell of the schedule.
        private Sprite[] sprites;          // the ground tile it lands as
        private Vector3[] centres;         // world position of its tile's anchor
        private Proxy[] proxies;           // its stand-in while under water or in the air; null otherwise
        private bool[] revealed;           // its real tile is on the tilemap
        private bool[] dry;                // its tilemap colour is back to white
        private float[] bubbleDebt;        // bubbles owed but not yet a whole one

        // Standing land off its place: when its neighbours touch down, how high each cell is now, and the
        // bent land that makes of it.
        private static readonly Vector2Int[] Diagonals = { new(1, 1), new(1, -1), new(-1, 1), new(-1, -1) };
        private static readonly Vector2Int[] CellsAtCorner = { new(0, 0), new(-1, 0), new(0, -1), new(-1, -1) };
        private readonly Dictionary<Vector2Int, List<float>> bounces = new();   // landed zone cell → its neighbours' touch-downs
        private readonly Dictionary<Vector2Int, float> lifts = new();          // cell → its lift this frame (snapped, ≠ 0)
        private readonly List<Vector2Int> raised = new();                      // scratch: corners the lifts raised
        private readonly IslandBend bend = new();

        // Per structure.
        private Transform[] roots;
        private Vector3[] rootPositions;
        private bool[] rides;              // moved up by hand; false for one drawn as part of the land, which bends
        private Vector3[] rootScales;
        private StructureDef[] defs;
        private AnimalSpawner[][] dens;    // the dens in each structure, whose animals pop with it
        private bool[] popped;
        private readonly Dictionary<Transform, Vector3> animalScales = new();   // animal root → its own scale

        private readonly Stack<Proxy> pool = new();
        private bool obstructorsBuilt;     // whether the pooled stand-ins carry an Obstructor

        // Sorting of the stand-ins, read from the island's tilemaps when a rise starts.
        private int sortingLayer, aboveOrder, underOrder;
        private Material tileMaterial;
        private Vector3 tileScale = Vector3.one;

        // What goes off.
        private float eventsUpTo;          // moments up to here have gone off, or were jumped over
        private float shakeLoad;           // kick still ringing, measured against the cap
        private float lastNoteAt = float.NegativeInfinity;   // real time of the last note played
        private readonly Dictionary<ParticleSystem, ParticleSystem> emitters = new();   // prefab → shared emitter (null = rejected)
        private AudioSource[] voices;
        private int nextVoice;
        private CinemachineImpulseSource impulse;

        public IslandRise(Transform host, bool pushWater, bool shakeCamera, bool simulateEffects)
        {
            this.host = host;
            this.pushWater = pushWater;
            this.shakeCamera = shakeCamera;
            this.simulateEffects = simulateEffects;
        }

        public bool IsPlaying => schedule != null;
        public bool IsOver => schedule != null && clock >= schedule.Duration;
        public float Clock => clock;                       // seconds into the rise
        public float Finale => schedule?.Finale ?? 0f;
        public float Duration => schedule?.Duration ?? 0f;

        // ---- control ----

        // Bring `section` (already on the drawing's grid, and hidden on it) up out of the sea and pop
        // `content` up on it. False, with nothing started, when there is nothing to play it with.
        public bool Start(IslandRiseProfile profile, IslandDrawing drawing, IslandSection section,
                          IReadOnlyList<StructureInstance> content, int seed)
        {
            if (IsPlaying) Finish();
            if (profile == null || drawing == null || drawing.Grid == null || drawing.GroundTilemap == null ||
                section == null) return false;

            this.profile = profile;
            this.drawing = drawing;
            var grid = drawing.Grid;
            var items = CollectItems(content, section.Content?.river);
            schedule = new IslandRiseSchedule(profile.timing, section.Cells, c => grid.GetCell(c) != null, items, seed);
            clock = 0f;
            speed = 1f;
            eventsUpTo = 0f;
            shakeLoad = 0f;

            ReadSorting();
            PrepareCells();
            PrepareBounces();
            grid.CellBounds(out var min, out var max);
            bend.Begin(drawing.GroundTilemap, min, max);
            foreach (var root in roots)
                if (root != null) root.localScale = Vector3.one * MinPopScale;

            ApplyState();
            return true;
        }

        // Forward play by `deltaTime` of the caller's time (times the tap's speed-up), setting off whatever
        // falls in between.
        public void Tick(float deltaTime)
        {
            if (!IsPlaying) return;

            float before = clock;
            clock = Mathf.Min(clock + deltaTime * speed, schedule.Duration);
            ApplyState();
            GoOff(clock - before, deltaTime);

            if (simulateEffects && deltaTime > 0f)
                foreach (var emitter in emitters.Values)
                    if (emitter != null) emitter.Simulate(deltaTime, true, false, false);
        }

        // Jump to any moment, forwards or back. Nothing in between goes off. The land shown becomes exactly
        // the cells settled by then, in one repaint whichever way the clock moved.
        public void Seek(float time)
        {
            if (!IsPlaying) return;
            clock = Mathf.Clamp(time, 0f, schedule.Duration);
            eventsUpTo = clock;

            bool changed = false;
            var stillHidden = new List<Vector2Int>();
            for (int i = 0; i < schedule.Cells.Count; i++)
            {
                bool settled = schedule.TileAt(i, clock).phase == IslandRisePhase.Settled;
                if (settled != revealed[i]) changed = true;
                revealed[i] = settled;
                dry[i] = false;   // the tint pass works the colour out again
                if (!settled) stillHidden.Add(schedule.Cells[i]);
            }
            if (changed) drawing.HideOnly(stillHidden);

            for (int j = 0; j < roots.Length; j++)
            {
                popped[j] = false;   // the pop pass works the scale out again
                if (roots[j] != null && !schedule.ItemStartedAt(j, clock))
                {
                    roots[j].localScale = rootScales[j] * MinPopScale;
                    ScaleAnimals(j, MinPopScale);
                }
            }

            ApplyState();
        }

        // Back to the start, as if just started: the tap's speed-up is undone too.
        public void Rewind()
        {
            if (!IsPlaying) return;
            speed = 1f;
            shakeLoad = 0f;
            Array.Clear(bubbleDebt, 0, bubbleDebt.Length);
            Seek(0f);
        }

        // The tap's first step: the rest of the rise at the profile's speed-up.
        public void SpeedUp()
        {
            if (IsPlaying) speed = Mathf.Max(1f, profile.tapSpeedUp);
        }

        // The tap's second step: straight to the finale. A jump, not fast play — nothing the skipped span
        // would have set off goes off; the land comes up in one repaint and every structure stands full size.
        // The finale itself still plays, so even a skip ends on its beat.
        public void SkipToFinale()
        {
            if (!IsPlaying || clock >= schedule.Finale) return;
            bool finaleDue = eventsUpTo < schedule.Finale;
            Seek(schedule.Finale);
            if (finaleDue) OnFinale();
        }

        // Everything to where the rise leaves it — all land shown and dry, every structure on and full size —
        // whether it got there or was cut short. Nothing may be left hidden.
        public void Finish()
        {
            if (!IsPlaying) return;

            for (int i = 0; i < proxies.Length; i++) Release(i);

            drawing.RevealAllLand();
            for (int i = 0; i < dry.Length; i++)
                if (!dry[i]) drawing.TintLand(schedule.Cells[i], Color.white);
            bend.End();

            for (int j = 0; j < roots.Length; j++)
                if (roots[j] != null)
                {
                    if (!roots[j].gameObject.activeSelf) roots[j].gameObject.SetActive(true);
                    roots[j].localScale = rootScales[j];
                    roots[j].position = rootPositions[j];
                }

            foreach (var animal in animalScales)
                if (animal.Key != null) animal.Key.localScale = animal.Value;
            animalScales.Clear();

            schedule = null;
            speed = 1f;
        }

        // ---- the frame ----

        private void ApplyState()
        {
            int n = schedule.Cells.Count;

            // Land first, colour second: a cell revealed later in this pass repaints its neighbours' tiles,
            // which resets their colour — tinting after everything has landed keeps a wet neighbour from
            // flashing white for a frame.
            for (int i = 0; i < n; i++)
            {
                var state = schedule.TileAt(i, clock);
                switch (state.phase)
                {
                    case IslandRisePhase.Waiting:
                        Release(i);
                        break;

                    case IslandRisePhase.Settled:
                        Release(i);
                        if (!revealed[i])
                        {
                            revealed[i] = true;
                            drawing.RevealLand(schedule.Cells[i]);
                        }
                        break;

                    default:
                        Pose(i, profile.EvaluateTile(state));
                        break;
                }
            }

            for (int i = 0; i < n; i++)
            {
                if (dry[i] || !revealed[i]) continue;
                var tint = profile.EvaluateTile(schedule.TileAt(i, clock)).tint;
                drawing.TintLand(schedule.Cells[i], tint);
                dry[i] = tint == Color.white;
            }

            for (int j = 0; j < roots.Length; j++) Pop(j);
            Lift();
        }

        // Standing land off its place: bouncing as neighbours touch down, breathing at the finale. Each
        // landed zone cell has a lift, and the land bends through them as one sheet: every corner of the grid
        // is as high as the highest lift around it — except where old land touches it, which never moves, so
        // the zone bends away from where it joins the island. A corner out on the water with a raised one
        // right above it hangs from it, so the coast's band below the island moves whole with its land.
        private void Lift()
        {
            lifts.Clear();
            foreach (var kv in bounces)
            {
                float lift = 0f;
                foreach (float at in kv.Value) lift = Mathf.Max(lift, profile.BounceLift(clock - at));
                Raise(kv.Key, lift);
            }
            for (int i = 0; i < schedule.Cells.Count; i++)
                if (schedule.BreathAt(i, clock, out float progress))
                    Raise(schedule.Cells[i], profile.breathHeight * profile.breathCurve.Evaluate(progress));

            bend.Clear();
            raised.Clear();
            foreach (var kv in lifts)
                for (int dx = 0; dx <= 1; dx++)
                    for (int dy = 0; dy <= 1; dy++)
                    {
                        var corner = new Vector2Int(kv.Key.x + dx, kv.Key.y + dy);
                        float now = bend.Get(corner);
                        if (kv.Value <= now || TouchesOldLand(corner)) continue;
                        if (now == 0f) raised.Add(corner);
                        bend.Set(corner, kv.Value);
                    }
            foreach (var corner in raised)
            {
                var below = new Vector2Int(corner.x, corner.y - 1);
                if (!TouchesLand(below)) bend.Set(below, Mathf.Max(bend.Get(below), bend.Get(corner)));
            }
            bend.Apply();

            // What stands on the zone rides up by the height under its root, the bottom-centre of its footprint.
            for (int j = 0; j < roots.Length; j++)
                if (roots[j] != null && rides[j])
                    roots[j].position = rootPositions[j] + new Vector3(0f, profile.Snap(bend.HeightAt(rootPositions[j])), 0f);
        }

        // A cell's lift for this frame: the highest asked of it, in whole pixels when the profile snaps — one
        // that snaps to nothing is no lift.
        private void Raise(Vector2Int cell, float lift)
        {
            float snapped = profile.Snap(lift);
            if (snapped > 0f && (!lifts.TryGetValue(cell, out float had) || snapped > had)) lifts[cell] = snapped;
        }

        // Whether land on screen, or old land (on screen and not the zone's), is one of the four cells
        // around a grid corner — corner (x, y) being the bottom-left corner of cell (x, y).
        private bool TouchesLand(Vector2Int corner)
        {
            foreach (var step in CellsAtCorner)
                if (drawing.ShowsLand(corner + step)) return true;
            return false;
        }

        private bool TouchesOldLand(Vector2Int corner)
        {
            foreach (var step in CellsAtCorner)
                if (drawing.ShowsLand(corner + step) && !schedule.TryGetIndex(corner + step, out _)) return true;
            return false;
        }

        private void Pose(int i, IslandRiseTilePose pose)
        {
            var sprite = sprites[i];
            if (sprite == null) return;

            var proxy = proxies[i] ??= Acquire(sprite);
            var renderer = proxy.renderer;
            renderer.color = pose.tint;
            renderer.sortingLayerID = sortingLayer;
            renderer.sortingOrder = pose.underwater ? underOrder : aboveOrder;
            if (proxy.obstructor != null) proxy.obstructor.data.child.gameObject.SetActive(!pose.underwater);

            // Under water the tile grows about its middle; above it, it stands on the bottom of its cell,
            // so a hop lifts it and a squash flattens it onto the ground rather than into the air.
            var bounds = sprite.bounds;
            float anchorY = pose.underwater ? bounds.center.y : bounds.min.y;
            var offset = new Vector3(bounds.center.x * (1f - pose.scale.x), anchorY * (1f - pose.scale.y) + pose.lift, 0f);
            proxy.transform.position = centres[i] + profile.Snap(Vector3.Scale(offset, tileScale));
            proxy.transform.localScale = Vector3.Scale(new Vector3(pose.scale.x, pose.scale.y, 1f), tileScale);
        }

        private void Pop(int j)
        {
            if (popped[j] || !schedule.ItemStartedAt(j, clock)) return;

            var root = roots[j];
            if (root == null)
            {
                popped[j] = true;   // sold or destroyed since the section was committed
                return;
            }

            if (!root.gameObject.activeSelf) root.gameObject.SetActive(true);
            float progress = schedule.ItemProgressAt(j, clock);
            float scale = Mathf.Max(MinPopScale, profile.PopScaleOf(defs[j], progress));
            root.localScale = rootScales[j] * scale;
            ScaleAnimals(j, scale);
            if (progress < 1f) return;

            root.localScale = rootScales[j];
            ScaleAnimals(j, 1f);
            popped[j] = true;
        }

        // A den's animals come out with it. They are made in the den's Start, a frame or so after its pop
        // switched it on, so they are picked up whenever they turn up and scaled in step with the den.
        private void ScaleAnimals(int j, float scale)
        {
            foreach (var den in dens[j])
            {
                if (den == null) continue;
                foreach (var animal in den.Animals)
                {
                    if (animal == null) continue;
                    var body = animal.transform.root;
                    if (!animalScales.TryGetValue(body, out var full)) animalScales[body] = full = body.localScale;
                    body.localScale = full * scale;
                }
            }
        }

        // ---- what goes off ----

        // Everything whose moment falls between the last frame's clock and this one's. `elapsed` is rise
        // time (sped up with the tap); `deltaTime` is the caller's own, for the kick's ring-down.
        private void GoOff(float elapsed, float deltaTime)
        {
            float from = eventsUpTo, to = clock;
            eventsUpTo = to;
            shakeLoad *= Mathf.Exp(-deltaTime / Mathf.Max(0.01f, profile.shakeDuration));

            var bubbles = Emitter(profile.bubblesFx, "bubbles");
            for (int i = 0; i < schedule.Cells.Count; i++)
            {
                float breach = schedule.BreachOf(i);
                if (bubbles != null && to < breach)
                {
                    bubbleDebt[i] += profile.bubblesPerSecond * elapsed;
                    int whole = (int)bubbleDebt[i];
                    bubbleDebt[i] -= whole;
                    SharedEmitter.EmitAt(bubbles, centres[i], whole);
                }
                if (from < breach && breach <= to) OnBreach(i);

                float touchDown = schedule.TouchDownOf(i);
                if (from < touchDown && touchDown <= to)
                    SharedEmitter.EmitAt(Emitter(profile.touchDownFx, "touch-down"), centres[i], profile.touchDownCount);
            }

            for (int j = 0; j < roots.Length; j++)
            {
                float start = schedule.ItemStartOf(j);
                if (from < start && start <= to && roots[j] != null)
                {
                    SharedEmitter.EmitAt(Emitter(profile.popFx, "pop"), roots[j].position, profile.popCount);
                    PlaySound(profile.popClip, profile.popVolume, 1f);
                }
            }

            if (from < schedule.Finale && schedule.Finale <= to) OnFinale();
        }

        private void OnBreach(int i)
        {
            SharedEmitter.EmitAt(Emitter(profile.splashFx, "splash"), centres[i], profile.splashCount);
            PlayNote(schedule.NoteOf(i));
            Kick();
        }

        private void OnFinale()
        {
            var sparkles = Emitter(profile.finaleFx, "finale");
            for (int i = 0; i < schedule.Cells.Count; i++)
                SharedEmitter.EmitAt(sparkles, centres[i], profile.finaleCountPerCell);
            PlaySound(profile.finaleClip, profile.finaleVolume, 1f);
        }

        // The slot's shared emitter, made on first use and kept for later rises. A rejected prefab is cached
        // as null, so it is reported once rather than every frame.
        private ParticleSystem Emitter(ParticleSystem prefab, string what)
        {
            if (prefab == null) return null;
            if (emitters.TryGetValue(prefab, out var cached)) return cached;

            var emitter = SharedEmitter.Create(prefab, host, $"Rise {what}",
                                               $"IslandRise: '{prefab.name}' ({what} effect of the island rise)",
                                               $"the profile's {what} count", pausedPlay: true);
            if (emitter != null) HideLikeHost(emitter.gameObject);
            emitters[prefab] = emitter;
            return emitter;
        }

        private void PlayNote(int note)
        {
            if (profile.breachClip == null) return;
            float now = Time.realtimeSinceStartup;
            if (now - lastNoteAt < profile.minNoteGap) return;
            lastNoteAt = now;
            PlaySound(profile.breachClip, profile.breachVolume,
                      IslandRiseProfile.NotePitch(note, schedule.Cells.Count, profile.noteSteps, profile.basePitch));
        }

        // One sound on the next voice in turn. Pitch belongs to an AudioSource, not to a single play, so
        // notes sharing one source would all bend to the newest; with a few voices taking turns, each note
        // keeps its own. A voice cuts its previous sound off — it is the oldest one playing by then.
        private void PlaySound(AudioClip clip, float volume, float pitch)
        {
            if (clip == null) return;
            voices ??= CreateVoices();
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.clip = clip;
            voice.volume = volume;
            voice.pitch = pitch;
            voice.Play();
        }

        private AudioSource[] CreateVoices()
        {
            var holder = new GameObject("Rise voices");
            holder.transform.SetParent(host, false);
            HideLikeHost(holder);
            var result = new AudioSource[VoiceCount];
            for (int v = 0; v < result.Length; v++)
            {
                result[v] = holder.AddComponent<AudioSource>();
                result[v].playOnAwake = false;
                result[v].spatialBlend = 0f;   // heard the same wherever the camera is
            }
            return result;
        }

        // A kick of the camera per tile breaking the surface; a rush of them adds up to the cap and no more.
        private void Kick()
        {
            if (!shakeCamera || !Application.isPlaying) return;

            float strength = Mathf.Min(profile.shakePerBreach, profile.shakeCap - shakeLoad);
            if (strength <= 0f) return;
            shakeLoad += strength;

            impulse ??= CreateImpulse();
            impulse.ImpulseDefinition.ImpulseDuration = Mathf.Max(0.01f, profile.shakeDuration);
            impulse.GenerateImpulseWithVelocity(Vector3.down * strength);
        }

        // Added here, not set up in the scene. AddComponent skips the component's editor-only Reset, so the
        // signal is spelled out: a bump, felt the same by every listener wherever the zone is. Impulses run
        // on game time by default and the rise plays paused, so the manager is switched to real time — for
        // every impulse, which is what a game that pauses this much wants anyway.
        private CinemachineImpulseSource CreateImpulse()
        {
            CinemachineImpulseManager.Instance.IgnoreTimeScale = true;
            var source = host.gameObject.AddComponent<CinemachineImpulseSource>();
            source.ImpulseDefinition = new CinemachineImpulseDefinition
            {
                ImpulseChannel = 1,
                ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump,
                ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform,
                ImpulseDuration = Mathf.Max(0.01f, profile.shakeDuration)
            };
            source.DefaultVelocity = Vector3.down;
            return source;
        }

        // ---- setup ----

        // `river`: the zone's river path, if it has one — a structure on it runs in with the river.
        private List<IslandRiseItem> CollectItems(IReadOnlyList<StructureInstance> content, IReadOnlyList<Vector2Int> river)
        {
            var onRiver = new Dictionary<Vector2Int, int>();
            if (river != null)
                for (int k = 0; k < river.Count; k++) onRiver[river[k]] = k;

            var items = new List<IslandRiseItem>();
            var rootList = new List<Transform>();
            var defList = new List<StructureDef>();

            if (content != null)
                foreach (var instance in content)
                {
                    if (instance == null || instance.RuntimeObject == null || instance.Def == null) continue;
                    int flowIndex = onRiver.TryGetValue(instance.Cell, out int k) ? k : -1;
                    items.Add(new IslandRiseItem(FootprintCells(instance), profile.PopDurationOf(instance.Def), flowIndex));
                    rootList.Add(instance.RuntimeObject.transform);
                    defList.Add(instance.Def);
                }

            roots = rootList.ToArray();
            defs = defList.ToArray();
            rootScales = new Vector3[roots.Length];
            rootPositions = new Vector3[roots.Length];
            rides = new bool[roots.Length];
            dens = new AnimalSpawner[roots.Length][];
            for (int j = 0; j < roots.Length; j++)
            {
                rootScales[j] = roots[j].localScale;
                rootPositions[j] = roots[j].position;
                rides[j] = !DrawnAsLand(roots[j]);
                dens[j] = roots[j].GetComponentsInChildren<AnimalSpawner>(true);
            }
            popped = new bool[roots.Length];
            animalScales.Clear();
            return items;
        }

        // A structure with any of its renderers on a bending material is drawn as part of the land (a river,
        // a mountain): it bends with the land, and moving it up as well would lift it twice.
        private static bool DrawnAsLand(Transform root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    if (IslandBend.Bends(material)) return true;
            return false;
        }

        private static List<Vector2Int> FootprintCells(StructureInstance instance)
        {
            var footprint = instance.Def.Footprint;
            var cells = new List<Vector2Int>(footprint.CellCount);
            for (int x = 0; x < footprint.Size.x; x++)
                for (int y = 0; y < footprint.Size.y; y++)
                    if (footprint.Contains(x, y)) cells.Add(instance.Cell + new Vector2Int(x, y));
            return cells;
        }

        // Each cell's sprite is the ground piece it will have when it lands — the land then being the old
        // island plus every cell that has landed by that moment — so its tile takes over without a change.
        private void PrepareCells()
        {
            int n = schedule.Cells.Count;
            sprites = new Sprite[n];
            centres = new Vector3[n];
            proxies = new Proxy[n];
            revealed = new bool[n];
            dry = new bool[n];
            bubbleDebt = new float[n];

            var tilemap = drawing.GroundTilemap;
            var anchor = tilemap.tileAnchor;
            bool warned = false;
            for (int i = 0; i < n; i++)
            {
                var cell = schedule.Cells[i];
                float lands = schedule.SettleOf(i);
                var tile = drawing.GroundTileAt(cell, c => schedule.TryGetIndex(c, out int k) && schedule.SettleOf(k) > lands);
                sprites[i] = (tile as Tile)?.sprite;
                if (tile != null && sprites[i] == null && !warned)
                {
                    Debug.LogWarning($"IslandRise: ground tile '{tile.name}' is not a plain Tile — its cells rise " +
                                     "without a stand-in and simply appear when they land.", tile);
                    warned = true;
                }

                var local = tilemap.CellToLocalInterpolated(new Vector3(cell.x + anchor.x, cell.y + anchor.y, 0f));
                centres[i] = tilemap.transform.TransformPoint(local);
            }
        }

        // Who bounces when: as each new tile touches down, its neighbours in the zone that have already
        // settled into real tiles. The old island never moves, so its land does not bounce. Worked out once,
        // like the rest of the schedule.
        private void PrepareBounces()
        {
            bounces.Clear();
            for (int i = 0; i < schedule.Cells.Count; i++)
            {
                float at = schedule.TouchDownOf(i);
                for (int d = 0; d < IslandShape.Dirs.Length + (profile.bounceDiagonals ? Diagonals.Length : 0); d++)
                {
                    var step = d < IslandShape.Dirs.Length ? IslandShape.Dirs[d] : Diagonals[d - IslandShape.Dirs.Length];
                    var neighbour = schedule.Cells[i] + step;
                    if (!schedule.TryGetIndex(neighbour, out int k)) continue;   // sea or old land
                    if (schedule.SettleOf(k) > at) continue;                     // still coming up
                    if (!bounces.TryGetValue(neighbour, out var times)) bounces[neighbour] = times = new List<float>();
                    times.Add(at);
                }
            }
        }

        private void ReadSorting()
        {
            var ground = drawing.GroundTilemap.GetComponent<TilemapRenderer>();
            var trim = drawing.TrimTilemap != null ? drawing.TrimTilemap.GetComponent<TilemapRenderer>() : null;

            sortingLayer = ground.sortingLayerID;
            int lowest = ground.sortingOrder, highest = ground.sortingOrder;
            if (trim != null && trim.sortingLayerID == ground.sortingLayerID)
            {
                lowest = Mathf.Min(lowest, trim.sortingOrder);
                highest = Mathf.Max(highest, trim.sortingOrder);
            }
            aboveOrder = highest + 1;
            underOrder = lowest - 2;   // under the side foam, which WaterSystem sorts one below the lowest coast layer

            tileMaterial = IslandBend.Unbent(ground.sharedMaterial);   // a tile in flight is not part of the sheet
            tileScale = drawing.GroundTilemap.transform.lossyScale;
        }

        // ---- the pool ----

        // The sprite goes on BEFORE the stand-in is switched on: an Obstructor copies its sprite as it
        // enables, and a missing one throws inside the package.
        private Proxy Acquire(Sprite sprite)
        {
            bool obstruct = pushWater && profile.obstructOnProxy;
            if (obstruct != obstructorsBuilt) ClearPool();   // the setting changed since they were made
            obstructorsBuilt = obstruct;

            var proxy = pool.Count > 0 ? pool.Pop() : Create(obstruct);
            proxy.renderer.sprite = sprite;
            proxy.renderer.sharedMaterial = tileMaterial;
            proxy.gameObject.SetActive(true);
            return proxy;
        }

        private Proxy Create(bool obstruct)
        {
            var go = new GameObject("Rising tile");
            go.SetActive(false);
            go.transform.SetParent(host, false);
            HideLikeHost(go);
            var proxy = new Proxy { gameObject = go, transform = go.transform, renderer = go.AddComponent<SpriteRenderer>() };
            if (obstruct)
            {
                proxy.obstructor = go.AddComponent<Obstructor>();   // wakes on first activation
                // The obstruction material's own height, which the coast's tilemap twins draw with. A sprite
                // Obstructor overrides it with this field, whose default of 0 is not what the coast uses.
                proxy.obstructor.height = 1f;
            }
            return proxy;
        }

        private void Release(int i)
        {
            var proxy = proxies[i];
            if (proxy == null) return;
            proxies[i] = null;
            if (proxy.gameObject == null) return;
            if ((proxy.obstructor != null) != obstructorsBuilt)
            {
                Discard(proxy.gameObject);        // made before the setting changed: the pool keeps one kind
                return;
            }
            proxy.gameObject.SetActive(false);   // takes the Obstructor's child — the obstruction — with it
            pool.Push(proxy);
        }

        private void ClearPool()
        {
            while (pool.Count > 0)
            {
                var proxy = pool.Pop();
                if (proxy.gameObject != null) Discard(proxy.gameObject);
            }
        }

        // What this makes is hidden and unsaved exactly as its host is: nothing in the game, everything in
        // the tuning window, whose objects must never reach the scene file.
        private void HideLikeHost(GameObject go)
        {
            var flags = host.gameObject.hideFlags;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = flags;
        }

        // Destroy is refused in Edit Mode, where the tuning window runs this.
        private static void Discard(Object obj)
        {
            if (Application.isPlaying) Object.Destroy(obj);
            else Object.DestroyImmediate(obj);
        }
    }
}
