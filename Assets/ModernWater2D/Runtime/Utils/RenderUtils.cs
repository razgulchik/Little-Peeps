using UnityEngine;
using UnityEngine.Rendering;

namespace LeafUtils
{
    public static class RenderUtils
    {
        private static Mesh _fullscreenQuadMesh;

        private static Mesh FullscreenQuadMesh
        {
            get
            {
                if (_fullscreenQuadMesh != null) return _fullscreenQuadMesh;

                _fullscreenQuadMesh = new Mesh
                {
                    name = "Modern Water 2D Fullscreen Quad"
                };
                _fullscreenQuadMesh.vertices = new[]
                {
                    new Vector3(-1f, -1f, 0f),
                    new Vector3(-1f,  1f, 0f),
                    new Vector3( 1f,  1f, 0f),
                    new Vector3( 1f, -1f, 0f)
                };
                _fullscreenQuadMesh.uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(1f, 0f)
                };
                _fullscreenQuadMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                _fullscreenQuadMesh.UploadMeshData(false);
                return _fullscreenQuadMesh;
            }
        }

        public static void RenderToRT2D(Material mat, RenderTexture result) => RenderToRT2D(new Rect(0, 0, result.width, result.height), mat, result);
        public static void RenderToRT2D(RenderTexture result, Material mat) => RenderToRT2D(new Rect(0, 0, result.width, result.height), mat, result);

        public static void RenderToRT2D(Rect viewport, Material mat, RenderTexture result)
        {
            if (mat == null || result == null) return;

            var cmd = CommandBufferPool.Get("Modern Water 2D RenderToRT2D");
            cmd.Clear();

            CoreUtils.SetRenderTarget(cmd, result);
            cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
            cmd.SetViewport(viewport);
            cmd.DrawMesh(FullscreenQuadMesh, Matrix4x4.identity, mat, 0, 0);

            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        public static RenderTexture DeepCopy(this RenderTexture tex)
        {
            RenderTexture tex2 = new RenderTexture(tex.width, tex.height, tex.depth, tex.format, tex.mipmapCount);
            if (tex.enableRandomWrite) tex2.enableRandomWrite = false;
            Graphics.CopyTexture(tex, tex2);
            return tex2;
        }
    }
}
