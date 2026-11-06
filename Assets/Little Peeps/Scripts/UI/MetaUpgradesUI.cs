using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LittlePeeps
{
    // The Ancestral Wisdom screen's content: the free prestige points, one icon per meta perk, and the button
    // to the next island. Points move here, through PrestigeSystem, and are free to move back while the screen
    // is open; the screen itself starts nothing — the button hands control to MetaUpgradesState, which builds
    // the next run.
    //
    // Cells are instantiated per Show rather than living in the prefab as fixed slots, like the perk cards:
    // the list is whatever the catalogue holds, in its order.
    //
    // Visibility is NOT this panel's job — UIVisibility shows it on UIMode.MetaUpgrades — but the object
    // must stay ACTIVE, for PerkSelectionUI's reason: a panel authored inactive would run its Awake inside
    // the very reveal that shows it.
    [RequireComponent(typeof(CanvasGroup))]
    public class MetaUpgradesUI : MonoBehaviour
    {
        [SerializeField] private MetaUpgradeSlotUI slotPrefab;

        [Tooltip("Parent of the cells. Its Grid Layout Group sets the cell, the spacing and the columns.")]
        [SerializeField] private Transform slotContainer;

        [Tooltip("The number beside the star: the points not spent yet.")]
        [SerializeField] private TMP_Text pointsText;

        [SerializeField] private MetaUpgradeTipUI tip;

        [SerializeField] private Button playButton;

        private readonly List<MetaUpgradeSlotUI> slots = new();
        private PrestigeSystem prestige;
        private Action onPlay;

        private void OnEnable()
        {
            if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);
        }

        private void OnDisable()
        {
            if (playButton != null) playButton.onClick.RemoveListener(OnPlayClicked);
        }

        // Fill the screen and hand it the way out. Called by MetaUpgradesState after it has announced the
        // mode, so the panel is already up: this owns the CONTENT of the screen, not the screen.
        public void Show(PrestigeSystem prestigeSystem, Action onPlay)
        {
            prestige = prestigeSystem;
            this.onPlay = onPlay;
            ClearSlots();

            if (slotPrefab == null || slotContainer == null)
                Debug.LogError($"MetaUpgradesUI on '{name}' is missing its slot prefab or container — the screen " +
                               "opens with nothing to spend on. Its button still works.", this);
            else if (prestige != null)
            {
                var upgrades = prestige.Upgrades;
                for (int i = 0; i < upgrades.Count; i++)
                {
                    if (upgrades[i] == null) continue;

                    var slot = Instantiate(slotPrefab, slotContainer);
                    slot.Init(upgrades[i], Take, Return, tip);
                    slots.Add(slot);
                }
            }

            if (playButton != null) playButton.interactable = true;
            Refresh();
        }

        public void Hide()
        {
            ClearSlots();
            if (tip != null) tip.Hide();
            prestige = null;
            onPlay = null;
        }

        private void Take(GlobalUpgradeDef def)
        {
            if (prestige != null && prestige.TryTake(def)) Refresh();
        }

        private void Return(GlobalUpgradeDef def)
        {
            if (prestige != null && prestige.TryReturn(def)) Refresh();
        }

        private void Refresh()
        {
            if (pointsText != null) pointsText.text = (prestige != null ? prestige.FreePoints : 0).ToString();

            for (int i = 0; i < slots.Count; i++)
                slots[i].Refresh(prestige != null && prestige.IsTaken(slots[i].Def));
        }

        // One-shot, like ConfirmDialog's answers: the callback is dropped and the button locked BEFORE the
        // call, so a double click cannot start two runs.
        private void OnPlayClicked()
        {
            var play = onPlay;
            onPlay = null;
            if (playButton != null) playButton.interactable = false;

            play?.Invoke();
        }

        private void ClearSlots()
        {
            for (int i = 0; i < slots.Count; i++)
                if (slots[i] != null) Destroy(slots[i].gameObject);

            slots.Clear();
        }
    }
}
