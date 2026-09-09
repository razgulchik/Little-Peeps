using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The perk cards. Owns the screen and nothing else: it does not apply the perk and does not know the
    // run. A confirmed card is published as PerkSelectedEvent and PerkSelectionState does the rest.
    //
    // That split is the same one BuildCardUI → BuildPanelUI → FSM already uses, and it is why this class
    // holds no RunContext. The previous stub took one in an Initialize() called once from
    // GameBootstrap.Awake — a long-lived UI object caching the run, which is exactly the shape that
    // froze the resource bar after the first prestige.
    //
    // The cards themselves land in the next commit, together with PerkCardUI.
    public class PerkSelectionUI : MonoBehaviour
    {
        // Show the panel with one card per offered perk. Never called with an empty list: PerkSelection-
        // State skips the whole step instead, so this does not need an "and if there are none" branch.
        public void Show(List<PerkDef> perks)
        {
            // TODO: build one PerkCardUI per perk into the card container and reveal the panel.
        }

        public void Hide()
        {
            // TODO: hide the panel and drop the cards built by Show.
        }
    }
}
