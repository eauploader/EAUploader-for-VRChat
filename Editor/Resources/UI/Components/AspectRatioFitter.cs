using UnityEngine;
using UnityEngine.UIElements;

namespace EAUploader.UI.Components
{
    /// <summary>
    /// USS には縦横比を指定する方法がないので、幅にあわせて高さを決める。
    /// </summary>
    internal static class AspectRatioFitter
    {
        /// <summary>
        /// 要素の高さを、常に「幅 × heightPerWidth」に保つ。画像なら枠いっぱいに表示する。
        /// </summary>
        public static void Keep(VisualElement element, float heightPerWidth)
        {
            if (element == null) return;

            if (element is Image image)
            {
                image.scaleMode = ScaleMode.ScaleAndCrop;
            }

            element.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                var height = Mathf.Round(evt.newRect.width * heightPerWidth);
                if (height > 0f && !Mathf.Approximately(element.resolvedStyle.height, height))
                {
                    element.style.height = height;
                }
            });
        }
    }
}
