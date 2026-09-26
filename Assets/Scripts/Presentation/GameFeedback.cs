using UnityEngine;

namespace BlastPuzzle.Presentation
{
    // Sound and haptics for events gameplay has already decided.
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
        private const int SmallGroup = 2;
        private const int LargeGroup = 10;
        private const float SmallGroupPitch = 0.95f;
        private const float LargeGroupPitch = 1.08f;
        // GameFlowController applies the persisted preferences to this session.
        public bool AudioEnabled
        {
            get => audioEnabled;
            set => audioEnabled = value;
        }

        public bool HapticsEnabled
        {
            get => hapticsEnabled;
            set => hapticsEnabled = value;
        }

        public int BlastCount { get; private set; }
        public int CrateBreakCount { get; private set; }
        public int RocketActivationCount { get; private set; }
        public int BombActivationCount { get; private set; }
        public int WinCount { get; private set; }
        public int LoseCount { get; private set; }
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
            sfxSource.PlayOneShot(clip, masterSfxVolume * volume);
        }

        private static float PitchForGroup(int groupSize)
        {
            float t = Mathf.InverseLerp(SmallGroup, LargeGroup, groupSize);
            return Mathf.Lerp(SmallGroupPitch, LargeGroupPitch, t);
        }
        private void RequestHaptic()
        {
            HapticRequestCount++;

            if (!hapticsEnabled)
            {
                return;
            }

#if UNITY_ANDROID || UNITY_IOS
            if (!Application.isEditor)
            {
                Handheld.Vibrate();
            }
#endif
        }
    }
}
