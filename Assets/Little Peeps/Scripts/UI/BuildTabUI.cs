using System;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LittlePeeps
{
    // One build-panel tab button, spawned by BuildPanelUI per non-empty BuildPaletteDef tab. Shows
    // whatever the prefab has slots for — a label, an icon, or both — and reports clicks to the panel,
    // which owns the selection.
    //
    // The root is the fixed layout/click slot: the container's Layout Group owns its position, so the
    // art lives on a `visual` child that slides right while the tab is hovered or open. Hover is read on
    // the fixed root, which keeps the sliding art from moving out from under the pointer and flickering.
    [RequireComponent(typeof(Button))]
    public class BuildTabUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;                 // optional: the tab's name
        [SerializeField] private Image iconImage;                // optional: the tab's icon
        [SerializeField] private GameObject selectedIndicator;   // optional: shown while this tab is open

        [Header("Slide")]
        [SerializeField] private RectTransform visual;           // child holding the art; the root stays put
        [SerializeField] private float slideOffset = 4f;         // px to the right while hovered or open
        [SerializeField, Min(0f)] private float slideDuration = 0.1f;
        [SerializeField] private Ease slideEase = Ease.OutQuad;

        private bool hovered;
        private bool selected;
        private float restX;   // the visual's authored x — the slide is an offset from it
        private MotionHandle slideMotion;

        private float TargetX => restX + (hovered || selected ? slideOffset : 0f);

        private void Reset()
        {
            button = GetComponent<Button>();
        }

        private void Awake()
        {
            if (visual != null) restX = visual.anchoredPosition.x;
        }

        // A slide cut short (the panel hidden mid-way) lands where it was going, not half-way.
        private void OnDisable()
        {
            CancelSlide();
            SetX(TargetX);
        }

        public void Init(BuildTab tab, int index, Action<int> onClick)
        {
            if (label != null) label.text = tab.name;
            if (iconImage != null)
            {
                iconImage.sprite = tab.icon;
                iconImage.enabled = tab.icon != null;   // no sprite would draw a white square
            }

            if (button == null) button = GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke(index));

            SetSelected(false);
        }

        public void SetSelected(bool value)
        {
            selected = value;
            if (selectedIndicator != null) selectedIndicator.SetActive(value);
            Slide();
        }

        // Leaving build mode with the pointer resting on a tab: the hidden panel may never be told the
        // pointer left, so the panel drops the hover itself and the tab snaps to where it belongs.
        public void ResetHover()
        {
            hovered = false;
            CancelSlide();
            SetX(TargetX);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            Slide();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            Slide();
        }

        // Build mode pauses the game, so the slide runs on unscaled time like the cards' motion.
        private void Slide()
        {
            if (visual == null) return;
            CancelSlide();

            float from = visual.anchoredPosition.x;
            float to = TargetX;
            if (slideDuration <= 0f || Mathf.Approximately(from, to))
            {
                SetX(to);
                return;
            }

            slideMotion = LMotion.Create(from, to, slideDuration)
                .WithEase(slideEase)
                .WithScheduler(MotionScheduler.InitializationIgnoreTimeScale)
                .Bind(SetX);
        }

        private void SetX(float x)
        {
            if (visual == null) return;
            Vector2 position = visual.anchoredPosition;
            position.x = Mathf.Round(x);   // whole UI pixels, as the cards snap theirs
            visual.anchoredPosition = position;
        }

        private void CancelSlide()
        {
            if (slideMotion.IsActive()) slideMotion.Cancel();
            slideMotion = default;
        }
    }
}
