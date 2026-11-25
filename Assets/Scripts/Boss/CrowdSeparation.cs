using System.Collections.Generic;
using UnityEngine;

namespace Slotgun
{
    /// <summary>
    /// Keeps the bodies of a wave out of one another.
    ///
    /// Every wave body walks by writing its own <c>transform.position</c>, so its capsule never takes
    /// part in a physics step and two bodies heading for the same spot of the player simply slide
    /// into each other. Two things fix that.
    ///
    /// <see cref="Push"/> is steering: a body adds a shove away from whatever stands too close to the
    /// direction it meant to walk in, so a pack fans out instead of stacking.
    ///
    /// <see cref="ResolveAll"/> is the hard guarantee, and it is the one that matters. Steering alone
    /// cannot hold a crowd: a lunging zombie covers about 18 cm per frame, and an order-dependent
    /// per-body correction lets whichever body updated last win the spot. The resolution pass runs
    /// once per frame after *every* body has moved, splits each remaining pair between both of them,
    /// and repeats, so no frame is ever drawn with two enemies inside each other.
    /// </summary>
    public static class CrowdSeparation
    {
        static readonly List<Transform> Bodies = new List<Transform>(64);

        /// <summary>Per body, the centre distance below which that body's capsule is inside another.</summary>
        static readonly List<float> Solids = new List<float>(64);

        /// <summary>How many times the pair loop is applied per frame.</summary>
        const int Passes = 3;

        static CrowdSeparationDriver _driver;

        public static void Register(Transform body, float solidRadius)
        {
            if (body == null) return;

            int at = Bodies.IndexOf(body);
            if (at >= 0) { Solids[at] = solidRadius; return; }

            Bodies.Add(body);
            Solids.Add(solidRadius);
            EnsureDriver();
        }

        public static void Unregister(Transform body)
        {
            if (body == null) return;

            int at = Bodies.IndexOf(body);
            if (at < 0) return;

            PruneAt(at);
        }

        /// <summary>
        /// Flat push away from every registered body closer than <paramref name="spacing"/> metres.
        /// Two bodies stacked on the exact same point have no direction between them, so they are
        /// split along the body's own right vector instead of dividing by zero.
        /// </summary>
        public static Vector3 Push(Vector3 position, Transform self, float spacing)
        {
            if (spacing <= 0.01f) return Vector3.zero;

            Vector3 push = Vector3.zero;

            for (int i = Bodies.Count - 1; i >= 0; i--)
            {
                Transform other = Bodies[i];
                if (other == null) { PruneAt(i); continue; }   // destroyed without dying
                if (other == self) continue;

                Vector3 d = position - other.position;
                d.y = 0f;
                float m = d.magnitude;
                if (m >= spacing) continue;

                if (m < 0.02f)
                {
                    push += FlatSide(other) * spacing;
                    continue;
                }

                // Linear falloff: full shove when overlapping, nothing at the spacing itself.
                push += d / m * (spacing - m);
            }

            return push;
        }

        /// <summary>
        /// Splits every pair of bodies that is still inside one another. Called once per frame after
        /// all movement, so the order the bodies happen to update in no longer decides who keeps the
        /// contested spot, and both sides give way instead of one being shot sideways. A single step
        /// is capped so a shallow overlap closes smoothly, while a deep one — a lunge that landed on
        /// a neighbour — is opened within a frame or two.
        /// </summary>
        public static void ResolveAll()
        {
            for (int i = Bodies.Count - 1; i >= 0; i--)
                if (Bodies[i] == null) PruneAt(i);

            if (Bodies.Count < 2) return;

            float step = Mathf.Max(0.16f, 9f * Time.deltaTime);

            for (int pass = 0; pass < Passes; pass++)
            {
                bool corrected = false;

                for (int i = 0; i < Bodies.Count; i++)
                {
                    Transform a = Bodies[i];
                    Vector3 aPosition = a.position;

                    for (int j = i + 1; j < Bodies.Count; j++)
                    {
                        Transform b = Bodies[j];

                        Vector3 d = aPosition - b.position;
                        d.y = 0f;
                        float m = d.magnitude;

                        float min = Mathf.Max(Solids[i], Solids[j]);
                        if (m >= min) continue;

                        Vector3 axis = m > 0.02f ? d / m : FlatSide(b);
                        float give = Mathf.Min((min - m) * 0.5f, step);

                        // Both give way: one body never eats the whole correction, which is what made
                        // a packed body jitter around the one that had moved last.
                        aPosition += axis * give;
                        BoundsPush(b, axis * give);

                        corrected = true;
                    }

                    a.position = aPosition;
                }

                if (!corrected) break;
            }
        }

        static void BoundsPush(Transform body, Vector3 shift)
        {
            Vector3 next = body.position - shift;
            next.y = body.position.y;
            body.position = next;
        }

        static Vector3 FlatSide(Transform body)
        {
            Vector3 side = body.right;
            side.y = 0f;
            return side.sqrMagnitude > 0.0001f ? side.normalized : Vector3.right;
        }

        static void PruneAt(int index)
        {
            Bodies.RemoveAt(index);
            Solids.RemoveAt(index);
        }

        static void EnsureDriver()
        {
            if (_driver != null) return;

            var go = new GameObject("[CrowdSeparation]");
            go.hideFlags = HideFlags.HideAndDontSave;
            _driver = go.AddComponent<CrowdSeparationDriver>();
        }
    }

    /// <summary>The single thing that drives the resolution pass: it has to run after every body.</summary>
    [DefaultExecutionOrder(1000)]
    internal sealed class CrowdSeparationDriver : MonoBehaviour
    {
        void LateUpdate() => CrowdSeparation.ResolveAll();
    }
}
