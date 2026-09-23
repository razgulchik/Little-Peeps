using System;
using UnityEngine;
using UnityEngine.Events;

namespace Water2D
{
    [Serializable]
    public enum ColoringType
    {
        single_color = 0,
        two_colors = 1,
        depthY = 2,
        distance_from_obstructors = 3
    }

    public enum WaterType
    {
        normal = 0,
        cheap = 1,
        mobile = 2

    }

    [Serializable]
    public class WaterSettings
    {

        public WaterCryo<Color> color = new WaterCryo<Color>(new Color(0.1f, 0.4f, 0.8f, 1f));
        public WaterCryo<Color> depthColor = new WaterCryo<Color>(new Color(0.05f, 0.2f, 0.5f, 1f));
        public WaterType waterType = WaterType.normal;
        public ColoringType coloringType = ColoringType.two_colors;
        public WaterCryo<Vector2> tiling = new WaterCryo<Vector2>(Vector2.one);
        public WaterCryo<float> baseAlpha = new WaterCryo<float>(1f);
        public Texture2D alphaTexture;
        public WaterCryo<int> numOfPixels = new WaterCryo<int>(128);
        public WaterCryo<bool> pixelPerfect = new WaterCryo<bool>(false);
        public WaterCryo<float> obstructionWidth = new WaterCryo<float>(0.05f);
        public WaterCryo<Color> obstructionColor = new WaterCryo<Color>(Color.white);
        public WaterCryo<float> obstructionAlpha = new WaterCryo<float>(1f);
        public WaterCryo<Color> foamColor = new WaterCryo<Color>(new Color(0.9f, 0.95f, 1f, 1f));
        public WaterCryo<float> foamSize = new WaterCryo<float>(0.75f);
        public WaterCryo<Vector2> foamSpeed = new WaterCryo<Vector2>(new Vector2(0.3f, 0.1f));
        public WaterCryo<float> foamAlpha = new WaterCryo<float>(0.6f);
        public WaterCryo<Vector2> distortionSpeed = new WaterCryo<Vector2>(new Vector2(0.1f, 0.05f));
        public WaterCryo<Vector2> distortionStrength = new WaterCryo<Vector2>(new Vector2(0.00f, 0.00f));
        public WaterCryo<Vector2> distortionTiling = new WaterCryo<Vector2>(Vector2.one);
        public WaterCryo<Vector2> distortionMinMax = new WaterCryo<Vector2>(new Vector2(0f, 1f));
        public WaterCryo<Color> distortionColor = new WaterCryo<Color>(Color.black);
        public Texture2D distortionTexture;
        public Texture2D sunStripsTexture;
        public WaterCryo<float> stripsSpeed = new WaterCryo<float>(0.4f);
        public WaterCryo<float> stripsScrollingSpeed = new WaterCryo<float>(0.2f);
        public WaterCryo<float> stripsSize = new WaterCryo<float>(0.3f);

        public SpriteRenderer surfaceSprite;
        public Texture2D surfaceTexture;
        public WaterCryo<Vector2> surfaceTiling = new WaterCryo<Vector2>(Vector2.one);
        public WaterCryo<Vector2> surfaceSpeed = new WaterCryo<Vector2>(new Vector2(0.1f, 0f));
        public WaterCryo<bool> useFoamSpeed = new WaterCryo<bool>(false);
        public WaterCryo<float> surfaceAlpha = new WaterCryo<float>(0f);

        public WaterCryo<float> stripsAlpha = new WaterCryo<float>(0.0f);
        public WaterCryo<float> stripsDensity = new WaterCryo<float>(1f);
        public WaterCryo<float> foamDensity = new WaterCryo<float>(0.05f);
        public WaterCryo<Vector2> perspective = new WaterCryo<Vector2>(Vector2.zero);
        public WaterCryo<bool> _useLighting = new WaterCryo<bool>(false);
        public WaterCryo<bool> depthFromObstructors = new WaterCryo<bool>(false);

        public WaterCryo<bool> enableBelowWater = new WaterCryo<bool>(false);
        public WaterCryo<Vector4> belowWaterUV = new WaterCryo<Vector4>(new Vector4(0, 0, 1, 1));
        public WaterCryo<float> belowWaterDistortionStrength = new WaterCryo<float>(0.02f);
        public WaterCryo<float> belowWaterAlpha = new WaterCryo<float>(1f);

        public WaterCryo<Gradient> colorGradient = new WaterCryo<Gradient>(new Gradient());
        public WaterCryo<float> depthMlp = new WaterCryo<float>(1f);

        internal void onValueChanged(UnityAction onWaterChanged)
        {
            depthMlp.onValueChanged = onWaterChanged;
            colorGradient.onValueChanged = onWaterChanged;
            enableBelowWater.onValueChanged = onWaterChanged;
            depthFromObstructors.onValueChanged = onWaterChanged;
            belowWaterAlpha.onValueChanged = onWaterChanged;
            belowWaterDistortionStrength.onValueChanged = onWaterChanged;
            belowWaterUV.onValueChanged = onWaterChanged;
            surfaceSpeed.onValueChanged = onWaterChanged;
            surfaceTiling.onValueChanged = onWaterChanged;
            useFoamSpeed.onValueChanged = onWaterChanged;
            surfaceAlpha.onValueChanged = onWaterChanged;
            color.onValueChanged = onWaterChanged;
            tiling.onValueChanged = onWaterChanged;
            pixelPerfect.onValueChanged = onWaterChanged;
            numOfPixels.onValueChanged = onWaterChanged;
            obstructionWidth.onValueChanged = onWaterChanged;
            obstructionColor.onValueChanged = onWaterChanged;
            obstructionAlpha.onValueChanged = onWaterChanged;
            depthColor.onValueChanged = onWaterChanged;
            foamColor.onValueChanged = onWaterChanged;
            foamSize.onValueChanged = onWaterChanged;
            foamSpeed.onValueChanged = onWaterChanged;
            foamAlpha.onValueChanged = onWaterChanged;
            distortionSpeed.onValueChanged = onWaterChanged;
            distortionStrength.onValueChanged = onWaterChanged;
            distortionTiling.onValueChanged = onWaterChanged;
            stripsSpeed.onValueChanged = onWaterChanged;
            stripsScrollingSpeed.onValueChanged = onWaterChanged;
            stripsSize.onValueChanged = onWaterChanged;
            stripsAlpha.onValueChanged = onWaterChanged;
            stripsDensity.onValueChanged = onWaterChanged;
            foamDensity.onValueChanged = onWaterChanged;
            baseAlpha.onValueChanged = onWaterChanged;
            perspective.onValueChanged = onWaterChanged;
            distortionMinMax.onValueChanged = onWaterChanged;
            distortionColor.onValueChanged = onWaterChanged;
            _useLighting.onValueChanged = onWaterChanged;
        }
    }

}