using System.Collections.Generic;
using Modern2DWater;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;

namespace Water2D
{

    [InitializeOnLoad]
    [CustomEditor(typeof(ModernWater2D))]
    public class ModernWater2DEditor : Editor
    {

        #region variables


        ModernWater2D water;

        private const int space1W = 20;
        private const int space2W = 14;
        private const int space3W = 8;

        private const int dropDown1W = 14;
        private const int dropDown2W = 12;
        private const int dropDown3W = 10;

        private static readonly Color bannerBackgroundColor = new Color(0.14f, 0.14f, 0.14f);
        private static readonly Color backgroundColor = new Color(0.18f, 0.18f, 0.18f);

        private static readonly Color sector1 = new Color(0.22f, 0.22f, 0.22f);
        private static readonly Color sector2 = new Color(0.18f, 0.25f, 0.18f);
        private static readonly Color sector3 = new Color(0.18f, 0.25f, 0.25f);
        private static readonly Color sector4 = new Color(0.4f, 0.20f, 0.25f);
        private static readonly Color sectorMode = new Color(0.20f, 0.22f, 0.28f);

        private AnimBool[] animBs = new AnimBool[64];
        public AnimBool GetAnimBs(int idx) { if (animBs[idx] == null) animBs[idx] = new AnimBool(); return animBs[idx]; }
        [SerializeField] private bool FancyEditor = false;
        [SerializeField] private TextureUtils.ResolutionEnum resEnum;

        #endregion

        #region utils


        Rect rect;
        Rect vrect;
        private void Banner()
        {
            rect = GUILayoutUtility.GetRect(1, 1);
            vrect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(new Rect(rect.x - 13, rect.y - 1, rect.width + 17, vrect.height + 9), bannerBackgroundColor);

            GUIStyle style = new GUIStyle();
            style.stretchWidth = true;
            style.stretchHeight = true;
            style.alignment = TextAnchor.MiddleCenter;

            GUILayout.Space(10);
            GUILayout.Label(Resources.Load<Texture>("Sprites/editor/banner"), style);
            GUILayout.Space(10);

            EditorGUILayout.EndVertical();
        }

        private void StartVB(Color bg)
        {
            rect = GUILayoutUtility.GetRect(1, 1);
            vrect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(new Rect(rect.x - 13, rect.y - 1, rect.width + 17, vrect.height + 9), bg);
        }

        private void EndVB() { EditorGUILayout.EndVertical(); }

        [SerializeField] List<int> layers = new List<int>();

        #endregion

        #region UndoCallback

        private void OnEnable()
        {
            Undo.undoRedoPerformed += UndoCallback;
        }


        private void OnDisable()
        {
            Undo.undoRedoPerformed -= UndoCallback;
        }

        void UndoCallback()
        {
            water.OnWaterChanged();
        }

        #endregion

        #region OnInspectorGUI


        public override void OnInspectorGUI()
        {

            AssetReviewPrompt.OnInspectorOpened();


            if (AssetReviewPrompt.IsPromptActive())
            {
                DrawReviewPrompt();

            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("Asset requires main camera to have MainCamera tag");

            base.OnInspectorGUI();
            water = (ModernWater2D)(target);
            Undo.RecordObject(water, "Modify Water2D");

            Banner();

            rect = GUILayoutUtility.GetRect(1, 1);
            vrect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(new Rect(rect.x - 13, rect.y - 1, rect.width + 17, vrect.height + 9), backgroundColor);

            using (new WaterLayoutUtils.FoldoutScope(FancyEditor, GetAnimBs(20), out var shouldDrawGlobal, "global settings"))
            {
                if (shouldDrawGlobal) GlobalSettings();
            }

            using (new WaterLayoutUtils.FoldoutScope(FancyEditor, GetAnimBs(0), out var shouldDraw, "looks"))
            {
                if (shouldDraw) LooksSettings();
            }
            if (water.settings._waterSettings.waterType == WaterType.normal && water.IsBlurAvailable)
            {
                using (new WaterLayoutUtils.FoldoutScope(FancyEditor, GetAnimBs(11), out var shouldDraw, "blurs"))
                {
                    if (shouldDraw) blursSettings();
                }
            }
            if (water.settings._waterSettings.waterType == WaterType.normal)
            {
                using (new WaterLayoutUtils.FoldoutScope(FancyEditor, GetAnimBs(14), out var shouldDraw, "wet surface"))
                {
                    if (shouldDraw) WetSurfaceSettings();
                }
            }

            using (new WaterLayoutUtils.FoldoutScope(FancyEditor, GetAnimBs(1), out var shouldDraw2, "reflections"))
            {
                if (shouldDraw2) reflectionsSettings();
            }

            using (new WaterLayoutUtils.FoldoutScope(FancyEditor, GetAnimBs(2), out var shouldDraw3, "obstructions"))
            {
                if (shouldDraw3) obstructionSettings();
            }
            if (water.settings._waterSettings.waterType == WaterType.normal)
            {
                using (new WaterLayoutUtils.FoldoutScope(FancyEditor, GetAnimBs(3), out var shouldDraw, "simulations"))
                {
                    if (shouldDraw) simulationSettings();
                }
            }

            if (water.IsWavesAvailable)
            {
                using (new WaterLayoutUtils.FoldoutScope(FancyEditor, GetAnimBs(15), out var shouldDraw, "surface waves"))
                {
                    if (shouldDraw) surfaceWavesSettings();
                }
            }

            editorSettings();

            using (new WaterLayoutUtils.FoldoutScope(FancyEditor, GetAnimBs(5), out var shouldDraw4, "utils"))
            {
                if (shouldDraw4) utilsSettings();
            }

            EditorGUILayout.EndVertical();

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(water);
            }
        }

        #endregion

        #region Global Settings

        private void GlobalSettings()
        {
            ModeSettings();
            EditorGUILayout.Space(4);
            PerformanceSettings();
        }

        private void ModeSettings()
        {
            StartVB(sectorMode);

            EditorGUILayout.LabelField("Render Mode", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            EditorGUILayout.HelpBox(
                "SpriteRenderer: Standard mode. Uses the SpriteRenderer on this GameObject directly (full backward compatibility).\n\n" +
                "Tilemap: Creates a 'WaterTilemap' child object with Grid + Tilemap + TilemapRenderer. " +
                "The child is counter-scaled to keep Grid cells at world-space 1:1 regardless of this object's scale. " +
                "Some features are not available (blur, perspective reflections, surface waves).",
                MessageType.Info);

            EditorGUILayout.Space(4);

            var previousMode = water.renderMode;
            water.renderMode = (WaterRenderMode)EditorGUILayout.EnumPopup(
                new GUIContent("render mode", "SpriteRenderer stays on this object (backward compatible). Tilemap creates a counter-scaled child with Grid + Tilemap + TilemapRenderer."),
                water.renderMode);

            if (previousMode != water.renderMode)
            {
                Undo.RecordObject(water, "Change Water Render Mode");
                water.ApplyRenderMode();
                water.OnWaterChanged();
                EditorUtility.SetDirty(water);
            }

            if (water.IsTilemapMode)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "Tilemap mode is active. A child object 'WaterTilemap' holds the Grid, Tilemap and TilemapRenderer.\n" +
                    "Paint water tiles on that child's Tilemap component. The child is counter-scaled so Grid cells stay at world-space 1:1 regardless of this object's scale.\n" +
                    "The water shader is applied automatically to the TilemapRenderer.",
                    MessageType.None);

                EditorGUILayout.Space(2);
                EditorGUILayout.HelpBox(
                    "Tilemap mode feature compatibility:\n\n" +
                    "  WORKS: coloring, foam, strips, distortion, obstructions, simulation, reflections (top-down, platformer, raymarched), wet surface, URP lighting, surface texture\n\n" +
                    "  NOT AVAILABLE: blur post-processing, perspective reflections, surface waves, surface sprite (world-mapped SpriteRenderer overlay)",
                    MessageType.Info);

                if (!water.IsBlurAvailable && water.settings._blurSettings.useBlur.value)
                    EditorGUILayout.HelpBox("Blur has been auto-disabled (not available in Tilemap mode).", MessageType.Warning);

                if (!water.IsWavesAvailable && water.enableWavesSimulation.value)
                    EditorGUILayout.HelpBox("Surface waves have been auto-disabled (not available in Tilemap mode).", MessageType.Warning);
            }

