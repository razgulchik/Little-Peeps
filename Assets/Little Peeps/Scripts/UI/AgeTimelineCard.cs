using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LittlePeeps
{
    // One row of the age timeline: the age number and the bonus that age grants. The SAME prefab is
    // both the highlighted "being bought now" card and a dimmed age still ahead — two prefabs would
    // be two places to restyle and two ways for the column to stop looking like one column.
    //
    // Every state field below is optional. A sprite left empty keeps whatever the prefab authored, so
    // the two states can be expressed with two sprites, with a tint, with alpha, or with any mix of
    // them — without the script having an opinion about which the art uses.
    [RequireComponent(typeof(CanvasGroup))]
    public class AgeTimelineCard : MonoBehaviour
    {
        [SerializeField] private TMP_Text ageLabel;
        [SerializeField] private TMP_Text bonusLabel;
        [SerializeField] private Image background;

        [Header("Next age — the one being bought now")]
        [SerializeField] private Sprite nextBackground;
        [SerializeField] private Color nextTint = Color.white;
        [Range(0f, 1f)] [SerializeField] private float nextAlpha = 1f;

        [Header("Ages still ahead")]
        [SerializeField] private Sprite upcomingBackground;
        [SerializeField] private Color upcomingTint = new Color(0.6f, 0.65f, 0.75f, 1f);
        [Range(0f, 1f)] [SerializeField] private float upcomingAlpha = 0.65f;

        private CanvasGroup canvasGroup;

        // Instantiate runs this before the panel calls Bind, so the group is always resolved in time.
        private void Awake() => canvasGroup = GetComponent<CanvasGroup>();

        public void Bind(int ageNumber, string bonus, bool isNext)
        {
            if (ageLabel != null) ageLabel.text = $"AGE {RomanNumeral.From(ageNumber)}";
            if (bonusLabel != null) bonusLabel.text = bonus ?? string.Empty;

            if (background != null)
            {
                Sprite sprite = isNext ? nextBackground : upcomingBackground;
                if (sprite != null) background.sprite = sprite;
                background.color = isNext ? nextTint : upcomingTint;
            }

            if (canvasGroup != null) canvasGroup.alpha = isNext ? nextAlpha : upcomingAlpha;
        }
    }
}
