using UnityEngine;
using UnityEngine.InputSystem;

namespace Slotgun
{
    /// <summary>
    /// First-person movement. Yaw lives on the root, pitch on the camera pivot, weapon recoil
    /// feeds an offset that decays back to the player's real aim.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Wiring")]
        public Camera cam;
        public Transform eye;

        [Header("Movement")]
        public float walkSpeed = 6.4f;
        public float sprintSpeed = 9.4f;
        [Tooltip("While sprinting the player stays a hair above the boss's live rage speed, so a clean line opens a small gap but never a free escape.")]
        public float escapeMargin = 1.04f;
        public float acceleration = 22f;
        [Tooltip("Lunar gravity: a weak pull, so jumps hang in the air instead of snapping back down.")]
        public float gravity = -9f;
        [Tooltip("Apex height of a jump in metres. With the low gravity above this is ~1.5 s of airtime.")]
        public float jumpHeight = 2.6f;

        [Header("Sprint FOV")]
        [Tooltip("FOV normal de la cÃ¡mara")]
        public float fovNormal = 80f;
        [Tooltip("FOV cuando hace sprint - aumenta el campo de visiÃ³n para sensaciÃ³n de velocidad")]
        public float fovSprint = 90f;
        [Tooltip("Velocidad de cambio de FOV al entrar/salir del sprint")]
        public float fovLerpSpeed = 10f;

        [Header("Sprint stamina")]
        [Tooltip("Segundos de sprint que paga una barra llena.")]
        public float sprintSeconds = 6f;
        [Tooltip("Segundos que tarda en rellenarse la barra mientras no esprintas.")]
        public float sprintRecoverySeconds = 3f;
        [Tooltip("La barra debe volver a esta fracciÃ³n antes de poder esprintar otra vez, para que no titile al vaciarse.")]
        [Range(0f, 1f)] public float sprintUnlockFraction = 0.25f;

        [Header("Look")]
        public float mouseSensitivity = 0.085f;
        public float maxPitch = 85f;

        [Header("Aim Down Sights")]
        [Tooltip("Button to aim (Right Mouse Button)")]
        public KeyCode aimKey = KeyCode.Mouse1;
        [Tooltip("Factor con que se acerca el arma a la cÃ¡mara al apuntar")]
        public float aimDistance = 0.3f;
        [Tooltip("Velocidad con que el arma se acerca/se aleja al entrar/salir del modo punterÃ­a")]
        public float aimLerpSpeed = 10f;

        public Transform Eye => eye != null ? eye : (cam != null ? cam.transform : transform);

        /// <summary>0..1 sprint charge, for the HUD bar.</summary>
        public float SprintFraction => _sprintCharge;

        /// <summary>True while the sprint actually has charge behind it (movement uses this).</summary>
        public bool IsSprinting => _sprint;

        CharacterController _cc;
        Transform _pivot;
        PlayerLoadout _loadout;
        Weapon _weapon;
        Vector2 _moveInput;
        Vector2 _lookInput;
        bool _sprint;
        float _sprintCharge = 1f;
        bool _sprintLocked;
        bool _jumpQueued;
        bool _aiming;