            EndVB();
        }

        private void PerformanceSettings()
        {
            StartVB(sector1);

            EditorGUILayout.LabelField("Performance", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            EditorGUILayout.LabelField("Main surface rendering shader branches");
            EditorGUILayout.LabelField("They limit expensive surface foam effect");
            EditorGUILayout.LabelField("normal - 3 layer gradient foam");
            EditorGUILayout.LabelField("cheap - 1 layer gradient foam");
            EditorGUILayout.LabelField("mobile - no gradient foam");
            var oldS = water.settings._waterSettings.waterType;
            water.settings._waterSettings.waterType = (WaterType)EditorGUILayout.EnumPopup(
                new GUIContent("water type", "Controls shader complexity. Normal has full foam layers, cheap reduces to 1, mobile removes gradient foam entirely."),
                water.settings._waterSettings.waterType);
            if (oldS != water.settings._waterSettings.waterType)
            {
                Undo.RecordObject(water.ActiveSharedMaterial, "Change Water Type");
                bool mobEnable = water.settings._waterSettings.waterType == WaterType.mobile;
                bool cheapEnable = water.settings._waterSettings.waterType == WaterType.cheap;
                water.ActiveSharedMaterial.SetInt("_cheapWater", cheapEnable ? 1 : 0);
                water.ActiveSharedMaterial.SetInt("_mobileWater", mobEnable ? 1 : 0);
            }

            EndVB();
        }

        #endregion

        #region Looks

        private void LooksSettings()
        {
            using (new WaterLayoutUtils.FoldoutScope(FancyEditor, GetAnimBs(6), out var shouldDraw, "alpha and tiling"))
            {
                if (shouldDraw)
                {
                    StartVB(sector1);
                    water.settings._waterSettings.baseAlpha.value = EditorGUILayout.Slider(
                        new GUIContent("base alpha", "Overall opacity of the water surface. 0 = fully transparent, 1 = fully opaque. Useful for layering water over backgrounds."),
                        water.settings._waterSettings.baseAlpha.value, 0f, 1f, GUILayout.ExpandWidth(true));
                    water.settings._waterSettings.alphaTexture = (Texture2D)EditorGUILayout.ObjectField(
                        new GUIContent("alpha texture", "Optional grayscale texture to control per-pixel transparency. White = opaque, black = transparent."),
                        water.settings._waterSettings.alphaTexture, typeof(Texture2D), true, GUILayout.ExpandWidth(true));

                    EditorGUILayout.Space(5);

                    if (!water.settings._blurSettings.useBlur.value)
                    {
                        water.lightingWhenBlur = water.settings._waterSettings._useLighting.value = EditorGUILayout.Toggle(
                            new GUIContent("receive URP Lighting", "When enabled, the water receives 2D lights from URP's lighting system. Disable for unlit water."),
                            water.settings._waterSettings._useLighting.value, GUILayout.ExpandWidth(true));
                    }
                    else
                    {
                        water.settings._waterSettings._useLighting.value = EditorGUILayout.Toggle(
                            new GUIContent("receive URP Lighting", "When enabled, the water receives 2D lights from URP's lighting system."),
                            water.settings._waterSettings._useLighting.value, GUILayout.ExpandWidth(true));
                        water.lightingWhenBlur = water.settings._waterSettings._useLighting.value;
                    }

                    EditorGUILayout.Space(5);

                    water.settings._waterSettings.tiling.value = EditorGUILayout.Vector2Field(
                        new GUIContent("tiling", "UV tiling of the water pattern. Higher values repeat the pattern more times across the surface."),
                        water.settings._waterSettings.tiling.value, GUILayout.ExpandWidth(true));
                    water.settings._waterSettings.numOfPixels.value = EditorGUILayout.IntField(
                        new GUIContent("number of pixels", "Pixel resolution for the pixelation effect. Higher = more pixels = finer detail."),
                        water.settings._waterSettings.numOfPixels.value, GUILayout.ExpandWidth(true));
                    water.settings._waterSettings.pixelPerfect.value = EditorGUILayout.Toggle(
                        new GUIContent("pixel perfect", "Snaps water pixels to screen pixels for crisp retro-style rendering."),
                        water.settings._waterSettings.pixelPerfect.value, GUILayout.ExpandWidth(true));

                    EndVB();
                }
            }


            using (new WaterLayoutUtils.FoldoutScope(FancyEditor, GetAnimBs(26), out var shouldDraw, "coloring"))
            {
                if (shouldDraw)
                {
                    StartVB(sector1);
                    water.settings._waterSettings.coloringType = (ColoringType)EditorGUILayout.EnumPopup(
                        new GUIContent("coloring type", "How water color is determined: single flat color, two-color gradient by depth, Y-depth gradient, or distance from obstructors."),
                        water.settings._waterSettings.coloringType);
                    if (water.settings._waterSettings.coloringType == ColoringType.distance_from_obstructors)
                    {
                        water.settings._waterSettings.depthMlp.value = EditorGUILayout.Slider(
                            new GUIContent("depth multiplier", "Scales the distance-from-obstructor calculation. Higher values push the gradient further into the water."),
                            water.settings._waterSettings.depthMlp.value, 0f, 32f);
                        water.settings._waterSettings.colorGradient.value = EditorGUILayout.GradientField(
                            new GUIContent("depth color gradient", "Gradient sampled based on distance from obstructors. Left = near edge, right = deep water."),
                            water.settings._waterSettings.colorGradient.value);
                    }
                    else if (water.settings._waterSettings.coloringType == ColoringType.single_color)
                    {
                        water.settings._waterSettings.color.value = EditorGUILayout.ColorField(
                            new GUIContent("water color", "Flat color applied to the entire water surface."),
                            water.settings._waterSettings.color.value, GUILayout.ExpandWidth(true));
                        water.settings._waterSettings.depthColor.value = water.settings._waterSettings.color.value;
                    }
                    else if (water.settings._waterSettings.coloringType == ColoringType.two_colors)
                    {
                        water.settings._waterSettings.color.value = EditorGUILayout.ColorField(
                            new GUIContent("edges color", "Color at the water edges (shallow areas)."),
                            water.settings._waterSettings.color.value, GUILayout.ExpandWidth(true));
                        water.settings._waterSettings.depthColor.value = EditorGUILayout.ColorField(
                            new GUIContent("depth color", "Color at the water center (deep areas)."),
                            water.settings._waterSettings.depthColor.value, GUILayout.ExpandWidth(true));
                    }
                    else if (water.settings._waterSettings.coloringType == ColoringType.depthY)
                    {
                        water.settings._waterSettings.color.value = EditorGUILayout.ColorField(
                            new GUIContent("edges color", "Color at the top of the water (Y-based)."),
                            water.settings._waterSettings.color.value, GUILayout.ExpandWidth(true));
                        water.settings._waterSettings.depthColor.value = EditorGUILayout.ColorField(
                            new GUIContent("depth color", "Color at the bottom of the water (Y-based)."),
                            water.settings._waterSettings.depthColor.value, GUILayout.ExpandWidth(true));
                    }
                    EndVB();
                }
            }


            using (new WaterLayoutUtils.FoldoutScope(FancyEditor, GetAnimBs(8), out var shouldDraw, "strips and foam"))
            {
                if (shouldDraw)
                {

                    StartVB(sector1);
                    water.settings._waterSettings.foamSpeed.value = EditorGUILayout.Vector2Field(
                        new GUIContent("speed", "Scrolling speed of the foam pattern in X and Y. Controls the flow direction and velocity."),
                        water.settings._waterSettings.foamSpeed.value, GUILayout.ExpandWidth(true));

                    water.settings._waterSettings.foamColor.value = EditorGUILayout.ColorField(
                        new GUIContent("foam color", "Tint color of the foam overlay."),
                        water.settings._waterSettings.foamColor.value, GUILayout.ExpandWidth(true));
                    water.settings._waterSettings.foamSize.value = EditorGUILayout.FloatField(
                        new GUIContent("foam size", "Scale of the foam noise pattern. Larger values produce bigger foam blobs."),
                        water.settings._waterSettings.foamSize.value, GUILayout.ExpandWidth(true));
                    water.settings._waterSettings.foamAlpha.value = EditorGUILayout.Slider(
                        new GUIContent("foam alpha", "Opacity of the foam layer. 0 = invisible, 1 = fully visible."),
                        water.settings._waterSettings.foamAlpha.value, 0f, 1f, GUILayout.ExpandWidth(true));
                    water.settings._waterSettings.foamDensity.value = EditorGUILayout.Slider(
                        new GUIContent("foam density", "Threshold for foam visibility. Lower values show more foam, higher values show less."),
                        water.settings._waterSettings.foamDensity.value, 0f, 1f, GUILayout.ExpandWidth(true));

                    EditorGUILayout.Space(10);

                    water.settings._waterSettings.sunStripsTexture = (Texture2D)EditorGUILayout.ObjectField(
                        new GUIContent("strips texture", "Texture used for sun/light strip caustic effects on the water surface."),
                        water.settings._waterSettings.sunStripsTexture, typeof(Texture2D), true, GUILayout.ExpandWidth(true));
                    EditorGUILayout.Space(5);
                    water.settings._waterSettings.stripsAlpha.value = EditorGUILayout.Slider(
                        new GUIContent("strips alpha", "Opacity of the light strips effect."),
                        water.settings._waterSettings.stripsAlpha.value, 0f, 1f, GUILayout.ExpandWidth(true));
                    water.settings._waterSettings.stripsSize.value = EditorGUILayout.FloatField(
                        new GUIContent("strips size", "Scale of the light strips pattern."),
                        water.settings._waterSettings.stripsSize.value, GUILayout.ExpandWidth(true));
                    water.settings._waterSettings.stripsSpeed.value = EditorGUILayout.FloatField(
                        new GUIContent("strips speed", "Animation speed of the light strips distortion."),
                        water.settings._waterSettings.stripsSpeed.value, GUILayout.ExpandWidth(true));
                    water.settings._waterSettings.stripsDensity.value = EditorGUILayout.FloatField(
                        new GUIContent("strips density", "How tightly packed the light strips pattern is."),
                        water.settings._waterSettings.stripsDensity.value, GUILayout.ExpandWidth(true));
                    EndVB();
                }
            }




            using (new WaterLayoutUtils.FoldoutScope(FancyEditor, GetAnimBs(9), out var shouldDraw, "distortion"))
            {
                if (shouldDraw)
                {
                    StartVB(sector1);

                    var c = water.settings._waterSettings.distortionTexture;
                    water.settings._waterSettings.distortionTexture = (Texture2D)EditorGUILayout.ObjectField(
                        new GUIContent("distortion texture", "Normal map or noise texture that drives the water distortion effect."),
                        water.settings._waterSettings.distortionTexture, typeof(Texture2D), true, GUILayout.ExpandWidth(true));
                    if (water.settings._waterSettings.surfaceTexture != null && c != water.settings._waterSettings.surfaceTexture) water.settings._waterSettings.surfaceSpeed.onValueChanged.Invoke();

                    EditorGUILayout.Space(5);
                    water.settings._waterSettings.distortionSpeed.value = EditorGUILayout.Vector2Field(
                        new GUIContent("speed", "Scrolling speed of the distortion texture in X and Y."),
                        water.settings._waterSettings.distortionSpeed.value, GUILayout.ExpandWidth(true));
                    water.settings._waterSettings.distortionStrength.value = EditorGUILayout.Vector2Field(
                        new GUIContent("strength", "Intensity of distortion in X and Y. Higher values produce more warping."),
                        water.settings._waterSettings.distortionStrength.value, GUILayout.ExpandWidth(true));
                    water.settings._waterSettings.distortionTiling.value = EditorGUILayout.Vector2Field(
                        new GUIContent("tiling", "UV tiling of the distortion texture."),
                        water.settings._waterSettings.distortionTiling.value, GUILayout.ExpandWidth(true));
                    water.settings._waterSettings.distortionMinMax.value = EditorGUILayout.Vector2Field(
                        new GUIContent("min max color strength", "Remaps distortion wave color contribution. X = min threshold, Y = max threshold."),
                        water.settings._waterSettings.distortionMinMax.value, GUILayout.ExpandWidth(true));
                    water.settings._waterSettings.distortionColor.value = EditorGUILayout.ColorField(
                        new GUIContent("color of distortion waves", "Tint applied to visible distortion waves on the surface."),
                        water.settings._waterSettings.distortionColor.value, GUILayout.ExpandWidth(true));
                    EndVB();
                }
            }
            if (water.settings._waterSettings.waterType == WaterType.normal)
            {

                using (new WaterLayoutUtils.FoldoutScope(FancyEditor, GetAnimBs(10), out var shouldDraw, "surface texture"))
                {
                    if (shouldDraw)
                    {
                        StartVB(sector1);
                        var c = water.settings._waterSettings.surfaceTexture;

                        if (water.IsSpriteMode)
                        {
                            water.settings._waterSettings.surfaceSprite = (SpriteRenderer)EditorGUILayout.ObjectField(
                                new GUIContent("surface sprite", "A SpriteRenderer in the scene whose texture is projected onto the water surface based on world positions."),
                                water.settings._waterSettings.surfaceSprite, typeof(SpriteRenderer), true, GUILayout.ExpandWidth(true));
                        }

                        water.settings._waterSettings.surfaceTexture = (Texture2D)EditorGUILayout.ObjectField(
                            new GUIContent("surface texture", "Static texture overlaid on the water surface. Scrolls with surface speed settings."),
                            water.settings._waterSettings.surfaceTexture, typeof(Texture2D), true, GUILayout.ExpandWidth(true));
                        if (c != water.settings._waterSettings.surfaceTexture) water.settings._waterSettings.surfaceSpeed.onValueChanged.Invoke();

                        EditorGUILayout.Space(5);
                        water.settings._waterSettings.surfaceTiling.value = EditorGUILayout.Vector2Field(
                            new GUIContent("tiling", "UV tiling of the surface texture."),
                            water.settings._waterSettings.surfaceTiling.value, GUILayout.ExpandWidth(true));
                        water.settings._waterSettings.surfaceSpeed.value = EditorGUILayout.Vector2Field(
                            new GUIContent("speed", "Scrolling speed of the surface texture in X and Y."),
                            water.settings._waterSettings.surfaceSpeed.value, GUILayout.ExpandWidth(true));
                        water.settings._waterSettings.surfaceAlpha.value = EditorGUILayout.Slider(
                            new GUIContent("alpha", "Opacity of the surface texture overlay."),
                            water.settings._waterSettings.surfaceAlpha.value, 0f, 1f, GUILayout.ExpandWidth(true));
                        water.settings._waterSettings.useFoamSpeed.value = EditorGUILayout.Toggle(
                            new GUIContent("use foam speed as surface speed", "When enabled, the surface texture scrolls at the same speed as foam instead of its own speed setting."),
                            water.settings._waterSettings.useFoamSpeed.value, GUILayout.ExpandWidth(true));

                        EndVB();
                    }
                }
            }

        }

        #endregion

        #region Reflections

        private void reflectionsSettings()
        {

            StartVB(sector1); water.enableReflections.value = GUILayout.Toggle(water.enableReflections.value, new GUIContent("enable", "Master toggle for all reflection systems.")); EndVB();
            StartVB(sector2); water.settings._reflectionsSettings.enableTopDownReflections.value = GUILayout.Toggle(water.settings._reflectionsSettings.enableTopDownReflections.value, new GUIContent("enable top down reflections", "Renders reflections from a top-down camera angle. Good for overhead/RPG views.")); EndVB();
            StartVB(sector3); water.settings._reflectionsSettings.enablePlatformerReflections.value = GUILayout.Toggle(water.settings._reflectionsSettings.enablePlatformerReflections.value, new GUIContent("enable platformer reflections", "Renders mirrored reflections below a horizontal line. Standard for side-view games.")); EndVB();
            if (water.settings._waterSettings.waterType == WaterType.normal)
            {
                StartVB(sector4); water.settings._reflectionsSettings.enableRaymarchedReflections.value = GUILayout.Toggle(water.settings._reflectionsSettings.enableRaymarchedReflections.value, new GUIContent("enable raymarched reflections", "High quality screen-space reflections using raymarching. Most expensive reflection type.")); EndVB();
            }
            StartVB(sector1);
            water.settings._reflectionsSettings.textureResolution.value = EditorGUILayout.Slider(
                new GUIContent("resolution", "Resolution multiplier for reflection render textures. Lower = faster but blurrier. 1 = full screen resolution."),
                water.settings._reflectionsSettings.textureResolution.value, 0f, 1f, GUILayout.ExpandWidth(true));

            EditorGUILayout.Space(5);
            water.settings._reflectionsSettings.originalColor.value = EditorGUILayout.Slider(
                new GUIContent("original color", "How much of the reflected object's original color is preserved. 0 = fully tinted, 1 = original colors."),
                water.settings._reflectionsSettings.originalColor.value, 0f, 1, GUILayout.ExpandWidth(true));
            water.settings._reflectionsSettings.alpha.value = EditorGUILayout.Slider(
                new GUIContent("alpha (meshes supported)", "Opacity of the reflection layer. Works with both sprite and mesh reflections."),
                water.settings._reflectionsSettings.alpha.value, 0f, 1f, GUILayout.ExpandWidth(true));
            water.settings._reflectionsSettings.color.value = EditorGUILayout.ColorField(
                new GUIContent("color (meshes supported)", "Tint color applied to reflections. Works with both sprite and mesh reflections."),
                water.settings._reflectionsSettings.color.value, GUILayout.ExpandWidth(true));
            EditorGUILayout.Space(5);

            EndVB();

            if (water.settings._reflectionsSettings.enableTopDownReflections.value)
            {
                StartVB(sector2);
                water.settings._reflectionsSettings.defaultReflectionSprflipx.value = EditorGUILayout.Toggle(
                    new GUIContent("default reflection x-orientation", "Flip the reflection horizontally. Useful if reflections appear mirrored incorrectly."),
                    water.settings._reflectionsSettings.defaultReflectionSprflipx.value, GUILayout.ExpandWidth(true));
                water.settings._reflectionsSettings.angle.value = EditorGUILayout.Slider(
                    new GUIContent("angle", "Rotation angle of top-down reflections in degrees."),
                    water.settings._reflectionsSettings.angle.value, -90, 90, GUILayout.ExpandWidth(true));
                water.settings._reflectionsSettings.tilt.value = EditorGUILayout.Slider(
                    new GUIContent("tilt", "Tilt angle for top-down reflections. Creates a perspective-like effect."),
                    water.settings._reflectionsSettings.tilt.value, 0, 90, GUILayout.ExpandWidth(true));
                water.settings._reflectionsSettings.topdownReflections_FalloffStart.value = EditorGUILayout.Slider(
                    new GUIContent("falloff start (meshes supported)", "Distance from water edge where reflection begins to fade. 0 = no offset."),
                    water.settings._reflectionsSettings.topdownReflections_FalloffStart.value, 0, 1, GUILayout.ExpandWidth(true));
                water.settings._reflectionsSettings.topdownReflections_FalloffStrength.value = EditorGUILayout.Slider(
                    new GUIContent("falloff strength (meshes supported)", "How aggressively reflections fade with distance. Higher = faster fade."),
                    water.settings._reflectionsSettings.topdownReflections_FalloffStrength.value, 0, 5, GUILayout.ExpandWidth(true));
                water.settings._reflectionsSettings.topdownReflections3D_FalloffColor.value = EditorGUILayout.ColorField(
                    new GUIContent("falloff color (meshes supported)", "Color that reflections fade into at maximum falloff distance."),
                    water.settings._reflectionsSettings.topdownReflections3D_FalloffColor.value, GUILayout.ExpandWidth(true));
                EditorGUILayout.Space(10);
                EndVB();
            }


            if (water.settings._reflectionsSettings.enablePlatformerReflections.value)
            {
                StartVB(sector3);

                if (water.IsTilemapMode)
                {
                    EditorGUILayout.HelpBox(
                        "In Tilemap mode, the water shader samples using object UVs that scale with the tilemap bounds. " +
                        "This can cause different tiling and visual appearance compared to SpriteRenderer mode. " +
                        "Adjust the tiling and distortion settings to compensate if needed.",
                        MessageType.Info);
                    EditorGUILayout.Space(4);
                }

                water.settings._reflectionsSettings.customReflectionStart.value = EditorGUILayout.Toggle(
                    new GUIContent("custom reflections starting point", "Override the automatic reflection mirror line position."),
                    water.settings._reflectionsSettings.customReflectionStart.value, GUILayout.ExpandWidth(true));
                if (water.settings._reflectionsSettings.customReflectionStart.value) water.settings._reflectionsSettings.mirrorY.value = EditorGUILayout.Slider(
                    new GUIContent("reflections starting point", "Vertical UV position where reflections begin. 1 = top of water sprite."),
                    water.settings._reflectionsSettings.mirrorY.value, 0f, 5f, GUILayout.ExpandWidth(true));


                EditorGUILayout.Space(5);

                water.settings._reflectionsSettings.enableScrolling.value = EditorGUILayout.Toggle(
                    new GUIContent("enable infinite scrolling", "Scrolls reflections based on player position. Essential for infinite/parallax scrolling games."),
                    water.settings._reflectionsSettings.enableScrolling.value, GUILayout.ExpandWidth(true));
                if (water.settings._reflectionsSettings.enableScrolling.value)
                {
                    water.settings._reflectionsSettings.playerPosition = (Transform)EditorGUILayout.ObjectField(
                        new GUIContent("player transform", "The transform used to calculate scrolling offset. Usually the player character."),
                        water.settings._reflectionsSettings.playerPosition, typeof(Transform), true);
                    water.settings._reflectionsSettings.scrollingStrength.value = EditorGUILayout.Slider(
                        new GUIContent("scrolling strength", "Multiplier for the parallax scrolling effect. Higher = reflections move more relative to player."),
                        water.settings._reflectionsSettings.scrollingStrength.value, 0f, 10f, GUILayout.ExpandWidth(true));
                    EditorGUILayout.Space(5);
                }

                var list = water.settings._reflectionsSettings.layers;

                EditorGUILayout.HelpBox(
                    "Add the Unity layer indices of objects you want reflected. " +
                    "Find a layer's index in Edit → Project Settings → Tags and Layers (e.g. Default = 0, Water = 4). " +
                    "Only objects on these layers will appear in reflections.",
                    MessageType.None);

                int newCount = Mathf.Max(0, EditorGUILayout.IntField(
                    new GUIContent("reflected layers count", "How many layers should be captured by this reflection camera."),
                    list.Count));
                while (newCount < list.Count)
                    list.RemoveAt(list.Count - 1);
                while (newCount > list.Count)
                    list.Add(0);

                for (int i = 0; i < list.Count; i++)
                {
                    list[i] = EditorGUILayout.IntField("layer " + i + " (index) : ", list[i]);
                }

                water.settings._reflectionsSettings.layers = list;

                if (water.IsTilemapMode)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.HelpBox(
                        "Perspective reflections are not available in Tilemap mode. " +
                        "The tilemap's non-uniform bounds make perspective projection unreliable. " +
                        "Switch to SpriteRenderer mode to use this feature.",
                        MessageType.Warning);
                    GUI.enabled = false;
                    EditorGUILayout.Toggle(
                        new GUIContent("use perspective", "NOT AVAILABLE in Tilemap mode."),
                        false, GUILayout.ExpandWidth(true));
                    GUI.enabled = true;
                }
                else
                {
                    water.settings._reflectionsSettings.usePerspective.value = EditorGUILayout.Toggle(
                        new GUIContent("use perspective", "Applies a perspective distortion to reflections, simulating depth. Useful for pseudo-3D water looks."),
                        water.settings._reflectionsSettings.usePerspective.value, GUILayout.ExpandWidth(true));
                    if (water.settings._reflectionsSettings.usePerspective.value)
                    {
                        water.settings._reflectionsSettings.waterPerspective.value = EditorGUILayout.Vector2Field(
                            new GUIContent("water perspective", "Perspective scaling applied to the water surface. X and Y control horizontal and vertical squash."),
                            water.settings._reflectionsSettings.waterPerspective.value, GUILayout.ExpandWidth(true));
                        water.settings._reflectionsSettings.reflectionsPerspective.value = EditorGUILayout.Vector2Field(
                            new GUIContent("reflections perspective", "Perspective scaling applied to the reflection image independently."),
                            water.settings._reflectionsSettings.reflectionsPerspective.value, GUILayout.ExpandWidth(true));
                    }
                }

                EditorGUILayout.Space(5);
                water.settings._reflectionsSettings.enableFalloff.value = EditorGUILayout.Toggle(
                    new GUIContent("enable falloff", "Fades reflections based on distance from the reflection start line."),
                    water.settings._reflectionsSettings.enableFalloff.value, GUILayout.ExpandWidth(true));
                if (water.settings._reflectionsSettings.enableFalloff.value)
                {
                    water.settings._reflectionsSettings.falloffStart.value = EditorGUILayout.Slider(
                        new GUIContent("falloff start", "UV position where reflection falloff begins. 0 = starts immediately."),
                        water.settings._reflectionsSettings.falloffStart.value, 0f, 1f, GUILayout.ExpandWidth(true));
                    water.settings._reflectionsSettings.falloffStrength.value = EditorGUILayout.Slider(
                        new GUIContent("falloff strength", "How aggressively reflections fade. Higher = more aggressive falloff."),
                        water.settings._reflectionsSettings.falloffStrength.value, 0f, 10f, GUILayout.ExpandWidth(true));
                }
                EditorGUILayout.Space(10);
                EndVB();
            }



            if (water.settings._reflectionsSettings.enableRaymarchedReflections.value)
            {
                StartVB(sector4);
                water.raymarchUnits = EditorGUILayout.FloatField(
                    new GUIContent("raymarch units", "World-space height in units that the raymarch covers. Determines how far above water reflections capture."),
                    water.raymarchUnits);
                Camera refCam = water.cameraOverride ? water.cameraOverride : Camera.main;
                int pixels = water.GetRaymarchPixelSteps(refCam);

                water.settings._reflectionsSettings.raymarchSteps.value = pixels;
                water.settings._reflectionsSettings.raymarchFalloffStart.value = EditorGUILayout.Slider(
                    new GUIContent("falloff start", "UV distance where raymarched reflection begins to fade."),
                    water.settings._reflectionsSettings.raymarchFalloffStart.value, 0f, 1f);
                water.settings._reflectionsSettings.raymarchFalloffEnd.value = EditorGUILayout.Slider(
                    new GUIContent("falloff end", "UV distance where raymarched reflection is fully faded."),
                    water.settings._reflectionsSettings.raymarchFalloffEnd.value, water.settings._reflectionsSettings.raymarchFalloffStart.value, 1f);

                var list = water.settings._reflectionsSettings.raymarchlayers;

                EditorGUILayout.HelpBox(
                    "Add the Unity layer indices of objects you want in raymarched reflections. " +
                    "Find a layer's index in Edit → Project Settings → Tags and Layers (e.g. Default = 0).",
                    MessageType.None);

                int newCount = Mathf.Max(0, EditorGUILayout.IntField(
                    new GUIContent("reflected layers count", "How many layers should be captured by the raymarch reflection camera."),
                    list.Count));
                while (newCount < list.Count)
                    list.RemoveAt(list.Count - 1);
                while (newCount > list.Count)
                    list.Add(0);

                for (int i = 0; i < list.Count; i++)
                {
                    list[i] = EditorGUILayout.IntField("layer " + i + " (index) : ", list[i]);
                }
                water.settings._reflectionsSettings.raymarchlayers = list;

                water.settings._reflectionsSettings.type2.value = EditorGUILayout.Toggle(
                    new GUIContent("type2 raymarching", "Alternative raymarching algorithm. Try this if default raymarching produces artifacts."),
                    water.settings._reflectionsSettings.type2.value);
                EndVB();
            }
        }

