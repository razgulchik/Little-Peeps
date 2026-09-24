using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Plays the island rise (IslandRise) on the game's island: an age's zone comes up out of the sea tile
    // by tile and its structures pop up on it. This component only drives it — the rise itself is a plain
    // class, so that the tuning window can run the very same code on a sample island in Edit Mode.
    //
    // Runs on unscaled time: an age transition plays with the game paused (timeScale 0), so people and
    // animals stand still while their island grows, and everything the rise shows has to keep moving
    // regardless. Wire: profile, islandSystem, waterSystem (optional — without it the tiles in the air do
    // not push on the water).
    public class IslandRisePlayer : MonoBehaviour
    {
        [SerializeField] private IslandRiseProfile profile;
        [SerializeField] private IslandSystem islandSystem;
        [SerializeField] private WaterSystem waterSystem;

        private IslandRise rise;
        private Action onDone;

        public bool IsPlaying => rise != null && rise.IsPlaying;

        // Bring a committed section up out of the sea and pop its structures up on it; onDone once the rise
        // is over and the age banner is due. The section's land must be hidden already.
        public void Play(IslandSection section, IReadOnlyList<StructureInstance> content, Action onDone = null)
        {
            if (IsPlaying) Finish();

            rise ??= new IslandRise(transform, pushWater: waterSystem != null && waterSystem.HasWater,
                                    shakeCamera: true, simulateEffects: false);

            var drawing = islandSystem != null ? islandSystem.Drawing : null;
            int seed = section != null && islandSystem != null
                ? unchecked((islandSystem.Generator?.Seed ?? 0) * 31 + section.Index) : 0;
            if (section == null || !rise.Start(profile, drawing, section, content, seed))
            {
                Debug.LogError("IslandRisePlayer: cannot play — " +
                               (profile == null ? "no profile assigned" : islandSystem == null ? "no IslandSystem assigned" :
                                section == null ? "no section" : "the island is not built"), this);

                // Whatever cannot be played is shown at once: the zone must not stay hidden or switched off.
                if (drawing != null) drawing.RevealAllLand();
                if (content != null)
                    foreach (var instance in content)
                        if (instance?.RuntimeObject != null) instance.RuntimeObject.gameObject.SetActive(true);
                onDone?.Invoke();
                return;
            }

            this.onDone = onDone;
            if (rise.IsOver) Finish();   // an empty section: nothing to play
        }

        // The tap's first step: the rest of the rise faster.
        public void SpeedUp() => rise?.SpeedUp();

        // The tap's second step: straight to the finale.
        public void SkipToFinale() => rise?.SkipToFinale();

        private void Update()
        {
            if (!IsPlaying) return;
            rise.Tick(Time.unscaledDeltaTime);
            if (rise.IsOver) Finish();
        }

        // Whatever cuts a rise short — the object going away, a new rise — the island must not be left
        // half hidden.
        private void OnDisable()
        {
            if (IsPlaying) Finish();
        }

        private void Finish()
        {
            var done = onDone;
            onDone = null;
            rise.Finish();
            done?.Invoke();
        }

        // Play Mode tuning aid: bring the last committed section up again. Right after a run starts that is
        // the start island — it has no old coast to grow from, so it rises from one corner. Its tiles are
        // hidden and its structures shrunk to nothing first; people keep walking, on land nobody can see.
        // Also the Play Mode button of the tuning window.
        [ContextMenu("Replay Last Zone")]
        public void ReplayLastZone()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("IslandRisePlayer: Replay Last Zone works in Play Mode only.", this);
                return;
            }
            var section = islandSystem != null ? islandSystem.LastSection : null;
            if (section == null || islandSystem.Drawing == null)
            {
                Debug.LogWarning("IslandRisePlayer: nothing to replay — no island built yet (or no IslandSystem assigned).", this);
                return;
            }

            if (IsPlaying) Finish();
            islandSystem.Drawing.HideLand(section.Cells);
            Play(section, islandSystem.LastContent);
        }
    }
}
