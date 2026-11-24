using UnityEngine;

namespace Slotgun
{
    public enum DoorWay { ArenaExit, PrepExit }

    /// <summary>
    /// The only physical link between two maps. It stays shut (gate down) until the director
    /// unlocks it, and once open the player walking into it hands control back to the director,
    /// which streams the next scene in and frees the current one.
    /// </summary>
    public class DoorPortal : MonoBehaviour
    {
        public DoorWay way = DoorWay.ArenaExit;

        [Header("Gate")]
        public Transform gate;
        public float openLift = 8f;
        public float liftSpeed = 2.4f;

        [Header("Trigger")]
        public float triggerRadius = 3.2f;

        public bool IsOpen { get; private set; }

        Vector3 _closedPosition;
        float _lift;
        bool _handedOff;

        void Awake()
        {
            if (gate != null) _closedPosition = gate.localPosition;
        }

        public void Unlock()
        {
            IsOpen = true;
            _handedOff = false;
        }

        public void Lock()
        {
            IsOpen = false;
            _handedOff = false;
        }

        void Update()
        {
            if (gate != null)
            {
                float target = IsOpen ? openLift : 0f;
                _lift = Mathf.Lerp(_lift, target, Time.deltaTime * liftSpeed);
                gate.localPosition = _closedPosition + Vector3.up * _lift;
            }

            if (!IsOpen || _handedOff) return;

            var director = GameDirector.Instance;
            if (director == null || !director.DoorsArmed) return;

            var target2 = director.PlayerTransform;
            if (target2 == null) return;

            Vector3 a = transform.position; a.y = 0f;
            Vector3 b = target2.position; b.y = 0f;
            if ((a - b).sqrMagnitude > triggerRadius * triggerRadius) return;

            _handedOff = true;
            director.OnDoorReached(this);
        }
    }
}
