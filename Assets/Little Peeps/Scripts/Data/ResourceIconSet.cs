using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The one authored answer to "what does this ResourceType look like". Lives in an asset rather than
    // on a component so every panel that draws resources — the top bar, the build cards, the age price
    // — reads the SAME table: swapping an icon is one edit, not a hunt through prefabs for the copies
    // that still show the old sprite.
    //
    // The list is ordered on purpose: ResourcePanel draws the bar straight from it, so an entry's
    // position here is its position on screen, and adding an entry adds it to the bar.
    //
    // The pickup effect is part of the same answer: the coin that flies out of anything that pays coins.
    // A ResourceSourceDef may still bring its own (ResourceSourceDef.pickupFx) — meat out of a boar where
    // wheat flies an ear — and a gain with no source at all (a house's tax) has only this one.
    [CreateAssetMenu(menuName = "LittlePeeps/ResourceIconSet")]
    public class ResourceIconSet : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public ResourceType type;
            public Sprite icon;

            [Tooltip("What flies out of whatever pays this resource, unless its ResourceSourceDef has a " +
                     "Pickup Fx of its own. A self-contained ParticleSystem prefab, as on the def. " +
                     "Empty = no pickup effect (only the floating number).")]
            public ParticleSystem pickupFx;

            [Tooltip("Particles emitted per harvest hit. Below 1 reads as 1.")]
            public int pickupFxCount;
        }

        [Tooltip("One entry per ResourceType, in the order the resource bar should show them. " +
                 "A type that is missing simply has no icon and no place in the bar.")]
        [SerializeField] private List<Entry> icons = new();

        public IReadOnlyList<Entry> Icons => icons;

        // Null for a type with no entry — callers show the amount without an icon rather than break.
        public Sprite IconFor(ResourceType type)
        {
            for (int i = 0; i < icons.Count; i++)
                if (icons[i].type == type) return icons[i].icon;
            return null;
        }

        // The resource's own pickup effect; false for a type with no entry or none authored.
        public bool TryGetPickupFx(ResourceType type, out ParticleSystem fx, out int count)
        {
            for (int i = 0; i < icons.Count; i++)
            {
                if (icons[i].type != type || icons[i].pickupFx == null) continue;
                fx = icons[i].pickupFx;
                count = Mathf.Max(1, icons[i].pickupFxCount);
                return true;
            }
            fx = null;
            count = 0;
            return false;
        }
    }
}