        #endregion

        #region Blur, Obstruction, Simulation, Waves

        private void blursSettings()
        {
            if (!water.IsBlurAvailable)
            {
                StartVB(sector1);
                EditorGUILayout.HelpBox("Blur is only available in SpriteRenderer mode.", MessageType.Info);
                EndVB();
                return;
            }

            StartVB(sector1);

            water.settings._blurSettings.useBlur.onValueChanged = water.OnBlurMaterialChanged;

            water.settings._blurSettings.useBlur.value = EditorGUILayout.Toggle(
                new GUIContent("enable", "Enables post-processing blur on the water. Creates a secondary camera for the blur pass."),
                water.settings._blurSettings.useBlur.value);

            if (water.settings._blurSettings.useFalloff.value = EditorGUILayout.Toggle(
                new GUIContent("enable falloff", "Applies a vertical gradient to the blur intensity."),
                water.settings._blurSettings.useFalloff.value))
            {
                water.settings._blurSettings.falloffStart.value = EditorGUILayout.Slider(
                    new GUIContent("blur falloff start", "UV position where blur begins fading in."),
                    water.settings._blurSettings.falloffStart.value, 0f, 1f);
                water.settings._blurSettings.falloffEnd.value = EditorGUILayout.Slider(
                    new GUIContent("blur falloff end", "UV position where blur reaches full intensity."),
                    water.settings._blurSettings.falloffEnd.value, Mathf.Min(0f, water.settings._blurSettings.falloffStart.value), 1f);
                water.settings._blurSettings.falloffStrength.value = EditorGUILayout.Slider(
                    new GUIContent("blur falloff strength", "Curve power for the falloff gradient. Higher = sharper transition."),
                    water.settings._blurSettings.falloffStrength.value, 0f, 3f);
            }

            BlurSettings.BlurType old = water.settings._blurSettings.blurType;
            water.settings._blurSettings.blurType = (BlurSettings.BlurType)EditorGUILayout.EnumPopup(
                new GUIContent("type of blur", "Box = fast and uniform. Gaussian = smooth bell curve. Bokeh = simulates lens bokeh."),
                water.settings._blurSettings.blurType);
            if (old != water.settings._blurSettings.blurType) water.OnBlurMaterialChanged();

            switch (water.settings._blurSettings.blurType)
            {
                case BlurSettings.BlurType.box:
                    water.settings._blurSettings.boxSamplingRange.value = EditorGUILayout.IntSlider(
                        new GUIContent("sampling area", "Pixel radius of the box blur kernel. Higher = blurrier but more expensive."),
                        water.settings._blurSettings.boxSamplingRange.value, 1, 32);
                    water.settings._blurSettings.boxStrength.value = EditorGUILayout.Slider(
                        new GUIContent("strength", "Blend factor of the blur effect."),
                        water.settings._blurSettings.boxStrength.value, 0f, 1f);

                    break;
                case BlurSettings.BlurType.gaussian:
                    water.settings._blurSettings.gaussianSamplingRange.value = EditorGUILayout.IntSlider(
                        new GUIContent("sampling area", "Pixel radius of the gaussian kernel."),
                        water.settings._blurSettings.gaussianSamplingRange.value, 1, 32);
                    water.settings._blurSettings.gaussianStrengthX.value = EditorGUILayout.FloatField(
                        new GUIContent("strength", "Sigma value of the gaussian distribution. Higher = wider blur."),
                        water.settings._blurSettings.gaussianStrengthX.value);

                    break;
                case BlurSettings.BlurType.bokeh:
                    water.settings._blurSettings.bokehArea.value = EditorGUILayout.Slider(
                        new GUIContent("sampling area", "Size of the bokeh disc in UV space."),
                        water.settings._blurSettings.bokehArea.value, 0f, 0.01f);
                    water.settings._blurSettings.bokehQuality.value = EditorGUILayout.IntSlider(
                        new GUIContent("sampling quality", "Number of samples per ring. Higher = smoother bokeh but more expensive."),
                        water.settings._blurSettings.bokehQuality.value, 1, 32);

                    water.settings._blurSettings.bokehGamma.value = EditorGUILayout.Slider(
                        new GUIContent("gamma", "Controls brightness response of bokeh highlights. Higher = brighter highlights pop more."),
                        water.settings._blurSettings.bokehGamma.value, 1f, 32f);
                    water.settings._blurSettings.bokehHardness.value = EditorGUILayout.Slider(
                        new GUIContent("hardness", "Edge hardness of the bokeh disc. 0 = soft, 1 = hard edged circles."),
                        water.settings._blurSettings.bokehHardness.value, 0f, 1f);
                    water.settings._blurSettings.bokehRatio.value = EditorGUILayout.FloatField(
                        new GUIContent("x-y ratio", "Aspect ratio of the bokeh shape. 1 = circular, other values create ellipses."),
                        water.settings._blurSettings.bokehRatio.value);

                    break;
            }
            EndVB();
        }

