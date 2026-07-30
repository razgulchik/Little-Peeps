using TMPro;
using UnityEditor;
using UnityEngine;

namespace LittlePeeps.EditorTools
{
    // Edit Mode authoring aid for harvest feedback: replays the whole thing a player sees when a node
    // is reaped — the node itself going away, the pickup particles firing, the number climbing — at a
    // point you drag around the open scene, all from the same tick. It never publishes gameplay
    // events or credits resources.
    //
    // A window rather than a scene component, deliberately. A component has to live in
    // SampleScene.unity, which several people edit and whose merges are done by hand — a tool object
    // parked there is pure merge tax, and one more thing that can ship by accident. Nothing here
    // touches the scene except the preview objects, and those are HideAndDontSave: not saved, not in
    // the hierarchy, gone when the window closes.
    //
    // The whole file lives in LittlePeeps.Editor, an Editor-only assembly, which is why there is not
    // a single #if UNITY_EDITOR in it and why none of it can reach a build.
    //
    // WHAT IT IS AND IS NOT: the number and the particles are driven by the very assets the game
    // reads (HarvestNumberMotionDef, ResourceSourceDef.pickupFx), and the fade goes through the same
    // HarvestFade the game uses. But this is a RECONSTRUCTION of the harvest, not the runtime path —
    // ResourceSource.OnHit cannot be called here without a Unit, a ResourceSystem, a published event
    // and credited resources. It matches because it reads the same authored values, not because it is
    // the same code.
    public class HarvestFeedbackWindow : EditorWindow
    {
        private const string PrefPrefix = "LittlePeeps.HarvestFeedback.";

        // What the node does on the hit that uses it up. Three different mechanics in the game, and
        // the tool has to be honest about which one a given prefab has.
        private enum NodeBehaviour
        {
            None,       // no prefab, or nothing recognisable on it
            Fades,      // ResourceSource with fadeOutTime > 0: ready visual dissolves
            Vanishes,   // ResourceSource with fadeOutTime == 0: ready visual switches off outright
            Infinite,   // ResourceSourceDef.infinite: Market/Smithy never change at all
            Despawns    // Animal: destroyed outright, no fade anywhere in the component
        }

        private HarvestNumberMotionDef motion;
        private TextMeshPro numberPrefab;
        private ResourceSourceDef source;
        private GameObject resourcePrefab;

        private float amount = 1f;

        // Not `position`: EditorWindow already has one, and it is the window's rect on screen.
        private Vector3 spawnPoint;

        private bool showObject = true;
        private bool finalHit = true;

        private bool loop = true;
        private float leadIn = 0.5f;
        private float interval = 1.2f;
        private float playbackSpeed = 1f;
        private bool includeNumberJitter;

        private bool previewing;
        private double lastTime;
        private float elapsed;
        private bool hitFired;

        private TextMeshPro numberView;
        private ParticleSystem pickupView;
        private Vector3 numberOrigin;
        private Vector2 currentJitter;

        private GameObject nodeView;
        private Vector3 fxOrigin;
        private NodeBehaviour behaviour;
        private SpriteRenderer[] fadeRenderers;
        private GameObject readyRootView;
        private GameObject harvestedRootView;
        private bool swapStateVisuals;
        private float fadeDuration;
        private AnimationCurve fadeCurve;

        [MenuItem("Window/Little Peeps/Harvest Feedback")]
        private static void Open()
        {
            var window = GetWindow<HarvestFeedbackWindow>();
            window.titleContent = new GUIContent("Harvest Feedback");
            window.minSize = new Vector2(340f, 460f);
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
            // Also runs on domain reload, which is what keeps the preview objects from outliving the
            // managed state that knows how to destroy them.
            EditorApplication.update -= Tick;
            SceneView.duringSceneGui -= OnSceneGUI;
            StopPreview();
            SavePrefs();
        }

        // ---------------------------------------------------------------- GUI

