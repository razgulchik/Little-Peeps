using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LittlePeeps
{
    // The meta screen's content: the free prestige points, one row per meta perk, and Play. Points move
    // here, through PrestigeSystem, and are free to move back while the screen is open; the screen itself
    // starts nothing — Play hands control to MetaUpgradesState, which builds the next run.
    //
    // Rows are instantiated per Show rather than living in the prefab as fixed slots, like the perk cards:
    // the list is whatever the catalogue holds, in its order.
    //
    // Visibility is NOT this panel's job — UIVisibility shows it on UIMode.MetaUpgrades — but the object
    // must stay ACTIVE, for PerkSelectionUI's reason: a panel authored inactive would run its Awake inside
    // the very reveal that shows it.
    [RequireComponent(typeof(CanvasGroup))]
    public class MetaUpgradesUI : MonoBehaviour
    {
        [SerializeField] private MetaUpgradeRowUI rowPrefab;

        [Tooltip("Parent for the rows — give it a Vertical Layout Group (the Content of a Scroll View, once " +
                 "the list outgrows the screen).")]
        [SerializeField] private Transform rowContainer;

        [SerializeField] private TMP_Text pointsText;

        [Tooltip("{0} becomes the free points.")]
        [SerializeField] private string pointsFormat = "Prestige points: {0}";

        [SerializeField] private Button playButton;

        private readonly List<MetaUpgradeRowUI> rows = new();
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

        // Fill the screen and hand it Play. Called by MetaUpgradesState after it has announced the mode, so
        // the panel is already up: this owns the CONTENT of the screen, not the screen.
        public void Show(PrestigeSystem prestigeSystem, Action onPlay)
        {
            prestige = prestigeSystem;
            this.onPlay = onPlay;
            ClearRows();

            if (rowPrefab == null || rowContainer == null)
                Debug.LogError($"MetaUpgradesUI on '{name}' is missing its row prefab or container — the screen " +
                               "opens with nothing to spend on. Play still works.", this);
            else if (prestige != null)
            {
                var upgrades = prestige.Upgrades;
                for (int i = 0; i < upgrades.Count; i++)
                {
                    if (upgrades[i] == null) continue;

                    var row = Instantiate(rowPrefab, rowContainer);
                    row.Init(upgrades[i], Raise, Lower);
                    rows.Add(row);
                }
            }

            if (playButton != null) playButton.interactable = true;
            Refresh();
        }

        public void Hide()
        {
            ClearRows();
            prestige = null;
            onPlay = null;
        }

        private void Raise(GlobalUpgradeDef def)
        {
            if (prestige != null && prestige.TryRaise(def)) Refresh();
        }

        private void Lower(GlobalUpgradeDef def)
        {
            if (prestige != null && prestige.TryLower(def)) Refresh();
        }

        // Everything, every time: one point moving changes the free total and with it every row's [+].
        private void Refresh()
        {
            int free = prestige != null ? prestige.FreePoints : 0;
            if (pointsText != null) pointsText.text = pointsFormat.Replace("{0}", free.ToString());

            for (int i = 0; i < rows.Count; i++)
            {
                var def = rows[i].Def;
                rows[i].Refresh(prestige != null ? prestige.LevelOf(def) : 0, def.maxLevel,
                                prestige != null && prestige.CanRaise(def),
                                prestige != null && prestige.CanLower(def));
            }
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

        private void ClearRows()
        {
            for (int i = 0; i < rows.Count; i++)
                if (rows[i] != null) Destroy(rows[i].gameObject);

            rows.Clear();
        }
    }
}
