using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace LittlePeeps
{
    // Orchestrates the age transition as an explicit sequential coroutine chain: the island grows by the
    // zone the player picked — the zone rising out of the sea in front of them (IslandRisePlayer) — and
    // then the age banner shows. No fade: the growing island IS the transition. Runs on UNSCALED time so it
    // plays while AgeTransitionState freezes the game (timeScale 0): people and animals stand still while
    // their island grows. Signals completion via the onComplete callback the caller passes in.
    //
    // It answers taps, because the player sits through it dozens of times a run: the first plays the rest
    // of the rise faster, the second jumps to its finale, and one on the banner closes it — though not in
    // the banner's first moments (bannerTapGrace), or the last tap of a quick double tap would take it away
    // unread. Taps come from InputHandler, which reports clicks whatever the time scale; TapSystem, which
    // ignores them at timeScale 0, never sees them as boosts or a pier click.
    //
    // Pure choreography, deliberately: every step here takes a KNOWN amount of time. The perk pick used
    // to hang off the end of this chain as an empty hook, and was moved out to PerkSelectionState — a
    // step that waits on a human is a mode, not a beat in an animation, and it has to run after the rise
    // so the player can see the island the perk may go on to change. The zone pick is a mode for the
    // same reason, and runs BEFORE this chain: it is about the island as it stands, which the rise changes.
    public class AgeSequencer : MonoBehaviour
    {
        [SerializeField] private IslandSystem islandSystem;

        [Tooltip("Brings the new zone up out of the sea. Empty = the zone simply appears.")]
        [SerializeField] private IslandRisePlayer risePlayer;

        [Tooltip("Sent to look at the zone as it rises. Optional.")]
        [SerializeField] private CameraController cameraController;

        [Tooltip("Where taps come from. Empty = no speed-up, no skip, and the banner always holds its full time.")]
        [SerializeField] private InputHandler inputHandler;

        [Header("Age banner")]
        [Tooltip("The CanvasGroup holding the banner, faded in around the title and out again. It used to " +
                 "black the screen out: an Image of its own should be switched off.")]
        [FormerlySerializedAs("fadeOverlay")]
        [SerializeField] private CanvasGroup banner;
        [SerializeField] private TMP_Text titleLabel;

        [Tooltip("Seconds the banner takes to fade in, and again to fade out.")]
        [FormerlySerializedAs("fadeDuration")]
        [SerializeField] private float bannerFade = 0.5f;

        [Tooltip("Seconds the banner stays up once in, unless a tap closes it.")]
        [SerializeField] private float titleHold = 2f;

        [Tooltip("Seconds from the banner starting to appear before a tap can close it.")]
        [SerializeField] private float bannerTapGrace = 0.4f;

        private int taps;   // taps not yet acted on

        // The prefab is authored transparent, but an alpha-zero CanvasGroup still participates in UI
        // raycasts when blocksRaycasts is left on. Its centred title would then create an invisible
        // click-blocking strip across the island while placement hover (which does not query UI) stayed
        // green. Establish the idle state explicitly before any input can be processed.
        private void Awake()
        {
            SetBannerAlpha(0f);
            if (titleLabel != null) titleLabel.gameObject.SetActive(false);
        }

        // Kick off the transition into newAge using def, growing the island by `zone` (null = no growth
        // this age), then invoke onComplete when the chain finishes.
        public void StartAgeTransition(int newAge, AgeDef def, ZoneOffer zone, Action onComplete)
        {
            StartCoroutine(AgeTransitionSequence(newAge, def, zone, onComplete));
        }

        private IEnumerator AgeTransitionSequence(int newAge, AgeDef def, ZoneOffer zone, Action onComplete)
        {
            taps = 0;
            if (inputHandler != null) inputHandler.OnWorldClick += OnTap;
            try
            {
                yield return GrowIsland(zone);
                yield return ShowAgeTitle(newAge, def);
            }
            finally
            {
                if (inputHandler != null) inputHandler.OnWorldClick -= OnTap;
            }
            onComplete?.Invoke();
        }

        private void OnTap(Vector2 _) => taps++;

        // The zone is committed at once — from here on the game knows the island has grown — and then comes
        // up out of the sea. Without a rise player it simply appears.
        private IEnumerator GrowIsland(ZoneOffer zone)
        {
            if (islandSystem == null || zone == null) yield break;

            bool rise = risePlayer != null;
            islandSystem.CommitZone(zone, forRise: rise);
            LookAt(islandSystem.LastSection);
            if (!rise) yield break;

            bool done = false, spedUp = false;
            taps = 0;
            risePlayer.Play(islandSystem.LastSection, islandSystem.LastContent, () => done = true);
            while (!done)
            {
                if (taps > 0)
                {
                    taps = 0;
                    if (!spedUp)
                    {
                        risePlayer.SpeedUp();
                        spedUp = true;
                    }
                    else risePlayer.SkipToFinale();
                }
                yield return null;
            }
        }

        // The camera's reach is widened to the grown island first: its bounds otherwise catch up only at the
        // banner, and until then the clamp would drag the view off a zone that lies past the old coast.
        private void LookAt(IslandSection section)
        {
            if (cameraController == null || section == null || section.Cells.Count == 0) return;

            cameraController.RefreshBounds();
            var centre = Vector2.zero;
            foreach (var cell in section.Cells) centre += islandSystem.Grid.GridToWorld(cell);
            cameraController.FocusOn(centre / section.Cells.Count);
        }

        // Fade in, hold, fade out. A tap past the grace cuts the fade-in or the hold short; the fade-out
        // always plays.
        private IEnumerator ShowAgeTitle(int newAge, AgeDef def)
        {
            string text = (def != null && !string.IsNullOrEmpty(def.title)) ? def.title : $"Age {newAge}";
            if (titleLabel != null)
            {
                titleLabel.text = text;
                titleLabel.gameObject.SetActive(true);
            }

            EventBus<AgeStartedEvent>.Publish(new AgeStartedEvent { Age = newAge });

            taps = 0;
            float fadeIn = Mathf.Max(0f, bannerFade);
            for (float since = 0f; since < fadeIn + titleHold; since += Time.unscaledDeltaTime)
            {
                if (taps > 0)
                {
                    taps = 0;
                    if (since >= bannerTapGrace) break;
                }
                SetBannerAlpha(fadeIn > 0f ? Mathf.Clamp01(since / fadeIn) : 1f);
                yield return null;
            }

            yield return FadeBannerOut();
            if (titleLabel != null) titleLabel.gameObject.SetActive(false);
        }

        private IEnumerator FadeBannerOut()
        {
            if (banner == null) yield break;

            float start = banner.alpha;
            if (bannerFade > 0f)
                for (float t = 0f; t < bannerFade; t += Time.unscaledDeltaTime)
                {
                    SetBannerAlpha(Mathf.Lerp(start, 0f, t / bannerFade));
                    yield return null;
                }
            SetBannerAlpha(0f);
        }

        // Blocks UI raycasts while the banner is (even partly) up; stops blocking once fully transparent.
        private void SetBannerAlpha(float alpha)
        {
            if (banner == null) return;
            banner.alpha = alpha;
            banner.blocksRaycasts = alpha > 0f;
        }
    }
}
