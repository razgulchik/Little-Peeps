using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace LittlePeeps.EditorTools
{
    // Edit Mode tuning tool for the island rise: grows a sample island with the game's own generator, puts
    // it in the open scene at a point you drag around, and plays the rise of its next zone over and over —
    // with a time slider to stop on any frame, forwards or back, a speed control, and the tap's two steps.
    //
    // The rise shown here is not a reconstruction: it is IslandRise, the class the game plays at an age
    // transition, run on an IslandDrawing of the window's own instead of the game's island. The profile is
    // read live — a curve edit shows on the next frame; a timing edit re-plans the rise where the clock
    // stands. What Edit Mode cannot show: the real water (the game spawns it at run time, so a flat sea
    // stands in for it), anything the structures' own scripts do (they are dormant here — no shadows, no
    // animals out of a den) and the camera kick. Play Mode shows all of it; the window then offers the
    // player's Replay Last Zone instead.
    //
    // A window rather than a scene component for the reason HarvestFeedbackWindow gives: nothing here may
    // reach SampleScene.unity. Every object the preview makes is HideAndDontSave — not saved, not in the
    // hierarchy, gone when the window closes (or the domain reloads, or Play Mode starts).
    public class IslandRiseWindow : EditorWindow
    {
        private const string PrefPrefix = "LittlePeeps.IslandRise.";

        // What to preview.
        private IslandRiseProfile profile;
        private BiomeDef biome;
        private IslandTileSet oldIslandTiles;
        private int seed = 1;
        private int agesBefore = 1;
        private Color seaColour = new(0.051f, 0.529f, 0.804f, 1f);   // tile_water_base
        private Vector3 origin = new(80f, 0f, 0f);

        // Playback.
        private bool playing;
        private bool loop = true;
        private float playbackSpeed = 1f;
        private float endPause = 1f;
        private double lastTime;
        private float heldAtEnd;

        // The preview.
        private GameObject root;
        private IslandDrawing drawing;
        private IslandRise rise;
        private IslandSection zone;
        private readonly List<StructureInstance> content = new();
        private Sprite seaSprite;
        private Bounds worldBounds;
        private string problem;       // why there is no preview, shown in the window
        private string scheduleKey;   // the profile values the rise was planned with
        private int profileDirtyCount;

        [MenuItem("Window/Little Peeps/Island Rise")]
        private static void Open()
        {
            var window = GetWindow<IslandRiseWindow>();
            window.titleContent = new GUIContent("Island Rise");
            window.minSize = new Vector2(340f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            LoadPrefs();
            EditorApplication.update += Tick;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            // Also runs on domain reload, which is what keeps the preview objects from outliving the managed
            // state that knows how to destroy them.
            EditorApplication.update -= Tick;
            SceneView.duringSceneGui -= OnSceneGUI;
            Cleanup();
            SavePrefs();
        }

        // ---------------------------------------------------------------- GUI

        private void OnGUI()
        {
            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("In Play Mode the rise plays in the game itself, with the real water, " +
                                        "the structures' own behaviour and the camera kick.", MessageType.Info);
                if (GUILayout.Button("Replay last zone"))
                {
                    var player = FindFirstObjectByType<IslandRisePlayer>();
                    if (player != null) player.ReplayLastZone();
                    else Debug.LogWarning("IslandRiseWindow: no IslandRisePlayer in the scene.");
                }
                return;
            }

            EditorGUILayout.LabelField("What to preview", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            profile = (IslandRiseProfile)EditorGUILayout.ObjectField("Profile", profile, typeof(IslandRiseProfile), false);
            biome = (BiomeDef)EditorGUILayout.ObjectField(
                new GUIContent("Zone biome", "The biome of the zone that rises: its tiles and what grows on it."),
                biome, typeof(BiomeDef), false);
            oldIslandTiles = (IslandTileSet)EditorGUILayout.ObjectField(
                new GUIContent("Old island tiles", "The tiles the island the zone grows from is drawn with."),
                oldIslandTiles, typeof(IslandTileSet), false);
            using (new EditorGUILayout.HorizontalScope())
            {
                seed = EditorGUILayout.IntField(new GUIContent("Seed", "Which island and zone the generator grows."), seed);
                if (GUILayout.Button("New shape", GUILayout.Width(90f))) seed++;
            }
            agesBefore = EditorGUILayout.IntSlider(
                new GUIContent("Ages before", "How many zones the island has grown before the one that rises."),
                agesBefore, 0, 4);
            seaColour = EditorGUILayout.ColorField(
                new GUIContent("Sea colour", "The flat sea under the preview. The real water only exists at run time."),
                seaColour);
            if (EditorGUI.EndChangeCheck()) Rebuild(keepTime: false);

            if (problem != null) EditorGUILayout.HelpBox(problem, MessageType.Warning);
            else if (rise == null && GUILayout.Button("Build preview")) Rebuild(keepTime: false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Playback", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(rise == null || !rise.IsPlaying))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(playing ? "Pause" : "Play")) SetPlaying(!playing);
                    if (GUILayout.Button("Restart")) Restart();
                    string tap = profile != null ? $"Tap ×{profile.tapSpeedUp:0.#}" : "Tap";
                    if (GUILayout.Button(new GUIContent(tap, "The first tap: the rest of the rise faster."))) { rise.SpeedUp(); SetPlaying(true); }
                    if (GUILayout.Button(new GUIContent("Skip", "The second tap: straight to the finale."))) { rise.SkipToFinale(); SetPlaying(true); }
                }

                loop = EditorGUILayout.Toggle("Loop", loop);
                playbackSpeed = EditorGUILayout.Slider(new GUIContent("Speed", "Preview speed, on top of the tap's."),
                                                       playbackSpeed, 0.25f, 3f);

                if (rise != null && rise.IsPlaying)
                {
                    EditorGUI.BeginChangeCheck();
                    float t = EditorGUILayout.Slider("Time", rise.Clock, 0f, rise.Duration);
                    if (EditorGUI.EndChangeCheck())
                    {
                        SetPlaying(false);
                        rise.Seek(t);
                        SceneView.RepaintAll();
                    }
                    EditorGUILayout.LabelField(" ", $"finale at {rise.Finale:0.00} s, banner at {rise.Duration:0.00} s",
                                               EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(root == null))
                if (GUILayout.Button("Frame in Scene view") && SceneView.lastActiveSceneView != null)
                    SceneView.lastActiveSceneView.Frame(worldBounds, false);

            EditorGUILayout.HelpBox("Drag the preview's handle in the Scene view to move it. Not shown here: the " +
                                    "real water, the structures' own behaviour (shadows, animals) and the camera " +
                                    "kick — Play Mode has them. Effects need Use Unscaled Time, as in the game.",
                                    MessageType.None);
        }

        private void OnSceneGUI(SceneView view)
        {
            if (Application.isPlaying || root == null) return;

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.PositionHandle(origin, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                origin = new Vector3(moved.x, moved.y, 0f);
                Rebuild(keepTime: true);
            }
            Handles.Label(origin + Vector3.up * 0.6f, "Island Rise Preview");
        }

        // ---------------------------------------------------------------- playback

        private void SetPlaying(bool on)
        {
            playing = on;
            lastTime = EditorApplication.timeSinceStartup;
            if (on && rise != null && rise.IsOver) Restart();
        }

        private void Restart()
        {
            if (rise == null) return;
            rise.Rewind();
            heldAtEnd = 0f;
            playing = true;
            lastTime = EditorApplication.timeSinceStartup;
            SceneView.RepaintAll();
        }

        private void Tick()
        {
            // Entering Play Mode would tear the scene down under the preview objects.
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (root != null)
                {
                    Cleanup();
                    Repaint();
                }
                playing = false;
                return;
            }
            if (rise == null) return;

            // A profile edit: a timing change re-plans the rise where the clock stands; anything else (a
            // curve, a colour) only needs the current frame drawn again, which a paused preview would not do
            // on its own.
            if (profile != null && EditorUtility.GetDirtyCount(profile) != profileDirtyCount)
            {
                profileDirtyCount = EditorUtility.GetDirtyCount(profile);
                if (ScheduleKey() != scheduleKey) Rebuild(keepTime: true);
                else if (!playing) rise.Seek(rise.Clock);
                SceneView.RepaintAll();
                Repaint();
            }
            if (rise == null || !playing) return;

            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Min((float)(now - lastTime), 0.1f) * playbackSpeed;
            lastTime = now;

            if (!rise.IsOver) rise.Tick(dt);
            else if ((heldAtEnd += dt) >= endPause)
            {
                heldAtEnd = 0f;
                if (loop) rise.Rewind();
                else playing = false;
            }

            SceneView.RepaintAll();
            Repaint();
        }

        // ---------------------------------------------------------------- the preview

        private void Rebuild(bool keepTime)
        {
            float time = keepTime && rise != null && rise.IsPlaying ? rise.Clock : 0f;
            Cleanup();
            problem = Build();
            if (problem != null) Cleanup();
            else if (keepTime) rise.Seek(time);
            heldAtEnd = 0f;
            lastTime = EditorApplication.timeSinceStartup;
            SceneView.RepaintAll();
        }

        // The sample island, drawn and ready to rise; or why not.
        private string Build()
        {
            if (profile == null) return "Pick a profile.";
            if (biome == null) return "Pick the zone's biome.";
            if (oldIslandTiles == null && biome.tileSet == null) return "Pick the old island's tiles.";
            try { biome.profile.Validate(); }
            catch (ArgumentException e) { return $"Biome '{biome.name}': {e.Message}"; }

            // The island the zone grows from, grown the way the game grows it.
            var generator = new IslandGenerator(new IslandRules(), seed);
            generator.Commit(generator.GenerateStart());
            for (int age = 0; age < agesBefore; age++)
            {
                var grown = generator.Propose(1);
                if (grown.Count == 0) break;
                generator.Commit(grown[0]);
            }
            var candidates = generator.Propose(1);
            if (candidates.Count == 0) return $"Seed {seed} leaves no room for another zone — try another.";
            var zoneContent = generator.Populate(candidates[0], biome.profile);
            zone = generator.Commit(candidates[0], zoneContent);

            var zoneTerrain = biome.profile.terrain;
            var grid = new IslandGrid(1f);
            foreach (var cell in generator.Land) grid.SetCell(cell, zone.Contains(cell) ? zoneTerrain : TerrainType.Grass);
            var zoneTiles = biome.tileSet != null ? biome.tileSet : oldIslandTiles;
            var otherTiles = oldIslandTiles != null ? oldIslandTiles : biome.tileSet;

            root = new GameObject("Island Rise Preview") { hideFlags = HideFlags.HideAndDontSave };
            root.transform.position = origin;

            // Drawn like the game's island: the same sorting and material as its tilemaps, when the scene has them.
            var sceneIsland = FindFirstObjectByType<IslandSystem>();
            var gridObject = Child(root, "Grid");
            gridObject.AddComponent<Grid>();
            var ground = NewTilemap(gridObject, "Ground", sceneIsland != null ? sceneIsland.GroundTilemap : null, 0);
            var trim = NewTilemap(gridObject, "GroundTrim", sceneIsland != null ? sceneIsland.TrimTilemap : null, 1);

            drawing = new IslandDrawing(grid, terrain => terrain == zoneTerrain ? zoneTiles : otherTiles, ground, trim);
            drawing.HideLand(zone.Cells);

            grid.CellBounds(out var min, out var max);
            MakeSea(min, max);
            PlaceContent(zoneContent, grid);

            var host = Child(root, "Rise");
            rise = new IslandRise(host.transform, pushWater: false, shakeCamera: false, simulateEffects: true);
            if (!rise.Start(profile, drawing, zone, content, seed)) return "The rise could not start.";

            scheduleKey = ScheduleKey();
            profileDirtyCount = EditorUtility.GetDirtyCount(profile);
            return null;
        }

        private static GameObject Child(GameObject parent, string name)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static Tilemap NewTilemap(GameObject parent, string name, Tilemap like, int fallbackOrder)
        {
            var go = Child(parent, name);
            var tilemap = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();

            var likeRenderer = like != null ? like.GetComponent<TilemapRenderer>() : null;
            if (likeRenderer != null)
            {
                tilemap.tileAnchor = like.tileAnchor;
                renderer.sortingLayerID = likeRenderer.sortingLayerID;
                renderer.sortingOrder = likeRenderer.sortingOrder;
                renderer.sharedMaterial = likeRenderer.sharedMaterial;
            }
            else
            {
                renderer.sortingLayerName = "Ground";
                renderer.sortingOrder = fallbackOrder;
            }
            return tilemap;
        }

        // A flat sea under the island, a few cells past its coast: the real water is spawned at run time.
        private void MakeSea(Vector2Int min, Vector2Int max)
        {
            const int margin = 6;
            if (seaSprite == null)
            {
                seaSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
                seaSprite.hideFlags = HideFlags.HideAndDontSave;
            }

            var sea = Child(root, "Sea");
            var renderer = sea.AddComponent<SpriteRenderer>();
            renderer.sprite = seaSprite;
            renderer.color = seaColour;
            renderer.sortingLayerName = "Water";
            renderer.sortingOrder = -100;

            var size = new Vector2(max.x - min.x + 1 + 2 * margin, max.y - min.y + 1 + 2 * margin);
            var centre = new Vector2((min.x + max.x + 1) * 0.5f, (min.y + max.y + 1) * 0.5f);
            sea.transform.localPosition = centre;
            sea.transform.localScale = new Vector3(size.x, size.y, 1f);
            worldBounds = new Bounds(origin + (Vector3)centre, new Vector3(size.x, size.y, 1f));
        }

        // The zone's structures as the game would place them, as pictures only: every script and collider
        // on them is off, so nothing ticks or takes part in anything.
        private void PlaceContent(IslandSectionContent zoneContent, IslandGrid grid)
        {
            content.Clear();
            if (zoneContent == null) return;

            foreach (var (cell, def) in zoneContent.Features(null))
            {
                if (def == null || def.prefab == null) continue;

                var go = Instantiate(def.prefab, root.transform);
                go.name = $"{def.prefab.name} (Island Rise Preview)";
                go.transform.localPosition = grid.OriginToWorldAnchor(cell, def.size);
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
                foreach (var col in go.GetComponentsInChildren<Collider2D>(true)) col.enabled = false;

                var structure = go.GetComponent<Structure>();
                if (structure == null)
                {
                    DestroyImmediate(go);
                    continue;
                }
                content.Add(new StructureInstance { Def = def, RuntimeObject = structure, Cell = cell });
            }
        }

        // The profile values the rise is PLANNED from — its timing, pop lengths and who bounces. The rest
        // (curves, colours, heights, effects) is read every frame and needs no re-plan.
        private string ScheduleKey()
        {
            if (profile == null) return string.Empty;
            var key = new StringBuilder(JsonUtility.ToJson(profile.timing));
            key.Append('|').Append(profile.popDuration).Append('|').Append(profile.bounceDiagonals);
            foreach (var own in profile.popOverrides)
                if (own != null) key.Append('|').Append(own.def != null ? own.def.name : "-").Append(':').Append(own.duration);
            return key.ToString();
        }

        private void Cleanup()
        {
            rise?.Finish();   // ends the land's bend, whose shader switch is the whole project's
            if (root != null) DestroyImmediate(root);
            root = null;
            drawing = null;
            rise = null;
            zone = null;
            content.Clear();
            if (seaSprite != null) DestroyImmediate(seaSprite);
            seaSprite = null;
        }

        // ---------------------------------------------------------------- persistence

        private void LoadPrefs()
        {
            profile = LoadAsset<IslandRiseProfile>("profile");
            biome = LoadAsset<BiomeDef>("biome");
            oldIslandTiles = LoadAsset<IslandTileSet>("oldIslandTiles");

            // First time: take what the scene's island uses, and the first profile there is.
            var sceneIsland = FindFirstObjectByType<IslandSystem>();
            if (biome == null && sceneIsland != null && sceneIsland.Biomes.Count > 0) biome = sceneIsland.Biomes[0];
            if (oldIslandTiles == null && sceneIsland != null)
                oldIslandTiles = new SerializedObject(sceneIsland).FindProperty("tileSet")?.objectReferenceValue as IslandTileSet;
            if (profile == null)
            {
                var found = AssetDatabase.FindAssets("t:IslandRiseProfile");
                if (found.Length > 0) profile = AssetDatabase.LoadAssetAtPath<IslandRiseProfile>(AssetDatabase.GUIDToAssetPath(found[0]));
            }

            seed = EditorPrefs.GetInt(PrefPrefix + "seed", 1);
            agesBefore = EditorPrefs.GetInt(PrefPrefix + "agesBefore", 1);
            origin = new Vector3(EditorPrefs.GetFloat(PrefPrefix + "posX", 80f), EditorPrefs.GetFloat(PrefPrefix + "posY", 0f), 0f);
            if (ColorUtility.TryParseHtmlString(EditorPrefs.GetString(PrefPrefix + "sea", string.Empty), out var sea)) seaColour = sea;
            loop = EditorPrefs.GetBool(PrefPrefix + "loop", true);
            playbackSpeed = EditorPrefs.GetFloat(PrefPrefix + "speed", 1f);
        }

        private void SavePrefs()
        {
            SaveAsset("profile", profile);
            SaveAsset("biome", biome);
            SaveAsset("oldIslandTiles", oldIslandTiles);

            EditorPrefs.SetInt(PrefPrefix + "seed", seed);
            EditorPrefs.SetInt(PrefPrefix + "agesBefore", agesBefore);
            EditorPrefs.SetFloat(PrefPrefix + "posX", origin.x);
            EditorPrefs.SetFloat(PrefPrefix + "posY", origin.y);
            EditorPrefs.SetString(PrefPrefix + "sea", "#" + ColorUtility.ToHtmlStringRGBA(seaColour));
            EditorPrefs.SetBool(PrefPrefix + "loop", loop);
            EditorPrefs.SetFloat(PrefPrefix + "speed", playbackSpeed);
        }

        // Asset references are remembered by GUID rather than by path, so renaming or moving an asset does not
        // silently empty the window.
        private static T LoadAsset<T>(string key) where T : Object
        {
            string guid = EditorPrefs.GetString(PrefPrefix + key, string.Empty);
            if (string.IsNullOrEmpty(guid)) return null;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static void SaveAsset(string key, Object asset)
        {
            string path = asset != null ? AssetDatabase.GetAssetPath(asset) : null;
            string guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            EditorPrefs.SetString(PrefPrefix + key, guid);
        }
    }
}
