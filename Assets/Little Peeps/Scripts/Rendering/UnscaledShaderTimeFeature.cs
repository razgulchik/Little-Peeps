using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace LittlePeeps
{
    // Shader time that ignores Time.timeScale. URP fills _Time / _TimeParameters from Time.time, so every
    // shader-animated surface stops dead whenever the game pauses by timeScale 0 — build mode, the zone
    // pick, the perk pick — and the water (Modern 2D Water: foam, strips, surface scroll are all Time
    // nodes) is the whole background. Shader animation is ambience, not gameplay, so it runs on real time.
    //
    // ALWAYS unscaled, not only while paused: scaled and unscaled time drift apart at the first pause, and
    // switching source on the way in or out would jump every wave's phase in a single frame.
    //
    // Where it sits: the 2D renderer sets the time twice per camera (frame init, then again after camera
    // setup) and only then records BeforeRendering passes, so this pass is the last word before anything
    // draws. Runs for every camera on the renderer — Modern 2D Water's own obstruction / below-water /
    // reflection cameras included, which keeps them in step with the main one. Edit mode keeps URP's own
    // choice (realtime since startup), so the Scene view animates as before.
    //
    // Setup: add it to the 2D renderer asset (Renderer2D → Add Renderer Feature). Render Graph path only —
    // the project runs with Compatibility Mode off.
    public class UnscaledShaderTimeFeature : ScriptableRendererFeature
    {
        private UnscaledShaderTimePass pass;

        public override void Create()
        {
            pass = new UnscaledShaderTimePass { renderPassEvent = RenderPassEvent.BeforeRendering };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(pass);
        }

        private class UnscaledShaderTimePass : ScriptableRenderPass
        {
            private static readonly int TimeId = Shader.PropertyToID("_Time");
            private static readonly int SinTimeId = Shader.PropertyToID("_SinTime");
            private static readonly int CosTimeId = Shader.PropertyToID("_CosTime");
            private static readonly int DeltaTimeId = Shader.PropertyToID("unity_DeltaTime");
            private static readonly int TimeParametersId = Shader.PropertyToID("_TimeParameters");
            private static readonly int LastTimeParametersId = Shader.PropertyToID("_LastTimeParameters");

            private class PassData { }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                using (var builder = renderGraph.AddUnsafePass<PassData>("Unscaled Shader Time", out _))
                {
                    builder.AllowPassCulling(false);          // writes no texture, so it would be culled
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((PassData _, UnsafeGraphContext context) => SetTime(context.cmd));
                }
            }

            // Same vectors URP builds in ScriptableRenderer.SetShaderTimeValues, fed from unscaled time.
            private static void SetTime(UnsafeCommandBuffer cmd)
            {
                float time = Application.isPlaying ? Time.unscaledTime : Time.realtimeSinceStartup;
                float dt = Time.unscaledDeltaTime;
                float last = time - dt;

                cmd.SetGlobalVector(TimeId, time * new Vector4(1f / 20f, 1f, 2f, 3f));
                cmd.SetGlobalVector(SinTimeId, new Vector4(Mathf.Sin(time / 8f), Mathf.Sin(time / 4f), Mathf.Sin(time / 2f), Mathf.Sin(time)));
                cmd.SetGlobalVector(CosTimeId, new Vector4(Mathf.Cos(time / 8f), Mathf.Cos(time / 4f), Mathf.Cos(time / 2f), Mathf.Cos(time)));
                cmd.SetGlobalVector(DeltaTimeId, new Vector4(dt, 1f / dt, dt, 1f / dt));
                cmd.SetGlobalVector(TimeParametersId, new Vector4(time, Mathf.Sin(time), Mathf.Cos(time), 0f));
                cmd.SetGlobalVector(LastTimeParametersId, new Vector4(last, Mathf.Sin(last), Mathf.Cos(last), 0f));
            }
        }
    }
}
