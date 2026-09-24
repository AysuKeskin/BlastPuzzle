using UnityEngine;

namespace BlastPuzzle.Presentation
{
    // Sound and vibration for things that have ALREADY happened.
    //
    // It decides nothing: not whether a group was valid, not which crates died, not whether
    // the level is won. GameplayController tells it "a blast of 7 removed 2 crates" and this
    // class decides only how that sounds and feels. Every method below takes facts, never
    // board state to inspect.
    //
    // A plain scene component, not a singleton. GameplayController holds a serialized
    // reference like it holds BoardView, so there is no global to reach through, nothing to
    // initialise in a particular order, and a test scene can simply not have one.
    public sealed class GameFeedback : MonoBehaviour
    {
        [Header("Sources")]
        [Tooltip("Plays everything except the blast. Pitch stays at 1.")]
        [SerializeField]
        private AudioSource sfxSource;

        [Tooltip("Plays only the blast, so its pitch variation cannot leak into other sounds.")]
        [SerializeField]
        private AudioSource blastSource;

        [Header("Clips")]
        [SerializeField] private AudioClip blockBlastClip;
        [SerializeField] private AudioClip crateBreakClip;
        [SerializeField] private AudioClip rocketClip;
        [SerializeField] private AudioClip bombClip;
        [SerializeField] private AudioClip winClip;
        [SerializeField] private AudioClip loseClip;

        [Header("Volume")]
        [SerializeField] [Range(0f, 1f)] private float masterSfxVolume = 0.8f;
        [SerializeField] [Range(0f, 1f)] private float blastVolume = 0.7f;
        [SerializeField] [Range(0f, 1f)] private float powerUpVolume = 0.9f;
        [SerializeField] [Range(0f, 1f)] private float terminalVolume = 1f;

        [Header("Enable")]
        [SerializeField] private bool audioEnabled = true;
        [SerializeField] private bool hapticsEnabled = true;

        // Blast pitch range. A bigger group sounds slightly brighter, which makes a large
        // clear feel more substantial without any extra clips.
        private const int SmallGroup = 2;
        private const int LargeGroup = 10;
        private const float SmallGroupPitch = 0.95f;
        private const float LargeGroupPitch = 1.08f;

        // --- test seam -------------------------------------------------------------------
        //
        // Counters, not a mocking framework. Tests assert that the feedback layer was ASKED
        // for the right thing the right number of times; whether a speaker actually moved is
        // not something an Editor test can or should check.
        public int BlastCount { get; private set; }
        public int CrateBreakCount { get; private set; }
        public int RocketActivationCount { get; private set; }
        public int BombActivationCount { get; private set; }
        public int WinCount { get; private set; }
        public int LoseCount { get; private set; }

        // Counts DECISIONS to vibrate, not confirmed vibrations. The platform call below is
        // additionally gated by hapticsEnabled and by the platform itself.
        public int HapticRequestCount { get; private set; }

        public void ResetCounters()
        {
            BlastCount = 0;
            CrateBreakCount = 0;
            RocketActivationCount = 0;
            BombActivationCount = 0;
            WinCount = 0;
            LoseCount = 0;
            HapticRequestCount = 0;
        }

        // ONE sound per player action, whatever the group size. Twelve removed blocks playing
        // twelve copies of the same clip would phase into a single loud smear rather than
        // sounding twelve times as satisfying.
        public void PlayBlast(int groupSize)
        {
            BlastCount++;

            if (!audioEnabled || blockBlastClip == null || blastSource == null)
            {
                return;
            }

            blastSource.pitch = PitchForGroup(groupSize);
            blastSource.PlayOneShot(blockBlastClip, masterSfxVolume * blastVolume);
        }

        // One sound for the whole batch: three crates in one blast is one break, not three
        // overlapping cracks.
        public void PlayCrateBreak(int crateCount)
        {
            if (crateCount <= 0)
            {
                return;
            }

            CrateBreakCount++;
            Play(crateBreakClip, blastVolume);
            RequestHaptic();
        }

        // Only when the PLAYER activates one. A rocket removed by another power-up's
        // footprint is destroyed, not fired, and must stay silent -- the same
        // removed-versus-activated distinction the gameplay rules already make.
        public void PlayRocketActivation()
        {
            RocketActivationCount++;
            Play(rocketClip, powerUpVolume);
            RequestHaptic();
        }

        public void PlayBombActivation()
        {
            BombActivationCount++;
            Play(bombClip, powerUpVolume);
            RequestHaptic();
        }

        public void PlayWin()
        {
            WinCount++;
            Play(winClip, terminalVolume);
            RequestHaptic();
        }

        public void PlayLose()
        {
            LoseCount++;
            Play(loseClip, terminalVolume);
        }

        private void Play(AudioClip clip, float volume)
        {
            if (!audioEnabled || clip == null || sfxSource == null)
            {
                return;
            }

            // PlayOneShot layers sounds on one source rather than replacing what is playing,
            // so a crate break can overlap the blast that caused it.
            sfxSource.PlayOneShot(clip, masterSfxVolume * volume);
        }

        private static float PitchForGroup(int groupSize)
        {
            float t = Mathf.InverseLerp(SmallGroup, LargeGroup, groupSize);
            return Mathf.Lerp(SmallGroupPitch, LargeGroupPitch, t);
        }

        // ONE haptic method, deliberately.
        //
        // Unity's built-in Handheld.Vibrate() is a single fixed buzz with no duration or
        // intensity parameter. Naming separate PlayLightHaptic / PlayHeavyHaptic methods
        // would suggest a distinction the platform cannot honour, so there is one honest
        // call. Real intensity control needs a native plugin, which this project has
        // deliberately not taken on.
        private void RequestHaptic()
        {
            HapticRequestCount++;

            if (!hapticsEnabled)
            {
                return;
            }

#if UNITY_ANDROID || UNITY_IOS
            // Editor-safe: Handheld.Vibrate is a no-op in the Editor on desktop, but guarding
            // explicitly keeps the intent obvious and costs nothing.
            if (!Application.isEditor)
            {
                Handheld.Vibrate();
            }
#endif
        }
    }
}
