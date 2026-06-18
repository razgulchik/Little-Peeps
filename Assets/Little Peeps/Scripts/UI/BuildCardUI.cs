using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One build-palette card: shows a structure's icon + cost, reports clicks to the BuildPanelUI,
// toggles a "selected" highlight, and dims when unaffordable. Unaffordable cards stay clickable
// (you can pick them) — attempting to build then plays a denied cue (stub for now).
[RequireComponent(typeof(Button))]
public class BuildCardUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private GameObject selectedHighlight;   // graphic shown when this card is selected
    [SerializeField] private CanvasGroup canvasGroup;        // dims the card when unaffordable

    public StructureDef Def { get; private set; }

    private Action<BuildCardUI> onClick;

    private void Reset()
    {
        button = GetComponent<Button>();
    }

    // Build the card for a structure. onClick is invoked (with this card) when the button is pressed.
    public void Init(StructureDef def, Action<BuildCardUI> onClick)
    {
        Def = def;
        this.onClick = onClick;

        if (iconImage != null) iconImage.sprite = def.icon;
        if (costText != null) costText.text = FormatCost(def);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => this.onClick?.Invoke(this));

        SetSelected(false);
        SetAffordable(true);
    }

    public void SetSelected(bool selected)
    {
        if (selectedHighlight != null) selectedHighlight.SetActive(selected);
    }

    // Dim unaffordable cards. Card stays interactable so the player can still pick it.
    public void SetAffordable(bool affordable)
    {
        if (canvasGroup != null) canvasGroup.alpha = affordable ? 1f : 0.5f;
    }

    // Feedback when the player tries to build this but can't afford it.
    public void PlayDeniedCue()
    {
        // TODO: DOTween blink + shake. Stub for now.
    }

    private static string FormatCost(StructureDef def)
    {
        if (def.cost == null || def.cost.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        for (int i = 0; i < def.cost.Count; i++)
        {
            if (i > 0) sb.Append("  ");
            sb.Append(def.cost[i].amount).Append(' ').Append(def.cost[i].resourceType);
        }
        return sb.ToString();
    }
}
