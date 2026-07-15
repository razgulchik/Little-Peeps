using System;
using System.Collections;
using TMPro;
using UnityEngine;

// Orchestrates the age transition as an explicit sequential coroutine chain: fade to black, grow the
// island, show the age banner, (perk pick — hooked, later), fade back. Runs on UNSCALED time so it
// still plays while AgeTransitionState freezes the game (timeScale 0). Signals completion via the
// onComplete callback the caller passes in.
public class AgeSequencer : MonoBehaviour
{
    [SerializeField] private IslandSystem islandSystem;
    [SerializeField] private PerkSystem perkSystem;

    [Header("Transition visuals")]
    [Tooltip("Full-screen overlay faded in/out. Its Image should have Raycast Target on so it also " +
             "swallows UI clicks while the transition plays.")]
    [SerializeField] private CanvasGroup fadeOverlay;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float titleHold = 2f;

    // Kick off the transition into newAge using def, then invoke onComplete when the chain finishes.
    public void StartAgeTransition(int newAge, AgeDef def, RunContext context, Action onComplete)
    {
        StartCoroutine(AgeTransitionSequence(newAge, def, context, onComplete));
    }

    private IEnumerator AgeTransitionSequence(int newAge, AgeDef def, RunContext context, Action onComplete)
    {
        yield return FadeTo(1f);
        ExpandIsland(def);
        yield return null;                         // let the tilemap refresh settle a frame
        yield return ShowAgeTitle(newAge, def);
        yield return WaitForPerkSelection(newAge, context);
        yield return FadeTo(0f);
        onComplete?.Invoke();
    }

    private void ExpandIsland(AgeDef def)
    {
        if (islandSystem != null) islandSystem.Expand(def);
    }

    private IEnumerator ShowAgeTitle(int newAge, AgeDef def)
    {
        string text = (def != null && !string.IsNullOrEmpty(def.title)) ? def.title : $"Age {newAge}";
        if (titleLabel != null)
        {
            titleLabel.text = text;
            titleLabel.gameObject.SetActive(true);
        }

        EventBus<AgeStartedEvent>.Publish(new AgeStartedEvent { Age = newAge });

        yield return new WaitForSecondsRealtime(titleHold);

        if (titleLabel != null) titleLabel.gameObject.SetActive(false);
    }

    // Hook for the perk-selection step (roll 3, show PerkSelectionUI, wait for a pick). Intentionally
    // a no-op for now — perks are a later milestone; the transition just proceeds.
    private IEnumerator WaitForPerkSelection(int newAge, RunContext context)
    {
        yield break;
    }

    // Lerp the overlay alpha to target over fadeDuration on unscaled time. Blocks UI raycasts while the
    // screen is (even partly) covered; stops blocking once fully transparent. Null overlay → instant.
    private IEnumerator FadeTo(float target)
    {
        if (fadeOverlay == null) yield break;

        fadeOverlay.blocksRaycasts = true;
        float start = fadeOverlay.alpha;

        if (fadeDuration > 0f)
        {
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                fadeOverlay.alpha = Mathf.Lerp(start, target, t / fadeDuration);
                yield return null;
            }
        }

        fadeOverlay.alpha = target;
        fadeOverlay.blocksRaycasts = target > 0f;
    }
}
