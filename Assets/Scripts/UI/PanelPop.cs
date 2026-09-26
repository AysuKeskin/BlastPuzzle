using System.Collections;
using UnityEngine;

namespace BlastPuzzle.UI
{
    [RequireComponent(typeof(RectTransform))]
    // A short scale-up when a panel is switched on.
    public sealed class PanelPop : MonoBehaviour
    {
        [SerializeField]
        private RectTransform target;

        [SerializeField]
        private float duration = 0.16f;

        [SerializeField]
        private float startScale = 0.85f;

        private void OnEnable()
        {
            StopAllCoroutines();
            StartCoroutine(Pop());
        }

        private void OnDisable()
        {
            target.localScale = Vector3.one;
        }

        private IEnumerator Pop()
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                float overshoot = Mathf.Sin(t * Mathf.PI) * 0.04f;

                target.localScale = Vector3.one * (Mathf.Lerp(startScale, 1f, eased) + overshoot);
                yield return null;
            }

            target.localScale = Vector3.one;
        }
    }
}
