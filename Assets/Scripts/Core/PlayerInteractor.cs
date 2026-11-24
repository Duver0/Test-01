using UnityEngine;
using UnityEngine.InputSystem;

namespace Slotgun
{
    /// <summary>
    /// Finds the closest usable <see cref="IInteractable"/> around the player and exposes its
    /// prompt to the HUD. The probe uses a fixed collider buffer so it never allocates per frame.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        public static PlayerInteractor Instance { get; private set; }

        public float radius = 3.8f;
        public string Prompt { get; private set; } = string.Empty;

        readonly Collider[] _buffer = new Collider[32];
        IInteractable _current;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            _current = FindNearest();
            Prompt = _current != null ? _current.Prompt : string.Empty;

            var keyboard = Keyboard.current;
            if (_current != null && keyboard != null && keyboard.eKey.wasPressedThisFrame)
                _current.Interact();
        }

        IInteractable FindNearest()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, radius, _buffer, ~0,
                                                     QueryTriggerInteraction.Collide);

            IInteractable best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var col = _buffer[i];
                if (col == null) continue;

                var candidate = col.GetComponentInParent<IInteractable>();
                if (candidate == null || !candidate.Available) continue;

                float sqr = (col.transform.position - transform.position).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = candidate;
            }

            return best;
        }
    }
}
