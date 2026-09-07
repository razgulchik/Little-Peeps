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
    [CreateAssetMenu(menuName = "LittlePeeps/ResourceIconSet")]
    public class ResourceIconSet : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public ResourceType type;
            public Sprite icon;
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
    }
}
