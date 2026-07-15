using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One row of the ResourcePanel: an icon plus an abbreviated amount label (e.g. "128.9k").
// Owns its own ReactiveValue subscription so it auto-updates and cleans itself up in OnDestroy
// (no reliance on the panel to unsubscribe). The panel just maps a type to an icon and binds it.
public class ResourceUnit : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;

    private ReactiveValue<float> reactive;

    // Wire this unit to a resource: show its icon and start tracking its amount. Renders the
    // current value immediately so the unit is correct before the first change arrives.
    public void Bind(Sprite iconSprite, ReactiveValue<float> value)
    {
        if (icon != null) icon.sprite = iconSprite;

        reactive = value;
        if (reactive == null) return;

        reactive.OnChanged += OnValueChanged;
        OnValueChanged(reactive.Value);
    }

    private void OnDestroy()
    {
        if (reactive != null) reactive.OnChanged -= OnValueChanged;
    }

    private void OnValueChanged(float newValue)
    {
        if (label != null) label.text = Format(newValue);
    }

    private static readonly string[] Suffixes = { "", "k", "M", "B", "T" };

    // Up to 4 digits + a suffix letter (1_256_000 → "1256k", 10_000 → "10k", 999 → "999"). Steps to
    // the next suffix only when the floored value would need a 5th digit, so the number stays ≤ 9999.
    // Floors (never rounds up) so the label only shows fully-earned units — 1.5 reads as "1". The
    // stored amount stays a precise float; only this display is truncated.
    private static string Format(float value)
    {
        int tier = 0;
        float v = value;
        while (Mathf.Abs(v) >= 10000f && tier < Suffixes.Length - 1)
        {
            v /= 1000f;
            tier++;
        }

        return Mathf.FloorToInt(v) + Suffixes[tier];
    }
}
