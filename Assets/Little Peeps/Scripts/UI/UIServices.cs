using UnityEngine;

namespace LittlePeeps
{
    // Everything from the world the Canvas may use, packed once by GameBootstrap and handed to UIRoot,
    // which passes it to every IUIElement under the Canvas. This is the ONE way a system reaches the UI:
    // no Canvas element holds a serialized reference to a scene object, so the Canvas prefab carries no
    // overrides in the scene — editing or reverting the Canvas cannot cut a wire it never held.
    //
    // Each element takes only what it needs. Every entry is named after its type, so where a system
    // lives here is never a question.
    //
    // Long-lived things only. The run is NOT here: a prestige replaces it, so an element that needs it
    // asks RunManager.CurrentRun, or keeps the one RunStartedEvent brings — the rule for every
    // long-lived object.
    //
    // A new system for the UI = one field here, one line where GameBootstrap fills it, one in WarnMissing.
    public sealed class UIServices
    {
        public ResourceSystem ResourceSystem;
        public AgeSystem AgeSystem;
        public OfferingSystem OfferingSystem;
        public RunManager RunManager;
        public PlacementController PlacementController;
        public CameraController CameraController;
        public ZonePreviewOverlay ZonePreviewOverlay;

        // The UI's tripwire in one place: a service left empty on GameBootstrap is named once, at boot,
        // instead of each element that needs it doing without in its own quiet way.
        public void WarnMissing(Object context)
        {
            Warn(ResourceSystem, nameof(ResourceSystem), context);
            Warn(AgeSystem, nameof(AgeSystem), context);
            Warn(OfferingSystem, nameof(OfferingSystem), context);
            Warn(RunManager, nameof(RunManager), context);
            Warn(PlacementController, nameof(PlacementController), context);
            Warn(CameraController, nameof(CameraController), context);
            Warn(ZonePreviewOverlay, nameof(ZonePreviewOverlay), context);
        }

        private static void Warn(Object service, string name, Object context)
        {
            if (service == null)
                Debug.LogWarning($"[UIServices] {name} is not assigned on GameBootstrap — the Canvas " +
                                 "elements that need it lose their part.", context);
        }
    }
}
