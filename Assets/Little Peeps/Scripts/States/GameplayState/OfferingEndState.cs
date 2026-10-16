using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LittlePeeps
{
    // The end of an offering cycle that was not paid early: the game stops and asks. Two answers —
    // Offer (only when the stock covers the quota) pays it and opens the perk pick; Accept wrath (always)
    // ends the run: it is cashed in and the meta screen follows, as after any prestige. Wrath is the
    // player's to accept even with a full stock, so this is also the way to end a run on purpose.
    //
    // Entered by GameplayContainerState from normal play when the clock runs out on an unpaid cycle. A
    // cycle paid early never comes here: the container just starts the next one.
    //
    // Frozen while the question is up, like every screen that waits on the player — the stock must not
    // change between "can you pay?" and the answer.
    public class OfferingEndState : IState
    {
        private readonly StateMachine gameplayFsm;
        private readonly OfferingSystem offerings;
        private readonly PrestigeSystem prestigeSystem;
        private readonly ConfirmDialog dialog;
        private readonly PlayingState playingState;
        private readonly PerkSelectionState perkSelection;
        private readonly MetaUpgradesState metaUpgrades;

        // RunManager, not RunContext: wrath ends the run this would hold. Read CurrentRun at the point of use.
        private readonly RunManager runManager;

        // Where to go when the question cannot be asked; acted on in Tick for PerkSelectionState's reason —
        // leaving from inside Enter would run this object's Exit before its Enter had returned.
        private IState leaveTo;

        public OfferingEndState(StateMachine gameplayFsm, OfferingSystem offerings, PrestigeSystem prestigeSystem,
                                RunManager runManager, ConfirmDialog dialog, PlayingState playingState,
                                PerkSelectionState perkSelection, MetaUpgradesState metaUpgrades)
        {
            this.gameplayFsm = gameplayFsm;
            this.offerings = offerings;
            this.prestigeSystem = prestigeSystem;
            this.runManager = runManager;
            this.dialog = dialog;
            this.playingState = playingState;
            this.perkSelection = perkSelection;
            this.metaUpgrades = metaUpgrades;
        }

        public void Enter()
        {
            leaveTo = null;

            var run = runManager != null ? runManager.CurrentRun : null;
            if (run == null || offerings == null || !offerings.IsActive)
            {
                leaveTo = playingState;
                return;
            }

            bool canOffer = offerings.CanOffer;

            // No window, no question — and a run is never ended unasked: it is the one thing the player cannot
            // take back. So without one the gods take what they can: paid if the stock covers it, forgiven if
            // not. Loud, because it is a wiring mistake, not a situation.
            if (dialog == null || !dialog.Show(Question(offerings.Cycle, offerings.Quota, canOffer),
                                               OnOffer, OnWrath, "Offer", "Accept wrath", canOffer))
            {
                if (dialog == null)
                    Debug.LogError("OfferingEndState has no ConfirmDialog — the cycle ends without asking and " +
                                   "the run goes on. Assign Confirm Dialog on the Canvas's UIRoot.");
                leaveTo = PayOrForgive();
                return;
            }

            // Asked BEFORE freezing: a window that could not show must not leave the game frozen with nothing
            // on screen to answer.
            Time.timeScale = 0f;
            EventBus<UIModeChangedEvent>.Publish(new UIModeChangedEvent { Mode = UIMode.OfferingEnd });
        }

        public void Exit()
        {
            // Whichever way the state is left, the question goes with it — a late click must not reach a
            // state that is no longer current. Harmless after an answer: the dialog is already closed.
            if (dialog != null) dialog.Hide();

            // Unconditional, like the other freezing states.
            Time.timeScale = 1f;
        }

        public void Tick()
        {
            if (leaveTo != null) gameplayFsm.ChangeState(leaveTo);
        }

        // The window's words: which cycle ended and what the gods want, and plainly whether the stock can
        // pay it. Public and static so the wording is pinned without a scene.
        public static string Question(int cycle, IReadOnlyList<ResourceCost> quota, bool canOffer)
        {
            var sb = new StringBuilder();
            sb.Append("Cycle ").Append(cycle).Append(" is over.\nThe gods want ");
            for (int i = 0; i < quota.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Mathf.CeilToInt(quota[i].amount)).Append(' ').Append(quota[i].resourceType);
            }
            sb.Append(canOffer ? "." : ".\nYou don't have enough.");
            return sb.ToString();
        }

        // Offer: pay, then the next cycle, then the perk the offering earned — from the pool of the cycle
        // that was PAID, so its grade is read before the cycle turns. The next cycle starts BEFORE the pick,
        // so the clock is already the new one's when play resumes. A payment refused here cannot happen
        // (Offer is greyed out unless the stock covers it, and the game has been frozen since); if it ever
        // did, back to play with the cycle still over — the container asks again on its next Tick.
        private void OnOffer()
        {
            if (!offerings.TryOffer())
            {
                gameplayFsm.ChangeState(playingState);
                return;
            }

            gameplayFsm.ChangeState(PickAfterPaying());
        }

        // Wrath: the run ends — cashed in for whatever it beat, then the meta screen, whose Play starts the
        // next run. The run ends even if the cash-in is refused (no profile to bank into): the player chose to
        // end it, and handing it back would be the one answer they did not give.
        private void OnWrath()
        {
            var run = runManager != null ? runManager.CurrentRun : null;
            if (run != null && prestigeSystem != null && !prestigeSystem.CashIn(run))
                Debug.LogWarning("[Offering] Wrath accepted but the cash-in was refused — the run ends unpaid.");

            Debug.Log($"[Offering] Wrath accepted at cycle {(run != null ? run.offeringCycle : 0)} — the run ends.");

            if (metaUpgrades != null)
            {
                gameplayFsm.ChangeState(metaUpgrades);
                return;
            }

            gameplayFsm.ChangeState(playingState);
            if (runManager != null) runManager.StartNewRun();
        }

        // Without a window: pay if the stock covers it (perk pick next), otherwise let the cycle go and play on.
        private IState PayOrForgive()
        {
            if (offerings.TryOffer()) return PickAfterPaying();

            offerings.NextCycle();
            return playingState;
        }

        // After a paid offering: turn the cycle, and hand over to the pick from the paid cycle's pool.
        private IState PickAfterPaying()
        {
            var grade = offerings.RewardGrade;
            offerings.NextCycle();
            return perkSelection != null ? perkSelection.For(grade) : playingState;
        }
    }
}
