using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


namespace Water2D
{
    
    [Serializable]
    public class ReflectionsSettings
    {
        public WaterCryo<bool> enableTopDownReflections = new WaterCryo<bool>(false);
        public WaterCryo<bool> enablePlatformerReflections = new WaterCryo<bool>(false);
        public WaterCryo<bool> enableRaymarchedReflections = new WaterCryo<bool>(false);

        public Camera mainCamera;
        public WaterCryo<float> textureResolution = new WaterCryo<float>(1f);
        public WaterCryo<bool> overrideMainCamera = new WaterCryo<bool>(false);
        public WaterCryo<bool> reflectionObjectsVisible = new WaterCryo<bool>(false);
        public WaterCryo<bool> cameraVisible = new WaterCryo<bool>(true);
        public WaterCryo<bool> defaultReflectionSprflipx = new WaterCryo<bool>(false);
        public WaterCryo<float> angle = new WaterCryo<float>(0f);
        public WaterCryo<float> tilt = new WaterCryo<float>(0f);
        public WaterCryo<float> length = new WaterCryo<float>(1f);
        public WaterCryo<float> topdownReflections_FalloffStrength = new WaterCryo<float>(1f);
        public WaterCryo<float> topdownReflections_FalloffStart = new WaterCryo<float>(0f);
        public WaterCryo<Color> topdownReflections3D_FalloffColor = new WaterCryo<Color>(Color.black);
        public WaterCryo<float> originalColor = new WaterCryo<float>(0f);
        public WaterCryo<Color> color = new WaterCryo<Color>(Color.white);
        public WaterCryo<float> alpha = new WaterCryo<float>(1f);

        public WaterCryo<float> mirrorY = new WaterCryo<float>(0f);
        public List<int> layers;

        public WaterCryo<bool> usePerspective = new WaterCryo<bool>(false);
        public WaterCryo<Vector2> waterPerspective = new WaterCryo<Vector2>(Vector2.zero);
        public WaterCryo<Vector2> reflectionsPerspective = new WaterCryo<Vector2>(Vector2.zero);

        public WaterCryo<float> falloffStrength = new WaterCryo<float>(1f);
        public WaterCryo<float> falloffStart = new WaterCryo<float>(0f);
        public WaterCryo<bool> enableFalloff = new WaterCryo<bool>(false);
        public WaterCryo<bool> enableScrolling = new WaterCryo<bool>(false);
        public WaterCryo<bool> customReflectionStart = new WaterCryo<bool>(false);
        public Transform playerPosition;
        public WaterCryo<float> scrollingStrength = new WaterCryo<float>(1f);

        public WaterCryo<bool> DistortionFPRH = new WaterCryo<bool>(false);

        public List<int> raymarchlayers;
        public WaterCryo<int> raymarchSteps = new WaterCryo<int>(16);
        public WaterCryo<bool> type2 = new WaterCryo<bool>(false);
        public WaterCryo<float> raymarchFalloffStart = new WaterCryo<float>(0f);
        public WaterCryo<float> raymarchFalloffEnd = new WaterCryo<float>(1f);

        internal void onValueChanged(UnityAction onReflectionsChanged)
        {
            raymarchSteps.onValueChanged = onReflectionsChanged;
            type2.onValueChanged = onReflectionsChanged;
            raymarchFalloffStart.onValueChanged = onReflectionsChanged;
            raymarchFalloffEnd.onValueChanged = onReflectionsChanged;
            enableRaymarchedReflections.onValueChanged = onReflectionsChanged;

            textureResolution.onValueChanged = onReflectionsChanged;
            overrideMainCamera.onValueChanged = onReflectionsChanged;
            reflectionObjectsVisible.onValueChanged = onReflectionsChanged;
            cameraVisible.onValueChanged = onReflectionsChanged;
            defaultReflectionSprflipx.onValueChanged = onReflectionsChanged;
            angle.onValueChanged = onReflectionsChanged;
            tilt.onValueChanged = onReflectionsChanged;
            length.onValueChanged = onReflectionsChanged;
            originalColor.onValueChanged = onReflectionsChanged;
            color.onValueChanged = onReflectionsChanged;
            alpha.onValueChanged = onReflectionsChanged;
            mirrorY.onValueChanged = onReflectionsChanged;
            usePerspective.onValueChanged = onReflectionsChanged;
            topdownReflections3D_FalloffColor.onValueChanged = onReflectionsChanged;
            waterPerspective.onValueChanged = onReflectionsChanged;
            reflectionsPerspective.onValueChanged = onReflectionsChanged;
            falloffStrength.onValueChanged = onReflectionsChanged;
            falloffStart.onValueChanged = onReflectionsChanged;
            enableFalloff.onValueChanged = onReflectionsChanged;
            scrollingStrength.onValueChanged = onReflectionsChanged;
            enableScrolling.onValueChanged = onReflectionsChanged;
            enableTopDownReflections.onValueChanged = onReflectionsChanged;
            enablePlatformerReflections.onValueChanged = onReflectionsChanged;
            DistortionFPRH.onValueChanged = onReflectionsChanged;
            customReflectionStart.onValueChanged = onReflectionsChanged;
            topdownReflections_FalloffStrength.onValueChanged = onReflectionsChanged;
            topdownReflections_FalloffStart.onValueChanged = onReflectionsChanged;
        }
    }

}