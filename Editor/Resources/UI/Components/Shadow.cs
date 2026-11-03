using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace EAUploader.UI.Components
{
    /// <summary>
    /// Draws a soft drop shadow behind its first child, modeled on CSS <c>box-shadow</c>:
    /// <c>offset-x offset-y blur spread rgba(0, 0, 0, opacity)</c>.
    /// The shadow is the child's rounded box blurred with a Gaussian whose standard deviation is half of
    /// <see cref="shadowBlur"/>, as the CSS spec defines it. The child's border radius is picked up automatically.
    /// </summary>
    class Shadow : VisualElement
    {
        // Samples per corner used to follow the rounded corner of the box.
        private const int k_CornerSegments = 6;
        // Grid steps across the blurred edge region of each side (the region is 6 sigma wide).
        private const int k_EdgeSteps = 12;
        // The Gaussian is cut off at this many sigmas.
        private const float k_Support = 3f;

        private float _blur = 10f;
        private float _spread;
        private float _offsetX;
        private float _offsetY;
        private float _opacity = 0.1f;

        /// <summary>CSS blur radius in px. The Gaussian's standard deviation is half of this.</summary>
        public float shadowBlur { get => _blur; set { _blur = value; MarkDirtyRepaint(); } }
        /// <summary>CSS spread radius in px. Grows (or shrinks, if negative) the shadow's box.</summary>
        public float shadowSpread { get => _spread; set { _spread = value; MarkDirtyRepaint(); } }
        public float shadowOffsetX { get => _offsetX; set { _offsetX = value; MarkDirtyRepaint(); } }
        public float shadowOffsetY { get => _offsetY; set { _offsetY = value; MarkDirtyRepaint(); } }
        /// <summary>Alpha of the black shadow color, 0 to 1.</summary>
        public float shadowOpacity { get => _opacity; set { _opacity = value; MarkDirtyRepaint(); } }

        public new class UxmlFactory : UxmlFactory<Shadow, UxmlTraits> { }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
            {
                get { yield break; }
            }

            UxmlFloatAttributeDescription blurAttr =
                new UxmlFloatAttributeDescription { name = "shadow-blur", defaultValue = 10 };

            UxmlFloatAttributeDescription spreadAttr =
                new UxmlFloatAttributeDescription { name = "shadow-spread", defaultValue = 0 };

            UxmlFloatAttributeDescription offsetXAttr =
                new UxmlFloatAttributeDescription { name = "shadow-offset-x", defaultValue = 0 };

            UxmlFloatAttributeDescription offsetYAttr =
                new UxmlFloatAttributeDescription { name = "shadow-offset-y", defaultValue = 0 };

            UxmlFloatAttributeDescription opacityAttr =
                new UxmlFloatAttributeDescription { name = "shadow-opacity", defaultValue = 0.1f };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                var shadow = ve as Shadow;

                shadow.shadowBlur = blurAttr.GetValueFromBag(bag, cc);
                shadow.shadowSpread = spreadAttr.GetValueFromBag(bag, cc);
                shadow.shadowOffsetX = offsetXAttr.GetValueFromBag(bag, cc);
                shadow.shadowOffsetY = offsetYAttr.GetValueFromBag(bag, cc);
                shadow.shadowOpacity = opacityAttr.GetValueFromBag(bag, cc);
            }
        }

        public Shadow()
        {
            generateVisualContent += OnGenerateVisualContent;
        }

        // One vertical strip of the rounded box: x range [p0, p1], where the box spans [top, bottom] vertically.
        private struct Slice
        {
            public float p0, p1, top, bottom;
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            Rect r = contentRect;
            if (r.width <= 0f || r.height <= 0f || _opacity <= 0f)
            {
                return;
            }

            float sigma = Mathf.Max(_blur * 0.5f, 0.25f);
            float extent = sigma * k_Support;

            // The shadow's box, in local coordinates.
            float boxL = -_spread + _offsetX;
            float boxR = r.width + _spread + _offsetX;
            float boxT = -_spread + _offsetY;
            float boxB = r.height + _spread + _offsetY;

            float childRadius = childCount > 0 ? this[0].resolvedStyle.borderTopLeftRadius : 0f;
            float radius = Mathf.Clamp(childRadius + _spread, 0f, Mathf.Min(boxR - boxL, boxB - boxT) * 0.5f);

            var slices = BuildSlices(boxL, boxR, boxT, boxB, radius);

            var xs = BuildAxis(boxL, boxR, extent);
            var ys = BuildAxis(boxT, boxB, extent);
            int nx = xs.Count;
            int ny = ys.Count;

            // The blur is separable: the horizontal mass of each strip depends only on x and its vertical mass
            // only on y, so both are computed once per grid line and combined per vertex.
            var xMass = new float[nx, slices.Length];
            for (int i = 0; i < nx; i++)
            {
                for (int s = 0; s < slices.Length; s++)
                {
                    xMass[i, s] = Cdf(slices[s].p1 - xs[i], sigma) - Cdf(slices[s].p0 - xs[i], sigma);
                }
            }

            var yMass = new float[ny, slices.Length];
            for (int j = 0; j < ny; j++)
            {
                for (int s = 0; s < slices.Length; s++)
                {
                    yMass[j, s] = Cdf(slices[s].bottom - ys[j], sigma) - Cdf(slices[s].top - ys[j], sigma);
                }
            }

            var vertices = new Vertex[nx * ny];
            for (int j = 0; j < ny; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    float coverage = 0f;
                    for (int s = 0; s < slices.Length; s++)
                    {
                        coverage += xMass[i, s] * yMass[j, s];
                    }

                    vertices[j * nx + i] = new Vertex
                    {
                        position = new Vector3(xs[i], ys[j], Vertex.nearZ),
                        tint = new Color(0f, 0f, 0f, Mathf.Clamp01(coverage) * _opacity)
                    };
                }
            }

            var indices = new ushort[(nx - 1) * (ny - 1) * 6];
            int k = 0;
            for (int j = 0; j < ny - 1; j++)
            {
                for (int i = 0; i < nx - 1; i++)
                {
                    ushort a = (ushort)(j * nx + i);
                    ushort b = (ushort)(a + 1);
                    ushort c = (ushort)(a + nx);
                    ushort d = (ushort)(c + 1);
                    // UI Toolkit culls counter-clockwise triangles (y points down), so wind these clockwise.
                    indices[k] = a;
                    indices[k + 1] = b;
                    indices[k + 2] = c;
                    indices[k + 3] = b;
                    indices[k + 4] = d;
                    indices[k + 5] = c;
                    k += 6;
                }
            }

            MeshWriteData mwd = ctx.Allocate(vertices.Length, indices.Length);
            mwd.SetAllVertices(vertices);
            mwd.SetAllIndices(indices);
        }

        /// <summary>
        /// Cuts the rounded box into vertical strips. Between the corners the box is full height; inside each
        /// corner it is split into a few strips whose height follows the corner arc.
        /// </summary>
        private static Slice[] BuildSlices(float boxL, float boxR, float boxT, float boxB, float radius)
        {
            int segments = radius >= 0.5f ? k_CornerSegments : 0;
            var slices = new Slice[1 + segments * 2];

            slices[0] = new Slice { p0 = boxL + radius, p1 = boxR - radius, top = boxT, bottom = boxB };

            for (int i = 0; i < segments; i++)
            {
                float d0 = radius * i / segments;
                float d1 = radius * (i + 1) / segments;
                float mid = (d0 + d1) * 0.5f;

                // How far the arc has moved inward from the top / bottom edge at this distance from the side.
                float dy = radius - Mathf.Sqrt(Mathf.Max(0f, radius * radius - (radius - mid) * (radius - mid)));

                slices[1 + i * 2] = new Slice { p0 = boxL + d0, p1 = boxL + d1, top = boxT + dy, bottom = boxB - dy };
                slices[2 + i * 2] = new Slice { p0 = boxR - d1, p1 = boxR - d0, top = boxT + dy, bottom = boxB - dy };
            }

            return slices;
        }

        /// <summary>
        /// Grid line positions along one axis: a fine run across the blurred region at each end of the box,
        /// merged and sorted. The flat region between the two ends needs no extra lines.
        /// </summary>
        private static List<float> BuildAxis(float a, float b, float extent)
        {
            var points = new List<float>();
            AddRun(points, a - extent, a + extent);
            AddRun(points, b - extent, b + extent);
            points.Sort();

            var result = new List<float>(points.Count);
            foreach (var p in points)
            {
                if (result.Count == 0 || p - result[result.Count - 1] > 0.01f)
                {
                    result.Add(p);
                }
            }
            return result;
        }

        private static void AddRun(List<float> points, float from, float to)
        {
            for (int i = 0; i <= k_EdgeSteps; i++)
            {
                points.Add(Mathf.Lerp(from, to, i / (float)k_EdgeSteps));
            }
        }

        // Standard normal CDF of (v / sigma).
        private static float Cdf(float v, float sigma)
        {
            return 0.5f * (1f + Erf(v / (sigma * 1.41421356f)));
        }

        // Abramowitz and Stegun 7.1.26, max error about 1.5e-7.
        private static float Erf(float x)
        {
            float sign = x < 0f ? -1f : 1f;
            x = Mathf.Abs(x);
            float t = 1f / (1f + 0.3275911f * x);
            float poly = ((((1.061405429f * t - 1.453152027f) * t) + 1.421413741f) * t - 0.284496736f) * t + 0.254829592f;
            return sign * (1f - poly * t * Mathf.Exp(-x * x));
        }
    }
}
