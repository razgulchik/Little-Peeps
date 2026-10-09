using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Plays the game world's sounds (SoundDef) through one pool of voices, and keeps a crowd of units from
    // turning into noise. The rules all live here, so no caller has to think about them:
    //   - a sound plays at most maxVoices copies at once and not again within minInterval; a play past
    //     either is simply dropped — with twenty units nobody misses one bounce, they hear the village
    //     knocking (a stealOldest sound cuts its oldest copy instead, so it always answers);
    //   - every play picks a take, a pitch and a volume a little off the last one (SoundDef);
    //   - one hit, one sound: a hit that means something (an axe in a tree, a door) silences the plain
    //     bounce of the same unit — see Bounce / PlayHit;
    //   - crowd sounds (SoundDef.followsCrowd: the bounces) get quieter the more of them play per second,
    //     so the village sounds as loud at thirty units as at five.
    //
    // Bounces are settled at the end of the frame, not on the spot: the unit's collision callback and the
    // target's (which may pay, or take the unit in) come in no set order, so a bounce waits in `pending`
    // until every callback of the step has run, and plays only if its unit has made no meaningful sound
    // meanwhile.
    //
    // A play can start late (Play's delay: the tap's ripple). A voice is busy from the moment it is given
    // a sound until that sound has played out, delay and pitch included — tracked here rather than read
    // from AudioSource.isPlaying, which says nothing reliable about a play that has not started yet.
    //
    // Reached through SpawnSystem.Sounds (units) or wired directly (TapSystem). Timing runs on unscaled
    // time: what counts is what the ear hears per real second, whatever the game speed.
    public class SoundSystem : MonoBehaviour
    {
        [Tooltip("Voices in the pool: the most sounds playing at once across the game world. A play that " +
                 "finds them all busy is dropped.")]
        [SerializeField, Min(1)] private int voiceCount = 16;

        [Header("Bounce surfaces")]
        [Tooltip("A bounce off a structure or an animal whose CollisionTarget sets no bounce sound of its own.")]
        [SerializeField] private SoundDef defaultBounce;

        [Tooltip("A bounce off anything that is not a CollisionTarget: the coast.")]
        [SerializeField] private SoundDef coastBounce;

        [Header("Crowd")]
        [Tooltip("Crowd sounds per second the village plays at full volume; past it each one gets quieter.")]
        [SerializeField, Min(0.1f)] private float calmRate = 4f;

        [Tooltip("How hard the volume follows the crowd. 0.5 = the village stays as loud however many play; " +
                 "0 = off; 1 = it grows quieter as it grows.")]
        [SerializeField, Range(0f, 1f)] private float crowdStrength = 0.5f;

        [Tooltip("Seconds of history the crowd rate is measured over.")]
        [SerializeField, Min(0.1f)] private float crowdWindow = 1f;

        private struct PendingBounce
        {
            public Unit unit;
            public SoundDef sound;
        }

        // Per-sound play history. Runtime state stays here, not on the asset: a ScriptableObject keeps
        // what it is given across Play Mode sessions in the editor.
        private sealed class History
        {
            public float lastStart = float.NegativeInfinity;
            public int lastClip = -1;
        }

        private AudioSource[] voices;
        private SoundDef[] voiceSound;      // what each voice was last given
        private float[] voiceStart;         // when that play starts (unscaled; later than now while delayed)
        private float[] voiceBusyUntil;     // when it has played out; the voice is free from then on
        private readonly Dictionary<SoundDef, History> history = new();
        private readonly List<PendingBounce> pending = new();
        private readonly HashSet<Unit> spoke = new();   // units whose hit made a meaningful sound this frame

        // Crowd plays over roughly the last crowdWindow seconds: +1 per play, decaying exponentially, so
        // crowdLevel / crowdWindow is the play rate per second.
        private float crowdLevel;

        private void Awake()
        {
            voices = new AudioSource[voiceCount];
            voiceSound = new SoundDef[voiceCount];
            voiceStart = new float[voiceCount];
            voiceBusyUntil = new float[voiceCount];
            for (int i = 0; i < voiceCount; i++)
            {
                var voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 0f;                  // heard the same wherever the camera is
                voice.hideFlags = HideFlags.HideInInspector;
                voices[i] = voice;
            }
        }

        // A sound on its own, starting `delay` seconds from now — the tap's click, a unit's voice in the
        // tap's ripple. Claims no hit.
        public void Play(SoundDef sound, float delay = 0f) => TryPlay(sound, Mathf.Max(0f, delay));

        // A unit bounced off `surface`. What it sounds like is the surface's (CollisionTarget.BounceSound,
        // else the defaults); whether it sounds at all is settled at the end of the frame.
        public void Bounce(Unit unit, Collider2D surface)
        {
            var sound = SurfaceSound(surface);
            if (sound == null || sound.IsSilent) return;
            pending.Add(new PendingBounce { unit = unit, sound = sound });
        }

        // The sound of what a hit DID — an axe in a tree, a unit going in at a door. It also claims the
        // hit: the unit's plain bounce from this frame stays silent, whichever callback came first. A
        // missing or empty sound claims nothing, so a tree with no chop set still knocks like wood.
        public void PlayHit(SoundDef sound, Unit unit)
        {
            if (sound == null || sound.IsSilent) return;
            if (unit != null) spoke.Add(unit);
            TryPlay(sound, 0f);
        }

        private void LateUpdate()
        {
            crowdLevel *= Mathf.Exp(-Time.unscaledDeltaTime / crowdWindow);

            for (int i = 0; i < pending.Count; i++)
                if (!spoke.Contains(pending[i].unit)) TryPlay(pending[i].sound, 0f);

            pending.Clear();
            spoke.Clear();
        }

        // Volume factor for a crowd sound while crowd sounds play at `rate` per second: 1 up to calmRate,
        // then (calmRate / rate) ^ strength. At strength 0.5 the total loudness holds still — the power of
        // overlapping plays adds up, so n plays at 1/√n of the volume carry the power of one.
        public static float CrowdFactor(float rate, float calmRate, float strength)
        {
            if (rate <= calmRate || strength <= 0f) return 1f;
            return Mathf.Pow(calmRate / rate, strength);
        }

        private SoundDef SurfaceSound(Collider2D surface)
        {
            var target = surface != null ? surface.GetComponentInParent<CollisionTarget>() : null;
            if (target == null) return coastBounce;
            return target.BounceSound != null ? target.BounceSound : defaultBounce;
        }

        // Plays `sound` `delay` seconds from now on a free voice unless one of the crowd rules drops it.
        // minInterval is measured between START times, either way round: the ripple schedules starts
        // ahead, and the next tap's may fall before the last of them.
        private void TryPlay(SoundDef sound, float delay)
        {
            if (sound == null || sound.IsSilent || voices == null) return;

            if (!history.TryGetValue(sound, out var past))
                history[sound] = past = new History();

            float now = Time.unscaledTime;
            float start = now + delay;
            if (Mathf.Abs(start - past.lastStart) < sound.minInterval) return;

            int busy = 0, free = -1, oldest = -1;
            for (int i = 0; i < voices.Length; i++)
            {
                if (voiceBusyUntil[i] > now)
                {
                    if (voiceSound[i] != sound) continue;
                    busy++;
                    if (oldest < 0 || voiceStart[i] < voiceStart[oldest]) oldest = i;
                }
                else if (free < 0) free = i;
            }

            int target;
            if (busy >= sound.maxVoices)
            {
                if (!sound.stealOldest) return;
                target = oldest;
            }
            else if (free >= 0) target = free;
            else return;

            var clip = sound.clips[PickClip(sound.clips.Length, past)];
            if (clip == null) return;

            float volume = sound.volume * (1f + Random.Range(-sound.volumeJitter, sound.volumeJitter));
            if (sound.followsCrowd)
            {
                volume *= CrowdFactor(crowdLevel / crowdWindow, calmRate, crowdStrength);
                crowdLevel += 1f;
            }
            float pitch = sound.pitch * (1f + Random.Range(-sound.pitchJitter, sound.pitchJitter));

            var voice = voices[target];
            voice.clip = clip;
            voice.outputAudioMixerGroup = sound.output;
            voice.volume = Mathf.Clamp01(volume);
            voice.pitch = pitch;
            if (delay > 0f) voice.PlayDelayed(delay);
            else voice.Play();

            voiceSound[target] = sound;
            voiceStart[target] = start;
            voiceBusyUntil[target] = start + clip.length / pitch;
            past.lastStart = start;
        }

        // A random take, never the last one when there is a choice: a repeat is redrawn uniformly among
        // the others.
        private static int PickClip(int count, History past)
        {
            int index = Random.Range(0, count);
            if (count > 1 && index == past.lastClip)
                index = (index + 1 + Random.Range(0, count - 1)) % count;
            past.lastClip = index;
            return index;
        }
    }
}
