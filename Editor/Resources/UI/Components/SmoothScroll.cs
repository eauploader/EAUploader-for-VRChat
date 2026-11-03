using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// Unity 2022.3 uses C# 9; file-scoped namespaces require C# 10. skipcq: CS-R1062
namespace EAUploader.UI.Components
{
    /// <summary>
    /// マウスホイールでのスクロールをなめらかにする。
    /// ScrollView は、ホイールを1目盛り回すたびに位置を一気に動かす。ここでは目的の位置だけを決めて、短い時間でそこまで滑らせる。
    /// </summary>
    internal static class SmoothScroll
    {
        // 1回のホイール操作で、目的の位置まで動くのにかける時間。Web ブラウザーのスムーズスクロールと同じくらいの短さ
        private const double DurationSeconds = 0.18;

        // ホイールの delta 1あたりに動かす距離。エディターではホイール1目盛りが delta 3 なので、
        // Web ブラウザーと同じく1目盛りでおよそ100px 動く。ScrollView の標準(1目盛り54px)より多い
        private const float PixelsPerDelta = 100f / 3f;

        public static void Enable(ScrollView scrollView)
        {
            if (scrollView == null) return;

            var target = 0f;
            var from = 0f;
            var start = -1.0;
            IVisualElementScheduledItem animation = null;

            void Step()
            {
                // 最初の更新の時刻を開始時刻にする(エディターが止まっていた直後でも、最初の1フレームで進みすぎない)
                var now = EditorApplication.timeSinceStartup;
                if (start < 0) start = now;

                var t = Mathf.Clamp01((float)((now - start) / DurationSeconds));
                var eased = 1f - (1f - t) * (1f - t) * (1f - t);

                scrollView.scrollOffset = new Vector2(scrollView.scrollOffset.x, Mathf.Lerp(from, target, eased));
                EAUploader.RequestSmoothFrames(0.2);

                if (t >= 1f) animation.Pause();
            }

            // ScrollView 自身の処理より先に受け取って、そちらは止める
            scrollView.RegisterCallback<WheelEvent>(evt =>
            {
                var max = scrollView.verticalScroller.highValue;
                if (scrollView.mode == ScrollViewMode.Horizontal || max <= 0.5f) return;

                var animating = animation != null && animation.isActive;
                if (!animating)
                {
                    target = scrollView.scrollOffset.y;
                }

                // 動いている途中で回したときは、いまの位置から新しい目的の位置へ向けて、動きを仕切り直す
                from = scrollView.scrollOffset.y;
                start = -1.0;
                target = Mathf.Clamp(target + evt.delta.y * PixelsPerDelta, 0f, max);
                evt.StopImmediatePropagation();

                if (animation == null)
                {
                    animation = scrollView.schedule.Execute(Step).Every(1);
                }
                else if (!animating)
                {
                    animation.Resume();
                }
            }, TrickleDown.TrickleDown);
        }
    }
}