        private void obstructionSettings()
        {
            StartVB(sector1);
            water.enableObstruction.value = GUILayout.Toggle(water.enableObstruction.value,
                new GUIContent("enable", "Enables the obstruction system. Objects with Obstructor component will create depth/masking data."));
            water.settings._obstructorSettings.textureResolution.value = EditorGUILayout.Slider(
                new GUIContent("resolution", "Resolution multiplier for the obstruction render texture. Lower = faster, higher = more detail."),
                water.settings._obstructorSettings.textureResolution.value, 0f, 1f, GUILayout.ExpandWidth(true));

            water.settings._waterSettings.obstructionAlpha.value = EditorGUILayout.Slider(
                new GUIContent("alpha", "Opacity of the obstruction edge coloring effect."),
                water.settings._waterSettings.obstructionAlpha.value, 0f, 1f, GUILayout.ExpandWidth(true));
            water.settings._waterSettings.obstructionColor.value = EditorGUILayout.ColorField(
                new GUIContent("color", "Color of the edge glow around obstructing objects."),
                water.settings._waterSettings.obstructionColor.value, GUILayout.ExpandWidth(true));
            water.settings._waterSettings.obstructionWidth.value = EditorGUILayout.FloatField(
                new GUIContent("width", "Pixel width of the obstruction edge effect."),
                water.settings._waterSettings.obstructionWidth.value, GUILayout.ExpandWidth(true));

            EndVB();
        }

