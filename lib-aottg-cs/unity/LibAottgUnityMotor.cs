// SPDX-License-Identifier: MIT
// Import LibAottg.Core.dll and the HERO asset rig into the host Unity project.
using System;
using System.Collections.Generic;
using LibAottg;
using UnityEngine;

namespace LibAottg.Unity
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class LibAottgUnityMotor : MonoBehaviour, IHookGeometryWorld
    {
        public Camera AimCamera;
        public Animation RigAnimation;
        // Import assets/audio/hook-launch.wav and assign it to this optional source.
        public AudioSource HookAudio;
        public Transform LeftBladeMuzzle, RightBladeMuzzle, LeftGunMuzzle, RightGunMuzzle;
        public LayerMask HookMask = ~0, ProjectileMask = ~0, GroundMask = ~0;
        public CharacterConfig Tuning = new CharacterConfig();
        public KeyCode BurstKey = KeyCode.LeftControl, DodgeKey = KeyCode.LeftAlt;
        public event Action<CharacterEvent> CharacterEventRaised;
        public CharacterMotor Motor { get; private set; }
        private Rigidbody _body;
        private CharacterInput _input;
        private int _animationRevision;
        private bool _hookSoundPending;
        private readonly Dictionary<int, Transform> _targets = new Dictionary<int, Transform>();

        private void Awake()
        {
            Motor = new CharacterMotor(Tuning);
            transform.localScale = Vector3.one * Motor.Scale;
            var capsule = GetComponent<CapsuleCollider>();
            capsule.direction = 1; capsule.radius = 0.5f; capsule.height = 1.7f;
            capsule.center = new Vector3(0, 0.85f, 0);
            _body = GetComponent<Rigidbody>();
            _body.useGravity = false; _body.mass = Tuning.Mass; _body.drag = 0;
            _body.constraints |= RigidbodyConstraints.FreezeRotation;
            _body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            Motor.SetPose(ToCore(_body.position), ToCore(_body.velocity), transform.eulerAngles.y);
            if (RigAnimation == null) RigAnimation = GetComponentInChildren<Animation>();
        }
        private static Button Key(KeyCode key)
        { return new Button(Input.GetKey(key), Input.GetKeyDown(key), Input.GetKeyUp(key)); }
        private static Button Mouse(int button)
        { return new Button(Input.GetMouseButton(button), Input.GetMouseButtonDown(button), Input.GetMouseButtonUp(button)); }
        private void Update()
        {
            Camera camera = AimCamera != null ? AimCamera : Camera.main;
            Ray aim = camera != null ? camera.ScreenPointToRay(Input.mousePosition) : new Ray(transform.position, transform.forward);
            _input = new CharacterInput {
                MoveX = Input.GetAxisRaw("Horizontal"), MoveY = Input.GetAxisRaw("Vertical"),
                LookDirection = ToCore(camera != null ? camera.transform.forward : transform.forward),
                HasAimOrigin = true, AimOrigin = ToCore(aim.origin), AimDirection = ToCore(aim.direction),
                Jump = Key(KeyCode.LeftShift), Dash = Key(BurstKey), Dodge = Key(DodgeKey),
                HookLeft = Key(KeyCode.Q), HookRight = Key(KeyCode.E), HookBoth = Key(KeyCode.Space),
                Attack = Mouse(0), Skill = Mouse(1), Reload = Key(KeyCode.R), Supply = Key(KeyCode.F), Flare = Key(KeyCode.Alpha1),
                Forward = Key(KeyCode.W), Back = Key(KeyCode.S), Left = Key(KeyCode.A), Right = Key(KeyCode.D),
                AttackLeft = Input.mousePosition.x < Screen.width * 0.5f,
                Reel = Input.GetAxis("Mouse ScrollWheel") * 10
            };
            Motor.SetPose(ToCore(_body.position), ToCore(_body.velocity), _body.rotation.eulerAngles.y);
            if (Time.deltaTime > 0) Motor.AdvanceFrame(Time.deltaTime, _input, Contacts(), this);
            if (_hookSoundPending)
            {
                _hookSoundPending = false;
                if (HookAudio != null) { HookAudio.Stop(); HookAudio.Play(); }
            }
            ApplyAnimation();
            if (camera != null) camera.fieldOfView = Motor.FieldOfView;
        }
        private void FixedUpdate()
        {
            Motor.SetPose(ToCore(_body.position), ToCore(_body.velocity), _body.rotation.eulerAngles.y);
            Motor.StepPhysics(Time.fixedDeltaTime, _input, Contacts(), this);
            // The core integrates forces. Unity resolves contacts and integrates position exactly once.
            _body.velocity = ToUnity(Motor.Velocity);
            _body.MoveRotation(Quaternion.Euler(0, Motor.FacingDegrees, 0));
        }
        private CharacterContacts Contacts()
        {
            float scale = Motor.Scale;
            Vector3 feet = _body.position;
            Vector3 forward = Quaternion.Euler(0, _body.rotation.eulerAngles.y, 0) * Vector3.forward;
            return new CharacterContacts {
                Grounded = Physics.Raycast(feet + Vector3.up * (0.1f * scale), Vector3.down, 0.3f * scale, GroundMask),
                GroundNormal = Vec3.Up,
                FrontWall = Physics.Raycast(feet + Vector3.up * scale, forward, scale, GroundMask),
                UpperFrontClear = !Physics.Raycast(feet + Vector3.up * (3 * scale), forward, 1.2f * scale, GroundMask)
            };
        }
        private void ApplyAnimation()
        {
            if (RigAnimation == null) return;
            var animation = Motor.Animation;
            var state = RigAnimation[animation.Clip];
            if (state == null) return;
            if (_animationRevision != animation.Revision)
            { RigAnimation.CrossFade(animation.Clip, animation.CrossFade); _animationRevision = animation.Revision; }
            state.speed = 0; state.time = animation.Time;
            state.wrapMode = animation.Loop ? WrapMode.Loop : WrapMode.ClampForever;
            RigAnimation.Sample();
        }
        public Vec3 HookOrigin(CharacterMotor motor, HookSide side)
        {
            Transform muzzle = motor.Equipment == EquipmentMode.Guns ? (side == HookSide.Left ? LeftGunMuzzle : RightGunMuzzle) :
                (side == HookSide.Left ? LeftBladeMuzzle : RightBladeMuzzle);
            return muzzle != null ? ToCore(muzzle.position) : motor.Position + Vec3.Up * (0.85f * motor.Scale);
        }
        private HookHit Hit(RaycastHit rayHit)
        {
            Transform target = rayHit.collider.transform;
            int id = target.GetInstanceID(); _targets[id] = target;
            return new HookHit(ToCore(rayHit.point)) { TargetId = id, LocalPoint = ToCore(target.InverseTransformPoint(rayHit.point)) };
        }
        public bool TryHook(Vec3 origin, Vec3 direction, float distance, HookSide side, out HookHit hit)
        {
            RaycastHit rayHit;
            bool found = Physics.Raycast(ToUnity(origin), ToUnity(direction), out rayHit, distance, HookMask);
            hit = found ? Hit(rayHit) : default(HookHit); return found;
        }
        public bool SweepHook(Vec3 start, Vec3 end, HookSide side, out HookHit hit)
        {
            RaycastHit rayHit;
            bool found = Physics.Linecast(ToUnity(start), ToUnity(end), out rayHit, ProjectileMask);
            hit = found ? Hit(rayHit) : default(HookHit); return found;
        }
        public bool ResolveHookAnchor(int targetId, Vec3 localPoint, out Vec3 point)
        {
            Transform target;
            if (!_targets.TryGetValue(targetId, out target) || target == null) { _targets.Remove(targetId); point = Vec3.Zero; return false; }
            point = ToCore(target.TransformPoint(ToUnity(localPoint))); return true;
        }
        public void OnCharacterEvent(CharacterEvent kind)
        {
            if (kind == CharacterEvent.HookLaunchedLeft || kind == CharacterEvent.HookLaunchedRight) _hookSoundPending = true;
            var handler = CharacterEventRaised; if (handler != null) handler(kind);
        }
        public static Vec3 ToCore(Vector3 v) { return new Vec3(v.x, v.y, v.z); }
        public static Vector3 ToUnity(Vec3 v) { return new Vector3(v.X, v.Y, v.Z); }
    }
}
