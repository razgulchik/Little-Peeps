using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LittlePeeps
{
    // One row of the age timeline: the age number and the bonus that age grants. The SAME prefab is
    // both the highlighted "being bought now" card and a dimmed bought one — two prefabs would be two
    // places to restyle and two ways for the column to stop looking like one column.
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

        [Header("Bought ages — history")]
        [SerializeField] private Sprite historyBackground;
        [SerializeField] private Color historyTint = new Color(0.6f, 0.65f, 0.75f, 1f);
        [Range(0f, 1f)] [SerializeField] private float historyAlpha = 0.65f;

        private CanvasGroup canvasGroup;

        // Instantiate runs this before the panel calls Bind, so the group is always resolved in time.
        private void Awake() => canvasGroup = GetComponent<CanvasGroup>();

        public void Bind(int ageNumber, string bonus, bool isNext)
        {
            if (ageLabel != null) ageLabel.text = $"AGE {RomanNumeral.From(ageNumber)}";
            if (bonusLabel != null) bonusLabel.text = bonus ?? string.Empty;

            if (background != null)
            {
                Sprite sprite = isNext ? nextBackground : historyBackground;
                if (sprite != null) background.sprite = sprite;
                background.color = isNext ? nextTint : historyTint;
            }

            if (canvasGroup != null) canvasGroup.alpha = isNext ? nextAlpha : historyAlpha;
        }
    }
}
