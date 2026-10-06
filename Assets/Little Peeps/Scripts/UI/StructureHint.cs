namespace LittlePeeps
{
    // What a build card's hint says about its building: the name, the description, the FULL price of the
    // next one (every resource, red where the wallet falls short), the count against the limit and, on a
    // shut card, what would open it. The card only holds the numbers; the wording lives here.
    public static class StructureHint
    {
        // `standing` / `limit` / `lockState` are what the card shows (BuildCardUI.ShowCount / SetLocked);
        // a null wallet counts every price as payable.
        public static void Fill(HintContent into, StructureDef def, int standing, int limit,
                                StructureLock lockState, ResourceSystem wallet)
        {
            into.Clear();
            into.title = def.ShownName;
            into.body = def.description;

            if (def.cost != null)
                for (int i = 0; i < def.cost.Count; i++)
                {
                    ResourceCost entry = def.cost[i];
                    if (entry == null) continue;

                    float amount = def.CostAt(i, standing);
                    if (amount <= 0f) continue;   // a resource it doesn't take is not a price line
                    into.costs.Add(new HintCost
                    {
                        resourceType = entry.resourceType,
                        amount = amount,
                        affordable = wallet == null || wallet.GetResource(entry.resourceType) >= amount,
                    });
                }

            string count = CountLine(def.HasLimit, standing, limit);
            string shut = LockLine(lockState, def.requiredAge);
            into.footer = shut == null ? count : count + "\n" + shut;
        }

        public static string CountLine(bool hasLimit, int standing, int limit) =>
            hasLimit ? $"Count: {standing} of {limit}" : "No limits";

        // Null while the card is open — and at the limit too: the count line already reads "2 of 2".
        public static string LockLine(StructureLock state, int requiredAge) => state switch
        {
            StructureLock.Age => $"Opens on the Age {RomanNumeral.From(requiredAge)}",
            StructureLock.Perk => "Opens with a perk",
            _ => null,
        };
    }
}