        float _yaw;
        float _aimPitch;
        float _recoilPitch;
        float _recoilYaw;
        float _verticalSpeed;
        Vector3 _horizontalVelocity;
        Vector3 _spawnPoint;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (cam == null) cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                _pivot = cam.transform.parent != null && cam.transform.parent != transform
                    ? cam.transform.parent
                    : cam.transform;
                eye = cam.transform;
            }
            _yaw = transform.eulerAngles.y;
            _spawnPoint = transform.position;
        }

        void Start()
        {
            // Cursor ownership belongs to GameDirector (it knows the run phase); locking here
            // would race with it on scene load.
            if (cam != null) cam.fieldOfView = fovNormal;
        }

        void Update()
        {
            // safety net: never let the player be lost to a hole in the arena
            if (transform.position.y < -8f)
            {
                _cc.enabled = false;
                transform.position = _spawnPoint;
                _cc.enabled = true;
                _verticalSpeed = 0f;
                _horizontalVelocity = Vector3.zero;
            }

            Look();
            PollSprint();
            UpdateSprintFov();
            Move();
            PollWeaponInput();

            // Aim down sights: weapon moves forward
            if (_weapon != null && _weapon.viewmodel != null)
            {
                float targetZ = _aiming ? -aimDistance : 0f;
                _weapon.viewmodel.localPosition = Vector3.Lerp(_weapon.viewmodel.localPosition, new Vector3(_weapon.viewmodel.localPosition.x, _weapon.viewmodel.localPosition.y, targetZ), Time.deltaTime * aimLerpSpeed);
            }
        }

        /// <summary>Sprint widens the lens a little and eases back when it ends, so the speed reads on screen.</summary>
        void UpdateSprintFov()
        {
            if (cam == null) return;

            float target = _sprint ? fovSprint : fovNormal;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, target, Time.deltaTime * fovLerpSpeed);
        }

        void OnMove(InputValue value) => _moveInput = value.Get<Vector2>();
        void OnLook(InputValue value) => _lookInput = value.Get<Vector2>();

        /// <summary>
        /// Sprint is read straight off the devices every frame instead of through an OnSprint message:
        /// the message arrives when shift goes down but not when it comes back up, which left the player
        /// sprinting for the rest of the run after a single tap.
        /// </summary>
        void PollSprint()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;

            bool held = (keyboard != null && keyboard.leftShiftKey.isPressed)
                     || (gamepad != null && gamepad.leftStickButton.isPressed);

            UpdateStamina(held);
        }

        /// <summary>
        /// Sprint draws on a bar: a full bar pays for <see cref="sprintSeconds"/> of running and refills in
        /// <see cref="sprintRecoverySeconds"/> once you let go, so it can never be held forever. A drained
        /// bar locks sprint out until it climbs back to <see cref="sprintUnlockFraction"/>, otherwise
        /// holding shift would stutter between sprint and walk every frame.
        /// </summary>
        void UpdateStamina(bool held)
        {
            if (_sprintLocked && _sprintCharge >= sprintUnlockFraction) _sprintLocked = false;

            bool sprinting = held && !_sprintLocked && _sprintCharge > 0f;

            if (sprinting)
            {
                _sprintCharge = Mathf.Max(0f, _sprintCharge - Time.deltaTime / Mathf.Max(0.01f, sprintSeconds));

                // Latch the lockout on the frame the bar empties, not after it: the refill below would
                // otherwise lift the charge above zero in the same call and shift held would flicker
                // between sprint and walk every frame, which is infinite sprint at half speed.
                if (_sprintCharge <= 0f) _sprintLocked = true;
            }
            else
            {
                _sprintCharge = Mathf.Min(1f, _sprintCharge + Time.deltaTime / Mathf.Max(0.01f, sprintRecoverySeconds));
            }

            _sprint = sprinting;
        }

        // PlayerInput lives on this GameObject with SendMessages, so this is where the weapon input is
        // read. Fire and reload are polled straight off the devices every frame: a latched
        // press/release message pair can desync (a missed release leaves the gun firing forever),
        // polling cannot. Slot keys 1..3 are polled the same way, and everything is routed through
        // the loadout so the fists and any future slot behave exactly like the sidearm.
        void PollWeaponInput()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;

            bool reload = (keyboard != null && keyboard.rKey.wasPressedThisFrame)
                       || (gamepad != null && gamepad.rightShoulder.wasPressedThisFrame);

            bool pressed = (Mouse.current != null && Mouse.current.leftButton.isPressed)
                        || (gamepad != null && (gamepad.buttonWest.isPressed || gamepad.rightTrigger.isPressed));

            bool aiming = (Mouse.current != null && Mouse.current.rightButton.isPressed)
                       || (gamepad != null && gamepad.rightStickButton.isPressed);

            if (_loadout == null) _loadout = GetComponent<PlayerLoadout>();

            if (_loadout == null)
            {
                if (_weapon == null) _weapon = GetComponentInChildren<Weapon>(true);
                if (_weapon == null) return;
                _weapon.SetTrigger(pressed);
                _aiming = aiming;
                if (reload) _weapon.RequestReload();
                return;
            }

            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) _loadout.SelectSlot(1);
                if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) _loadout.SelectSlot(2);
                if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) _loadout.SelectSlot(3);
            }

            if (gamepad != null)
            {
                if (gamepad.dpad.left.wasPressedThisFrame) _loadout.SelectSlot(1);
                if (gamepad.dpad.up.wasPressedThisFrame) _loadout.SelectSlot(2);
                if (gamepad.dpad.right.wasPressedThisFrame) _loadout.SelectSlot(3);
            }

            _loadout.SetTrigger(pressed);
            _aiming = aiming;
            if (reload) _loadout.RequestReload();
        }

        void OnJump(InputValue value)
        {
            if (value.isPressed) _jumpQueued = true;
        }

        public void AddRecoil(float strength)
        {
            _recoilPitch += strength * 4.5f + Random.Range(-0.4f, 0.4f) * strength;
            _recoilYaw += Random.Range(-1f, 1f) * strength * 0.6f;
        }

        /// <summary>
        /// Hard placement used when a streamed map hands the player to another one. Velocity and
        /// aim are cleared so momentum from the previous arena never leaks through the door.
        /// </summary>
        public void Teleport(Vector3 position, float yaw)
        {
            bool wasEnabled = _cc.enabled;
            _cc.enabled = false;

            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            _yaw = yaw;
            _aimPitch = 0f;
            _recoilPitch = 0f;
            _recoilYaw = 0f;
            _verticalSpeed = 0f;
            _horizontalVelocity = Vector3.zero;
            _moveInput = Vector2.zero;
            _lookInput = Vector2.zero;
            _jumpQueued = false;
            _spawnPoint = position;

            if (_pivot != null) _pivot.localRotation = Quaternion.identity;

            _cc.enabled = wasEnabled;
        }

        void Look()
        {
            _yaw += _lookInput.x * mouseSensitivity;
            _aimPitch = Mathf.Clamp(_aimPitch - _lookInput.y * mouseSensitivity, -maxPitch, maxPitch);

            _recoilPitch = Mathf.Lerp(_recoilPitch, 0f, Time.deltaTime * 14f);
            _recoilYaw = Mathf.Lerp(_recoilYaw, 0f, Time.deltaTime * 14f);

            transform.rotation = Quaternion.Euler(0f, _yaw + _recoilYaw, 0f);
            if (_pivot != null)
                _pivot.localRotation = Quaternion.Euler(Mathf.Clamp(_aimPitch + _recoilPitch, -maxPitch, maxPitch), 0f, 0f);
        }

        /// <summary>
        /// Sprint follows the boss: the base speed, lifted just above whatever the boss can do
        /// right now (enrage, weak-point surge, a squad's fastest body). A clean line always opens
        /// a small gap, but the boss is never left far behind on foot.
        /// </summary>
        float SprintSpeed()
        {
            float speed = sprintSpeed;
            var director = GameDirector.Instance;
            if (director != null && director.CurrentBoss != null)
                speed = director.CurrentBoss.ChaseSpeed * escapeMargin;

            // Stay a hair faster than the boss, so a clean line always opens a gap.
            return Mathf.Max(speed + 0.5f, speed);
        }

        void Move()
        {
            Vector3 wish = transform.right * _moveInput.x + transform.forward * _moveInput.y;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            float targetSpeed = (_sprint ? SprintSpeed() : walkSpeed);
            Vector3 targetVel = wish * targetSpeed;
            _horizontalVelocity = Vector3.Lerp(_horizontalVelocity, targetVel, Time.deltaTime * acceleration);

            bool grounded = _cc.isGrounded;
            if (grounded && _verticalSpeed < 0f) _verticalSpeed = -2f;

            if (_jumpQueued && grounded)
            {
                _verticalSpeed = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            _jumpQueued = false;

            _verticalSpeed += gravity * Time.deltaTime;

            Vector3 motion = _horizontalVelocity + Vector3.up * _verticalSpeed;
            _cc.Move(motion * Time.deltaTime);
        }
    }
}
