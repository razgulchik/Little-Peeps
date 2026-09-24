using System;
using UnityEngine;

namespace LittlePeeps
{
    // The order the new zone's tiles start rising in: which cell of the zone goes early and which late.
    public enum IslandRiseOrder
    {
        JoinNoise,   // cell distance from the old coast, bent by smooth noise: the front grows organically
        Join,        // cell distance from the old coast: honest growth, but the front advances in diamonds
        Radial,      // straight-line distance from the middle of the join: rings
        Sweep,       // a straight front travelling from the join across the zone
        Random       // no wave at all; only there as a contrast
    }

    // WHEN things happen in the island rise, and nothing about how they look. Plain data, split out of
    // IslandRiseProfile so IslandRiseSchedule can be built and tested without a ScriptableObject — the
    // offline test harness cannot create one. The profile holds one of these; the curves stay there.
    //
    // All times are seconds of the rise's own clock, which runs from 0 when the transition starts.
    [Serializable]
    public class IslandRiseTiming
    {
        [Header("Wave")]
        public IslandRiseOrder order = IslandRiseOrder.JoinNoise;

        [Tooltip("Seconds from the start of the transition to the first tile starting to rise: the " +
                 "anticipation, while the water bubbles over the zone.")]
        [Min(0f)] public float leadIn = 0.5f;

        [Tooltip("Seconds from the first tile starting to rise to the last one starting. The same for any " +
                 "zone size: a big zone rises denser, not longer.")]
        [Min(0.05f)] public float cascade = 1.6f;

        [Tooltip("1 = the wave keeps an even pace. Above 1 it speeds up as it goes: the first tiles come " +
                 "one by one, the last ones in a rush.")]
        [Range(1f, 3f)] public float crescendo = 1.8f;

        [Tooltip("How far a tile may start out of its place in the wave. 1 = up to ±15% of the cascade.")]
        [Range(0f, 1f)] public float jitter = 0.4f;

        [Tooltip("Join Noise only: size of the noise's blobs, in cells.")]
        [Min(0.5f)] public float noiseScale = 3.2f;

        [Tooltip("Join Noise only: how far the noise bends the front, as a share of the zone's depth from " +
                 "the join. 0 = plain Join.")]
        [Range(0f, 2f)] public float noiseStrength = 0.7f;

        [Header("Tile")]
        [Tooltip("Seconds a tile spends rising from the depth, seen only through the water, before it " +
                 "breaks the surface.")]
        [Min(0.01f)] public float depthRise = 0.4f;

        [Tooltip("Seconds from breaking the surface to touching down.")]
        [Min(0.01f)] public float hop = 0.32f;

        [Tooltip("Seconds of squash after touching down. At its end the tile becomes a real tilemap tile " +
                 "and the coast around it is redrawn.")]
        [Min(0f)] public float squash = 0.2f;

        [Header("Content (act 2)")]
        [Tooltip("When the trees, rocks and mountains start popping up, as a share of the cascade: 0.7 = " +
                 "once 70% of the wave's time has passed. Nothing pops before the land under it settles.")]
        [Range(0f, 1.5f)] public float contentStart = 0.7f;

        [Tooltip("Seconds from the first pop to the last, in the order their land settled.")]
        [Min(0f)] public float contentSpread = 0.9f;

        [Tooltip("Least seconds between the land under a structure settling and the structure popping.")]
        [Min(0f)] public float contentGap = 0.05f;

        [Tooltip("Random shift of each pop, ± seconds, so neighbours don't pop in lockstep.")]
        [Min(0f)] public float contentJitter = 0.04f;

        [Tooltip("Seconds between one river cell and the next as the river runs in from its source (the end " +
                 "the wave reaches first). A river cell never comes before its own land.")]
        [Min(0f)] public float riverStep = 0.07f;

        [Header("Finale")]
        [Tooltip("Seconds from the last tile or pop settling to the finale: the breath and the sparkles.")]
        [Min(0f)] public float finaleDelay = 0.15f;

        [Tooltip("Seconds one cell of the new zone takes to breathe — rise a little and settle back.")]
        [Min(0f)] public float breathTime = 0.4f;

        [Tooltip("How fast the breath runs across the new zone from the join, in cells per second.")]
        [Min(1f)] public float breathSpeed = 20f;

        [Tooltip("Least seconds from the finale to the age banner. The banner also waits for the breath to " +
                 "finish crossing the zone.")]
        [Min(0f)] public float finaleLength = 0.55f;
    }
}
