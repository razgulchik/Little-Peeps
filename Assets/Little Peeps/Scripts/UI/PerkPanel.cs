using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The HUD's column of taken perks, top-right under the resource bar: one PerkSlotUI per perk in
    // RunContext.perksChosen, in the order they were taken. Cells appear as perks are taken — no empty
    // places; the Grid Layout Group on this object lays them out and the Content Size Fitter grows it down.
    //
    // The run's list is the truth: built from it at boot and on every new run, then one cell per
    // PerkAppliedEvent (PerkSystem publishes it once the perk stands in the list).
    //
    // Cells are kept, not destroyed: a new run switches the extras off and the next perks reuse them —
    // a layout group skips inactive children.
    public class PerkPanel : MonoBehaviour, IUIElement
    {
        [SerializeField] private PerkSlotUI slotPrefab;

        private readonly List<PerkSlotUI> slots = new();
        private RunContext run;
        private int shown;   // cells in use, from the front of `slots`

        // From UIRoot once the run exists; RunStartedEvent replaces it.
        public void Initialize(UIServices ui) => run = ui.RunManager != null ? ui.RunManager.CurrentRun : null;

        private void OnEnable()
        {
            EventBus<RunStartedEvent>.Subscribe(OnRunStarted);
            EventBus<PerkAppliedEvent>.Subscribe(OnPerkApplied);
        }

        private void OnDisable()
        {
            EventBus<RunStartedEvent>.Unsubscribe(OnRunStarted);
            EventBus<PerkAppliedEvent>.Unsubscribe(OnPerkApplied);
        }

        private void Start()
        {
            if (slotPrefab == null)
                Debug.LogWarning("[PerkPanel] no PerkSlot prefab assigned — taken perks are not shown.", this);
            Rebuild();
        }

        private void OnRunStarted(RunStartedEvent e)
        {
            run = e.Run;
            Rebuild();
        }

        private void OnPerkApplied(PerkAppliedEvent e) => Add(e.Perk);

        private void Rebuild()
        {
            shown = 0;
            if (run != null)
                for (int i = 0; i < run.perksChosen.Count; i++)
                    Add(run.perksChosen[i]);

            for (int i = shown; i < slots.Count; i++) slots[i].gameObject.SetActive(false);
        }

        private void Add(PerkDef perk)
        {
            if (perk == null || slotPrefab == null) return;

            if (shown == slots.Count) slots.Add(Instantiate(slotPrefab, transform));

            PerkSlotUI slot = slots[shown++];
            slot.gameObject.SetActive(true);
            slot.Bind(perk);
        }
    }
}
