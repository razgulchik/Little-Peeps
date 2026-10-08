using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LittlePeeps
{
    // One meta perk on the meta screen: its name, its one-level description under it, and "[-] 1/3 [+]".
    // Draws what it is told and forwards the clicks; whether a click may spend or refund a point is
    // PrestigeSystem's call, made by MetaUpgradesUI, which then redraws every row — one point moving
    // changes what every other [+] may do.
    public class MetaUpgradeRowUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private Button minusButton;
        [SerializeField] private Button plusButton;

        [Tooltip("{0} = points put in, {1} = the maximum.")]
        [SerializeField] private string levelFormat = "{0}/{1}";

        private Action<GlobalUpgradeDef> onRaise;
        private Action<GlobalUpgradeDef> onLower;

        public GlobalUpgradeDef Def { get; private set; }

        public void Init(GlobalUpgradeDef def, Action<GlobalUpgradeDef> onRaise, Action<GlobalUpgradeDef> onLower)
        {
            Def = def;
            this.onRaise = onRaise;
            this.onLower = onLower;

            if (titleText != null) titleText.text = def != null ? def.title : string.Empty;
            if (descriptionText != null) descriptionText.text = def != null ? def.DescriptionText : string.Empty;
        }

        public void Refresh(int level, int maxLevel, bool canRaise, bool canLower)
        {
            if (levelText != null)
                levelText.text = levelFormat.Replace("{0}", level.ToString()).Replace("{1}", maxLevel.ToString());

            if (plusButton != null) plusButton.interactable = canRaise;
            if (minusButton != null) minusButton.interactable = canLower;
        }

        // Listeners in OnEnable rather than Init: a row is instantiated active, so OnEnable has already run
        // by the time Init hands it its callbacks — and until then a click simply reaches nobody.
        private void OnEnable()
        {
            if (plusButton != null) plusButton.onClick.AddListener(OnPlus);
            if (minusButton != null) minusButton.onClick.AddListener(OnMinus);
        }

        private void OnDisable()
        {
            if (plusButton != null) plusButton.onClick.RemoveListener(OnPlus);
            if (minusButton != null) minusButton.onClick.RemoveListener(OnMinus);
        }

        private void OnPlus() => onRaise?.Invoke(Def);
        private void OnMinus() => onLower?.Invoke(Def);
    }
}
