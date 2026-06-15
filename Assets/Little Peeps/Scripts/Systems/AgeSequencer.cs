using System.Collections;
using UnityEngine;

// Orchestrates the age transition as an explicit sequential coroutine chain
public class AgeSequencer : MonoBehaviour
{
    [SerializeField] private IslandSystem islandSystem;
    [SerializeField] private PerkSystem perkSystem;

    // Kick off the 6-step transition sequence for the given age
    public void StartAgeTransition(int newAge, RunContext context)
    {
        StartCoroutine(AgeTransitionSequence(newAge, context));
    }

    private IEnumerator AgeTransitionSequence(int newAge, RunContext context)
    {
        yield return StartCoroutine(FadeOut());
        yield return StartCoroutine(ExpandIsland(newAge));
        yield return StartCoroutine(SpawnNewTerrain(newAge));
        yield return StartCoroutine(ShowAgeTitle(newAge));
        yield return StartCoroutine(WaitForPerkSelection(newAge, context));
        yield return StartCoroutine(FadeIn());
    }

    private IEnumerator FadeOut()
    {
        // TODO: tween a full-screen overlay alpha from 0 to 1 over 0.5s using DOTween or manual Lerp
        yield break;
    }

    private IEnumerator ExpandIsland(int age)
    {
        // TODO: islandSystem.Generator.Expand(age); yield return null to let tilemap refresh
        yield break;
    }

    private IEnumerator SpawnNewTerrain(int age)
    {
        // TODO: instantiate visual prefabs for newly added cells based on their TerrainType
        yield break;
    }

    private IEnumerator ShowAgeTitle(int age)
    {
        // TODO: activate age title UI, set text "Age N"; EventBus<AgeStartedEvent>.Publish(new AgeStartedEvent { Age = age }); yield WaitForSeconds(2); deactivate
        yield break;
    }

    private IEnumerator WaitForPerkSelection(int age, RunContext context)
    {
        // TODO: perkSystem.Roll3Perks(age, context) → show PerkSelectionUI; yield until PerkSelectedEvent fires via a local bool flag
        yield break;
    }

    private IEnumerator FadeIn()
    {
        // TODO: tween overlay alpha from 1 to 0 over 0.5s
        yield break;
    }
}
