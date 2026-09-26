using System.Reflection;
using BlastPuzzle.Boards;
using BlastPuzzle.Presentation;
using BlastPuzzle.UI;
using NUnit.Framework;
using UnityEngine;

namespace BlastPuzzle.Tests.EditMode
{
    public sealed class MobileLayoutTests
    {
        [TestCase(1080,1920)]
        [TestCase(1080,2340)]
        [TestCase(1080,2400)]
        public void SafeArea_AppliesInsetsAndUpdatesWhenAreaChanges(int width, int height)
        {
            var host = new GameObject("SafeArea", typeof(RectTransform));
            try
            {
                var fitter = host.AddComponent<SafeAreaFitter>();
                var rect = (RectTransform)host.transform;
                var area = new Rect(20, 80, width - 40, height - 200);
                fitter.Apply(area, new Vector2Int(width,height));
                Assert.That(rect.anchorMin.x, Is.EqualTo(20f/width).Within(.0001f));
                Assert.That(rect.anchorMax.y, Is.EqualTo((height-120f)/height).Within(.0001f));
                Assert.That(rect.offsetMin, Is.EqualTo(Vector2.zero));
                fitter.Apply(new Rect(0,0,width,height),new Vector2Int(width,height));
                Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [TestCase(6, 9f/16f)] [TestCase(8, 9f/16f)]
        [TestCase(6, 9f/19.5f)] [TestCase(8, 9f/19.5f)]
        [TestCase(6, 9f/20f)] [TestCase(8, 9f/20f)]
        public void BoardFit_ContainsBoardInsideSafeGameplayRegion(int size, float aspect)
        {
            var host = new GameObject("Board");
            var cameraHost = new GameObject("Camera");
            try
            {
                var view = host.AddComponent<BoardView>();
                typeof(BoardView).GetField("board",BindingFlags.Instance|BindingFlags.NonPublic)
                    .SetValue(view,new Board(size,size));
                var camera = cameraHost.AddComponent<Camera>();
                camera.orthographic = true; camera.aspect = aspect;
                camera.transform.position = new Vector3(0,0,-10);
                var area = Rect.MinMaxRect(.05f,.12f,.95f,.70f);
                view.FitCamera(camera,area);
                float half = (size*1.05f-.05f)*.5f;
                foreach (int x in new[] {-1,1}) foreach (int y in new[] {-1,1})
                {
                    Vector3 point = camera.WorldToViewportPoint(new Vector3(x*half,y*half,0));
                    Assert.That(point.x,Is.InRange(area.xMin,area.xMax));
                    Assert.That(point.y,Is.InRange(area.yMin,area.yMax));
                }
                Assert.That(host.transform.localScale,Is.EqualTo(Vector3.one));
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(cameraHost); }
        }
    }
}
