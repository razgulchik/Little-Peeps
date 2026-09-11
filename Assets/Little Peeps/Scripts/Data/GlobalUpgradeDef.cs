using UnityEngine;

namespace LittlePeeps
{
    [CreateAssetMenu(menuName = "LittlePeeps/GlobalUpgradeDef")]
    public class GlobalUpgradeDef : ScriptableObject
    {
        [Tooltip("Stable key for saves and for the level MetaContext stores. Must be unique across the " +
                 "catalogue and must not be empty.")]
        public string id;
        [TextArea] public string description;
    }
}
