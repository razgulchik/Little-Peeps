using UnityEngine;

namespace LittlePeeps
{
    // Top-of-screen resource bar. Spawns one ResourceUnit per entry in the shared ResourceIconSet and
    // binds each to its ReactiveValue in the ResourceSystem, so the row auto-updates on every
    // AddResource/Spend.
    //
    // The icon set IS the bar's contents: its order is the display order, and adding an entry there
    // adds it here. Set up in the inspector: assign the ResourceSystem, the ResourceUnit prefab, the
    // container (a child with a Horizontal Layout Group) and the icon set. A type the ResourceSystem
    // never seeded is simply not shown.
    public class ResourcePanel : MonoBehaviour
    {
        [SerializeField] private ResourceSystem resourceSystem;
        [SerializeField] private ResourceUnit unitPrefab;
        [SerializeField] private Transform container;   // parent with a Horizontal Layout Group
        [SerializeField] private ResourceIconSet iconSet;

        // Build in Start (not Awake): by now GameBootstrap.Awake has run ResourceSystem.Initialize,
        // so the ReactiveValues exist. See the Awake note in GameBootstrap / SCENE_SETUP.md.
        private void Start()
        {
            if (resourceSystem == null || unitPrefab == null || container == null) return;

            if (iconSet == null)
            {
                Debug.LogWarning("[ResourcePanel] no ResourceIconSet assigned — the bar stays empty.", this);
                return;
            }

            var icons = iconSet.Icons;
            for (int i = 0; i < icons.Count; i++)
            {
                var value = resourceSystem.GetReactive(icons[i].type);
                if (value == null) continue;   // type not seeded by ResourceSystem — skip

                var unit = Instantiate(unitPrefab, container);
                unit.Bind(icons[i].icon, value);
            }
        }
    }
}
