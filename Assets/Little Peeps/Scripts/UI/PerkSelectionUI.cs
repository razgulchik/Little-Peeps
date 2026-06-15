using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Displays 3 perk cards for the player to choose; each card fires PerkSelectedEvent on confirm
public class PerkSelectionUI : MonoBehaviour
{
    [SerializeField] private GameObject[] cardSlots;   // 3 slots; each needs a TMP_Text + Button child
    [SerializeField] private TMP_Text[] cardLabels;
    [SerializeField] private Button[] cardButtons;

    private List<PerkDef> currentPerks;
    private PerkSystem perkSystem;
    private RunContext runContext;

    public void Initialize(PerkSystem perkSystem, RunContext runContext)
    {
        this.perkSystem = perkSystem;
        this.runContext = runContext;
    }

    // Show the panel with 3 perk options
    public void Show(List<PerkDef> perks)
    {
        // TODO: currentPerks = perks; gameObject.SetActive(true); populate cardLabels with perk descriptions; wire cardButtons[i].onClick to OnCardSelected(i)
    }

    public void Hide()
    {
        // TODO: gameObject.SetActive(false); clear all cardButtons.onClick listeners
    }

    private void OnCardSelected(int index)
    {
        // TODO: perkSystem.ApplyPerk(currentPerks[index], runContext); Hide()
    }
}
