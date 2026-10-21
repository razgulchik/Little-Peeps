using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Runs the unit's profession abilities (ProfessionDef.abilities — the hunter's bow). Knows nothing
    // about any one of them: when the profession changes it drops the old states and asks the new
    // profession's abilities for fresh ones (ProfessionAbility.CreateState), and every frame it ticks
    // them while the unit is WORKING on the field.
    //
    // "Working on the field" is settled here, once, for every ability: inside a house or a tavern the
    // unit is off the field (Unit.IsOnField) and nothing ticks; tired, it does no work of any kind — the
    // same rule CollisionTarget applies to a hit by contact — so a tired hunter does not shoot. A drunk
    // one does: drunk units work.
    //
    // On every unit prefab, next to Unit; a unit whose profession lists no abilities costs one reference
    // comparison a frame. Polled like ProfessionView: the profession changes at a rack and at the end of
    // the outing, and comparing is cheaper than an event both ways.
    [RequireComponent(typeof(Unit))]
    public class UnitAbilities : MonoBehaviour
    {
        private Unit unit;
        private ProfessionDef running;   // the profession the states below belong to
        private readonly List<IAbilityState> states = new();

        private void Awake()
        {
            unit = GetComponent<Unit>();
        }

        // Back into the pool: the next outing starts from scratch, whatever profession it begins with.
        private void OnDisable()
        {
            running = null;
            states.Clear();
        }

        private void Update()
        {
            if (unit.Profession != running) Rebuild(unit.Profession);
            if (states.Count == 0) return;
            if (!unit.IsOnField || unit.IsTired) return;

            float dt = Time.deltaTime;
            for (int i = 0; i < states.Count; i++)
                states[i].Tick(dt);
        }

        private void Rebuild(ProfessionDef profession)
        {
            running = profession;
            states.Clear();

            var abilities = profession != null ? profession.abilities : null;
            if (abilities == null) return;

            for (int i = 0; i < abilities.Length; i++)
            {
                if (abilities[i] == null) continue;
                var state = abilities[i].CreateState(unit);
                if (state != null) states.Add(state);
            }
        }
    }
}