        private void simulationSettings()
        {
            StartVB(sector1);
            water.enableSimulation.value = GUILayout.Toggle(water.enableSimulation.value,
                new GUIContent("enable", "Enables the fluid simulation system. Creates interactive ripples from obstructors."),
                GUILayout.ExpandWidth(true));
            if (water.enableSimulation.value && !water.enableObstruction.value) water.enableObstruction.value = true;

            var sim = water._waterSimulationType;
            EditorGUILayout.LabelField("Simple - object space (finite, quality depends on object size)");
            EditorGUILayout.LabelField("Advanced - world space (infinite, constant quality)");
            water._waterSimulationType = (SimulationType)EditorGUILayout.EnumPopup(
                new GUIContent("simulation type", "Simple runs in local UV space (cheaper, resolution depends on sprite size). Advanced runs in world space (constant quality at any scale)."),
                water._waterSimulationType);
            if (water._waterSimulationType != sim) water.settings._simulationSettings.rainSpeed.onValueChanged.Invoke();
            water.settings._simulationSettings.moreStableObstructionSimulation.value = EditorGUILayout.Toggle(
                new GUIContent("more stable obstruction simulation", "Uses 3x obstruction render margins instead of the normal simulation margin. More stable during fast camera movement, but uses more render texture memory and GPU time."),
                water.settings._simulationSettings.moreStableObstructionSimulation.value);

            EditorGUILayout.Space(10);

            if (water._waterSimulationType == SimulationType.advanced)
            {
                EditorGUILayout.LabelField("max simulation size : 2048 (4 194 304 cells)");
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("for 3 iterations :");
                EditorGUILayout.LabelField("high-end pc -> 2048");
                EditorGUILayout.LabelField("mid pc -> 1024");
                EditorGUILayout.LabelField("old pc -> 512");
                EditorGUILayout.LabelField("mid-mobiles -> 256");
            }
            else
            {
                EditorGUILayout.LabelField("max simulation size : 4096 (16 777 216 cells)");
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("for 3 iterations :");
                EditorGUILayout.LabelField("high-end pc -> 4096");
                EditorGUILayout.LabelField("mid pc -> 2048");
                EditorGUILayout.LabelField("old pc -> 1024");
                EditorGUILayout.LabelField("mid-mobiles -> 512");
            }

            EditorGUILayout.Space(10);

            int x;
            resEnum = TextureUtils.ToPowerOf2(water.settings._simulationSettings.resolution.value.x);
            resEnum = (TextureUtils.ResolutionEnum)EditorGUILayout.EnumPopup(
                new GUIContent("resolution (x)", "Horizontal resolution of the simulation texture. Must be power of 2. Higher = finer ripples but more GPU cost."),
                resEnum, GUILayout.ExpandWidth(true));
            if (water._waterSimulationType == SimulationType.advanced && resEnum == TextureUtils.ResolutionEnum._4096) resEnum = TextureUtils.ResolutionEnum._2048;
            x = resEnum.ToInt();

            if (water.ActiveRenderer != null && water.ActiveBounds.size.x > 0 && water.ActiveBounds.size.y > 0)
                water.settings._simulationSettings.resolution.value = new Vector2Int(x, Mathf.FloorToInt(x * 1 / (water.ActiveBounds.size.x / water.ActiveBounds.size.y)));
            else
                water.settings._simulationSettings.resolution.value = new Vector2Int(x, x);

            EditorGUILayout.Space(10);

            water.settings._simulationSettings.normalStrength.value = EditorGUILayout.Slider(
                new GUIContent("normals strength", "Intensity of normal map generated from the simulation. Higher = more pronounced lighting on ripples."),
                water.settings._simulationSettings.normalStrength.value, 0f, 5f, GUILayout.ExpandWidth(true));
            water.settings._simulationSettings.waveColor.value = EditorGUILayout.ColorField(
                new GUIContent("wave color", "Tint color applied to simulation wave crests."),
                water.settings._simulationSettings.waveColor.value, GUILayout.ExpandWidth(true));
            water.settings._simulationSettings.waveColorMinMaxHeight.value = EditorGUILayout.Vector2Field(
                new GUIContent("wave color min/max height", "Height thresholds for wave coloring. X = min height to start coloring, Y = height at full color."),
                water.settings._simulationSettings.waveColorMinMaxHeight.value, GUILayout.ExpandWidth(true));

            EditorGUILayout.Space(10);

            water.settings._simulationSettings.waveHeight.value = EditorGUILayout.FloatField(
                new GUIContent("wave height", "Amplitude of simulation waves. Higher = taller ripples from interactions."),
                water.settings._simulationSettings.waveHeight.value, GUILayout.ExpandWidth(true));
            water.settings._simulationSettings.dispersion.value = EditorGUILayout.Slider(
                new GUIContent("water dispersion", "How quickly waves spread and decay. 1 = no decay, lower values = waves die faster."),
                water.settings._simulationSettings.dispersion.value, 0.75f, 1f, GUILayout.ExpandWidth(true));

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("iterations will increase the cost of simulation linearly, 3 recommended");
            water.settings._simulationSettings.iterations.value = EditorGUILayout.IntSlider(
                new GUIContent("iterations", "Simulation passes per frame. More iterations = faster wave propagation. 3 is a good balance."),
                water.settings._simulationSettings.iterations.value, 1, 16, GUILayout.ExpandWidth(true));

            EditorGUILayout.Space(5);
            water.settings._simulationSettings.enableRain.value = EditorGUILayout.Toggle(
                new GUIContent("enable rain effect", "Spawns random ripples across the water surface simulating rainfall."),
                water.settings._simulationSettings.enableRain.value);

            if (water.settings._simulationSettings.enableRain.value)
            {
                water.settings._simulationSettings.rainSpeed.value = EditorGUILayout.FloatField(
                    new GUIContent("rain speed", "How frequently new raindrops spawn. Higher = more intense rain."),
                    water.settings._simulationSettings.rainSpeed.value);
                water.settings._simulationSettings.rainWaveHeight.value = EditorGUILayout.FloatField(
                    new GUIContent("rain strength", "Amplitude of individual raindrop ripples."),
                    water.settings._simulationSettings.rainWaveHeight.value);

                water.settings._simulationSettings.rainSizeX.value = EditorGUILayout.IntSlider(
                    new GUIContent("rain size X", "Horizontal pixel radius of each raindrop impact."),
                    water.settings._simulationSettings.rainSizeX.value, 1, 4);
                water.settings._simulationSettings.rainSizeY.value = EditorGUILayout.IntSlider(
                    new GUIContent("rain size Y", "Vertical pixel radius of each raindrop impact."),
                    water.settings._simulationSettings.rainSizeY.value, 1, 4);

            }
            EndVB();

        }

