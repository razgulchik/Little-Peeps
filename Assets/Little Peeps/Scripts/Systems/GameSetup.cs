using UnityEngine;

namespace LittlePeeps
{
    // The game's rule switches, gathered in one place in the scene so a playtest never has to hunt for them
    // across prefabs and systems. Place on a dedicated "GameSetup" GameObject; GameBootstrap hands it to
    // whoever asks, before the first run.
    //
    // Only switches that say HOW THE GAME PLAYS belong here. Numbers that shape one thing (a unit's autopilot
    // radius, the tap's radius) stay on that thing. Not TestMode: this is the real game, shared by the team
    // and in the build.
    //
    // Everyone reads these at the moment they matter and caches nothing, so a checkbox flipped in play mode
    // takes effect at once.
    public class GameSetup : MonoBehaviour
    {
        [Header("Out of stamina")]
        [Tooltip("On: a unit goes on autopilot the moment it runs out of stamina and stays on it until a house " +
                 "takes it in. Off: only the tavern puts units on autopilot (its own checkbox). How far a unit " +
                 "looks and how sharply it turns is on the unit's prefab.")]
        [SerializeField] private bool autopilotWhenTired = true;

        [Tooltip("On: the moment it runs out of stamina a unit vanishes in a puff and is in the house it was " +
                 "born in, however far it is. If that house is full or sold: in the nearest one with a free " +
                 "slot. If every slot is taken it stays on the field as a tired unit. Off: it has to get home " +
                 "by itself.")]
        [SerializeField] private bool despawnWhenTired = false;

        [Header("Tap")]
        [Tooltip("On: a tap on a unit that is out of stamina sends it straight to the nearest house with a " +
                 "free slot; if every slot is taken, it is boosted as usual. Off: a tap boosts every unit, " +
                 "tired or not. TapSystem's Refresh Stamina wins over this: a tap that refills stamina sends " +
                 "nobody home.")]
        [SerializeField] private bool tapSendsTiredHome = true;

        public bool AutopilotWhenTired => autopilotWhenTired;
        public bool DespawnWhenTired => despawnWhenTired;
        public bool TapSendsTiredHome => tapSendsTiredHome;
    }
}
