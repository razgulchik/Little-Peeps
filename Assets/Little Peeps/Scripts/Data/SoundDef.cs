using UnityEngine;
using UnityEngine.Audio;

namespace LittlePeeps
{
    // One sound the game world makes — a bounce off wood, an axe in a tree, a door — as a set of takes plus
    // the rules for playing it. SoundSystem does the playing; this only describes. Several near-identical
    // takes and a small random spread in pitch and volume are what keep a sound heard dozens of times a
    // minute from sounding like one recording on repeat; maxVoices and minInterval are what keep twenty
    // units from turning it into noise (see SoundSystem).
    [CreateAssetMenu(menuName = "LittlePeeps/Sound")]
    public class SoundDef : ScriptableObject
    {
        [Tooltip("Takes of the same sound. Each play picks one at random, never the one played last time " +
                 "(with two or more). Empty = silent.")]
        public AudioClip[] clips;

        [Range(0f, 1f)] public float volume = 0.5f;

        [Tooltip("Random volume spread per play, ± this share (0.1 = ±10%).")]
        [Range(0f, 0.5f)] public float volumeJitter = 0.1f;

        [Tooltip("Base pitch. Above 1 makes a sound smaller and lighter: a knock on wood becomes a toy's.")]
        [Range(0.5f, 2f)] public float pitch = 1f;

        [Tooltip("Random pitch spread per play, ± this share (0.08 = ±8%).")]
        [Range(0f, 0.5f)] public float pitchJitter = 0.08f;

        [Header("Crowd control")]
        [Tooltip("At most this many copies of this sound at once; a play past it is dropped. Low for sounds " +
                 "that come in floods (bounces: 3), higher for rare ones.")]
        [Min(1)] public int maxVoices = 3;

        [Tooltip("At the voice limit, cut the oldest copy instead of dropping the new play. For sounds that " +
                 "must always answer the player, however fast they tap.")]
        public bool stealOldest;

        [Tooltip("Seconds after a play before this sound may play again; a play sooner is dropped, so five " +
                 "units hitting in one instant sound once.")]
        [Min(0f)] public float minInterval = 0.05f;

        [Tooltip("Gets quieter the more crowd sounds play per second, so a big village is no louder than a " +
                 "small one (SoundSystem → Crowd). For sounds that come in floods: the bounces.")]
        public bool followsCrowd;

        [Tooltip("Mixer group this sound plays through. Empty = straight to the listener.")]
        public AudioMixerGroup output;

        public bool IsSilent => clips == null || clips.Length == 0;
    }
}
