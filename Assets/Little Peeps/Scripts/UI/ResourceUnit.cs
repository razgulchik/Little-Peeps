using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LittlePeeps
{
    // One row of the ResourcePanel: an icon plus an abbreviated amount label (e.g. "128.9k").
    // Owns its own ReactiveValue subscription so it auto-updates and cleans itself up in OnDestroy
    // (no reliance on the panel to unsubscribe). The panel just maps a type to an icon and binds it.
    //
    // The same unit also serves as a PRICE tag (AgeCostPanel, and any cost readout added later) via the
    // fixed-amount Bind overload: a price is a number the player reads in exactly the same visual
    // language as their wallet, so it shares the prefab and the abbreviation rules rather than growing
    // a parallel widget that would drift from them.
    public class ResourceUnit : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text label;

        private ReactiveValue<float> reactive;

        // The colour the prefab authored for the label. Captured before anything can tint it, so
        // ClearTint restores the designed look instead of a hard-coded white.
        private Color baseLabelColor = Color.white;

        private void Awake()
        {
            if (label != null) baseLabelColor = label.color;
        }

        // Wire this unit to a resource: show its icon and start tracking its amount. Renders the
        // current value immediately so the unit is correct before the first change arrives.
        public void Bind(Sprite iconSprite, ReactiveValue<float> value)
        {
            Unsubscribe();
            if (icon != null) icon.sprite = iconSprite;

            reactive = value;
            if (reactive == null) return;

            reactive.OnChanged += OnValueChanged;
            OnValueChanged(reactive.Value);
        }

        // Show a FIXED amount — a price, not a running total. Drops any live subscription first: a unit
        // that was tracking a wallet and is then re-bound to a price must stop following the wallet, or
        // the next harvest would overwrite the price with the player's balance.
        public void Bind(Sprite iconSprite, float amount)
        {
            Unsubscribe();
            if (icon != null) icon.sprite = iconSprite;
            OnValueChanged(amount);
        }

        // Recolour the amount label — used by cost panels to flag a price the player can't pay yet.
        public void Tint(Color color)
        {
            if (label != null) label.color = color;
        }

        public void ClearTint()
        {
            if (label != null) label.color = baseLabelColor;
        }

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            if (reactive != null) reactive.OnChanged -= OnValueChanged;
            reactive = null;
        }

        private void OnValueChanged(float newValue)
        {
            if (label != null) label.text = ResourceFormat.Abbreviate(newValue);
        }
    }
}