        private void OnGUI()
        {
            EditorGUILayout.LabelField("What to preview", EditorStyles.boldLabel);

            resourcePrefab = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Resource Prefab",
                    "A node prefab: BaseWheat, BaseTree, Market, Smithy, Alpaka, Boar, Fox. Its own " +
                    "ResourceSourceDef, Fx Anchor and fade settings are read from it, so the preview " +
                    "leaves from where the game would and disappears the way the game would."),
                resourcePrefab, typeof(GameObject), false);

            var prefabDef = ResolvePrefabDef();
            using (new EditorGUI.DisabledScope(prefabDef != null))
            {
                var shown = prefabDef != null ? prefabDef : source;
                var picked = (ResourceSourceDef)EditorGUILayout.ObjectField(
                    new GUIContent("Source",
                        "Picks the pickup particles. Filled in from the prefab when one is assigned."),
                    shown, typeof(ResourceSourceDef), false);
                if (prefabDef == null) source = picked;
            }

            motion = (HarvestNumberMotionDef)EditorGUILayout.ObjectField(
                new GUIContent("Motion", "The asset the game reads. Tuning it here tunes the game."),
                motion, typeof(HarvestNumberMotionDef), false);

            numberPrefab = (TextMeshPro)EditorGUILayout.ObjectField(
                new GUIContent("Number Prefab", "The world-space TextMeshPro prefab used for the popup."),
                numberPrefab, typeof(TextMeshPro), false);

            using (new EditorGUI.DisabledScope(motion == null))
                if (GUILayout.Button("Select Motion Asset"))
                    Selection.activeObject = motion;

