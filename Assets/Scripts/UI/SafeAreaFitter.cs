using UnityEngine;

namespace BlastPuzzle.UI
{
    // Attach to a full-screen UI container directly below a full-screen Canvas.
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform target;
        private Rect lastArea;
        private Vector2Int lastSize;

        private void OnEnable()
        {
            target = (RectTransform)transform;
            Apply(Screen.safeArea, new Vector2Int(Screen.width, Screen.height));
        }

        private void Update()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (lastArea != Screen.safeArea || lastSize != size) Apply(Screen.safeArea, size);
        }

        public void Apply(Rect area, Vector2Int size)
        {
            if (size.x <= 0 || size.y <= 0) return;
            if (target == null) target = (RectTransform)transform;
            lastArea = area;
            lastSize = size;
            target.anchorMin = new Vector2(Mathf.Clamp01(area.xMin / size.x), Mathf.Clamp01(area.yMin / size.y));
            target.anchorMax = new Vector2(Mathf.Clamp01(area.xMax / size.x), Mathf.Clamp01(area.yMax / size.y));
            target.offsetMin = target.offsetMax = Vector2.zero;
        }
    }
}
