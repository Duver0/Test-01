using System.Collections.Generic;
using UnityEngine;

namespace Slotgun
{
    /// <summary>
    /// Builds a boss out of chunky primitives. Five archetypes, each deliberately absurd:
    /// a slot-machine golem, a three-headed worm, a fridge-crab, a balloon toad and a gear blob.
    /// Silhouette first, asymmetric limbs, two-tone minimum, nothing axis-aligned.
    /// </summary>
    public static class BossVisual
    {
        public static Transform Build(BossConfig cfg, Transform root, List<Transform> spinners)
        {
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root, false);
            visual.localScale = Vector3.one * cfg.visualScale;

            Color a = cfg.primaryColor;
            Color b = cfg.secondaryColor;
            Color dark = Color.Lerp(a, Palette.Shadow, 0.7f);

            switch (cfg.body)
            {
                case BossBody.SlotGolem:     BuildSlotGolem(visual, a, b, dark, spinners); break;
                case BossBody.MultiHeadWorm: BuildWorm(visual, a, b, dark, spinners);      break;
                case BossBody.CrabFridge:    BuildCrabFridge(visual, a, b, dark, spinners);break;
                case BossBody.BalloonToad:   BuildToad(visual, a, b, dark, spinners);      break;
                case BossBody.LovecraftBlob: BuildLovecraftBlob(visual, a, b, dark, spinners); break;
                default:                     BuildClockBlob(visual, a, b, dark, spinners); break;
            }

            return visual;
        }

        /// <summary>
        /// Where the red circle sits on each archetype, in visual space. It must clear the boss
        /// capsule (radius 1.05 in visual space) or nothing can ever hit it: the capsule would
        /// block the shot.
        /// </summary>
        public static Vector3 WeakPointLocal(BossBody body)
        {
            switch (body)
            {
                case BossBody.MultiHeadWorm: return new Vector3(0f, 1.95f, 1.16f);
                case BossBody.BalloonToad:   return new Vector3(0f, 1.45f, 1.20f);
                case BossBody.CrabFridge:    return new Vector3(0f, 1.46f, 1.18f);
                case BossBody.ClockworkBlob: return new Vector3(0f, 1.55f, 1.20f);
                default:                     return new Vector3(0f, 1.55f, 1.16f);
            }
        }

        /// <summary>
        /// Mounts a small red target circle on a short dark stalk. The mount carries the hit sphere
        /// and the BossWeakPoint behaviour; the disc is only the readable face of it.
        /// </summary>
        public static BossWeakPoint AttachWeakPoint(Transform parent, Vector3 localPosition, float radius,
                                                    IDamageable owner, System.Action onSurge)
        {
            var mount = new GameObject("WeakPoint").transform;
            mount.SetParent(parent, false);
            mount.localPosition = localPosition;
            mount.localRotation = Quaternion.identity;

            Part(mount, PrimitiveType.Cube, new Vector3(0f, 0f, -0.34f), new Vector3(0.16f, 0.16f, 0.55f),
                 Vector3.zero, Color.Lerp(Palette.Danger, Palette.Shadow, 0.72f));

            var disc = Part(mount, PrimitiveType.Cylinder, Vector3.zero,
                            new Vector3(radius * 2.1f, 0.05f, radius * 2.1f), new Vector3(90f, 0f, 0f),
                            Palette.Danger, true);

            var col = mount.gameObject.AddComponent<SphereCollider>();
            col.radius = radius;
            col.isTrigger = false;

            var point = mount.gameObject.AddComponent<BossWeakPoint>();
            point.Initialize(owner, disc, onSurge, radius);
            return point;
        }

