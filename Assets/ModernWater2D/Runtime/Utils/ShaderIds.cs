
namespace Water2D
{


    public static class WaterShaderIdsREF
    {
        public static readonly string color = "_RFcolor";
        public static readonly string orgColor = "_RForgColor";
        public static readonly string alpha = "_RFalpha";
        public static readonly string topdownReflections_FalloffStrength = "_yFalloffStrength";
        public static readonly string topdownReflections_FalloffStart = "_yFalloffStart";
        public static readonly string topdownReflections3D_FalloffColor = "_yFalloff3dColor";
        public static readonly string reflectionsTexture = "_RFreflectionsTexture";
        public static readonly string reflectionsTexture2 = "_RFreflectionsTexture2";
        public static readonly string reflectionsTexture3 = "_RFreflectionsTexture3";
    }

    public static class WaterShaderIdsSUR
    {
        public static readonly string surfaceTexture = "_SurfaceTexture";
    }

    public static class WaterShaderIdsOBS
    {
        public static readonly string DepthTexture = "_DepthTexture";
        public static readonly string OBStexture = "_OBStexture";
    }

}
