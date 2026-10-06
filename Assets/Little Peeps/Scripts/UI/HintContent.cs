using System.Collections.Generic;

namespace LittlePeeps
{
    // What one hint says. Every part is optional: HintView switches off the section of a part left empty
    // and the window shrinks to what is left — a resource's hint can be a title alone, a build card's
    // fills all four. A source keeps ONE instance and refills it (Clear, then set), so a hint shown again
    // allocates nothing.
    public sealed class HintContent
    {
        public string title;
        public string body;
        public readonly List<HintCost> costs = new();
        public string footer;

        public void Clear()
        {
            title = null;
            body = null;
            costs.Clear();
            footer = null;
        }
    }

    // One price line: the resource, how much, and whether the wallet covers it right now (red if not).
    public struct HintCost
    {
        public ResourceType resourceType;
        public float amount;
        public bool affordable;
    }
}
