using UnityEngine;

namespace Slotgun
{
    /// <summary>
    /// Height measurement shared by everything that dresses a body with an imported model.
    ///
    /// The character packs do not agree on scale — the zombie characters import about 2.2 m tall
    /// and the skeletons sit at a different size again — so a wave built out of them came out with
    /// bodies both taller and shorter than the player. Rather than trusting a magic scale factor
    /// per pack, measure the instance and fit it to a target height.
    /// </summary>
    public static class ModelHeight
    {
        /// <summary>Used when nothing could be measured, so a body is never scaled to nothing.</summary>
        public const float Fallback = 1.8f;

        /// <summary>World-space height of everything rendered under <paramref name="root"/>.</summary>
        public static float Measure(Transform root)
        {
            if (root == null) return 0f;

            bool any = false;
            float min = float.MaxValue;
            float max = float.MinValue;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;

                Bounds b = r.bounds;
                if (b.size.sqrMagnitude <= 0f) continue;

                min = Mathf.Min(min, b.min.y);
                max = Mathf.Max(max, b.max.y);
                any = true;
            }

            return any ? max - min : 0f;
        }

        /// <summary>
        /// Uniformly scales <paramref name="visual"/> so it stands <paramref name="targetHeight"/>
        /// metres tall. Does nothing when either the target or the measurement is unusable.
        /// </summary>
        public static void Fit(Transform visual, float targetHeight)
        {
            if (visual == null) return;

            float target = targetHeight > 0.01f ? targetHeight : Fallback;
            float current = Measure(visual);
            if (current <= 0.01f) return;

            visual.localScale *= target / current;
        }
    }
}
