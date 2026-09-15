using UnityEngine;

namespace LittlePeeps
{
    // Fading a harvested node's ready visual out — its alpha and its vertical squash. Its own type for
    // the same reason as HarvestNumberFormat: the game runs it on every reaped field, and the Edit
    // Mode authoring tool replays it to preview the harvest, so there must be exactly one
    // implementation of it.
    public static class HarvestFade
    {
        // SpriteRenderer.color is a per-renderer vertex colour, NOT a material property: tinting here
        // creates no material instance and so cannot quietly break batching across hundreds of nodes.
        public static void ApplyAlpha(SpriteRenderer[] renderers, float alpha)
        {
            if (renderers == null) return;

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;

                var c = r.color;
                c.a = alpha;
                r.color = c;
            }
        }

        // Scales the root's transform rather than any sprite, so the pivot authored on the art decides
        // what stays put — bottom-centre wheat squashes toward the ground — and everything under the
        // root (a second sprite, a shadow) follows without wiring. Y only: x is left to the prefab.
        public static void ApplyScaleY(Transform root, float scaleY)
        {
            if (root == null) return;

            var s = root.localScale;
            s.y = scaleY;
            root.localScale = s;
        }
    }
}
