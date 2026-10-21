namespace LittlePeeps
{
    // One unit's running copy of a ProfessionAbility: whatever the ability has to remember between
    // frames (a cooldown, a target), owned by that unit alone. Made by ProfessionAbility.CreateState,
    // ticked by UnitAbilities, and simply dropped when the profession goes — nothing to tear down.
    public interface IAbilityState
    {
        // Called every frame while the unit is out on the field and working — never while it rests,
        // sits in a building or is tired (UnitAbilities settles all three, so no ability checks them).
        void Tick(float deltaTime);
    }
}