        public static Transform Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Vector3 euler,
                                     Color color, bool emissive = false)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            StylizedMaterials.Apply(go, color, emissive);
            return go.transform;
        }

        // ---- archetype 1: a slot machine that grew arms -------------------
        static void BuildSlotGolem(Transform v, Color a, Color b, Color dark, List<Transform> spinners)
        {
            Part(v, PrimitiveType.Cube, new Vector3(0f, 1.05f, 0f), new Vector3(1.7f, 2.1f, 1.2f), Vector3.zero, a);
            // belly panel
            Part(v, PrimitiveType.Cube, new Vector3(0f, 0.95f, 0.61f), new Vector3(1.5f, 1.25f, 0.08f), Vector3.zero, dark);
            // three spinning reels for a face
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 0.48f;
                var reel = Part(v, PrimitiveType.Cylinder, new Vector3(x, 1.72f, 0.62f),
                                new Vector3(0.36f, 0.09f, 0.36f), new Vector3(90f, 0f, 0f), b, true);
                spinners.Add(reel);
            }
            // two mismatched eyes
            Part(v, PrimitiveType.Sphere, new Vector3(-0.45f, 2.28f, 0.45f), Vector3.one * 0.38f, Vector3.zero, Palette.Accent, true);
            Part(v, PrimitiveType.Sphere, new Vector3(0.48f, 2.32f, 0.42f), Vector3.one * 0.26f, Vector3.zero, Palette.Accent, true);
            // lever
            Part(v, PrimitiveType.Cylinder, new Vector3(0.98f, 2.15f, 0f), new Vector3(0.09f, 0.55f, 0.09f), new Vector3(0f, 0f, -22f), dark);
            Part(v, PrimitiveType.Sphere, new Vector3(1.2f, 2.6f, 0f), Vector3.one * 0.26f, Vector3.zero, b, true);
            // asymmetric arms
            Part(v, PrimitiveType.Cube, new Vector3(-1.02f, 1.25f, 0.02f), new Vector3(0.42f, 1.75f, 0.44f), new Vector3(0f, 0f, 9f), a);
            Part(v, PrimitiveType.Cube, new Vector3(1.05f, 1.05f, -0.05f), new Vector3(0.58f, 2.05f, 0.55f), new Vector3(0f, 0f, -13f), a);
            // stubby legs
            Part(v, PrimitiveType.Cylinder, new Vector3(-0.5f, 0.16f, 0f), new Vector3(0.26f, 0.18f, 0.26f), Vector3.zero, dark);
            Part(v, PrimitiveType.Cylinder, new Vector3(0.52f, 0.16f, 0.06f), new Vector3(0.22f, 0.18f, 0.22f), Vector3.zero, dark);
        }

        // ---- archetype 2: a stack of segments wearing three heads ---------
        static void BuildWorm(Transform v, Color a, Color b, Color dark, List<Transform> spinners)
        {
            float[] ys = { 0.55f, 1.05f, 1.55f, 2.0f };
            float[] ss = { 1.35f, 1.2f, 1.05f, 0.9f };
            for (int i = 0; i < ys.Length; i++)
            {
                float wob = Mathf.Sin(i * 1.4f) * 0.14f;
                Part(v, PrimitiveType.Sphere, new Vector3(wob, ys[i], 0f), Vector3.one * ss[i],
                     new Vector3(0f, i * 11f, 0f), i % 2 == 0 ? a : b);
            }

            Vector3[] headPos = { new Vector3(-0.5f, 2.45f, 0.3f), new Vector3(0f, 2.7f, 0.22f), new Vector3(0.55f, 2.42f, 0.28f) };
            float[] headScale = { 0.5f, 0.62f, 0.44f };
            for (int i = 0; i < 3; i++)
            {
                Part(v, PrimitiveType.Sphere, headPos[i], Vector3.one * headScale[i], Vector3.zero, a);
                Part(v, PrimitiveType.Sphere, headPos[i] + new Vector3(0f, -0.05f, headScale[i] * 0.8f),
                     Vector3.one * (headScale[i] * 0.5f), Vector3.zero, b);
                Part(v, PrimitiveType.Sphere, headPos[i] + new Vector3(0.1f, 0.12f, headScale[i] * 0.65f),
                     Vector3.one * (headScale[i] * 0.3f), Vector3.zero, Palette.Accent, true);
            }
            // drooping tendrils
            Part(v, PrimitiveType.Capsule, new Vector3(-0.75f, 1.55f, 0.1f), new Vector3(0.14f, 0.5f, 0.14f), new Vector3(0f, 0f, 38f), dark);
            Part(v, PrimitiveType.Capsule, new Vector3(0.8f, 1.35f, -0.05f), new Vector3(0.11f, 0.42f, 0.11f), new Vector3(0f, 0f, -46f), dark);
        }

        // ---- archetype 3: a fridge with crab legs -------------------------
        static void BuildCrabFridge(Transform v, Color a, Color b, Color dark, List<Transform> spinners)
        {
            Part(v, PrimitiveType.Cube, new Vector3(0f, 1.15f, 0f), new Vector3(1.5f, 2.05f, 1.15f), Vector3.zero, a);
            Part(v, PrimitiveType.Cube, new Vector3(0f, 1.2f, 0.6f), new Vector3(1.55f, 0.1f, 0.06f), Vector3.zero, dark);
            Part(v, PrimitiveType.Cube, new Vector3(0.45f, 1.55f, 0.63f), new Vector3(0.07f, 0.5f, 0.07f), Vector3.zero, b, true);
            // little freezer door eye
            Part(v, PrimitiveType.Sphere, new Vector3(0f, 1.78f, 0.61f), Vector3.one * 0.34f, Vector3.zero, Palette.Accent, true);
            Part(v, PrimitiveType.Sphere, new Vector3(0f, 1.78f, 0.75f), Vector3.one * 0.14f, Vector3.zero, dark);
            // six legs
            for (int i = 0; i < 6; i++)
            {
                float x = (i % 2 == 0) ? -1f : 1f;
                float z = -0.5f + (i / 2) * 0.5f;
                float roll = x * (34f + i * 3f);
                Part(v, PrimitiveType.Capsule, new Vector3(x * 0.82f, 0.55f, z), new Vector3(0.16f, 0.55f, 0.16f),
                     new Vector3(0f, 0f, roll), dark);
            }
            // two mismatched claws
            Part(v, PrimitiveType.Sphere, new Vector3(-1.15f, 1.35f, 0.2f), new Vector3(0.62f, 0.48f, 0.55f), Vector3.zero, b);
            Part(v, PrimitiveType.Cube, new Vector3(-1.42f, 1.3f, 0.4f), new Vector3(0.4f, 0.22f, 0.5f), new Vector3(0f, 12f, 0f), b);
            Part(v, PrimitiveType.Sphere, new Vector3(1.18f, 1.1f, 0.05f), new Vector3(0.85f, 0.6f, 0.7f), Vector3.zero, b);
            Part(v, PrimitiveType.Cube, new Vector3(1.55f, 1.05f, 0.25f), new Vector3(0.5f, 0.28f, 0.62f), new Vector3(0f, -18f, 0f), b);
        }

        // ---- archetype 4: an enormous balloon toad ------------------------
        static void BuildToad(Transform v, Color a, Color b, Color dark, List<Transform> spinners)
        {
            Part(v, PrimitiveType.Sphere, new Vector3(0f, 1.15f, 0f), new Vector3(2.6f, 2.05f, 2.35f), Vector3.zero, a);
            Part(v, PrimitiveType.Sphere, new Vector3(0f, 0.75f, 0.55f), new Vector3(2.2f, 1.2f, 1.7f), Vector3.zero, b);
            // bulging eyes on top
            Part(v, PrimitiveType.Sphere, new Vector3(-0.72f, 2.15f, 0.35f), Vector3.one * 0.78f, Vector3.zero, a);
            Part(v, PrimitiveType.Sphere, new Vector3(-0.72f, 2.3f, 0.78f), Vector3.one * 0.3f, Vector3.zero, Palette.Accent, true);
            Part(v, PrimitiveType.Sphere, new Vector3(0.78f, 2.22f, 0.3f), Vector3.one * 0.62f, Vector3.zero, a);
            Part(v, PrimitiveType.Sphere, new Vector3(0.78f, 2.35f, 0.66f), Vector3.one * 0.22f, Vector3.zero, Palette.Accent, true);
            // wide flat mouth
            Part(v, PrimitiveType.Cube, new Vector3(0f, 0.82f, 1.12f), new Vector3(1.6f, 0.2f, 0.28f), Vector3.zero, dark);
            // tongue
            Part(v, PrimitiveType.Capsule, new Vector3(0.05f, 0.52f, 1.36f), new Vector3(0.2f, 0.42f, 0.2f), new Vector3(72f, 0f, 0f), Palette.CharacterPrimary);
            // tiny legs, comedically undersized
            Part(v, PrimitiveType.Cylinder, new Vector3(-0.95f, 0.16f, 0.15f), new Vector3(0.2f, 0.18f, 0.2f), Vector3.zero, dark);
            Part(v, PrimitiveType.Cylinder, new Vector3(1f, 0.16f, -0.1f), new Vector3(0.2f, 0.18f, 0.2f), Vector3.zero, dark);
        }

        // ---- archetype 5: a blob of grinding gears ------------------------
        static void BuildClockBlob(Transform v, Color a, Color b, Color dark, List<Transform> spinners)
        {
            Part(v, PrimitiveType.Sphere, new Vector3(0f, 1.25f, 0f), new Vector3(2.05f, 1.95f, 2.0f), Vector3.zero, a);
            // gears
            Vector3[] gearPos = { new Vector3(1.0f, 1.6f, 0.55f), new Vector3(-1.05f, 1.3f, 0.4f),
                                  new Vector3(0.45f, 1.9f, -0.95f), new Vector3(-0.5f, 0.85f, 1.0f) };
            for (int i = 0; i < gearPos.Length; i++)
            {
                var gear = Part(v, PrimitiveType.Cylinder, gearPos[i],
                                new Vector3(0.5f - i * 0.05f, 0.12f, 0.5f - i * 0.05f),
                                new Vector3(90f, i * 34f, i * 12f), b, true);
                // teeth
                for (int t = 0; t < 6; t++)
                {
                    float ang = t * 60f;
                    var tooth = Part(gear, PrimitiveType.Cube, new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad) * 0.55f, 0f, Mathf.Cos(ang * Mathf.Deg2Rad) * 0.55f),
                                     new Vector3(0.22f, 0.9f, 0.22f), new Vector3(0f, ang, 0f), b, true);
                    tooth.localScale = new Vector3(0.22f, 0.9f, 0.22f);
                }
                spinners.Add(gear);
            }
            // eye stalks
            Part(v, PrimitiveType.Capsule, new Vector3(-0.45f, 2.28f, 0.2f), new Vector3(0.12f, 0.42f, 0.12f), new Vector3(0f, 0f, 14f), dark);
            Part(v, PrimitiveType.Sphere, new Vector3(-0.5f, 2.62f, 0.22f), Vector3.one * 0.3f, Vector3.zero, Palette.Accent, true);
            Part(v, PrimitiveType.Capsule, new Vector3(0.4f, 2.18f, 0.15f), new Vector3(0.1f, 0.36f, 0.1f), new Vector3(0f, 0f, -18f), dark);
            Part(v, PrimitiveType.Sphere, new Vector3(0.46f, 2.48f, 0.16f), Vector3.one * 0.24f, Vector3.zero, Palette.Accent, true);
            // stub arms
            for (int i = 0; i < 6; i++)
            {
                float ang = i * 60f * Mathf.Deg2Rad;
                Part(v, PrimitiveType.Capsule,
                     new Vector3(Mathf.Sin(ang) * 1.05f, 1.15f + (i % 3) * 0.18f, Mathf.Cos(ang) * 1.05f),
                      new Vector3(0.15f, 0.4f, 0.15f),
                      new Vector3(Mathf.Cos(ang) * 52f, 0f, Mathf.Sin(ang) * -52f), a);
            }
        }

        // ---- archetype 6: a fat lovecraftian entity ------------------------
        static void BuildLovecraftBlob(Transform v, Color a, Color b, Color dark, List<Transform> spinners)
        {
            // body - fat spherical shape
            Part(v, PrimitiveType.Sphere, new Vector3(0f, 2.0f, 0f), new Vector3(3.0f, 3.0f, 3.0f), Vector3.zero, a);
            // belly wrinkles
            Part(v, PrimitiveType.Cube, new Vector3(0f, 1.2f, 0f), new Vector3(2.8f, 0.3f, 2.8f), Vector3.zero, dark);
            // multiple eye stalks
            Color eyeColor = new Color(1f, 0.8f, 0.2f);
            for (int i = 0; i < 6; i++)
            {
                float ang = i * 60f * Mathf.Deg2Rad;
                Part(v, PrimitiveType.Cylinder, new Vector3(Mathf.Sin(ang) * 1.5f, 2.5f, Mathf.Cos(ang) * 1.5f),
                     new Vector3(0.3f, 1.2f, 0.3f), new Vector3(0f, ang, 0f), eyeColor, true);
                Part(v, PrimitiveType.Sphere, new Vector3(Mathf.Sin(ang) * 1.5f, 2.8f, Mathf.Cos(ang) * 1.5f),
                     Vector3.one * 0.5f, Vector3.zero, Palette.Accent, true);
            }
            // hanging tendrils
            for (int i = 0; i < 8; i++)
            {
                float ang = i * 45f * Mathf.Deg2Rad;
                Part(v, PrimitiveType.Cylinder, new Vector3(Mathf.Sin(ang) * 2.0f, 0.5f, Mathf.Cos(ang) * 2.0f),
                     new Vector3(0.2f, 1.5f, 0.2f), new Vector3(0f, 0f, ang), dark);
            }
            // floating cubes around body
            for (int i = 0; i < 4; i++)
            {
                float ang = i * 90f * Mathf.Deg2Rad;
                Part(v, PrimitiveType.Cube, new Vector3(Mathf.Sin(ang) * 2.5f, 1.0f, Mathf.Cos(ang) * 2.5f),
                     new Vector3(0.8f, 0.8f, 0.8f), new Vector3(0f, ang, 0f), b, true);
            }
        }
    }
}