        private void surfaceWavesSettings()
        {
            if (!water.IsWavesAvailable)
            {
                StartVB(sector1);
                EditorGUILayout.HelpBox("Surface waves are only available in SpriteRenderer mode.", MessageType.Info);
                EndVB();
                return;
            }

            StartVB(sector1);

            water.enableWavesSimulation.value = EditorGUILayout.Toggle(
                new GUIContent("enable waves", "Enables mesh-deformation surface waves with physics collision support."),
                water.enableWavesSimulation.value);
            water.settings._wavesSettings.wavePoints.value = EditorGUILayout.IntField(
                new GUIContent("wave points", "Number of vertices along the wave surface mesh. More points = smoother waves but more CPU cost."),
                water.settings._wavesSettings.wavePoints.value);
            water.settings._wavesSettings.simulationSteps.value = EditorGUILayout.IntField(
                new GUIContent("simulation steps", "Physics sub-steps per frame for wave simulation. More = more stable at high wave speeds."),
                water.settings._wavesSettings.simulationSteps.value);
            EditorGUILayout.Space(5);
            water.settings._wavesSettings.waveHeight.value = EditorGUILayout.Slider(
                new GUIContent("height", "Maximum displacement height of the wave mesh vertices."),
                water.settings._wavesSettings.waveHeight.value, 0f, 0.5f);
            water.settings._wavesSettings.stringDampening.value = EditorGUILayout.Slider(
                new GUIContent("dampening", "How quickly wave energy dissipates. Higher = waves die faster."),
                water.settings._wavesSettings.stringDampening.value, 0.0f, 1f);
            water.settings._wavesSettings.stringSpread.value = EditorGUILayout.Slider(
                new GUIContent("spread", "How quickly wave motion propagates to neighboring vertices."),
                water.settings._wavesSettings.stringSpread.value, 0f, 0.1f);
            water.settings._wavesSettings.stringStiffness.value = EditorGUILayout.Slider(
                new GUIContent("stiffness", "Spring stiffness of each wave vertex. Higher = snappier return to rest."),
                water.settings._wavesSettings.stringStiffness.value, 0f, 0.5f);
            EditorGUILayout.Space(5);
            water.settings._wavesSettings.splashForceMin.value = EditorGUILayout.FloatField(
                new GUIContent("min kinetic force needed to splash", "Minimum rigidbody kinetic energy required to create a splash on contact."),
                water.settings._wavesSettings.splashForceMin.value);
            water.settings._wavesSettings.splashForceMax.value = EditorGUILayout.FloatField(
                new GUIContent("max kinetic force needed to splash", "Kinetic energy at which splash reaches maximum intensity."),
                water.settings._wavesSettings.splashForceMax.value);
            water.settings._wavesSettings.splashVelMin.value = EditorGUILayout.FloatField(
                new GUIContent("min velocity of splash waves", "Minimum initial velocity of splash wave vertices."),
                water.settings._wavesSettings.splashVelMin.value);
            water.settings._wavesSettings.splashVelMax.value = EditorGUILayout.FloatField(
                new GUIContent("max velocity of splash waves", "Maximum initial velocity of splash wave vertices at max kinetic force."),
                water.settings._wavesSettings.splashVelMax.value);
            water.settings._wavesSettings.splashNodesWidthMin.value = EditorGUILayout.IntSlider(
                new GUIContent("min width of created splash", "Minimum number of vertices displaced by a single splash event."),
                water.settings._wavesSettings.splashNodesWidthMin.value, 1, water.settings._wavesSettings.wavePoints.value / 4);
            water.settings._wavesSettings.splashNodesWidthMax.value = EditorGUILayout.IntSlider(
                new GUIContent("max width of created splash", "Maximum number of vertices displaced by a splash at max force."),
                water.settings._wavesSettings.splashNodesWidthMax.value, water.settings._wavesSettings.splashNodesWidthMin.value, water.settings._wavesSettings.wavePoints.value / 4);
            EditorGUILayout.Space(10);
            water.settings._wavesSettings.edgeColor.value = EditorGUILayout.ColorField(
                new GUIContent("edge color", "Color applied to the leading edge of wave crests."),
                water.settings._wavesSettings.edgeColor.value);
            water.settings._wavesSettings.edgeColoringSize.value = EditorGUILayout.Slider(
                new GUIContent("edge size", "Thickness of the colored edge on wave crests."),
                water.settings._wavesSettings.edgeColoringSize.value, 0f, 0.3f);
            water.settings._wavesSettings.edgeIgnoreTransparency.value = EditorGUILayout.Toggle(
                new GUIContent("edge ignore transparency", "When true, edge coloring ignores the water's alpha value."),
                water.settings._wavesSettings.edgeIgnoreTransparency.value);
            EditorGUILayout.Space(10);
            water.settings._wavesSettings.automaticWaves.value = EditorGUILayout.Toggle(
                new GUIContent("auto waves", "Generates continuous ambient waves automatically without physics interaction."),
                water.settings._wavesSettings.automaticWaves.value);
            water.settings._wavesSettings.waveDensity.value = EditorGUILayout.FloatField(
                new GUIContent("auto speed", "Speed/frequency of automatic ambient waves."),
                water.settings._wavesSettings.waveDensity.value);
            water.settings._wavesSettings.waveDensity2.value = EditorGUILayout.FloatField(
                new GUIContent("auto density", "Spatial density of automatic ambient waves. Higher = more waves visible at once."),
                water.settings._wavesSettings.waveDensity2.value);
            EditorGUILayout.Space(10);
            water.settings._wavesSettings.enableBuoyancy.value = EditorGUILayout.Toggle(
                new GUIContent("enable rigidbody buoyancy", "Applies upward buoyancy force to Rigidbody2D objects floating in the water."),
                water.settings._wavesSettings.enableBuoyancy.value);
            water.settings._wavesSettings.enableRigidbodyCollisions.value = EditorGUILayout.Toggle(
                new GUIContent("enable rigidbody collisions", "Creates splash waves when Rigidbody2D objects collide with the water surface."),
                water.settings._wavesSettings.enableRigidbodyCollisions.value);
            EditorGUILayout.Space(10);


            EndVB();
        }

