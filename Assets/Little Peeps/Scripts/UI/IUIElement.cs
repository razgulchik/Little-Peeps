namespace LittlePeeps
{
    // A Canvas element that needs something from the world. UIRoot finds every one under the Canvas —
    // inactive ones too — and hands it UIServices once, from GameBootstrap.Awake, before the first frame.
    //
    // Adding an element = implement this and put it anywhere under the Canvas. No field on UIRoot, no
    // drag in the Inspector, no override in the scene.
    //
    // The one rule: Initialize may arrive BEFORE the element's own Awake (bootstrap's Awake can run
    // first), so take what you need and stop there — at most subscribe to a system's C# event. Draw from
    // Start onward: by then every Awake has run, and with it every Initialize.
    //
    // Only for elements authored in the Canvas prefab. What is spawned at runtime (a build card, a
    // resource unit) gets what it needs from the element that spawns it.
    public interface IUIElement
    {
        void Initialize(UIServices ui);
    }
}
