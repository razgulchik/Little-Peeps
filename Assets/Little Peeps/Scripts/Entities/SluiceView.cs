using System;
using UnityEngine;

namespace LittlePeeps
{
    // The black sand in the sluice's tray. Presentation only: reads Sluice.Stage every frame and never
    // writes to it, so removing this component leaves the mechanic untouched. Sits on the sluice's root,
    // next to Sluice, like every view; the renderers it drives are wired in.
    //
    // Every spot's art (WaterSideVisual: one root per spot, one shown) has its own sand overlay — a
    // renderer laid over the model with the same canvas and pivot, so the sand sits in that spot's tray
    // pixel for pixel. The view sets every overlay, shown root or not: the spot is decided once, when the
    // sluice lands, and the hidden ones cost a sprite assignment per stage change. Every spot has sand of
    // its own, the mirrored spots included — drawn mirrored, like their models, never flipped in code.
    //
    // The overlays are authored DISABLED — an empty tray. The placement ghost is this same prefab with
    // every behaviour disabled, so what is authored on is what it shows: a sluice with nothing settled yet.
    public class SluiceView : MonoBehaviour
    {
        [Serializable]
        private class Tray
        {
            [Tooltip("The sand overlay of one spot's art, next to its Model. Author it disabled.")]
            public SpriteRenderer sand;

            [Tooltip("The sand at each stage, from the first — as many as the sluice has stages.")]
            public Sprite[] stages;
        }

        [Tooltip("One per spot's art. The overlays sort above their models inside the Visual's SortingGroup.")]
        [SerializeField] private Tray[] trays;

        private Sluice sluice;
        private int shown;              // the stage on screen, so renderers are only touched on change

        private void Awake()
        {
            sluice = GetComponentInParent<Sluice>();
            if (sluice == null)
                Debug.LogError($"SluiceView on '{name}' has no Sluice above it in the hierarchy.", this);
            trays ??= new Tray[0];
        }

        private void Update()
        {
            if (sluice == null || sluice.Stage == shown) return;

            shown = sluice.Stage;
            foreach (var tray in trays)
            {
                if (tray?.sand == null) continue;

                var sprite = shown > 0 && tray.stages != null && shown <= tray.stages.Length
                    ? tray.stages[shown - 1]
                    : null;
                tray.sand.sprite = sprite;
                tray.sand.enabled = sprite != null;
            }
        }
    }
}