        private void WetSurfaceSettings()
        {
            StartVB(sector1);

            EditorGUILayout.LabelField("You can use this settings to make the water act as a wet/rainy surface");
            EditorGUILayout.LabelField("Example is in the: 'wet surface' demo scene");

            water.settings._waterSettings.enableBelowWater.value = EditorGUILayout.Toggle(
                new GUIContent("enable", "Renders the scene below/behind the water with distortion. Creates a wet glass or rainy window effect."),
                water.settings._waterSettings.enableBelowWater.value);


            water.settings._waterSettings.belowWaterAlpha.value = EditorGUILayout.Slider(
                new GUIContent("alpha (surface below water)", "Opacity of the below-water scene view. 0 = invisible, 1 = fully visible through water."),
                water.settings._waterSettings.belowWaterAlpha.value, 0f, 1f);
            water.settings._waterSettings.belowWaterDistortionStrength.value = EditorGUILayout.Slider(
                new GUIContent("simulation/distortion strength", "How strongly the simulation or distortion warps the below-water view."),
                water.settings._waterSettings.belowWaterDistortionStrength.value, 0f, 1f);

            EndVB();
        }

        #endregion

        #region Editor & Utils

        private void editorSettings()
        {
        }

        GameObject set;


        private void utilsSettings()
        {
            StartVB(sector1);

            set = (GameObject)EditorGUILayout.ObjectField(set, typeof(GameObject), true);
            if (GUILayout.Button("copy water settings form another water object"))
            {
                if (set.GetComponent<ModernWater2D>() != null)
                {
                    Undo.RecordObject(water, "Copy Water Settings");
                    water.settings = set.GetComponent<ModernWater2D>().settings;
                    Debug.Log("water settings copied");
                }
                else Debug.LogError("couldn't load settings, " + set.name + "doesn't have ModernWater2D component");

            }
            EndVB();
        }

