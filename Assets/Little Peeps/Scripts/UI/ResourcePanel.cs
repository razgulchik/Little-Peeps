using UnityEngine;

// Top-of-screen resource bar. Spawns one ResourceUnit per configured resource and binds each to
// its ReactiveValue in the ResourceSystem, so the row auto-updates on every AddResource/Spend.
//
// Set up in the inspector: assign the ResourceSystem, the ResourceUnit prefab, the container
// (a child with a Horizontal Layout Group), and the icon-per-type list. Order of the list is the
// display order. A type missing from the list is simply not shown.
public class ResourcePanel : MonoBehaviour
{
    [System.Serializable]
    private struct ResourceIcon
    {
        public ResourceType type;
        public Sprite icon;
    }

    [SerializeField] private ResourceSystem resourceSystem;
    [SerializeField] private ResourceUnit unitPrefab;
    [SerializeField] private Transform container;   // parent with a Horizontal Layout Group
    [SerializeField] private ResourceIcon[] resources;

    // Build in Start (not Awake): by now GameBootstrap.Awake has run ResourceSystem.Initialize,
    // so the ReactiveValues exist. See the Awake note in GameBootstrap / SCENE_SETUP.md.
    private void Start()
    {
        if (resourceSystem == null || unitPrefab == null || container == null) return;

        foreach (var entry in resources)
        {
            var value = resourceSystem.GetReactive(entry.type);
            if (value == null) continue;   // type not seeded by ResourceSystem — skip

            var unit = Instantiate(unitPrefab, container);
            unit.Bind(entry.icon, value);
        }
    }
}