            DrawPrefabNote();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);
            amount = Mathf.Max(0f, EditorGUILayout.FloatField(
                new GUIContent("Amount", "Value written into the floating number."), amount));
            spawnPoint = EditorGUILayout.Vector3Field(
                new GUIContent("Position", "Drag the handle in the Scene View to move this."), spawnPoint);

            EditorGUI.BeginChangeCheck();
            showObject = EditorGUILayout.Toggle(
                new GUIContent("Show Object",
                    "Off hides the node's sprites while keeping it in place, so the number and the " +
                    "particles can be judged on their own. The Fx Anchor still decides where they " +
                    "come from, so nothing moves when you toggle this."),
                showObject);
            if (EditorGUI.EndChangeCheck() && previewing) ApplyShowObject();

            finalHit = EditorGUILayout.Toggle(
                new GUIContent("Final Hit",
                    "On: the hit that uses the node up, so it disappears. Off: an ordinary hit — the " +
                    "node stays put and only the number and particles play. A node takes several hits " +
                    "before it goes, so both are worth looking at."),
                finalHit);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Playback", EditorStyles.boldLabel);
            loop = EditorGUILayout.Toggle(
                new GUIContent("Loop", "Restart the cycle when it ends."), loop);
            leadIn = Mathf.Max(0f, EditorGUILayout.FloatField(
                new GUIContent("Lead In",
                    "Seconds the node is shown intact before the hit lands. Preview pacing only — it " +
                    "has no counterpart in the game, but without it a node that vanishes instantly " +
                    "is never actually seen."),
                leadIn));
            interval = Mathf.Max(0.1f, EditorGUILayout.FloatField(
                new GUIContent("Interval",
                    "Seconds from the hit to the end of the cycle. Keep at least as long as the " +
                    "slowest of the three effects."),
                interval));
            playbackSpeed = EditorGUILayout.Slider(
                new GUIContent("Speed", "Preview-only playback multiplier."), playbackSpeed, 0.1f, 3f);
            includeNumberJitter = EditorGUILayout.Toggle(
                new GUIContent("Number Jitter",
                    "Roll a new point inside Spawn Jitter on every restart. Leave off while tuning " +
                    "motion so every replay starts from the same place."),
                includeNumberJitter);

            EditorGUILayout.Space();

            if (motion == null || numberPrefab == null)
                EditorGUILayout.HelpBox(
                    "Assign a motion asset and a number prefab to preview the floating number.",
                    MessageType.Info);

            var effective = prefabDef != null ? prefabDef : source;
            if (effective == null || effective.pickupFx == null)
                EditorGUILayout.HelpBox(
                    "Assign a prefab or a source with a Pickup Fx to preview the particles.",
                    MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(previewing ? "Restart" : "Play", GUILayout.Height(24f)))
                    StartPreview();

                using (new EditorGUI.DisabledScope(!previewing))
                    if (GUILayout.Button("Stop", GUILayout.Height(24f)))
                        StopPreview();
            }

            if (Application.isPlaying)
                EditorGUILayout.HelpBox("The preview runs in Edit Mode only.", MessageType.Warning);
        }

        // Spells out which of the three disappearance mechanics this prefab has, because the
        // difference between "fades", "stays put" and "gone instantly" is authored in three different
        // places and is the single most confusing thing about harvest feedback.
        private void DrawPrefabNote()
        {
            if (resourcePrefab == null) return;

            var node = FindNode(resourcePrefab, out bool isAnimal);
            if (node == null)
            {
                EditorGUILayout.HelpBox(
                    "No ResourceSource or Animal on this prefab — nothing to read.", MessageType.Warning);
                return;
            }

            var so = new SerializedObject(node);
            var def = Prop(so, "def")?.objectReferenceValue as ResourceSourceDef;
            if (def == null)
            {
                EditorGUILayout.HelpBox(
                    "This prefab has no ResourceSourceDef assigned.", MessageType.Warning);
                return;
            }

            string hits = def.infinite
                ? "never used up"
                : $"{def.hitsBeforeDespawn} hit(s) to use up";

            string ending;
            if (isAnimal)
                ending = def.infinite ? "stays put (infinite)" : "destroyed outright, no fade";
            else if (def.infinite)
                ending = "stays put (infinite) — Market and Smithy never change state";
            else
            {
                float t = Prop(so, "fadeOutTime")?.floatValue ?? 0f;
                ending = t > 0f ? $"ready visual fades out over {t:0.##}s" : "ready visual switches off at once";
            }

            EditorGUILayout.HelpBox($"{def.name}: {hits}; {ending}.", MessageType.None);
        }

        private void OnSceneGUI(SceneView view)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.PositionHandle(spawnPoint, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                spawnPoint = new Vector3(moved.x, moved.y, 0f);
                MovePreview();
                Repaint();
            }

            Handles.color = new Color(1f, 0.75f, 0.15f, 0.9f);
            const float size = 0.12f;
            Handles.DrawLine(spawnPoint - Vector3.right * size, spawnPoint + Vector3.right * size);
            Handles.DrawLine(spawnPoint - Vector3.up * size, spawnPoint + Vector3.up * size);
        }

        // ---------------------------------------------------------------- playback

        private void StartPreview()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("Harvest feedback preview runs in Edit Mode only.");
                return;
            }

            previewing = true;
            lastTime = EditorApplication.timeSinceStartup;
            RestartCycle();
        }

        private void StopPreview()
        {
            previewing = false;
            Cleanup();
            SceneView.RepaintAll();
        }

        private void Tick()
        {
            if (!previewing) return;

            // Entering Play Mode would tear the scene down under the preview objects.
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                StopPreview();
                Repaint();
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Min((float)(now - lastTime), 0.1f) * playbackSpeed;
            lastTime = now;

            elapsed += dt;

            if (!hitFired && elapsed >= leadIn)
            {
                FireHit();
            }
            else if (hitFired)
            {
                float since = elapsed - leadIn;

                if (since >= interval)
                {
                    if (!loop)
                    {
                        StopPreview();
                        Repaint();
                        return;
                    }

                    RestartCycle();
                    SceneView.RepaintAll();
                    return;
                }

                Advance(since, dt);
            }

            SceneView.RepaintAll();
        }

        // Sets the stage: the node is placed and shown intact, nothing has been harvested yet.
        private void RestartCycle()
        {
            Cleanup();
            elapsed = 0f;
            hitFired = false;

            fxOrigin = spawnPoint;
            behaviour = NodeBehaviour.None;

            if (resourcePrefab != null) SpawnNode();

            // With no lead-in there is nothing to wait for, so the hit lands on this very tick.
            if (leadIn <= 0f) FireHit();
        }

        // Instantiates the node prefab and reads everything the preview needs off it. The fields are
        // private [SerializeField] on ResourceSource/Animal and are read through SerializedObject on
        // purpose: a preview is no reason to widen a gameplay class's public surface. The cost is that
        // the field NAMES are the contract, so a rename must fail loudly — see Prop.
        private void SpawnNode()
        {
            nodeView = Instantiate(resourcePrefab);
            nodeView.name = $"{resourcePrefab.name} (Harvest Feedback Preview)";
            nodeView.transform.position = spawnPoint;
            HideRecursively(nodeView);

            // Nothing on the instance may tick or take part in anything. In Edit Mode a MonoBehaviour
            // without [ExecuteAlways] is dormant anyway; this makes it true regardless.
            foreach (var mb in nodeView.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
            foreach (var col in nodeView.GetComponentsInChildren<Collider2D>(true)) col.enabled = false;

            var node = FindNode(nodeView, out bool isAnimal);
            if (node == null)
            {
                Debug.LogWarning(
                    $"HarvestFeedbackWindow: '{resourcePrefab.name}' has no ResourceSource or Animal; " +
                    "showing it as scenery only.", resourcePrefab);
                ApplyShowObject();
                return;
            }

            var so = new SerializedObject(node);
            var def = Prop(so, "def")?.objectReferenceValue as ResourceSourceDef;
            var anchor = Prop(so, "fxAnchor")?.objectReferenceValue as Transform;

            // Mirrors ResourceSource.FxOrigin / Animal's inline equivalent exactly.
            fxOrigin = anchor != null ? anchor.position : nodeView.transform.position;

            if (def == null) { ApplyShowObject(); return; }

            if (def.infinite) behaviour = NodeBehaviour.Infinite;
            else if (isAnimal) behaviour = NodeBehaviour.Despawns;
            else
            {
                readyRootView = Prop(so, "readyRoot")?.objectReferenceValue as GameObject;
                harvestedRootView = Prop(so, "harvestedRoot")?.objectReferenceValue as GameObject;
                swapStateVisuals = Prop(so, "swapStateVisuals")?.boolValue ?? false;
                fadeDuration = Prop(so, "fadeOutTime")?.floatValue ?? 0f;
                fadeCurve = Prop(so, "fadeCurve")?.animationCurveValue;

                if (readyRootView != null)
                    fadeRenderers = readyRootView.GetComponentsInChildren<SpriteRenderer>(true);

                bool canFade = fadeDuration > 0f && fadeRenderers != null && fadeRenderers.Length > 0;
                behaviour = canFade ? NodeBehaviour.Fades : NodeBehaviour.Vanishes;

                ApplyReadyVisual();
            }

            ApplyShowObject();
        }

        // The harvest lands: particles, number and the node's disappearance all begin on this tick,
        // which is the whole reason the three are previewed together.
        private void FireHit()
        {
            hitFired = true;

            var def = ResolvePrefabDef() ?? source;

            if (motion != null && numberPrefab != null)
            {
                numberView = Instantiate(numberPrefab);
                numberView.name = $"{numberPrefab.name} (Harvest Feedback Preview)";
                numberView.gameObject.hideFlags = HideFlags.HideAndDontSave;

                HarvestNumberFormat.Write(numberView, amount);

                currentJitter = includeNumberJitter
                    ? new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f))
                    : Vector2.zero;
                numberOrigin = motion.ResolveOrigin(fxOrigin, currentJitter);
                motion.Apply(numberView, numberOrigin, 0f, numberPrefab.transform.localScale);
            }

            if (def != null && def.pickupFx != null)
            {
                pickupView = Instantiate(def.pickupFx);
                pickupView.name = $"{def.pickupFx.name} (Harvest Feedback Preview)";
                pickupView.gameObject.hideFlags = HideFlags.HideAndDontSave;
                pickupView.transform.position = fxOrigin;

                // Same contract the game uses: the system is fed by hand, never emits on its own.
                pickupView.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                pickupView.Emit(Mathf.Max(1, def.pickupFxCount));
            }

            if (!finalHit) return;

            switch (behaviour)
            {
                case NodeBehaviour.Fades:
                    // Mirrors ResourceSource.Deplete: the harvested sprite is brought up front by hand
                    // so it shows THROUGH the fading one, and the ready root is left on to be animated.
                    if (harvestedRootView != null) harvestedRootView.SetActive(true);
                    HarvestFade.ApplyAlpha(fadeRenderers, 1f);
                    break;

                case NodeBehaviour.Vanishes:
                    ApplyHarvestedVisual();
                    break;

                case NodeBehaviour.Despawns:
                    if (nodeView != null) nodeView.SetActive(false);
                    break;
            }
        }

        private void Advance(float since, float dt)
        {
            if (numberView != null && motion != null && numberPrefab != null)
            {
                float lifetime = Mathf.Max(0.0001f, motion.lifetime);
                float k = since / lifetime;

                // The number is done before the cycle is: it goes away and the rest plays on.
                if (k >= 1f)
                {
                    if (numberView.gameObject.activeSelf) numberView.gameObject.SetActive(false);
                }
                else
                {
                    motion.Apply(numberView, numberOrigin, k, numberPrefab.transform.localScale);
                }
            }

            if (pickupView != null && dt > 0f)
                pickupView.Simulate(dt, true, false, false);

            if (finalHit && behaviour == NodeBehaviour.Fades && fadeCurve != null)
            {
                if (since < fadeDuration)
                {
                    HarvestFade.ApplyAlpha(fadeRenderers, fadeCurve.Evaluate(since / fadeDuration));
                }
                else
                {
                    // Mirrors ResourceSource.EndFade: alpha goes back to 1 before the roots settle, or
                    // the node would come back invisible when it regrows.
                    HarvestFade.ApplyAlpha(fadeRenderers, 1f);
                    ApplyHarvestedVisual();
                }
            }
        }

        // Follows the handle without restarting: particles already in flight simulate in World space,
        // so they stay where they were emitted, exactly as they would in the game.
        private void MovePreview()
        {
            if (!previewing) return;

            Vector3 delta = spawnPoint - (nodeView != null ? nodeView.transform.position : fxOrigin);

            if (nodeView != null)
            {
                nodeView.transform.position = spawnPoint;
                fxOrigin += delta;
            }
            else
            {
                fxOrigin = spawnPoint;
            }

            if (motion != null) numberOrigin = motion.ResolveOrigin(fxOrigin, currentJitter);
            if (pickupView != null) pickupView.transform.position = fxOrigin;
        }

        // ---------------------------------------------------------------- node visuals

        // Mirrors ResourceSource.ApplyStateVisual for the Ready state.
        private void ApplyReadyVisual()
        {
            if (!swapStateVisuals)
            {
                if (harvestedRootView != null) harvestedRootView.SetActive(true);
                if (readyRootView != null) readyRootView.SetActive(true);
            }
            else
            {
                if (readyRootView != null) readyRootView.SetActive(true);
                if (harvestedRootView != null) harvestedRootView.SetActive(false);
            }
        }

        // Mirrors ResourceSource.ApplyStateVisual for the Harvested state.
        private void ApplyHarvestedVisual()
        {
            if (harvestedRootView != null) harvestedRootView.SetActive(true);
            if (readyRootView != null) readyRootView.SetActive(false);
        }

        // Hides the node's sprites without moving anything: the transforms stay, so the Fx Anchor goes
        // on deciding where the feedback comes from and toggling this never shifts the effect.
        private void ApplyShowObject()
        {
            if (nodeView == null) return;

            foreach (var r in nodeView.GetComponentsInChildren<Renderer>(true))
                r.enabled = showObject;
        }

        // ---------------------------------------------------------------- plumbing

        private static Component FindNode(GameObject root, out bool isAnimal)
        {
            isAnimal = false;
            if (root == null) return null;

            var rs = root.GetComponentInChildren<ResourceSource>(true);
            if (rs != null) return rs;

            var animal = root.GetComponentInChildren<Animal>(true);
            if (animal == null) return null;

            isAnimal = true;
            return animal;
        }

        private ResourceSourceDef ResolvePrefabDef()
        {
            var node = FindNode(resourcePrefab, out _);
            if (node == null) return null;

            var so = new SerializedObject(node);
            return Prop(so, "def")?.objectReferenceValue as ResourceSourceDef;
        }

        // The field names are this tool's contract with the gameplay components. Reading them through
        // SerializedObject keeps the gameplay classes free of any API that exists only for tooling, at
        // the price of string keys — so a rename must be impossible to miss.
        private static SerializedProperty Prop(SerializedObject so, string name)
        {
            var p = so.FindProperty(name);
            if (p == null)
                Debug.LogError(
                    $"HarvestFeedbackWindow: '{so.targetObject.GetType().Name}' has no serialized field " +
                    $"'{name}'. It was renamed or removed — update this tool to match.", so.targetObject);
            return p;
        }

        private static void HideRecursively(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.HideAndDontSave;
        }

        private void Cleanup()
        {
            if (numberView != null) DestroyImmediate(numberView.gameObject);
            if (pickupView != null) DestroyImmediate(pickupView.gameObject);
            if (nodeView != null) DestroyImmediate(nodeView);

            numberView = null;
            pickupView = null;
            nodeView = null;

            fadeRenderers = null;
            readyRootView = null;
            harvestedRootView = null;
            fadeCurve = null;
            behaviour = NodeBehaviour.None;
        }

        // ---------------------------------------------------------------- persistence

        private void LoadPrefs()
        {
            motion = LoadAsset<HarvestNumberMotionDef>("motion");
            source = LoadAsset<ResourceSourceDef>("source");
            resourcePrefab = LoadAsset<GameObject>("resourcePrefab");

            var prefabRoot = LoadAsset<GameObject>("numberPrefab");
            numberPrefab = prefabRoot != null ? prefabRoot.GetComponentInChildren<TextMeshPro>(true) : null;

            amount = EditorPrefs.GetFloat(PrefPrefix + "amount", 1f);
            spawnPoint = new Vector3(
                EditorPrefs.GetFloat(PrefPrefix + "posX", 0f),
                EditorPrefs.GetFloat(PrefPrefix + "posY", 0f),
                0f);
            showObject = EditorPrefs.GetBool(PrefPrefix + "showObject", true);
            finalHit = EditorPrefs.GetBool(PrefPrefix + "finalHit", true);
            loop = EditorPrefs.GetBool(PrefPrefix + "loop", true);
            leadIn = EditorPrefs.GetFloat(PrefPrefix + "leadIn", 0.5f);
            interval = EditorPrefs.GetFloat(PrefPrefix + "interval", 1.2f);
            playbackSpeed = EditorPrefs.GetFloat(PrefPrefix + "speed", 1f);
            includeNumberJitter = EditorPrefs.GetBool(PrefPrefix + "jitter", false);
        }

        private void SavePrefs()
        {
            SaveAsset("motion", motion);
            SaveAsset("source", source);
            SaveAsset("resourcePrefab", resourcePrefab);
            SaveAsset("numberPrefab", numberPrefab);

            EditorPrefs.SetFloat(PrefPrefix + "amount", amount);
            EditorPrefs.SetFloat(PrefPrefix + "posX", spawnPoint.x);
            EditorPrefs.SetFloat(PrefPrefix + "posY", spawnPoint.y);
            EditorPrefs.SetBool(PrefPrefix + "showObject", showObject);
            EditorPrefs.SetBool(PrefPrefix + "finalHit", finalHit);
            EditorPrefs.SetBool(PrefPrefix + "loop", loop);
            EditorPrefs.SetFloat(PrefPrefix + "leadIn", leadIn);
            EditorPrefs.SetFloat(PrefPrefix + "interval", interval);
            EditorPrefs.SetFloat(PrefPrefix + "speed", playbackSpeed);
            EditorPrefs.SetBool(PrefPrefix + "jitter", includeNumberJitter);
        }

        // Asset references are remembered by GUID rather than by path, so renaming or moving an asset
        // does not silently empty the window.
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