        #endregion


        #region Review Prompt

        private GUIStyle headerStyle;
        private GUIStyle buttonStyle;
        private GUIStyle labelStyle;
        private bool stylesInitialized = false;

        private void InitializeStyles()
        {
            if (stylesInitialized) return;

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.2f, 0.6f, 1f) }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                fixedHeight = 30
            };

            labelStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter
            };

            stylesInitialized = true;
        }

        private void DrawReviewPrompt()
        {
            InitializeStyles();

            EditorGUILayout.Space(10);

            Rect boxRect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Space(10);
            EditorGUILayout.LabelField("⭐ Enjoying Modern 2D Water? ⭐", headerStyle);
            GUILayout.Space(10);

            string usageHours = AssetReviewPrompt.GetUsageHours().ToString("F1");
            EditorGUILayout.LabelField(

                "If you're finding this asset helpful, would you mind leaving a 5-star review?\n" +
                "It really helps support continued development!",
                labelStyle
            );

            GUILayout.Space(10);

            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
            if (GUILayout.Button("⭐ Leave a 5-Star Review ⭐", buttonStyle))
            {
                AssetReviewPrompt.OnReviewClicked();
            }
            GUI.backgroundColor = originalColor;

            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Maybe Later", EditorStyles.miniButton))
            {
                AssetReviewPrompt.OnDismissed();
            }

            if (GUILayout.Button("Don't Ask Again", EditorStyles.miniButton))
            {
                if (EditorUtility.DisplayDialog(
                    "Disable Review Prompts",
                    "Are you sure you want to disable review prompts permanently?",
                    "Yes", "No"))
                {
                    AssetReviewPrompt.OnNeverAskAgain();
                }
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(10);
        }

        #endregion
    }
}