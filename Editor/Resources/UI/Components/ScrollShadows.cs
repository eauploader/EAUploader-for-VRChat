using UnityEngine;
using UnityEngine.UIElements;

// Unity 2022.3 uses C# 9; file-scoped namespaces require C# 10. skipcq: CS-R1062
namespace EAUploader.UI.Components
{
    /// <summary>
    /// スクロールの位置に応じて、スクロール領域の上下の境界に影を出す。
    /// 先頭にいるときは上の影を、末尾にいるときは下の影を出さず、境目から少し動かす間に徐々に濃くする。
    /// </summary>
    internal static class ScrollShadows
    {
        // 端から、影が最大の濃さになるまでのスクロール量
        private const float RampPx = 16f;

        public static void Attach(ScrollView scrollView, Shadow topShadow, Shadow bottomShadow)
        {
            if (scrollView == null || topShadow == null || bottomShadow == null) return;

            var maxTopOpacity = topShadow.shadowOpacity;
            var maxBottomOpacity = bottomShadow.shadowOpacity;

            void Update()
            {
                var scroller = scrollView.verticalScroller;
                var topAmount = Mathf.Clamp01(scroller.value / RampPx);
                topShadow.EnableInClassList("hidden", topAmount <= 0f);
                topShadow.shadowOpacity = maxTopOpacity * topAmount;

                var remaining = scroller.highValue - scroller.value;
                var bottomAmount = scroller.highValue > 0f ? Mathf.Clamp01(remaining / RampPx) : 0f;
                bottomShadow.EnableInClassList("hidden", bottomAmount <= 0f);
                bottomShadow.shadowOpacity = maxBottomOpacity * bottomAmount;
            }

            scrollView.verticalScroller.valueChanged += _ => Update();
            scrollView.contentContainer.RegisterCallback<GeometryChangedEvent>(_ => Update());
            scrollView.RegisterCallback<GeometryChangedEvent>(_ => Update());
            Update();
        }
    }
}
