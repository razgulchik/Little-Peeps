using System.Collections.Generic;
using UnityEngine;

// Rolls and applies perks; weighted random from the designer-authored catalogue
public class PerkSystem : MonoBehaviour
{
    [SerializeField] private List<PerkDef> catalogue;

    // Return 3 unique perks weighted by PerkDef.weight, excluding already-chosen ones
    public List<PerkDef> Roll3Perks(int currentAge, RunContext context)
    {
        // TODO: filter catalogue excluding context.perksChosen; run weighted random until 3 unique perks picked
        return new List<PerkDef>();
    }

    // Apply the perk effect and record it so it can't be rolled again this run
    public void ApplyPerk(PerkDef perk, RunContext context)
    {
        // TODO: perk.ApplyPerk(context); context.perksChosen.Add(perk); EventBus<PerkSelectedEvent>.Publish(new PerkSelectedEvent { Perk = perk })
    }
}
