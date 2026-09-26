using System.Collections.Generic;
using BlastPuzzle.Blocks;
using UnityEngine;

namespace BlastPuzzle.Presentation
{
    // Cosmetic effects for events gameplay has already decided. It never decides anything.
    public sealed class GameplayVFX : MonoBehaviour
    {
        // Shared effect renderers must finish before the next chain activation reuses them.
        public float ActivationDuration(BlockKind kind) => kind == BlockKind.Rocket
            ? streakDuration : Mathf.Max(shockwaveDuration, bombShakeDuration);

        [SerializeField] private ParticleSystem blockBurst;
        [SerializeField] private ParticleSystem crateBreak;
        [SerializeField] private ParticleSystem powerUpCreated;
        [SerializeField] private ParticleSystem bombBlast;
        [SerializeField] private ParticleSystem winBurst;

        [SerializeField] private SpriteRenderer rocketStreak;
        [SerializeField] private SpriteRenderer shockwave;

        [SerializeField] private int particlesPerBlock = 6;
        [SerializeField] private int particlesPerCrate = 8;
        [SerializeField] private float streakDuration = 0.22f;
        [SerializeField] private float shockwaveDuration = 0.34f;
        [SerializeField] private float shockwaveEndScale = 2.6f;

        [Tooltip("Very small, bomb only. Zero disables it.")]
        [SerializeField] private float bombShakeAmplitude = 0.06f;

        [SerializeField] private float bombShakeDuration = 0.14f;
        [SerializeField] private Camera shakeCamera;

        // Counters, so the effects can be tested without inspecting pixels.
        public int BlockBurstCount { get; private set; }
        public int CrateBreakCount { get; private set; }
        public int RocketStreakCount { get; private set; }
        public int BombBlastCount { get; private set; }
        public int PowerUpCreatedCount { get; private set; }
        public int WinCount { get; private set; }

        public void ResetCounters()
        {
            BlockBurstCount = CrateBreakCount = RocketStreakCount = 0;
            BombBlastCount = PowerUpCreatedCount = WinCount = 0;
        }

        // One Emit call per position rather than one system per block: a single reused
        // ParticleSystem means nothing is instantiated and nothing needs cleaning up.
        public void PlayBlockBurst(IReadOnlyList<Vector3> positions, IReadOnlyList<BlockColor> colors)
        {
            if (blockBurst == null || positions == null || positions.Count == 0)
            {
                return;
            }

            for (int i = 0; i < positions.Count; i++)
            {
                Color tint = i < colors.Count
                    ? BlockColorPalette.ToDisplayColor(colors[i])
                    : Color.white;

                Emit(blockBurst, positions[i], particlesPerBlock, tint);
            }

            BlockBurstCount++;
        }

        public void PlayCrateBreak(IReadOnlyList<Vector3> positions)
        {
            if (crateBreak == null || positions == null || positions.Count == 0)
            {
                return;
            }

            foreach (Vector3 position in positions)
            {
                Emit(crateBreak, position, particlesPerCrate, Color.white);
            }

            CrateBreakCount++;
        }

        public void PlayPowerUpCreated(Vector3 position)
        {
            if (powerUpCreated == null)
            {
                return;
            }

            Emit(powerUpCreated, position, 12, Color.white);
            PowerUpCreatedCount++;
        }

        // The streak only draws the line; which cells were cleared was decided long before.
        public void PlayRocketStreak(Vector3 position, RocketDirection direction)
        {
            if (rocketStreak == null)
            {
                return;
            }

            RocketStreakCount++;
            StartCoroutine(Streak(position, direction));
        }

        public void PlayBombBlast(Vector3 position)
        {
            BombBlastCount++;

            if (bombBlast != null)
            {
                Emit(bombBlast, position, 16, Color.white);
            }

            if (shockwave != null)
            {
                StartCoroutine(Shockwave(position));
            }

            if (shakeCamera != null && bombShakeAmplitude > 0f)
            {
                StartCoroutine(Shake());
            }
        }

        public void PlayWin(Vector3 position)
        {
            if (winBurst == null)
            {
                return;
            }

            Emit(winBurst, position, 60, Color.white);
            WinCount++;
        }

        private static void Emit(ParticleSystem system, Vector3 worldPosition, int count, Color tint)
        {
            var parameters = new ParticleSystem.EmitParams
            {
                position = system.transform.InverseTransformPoint(worldPosition),
                applyShapeToPosition = true,
                startColor = tint
            };

            system.Emit(parameters, count);
        }

        private System.Collections.IEnumerator Streak(Vector3 position, RocketDirection direction)
        {
            Transform streak = rocketStreak.transform;
            bool horizontal = direction == RocketDirection.Horizontal;

            streak.position = position;
            streak.localRotation = Quaternion.Euler(0f, 0f, horizontal ? 0f : 90f);
            rocketStreak.enabled = true;

            float elapsed = 0f;

            while (elapsed < streakDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / streakDuration);

                // Stretches along its line and fades out.
                streak.localScale = new Vector3(Mathf.Lerp(0.4f, 3.2f, t), Mathf.Lerp(1.1f, 0.6f, t), 1f);
                Color color = rocketStreak.color;
                rocketStreak.color = new Color(color.r, color.g, color.b, 1f - t);
                yield return null;
            }

            rocketStreak.enabled = false;
        }

        private System.Collections.IEnumerator Shockwave(Vector3 position)
        {
            Transform ring = shockwave.transform;
            ring.position = position;
            shockwave.enabled = true;

            float elapsed = 0f;

            while (elapsed < shockwaveDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / shockwaveDuration);

                float scale = Mathf.Lerp(0.3f, shockwaveEndScale, 1f - Mathf.Pow(1f - t, 3f));
                ring.localScale = new Vector3(scale, scale, 1f);

                Color color = shockwave.color;
                shockwave.color = new Color(color.r, color.g, color.b, 1f - t);
                yield return null;
            }

            shockwave.enabled = false;
        }

        // Always returns to the exact starting position, never drifts.
        private System.Collections.IEnumerator Shake()
        {
            Vector3 home = shakeCamera.transform.position;
            float elapsed = 0f;

            while (elapsed < bombShakeDuration)
            {
                elapsed += Time.deltaTime;
                float fade = 1f - Mathf.Clamp01(elapsed / bombShakeDuration);
                Vector2 offset = Random.insideUnitCircle * bombShakeAmplitude * fade;
                shakeCamera.transform.position = home + new Vector3(offset.x, offset.y, 0f);
                yield return null;
            }

            shakeCamera.transform.position = home;
        }
    }
}
