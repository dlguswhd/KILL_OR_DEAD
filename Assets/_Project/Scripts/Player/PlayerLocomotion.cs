using KINEMATION.TacticalShooterPack.Scripts.Animation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KillOrDead.Player
{
    [RequireComponent(typeof(CharacterController))]
    [AddComponentMenu("KILL OR DEAD/Player/Player Locomotion")]
    public class PlayerLocomotion : MonoBehaviour
    {
        [Header("Look")]
        [Tooltip("Beyond this many degrees of camera-glance offset, the body starts turning to catch up.")]
        [SerializeField] private float freeGlanceRange = 45f;
        [Tooltip("How fast (deg/sec) the body turns to absorb glance offset beyond freeGlanceRange.")]
        [SerializeField] private float bodyTurnSpeed = 720f;

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 2.5f;
        [SerializeField] private float sprintSpeed = 5.5f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -20f;

        private CharacterController _controller;
        private TacticalProceduralAnimation _proceduralAnimation;
        private Camera _camera;
        private float _verticalVelocity;

        public bool InputEnabled { get; set; } = true;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _proceduralAnimation = GetComponentInChildren<TacticalProceduralAnimation>();
            _camera = GetComponentInChildren<Camera>();
        }

        private void Update()
        {
            if (!InputEnabled) return;

            RotateWithMouse();
            Move();
        }

        private void RotateWithMouse()
        {
            // Kinemation's own yawInput drives a ±90° cosmetic "glance" offset between the body root
            // and the camera (see FPSCameraAnimator: camera.rotation = root.rotation * yawInput...).
            // Rather than reading the mouse ourselves (which would double-count the same delta that
            // TacticalShooterPlayer already feeds into yawInput), we hand off whatever glance exceeds
            // a comfortable range into real body rotation, subtracting exactly what we add. Since both
            // are pure yaw (Y-axis) rotations, root.rotation * yawInput stays mathematically identical
            // before and after the handoff, so the camera never jumps or gets pulled back - it just
            // keeps turning smoothly while the body catches up underneath it.
            if (_proceduralAnimation == null) return;

            float yaw = _proceduralAnimation.yawInput;
            float excess = Mathf.Abs(yaw) - freeGlanceRange;
            if (excess <= 0f) return;

            float maxStep = bodyTurnSpeed * Time.deltaTime;
            float turnStep = Mathf.Sign(yaw) * Mathf.Min(excess, maxStep);

            transform.Rotate(Vector3.up, turnStep);
            _proceduralAnimation.yawInput -= turnStep;
        }

        private void Move()
        {
            Vector2 input = _proceduralAnimation != null ? _proceduralAnimation.moveInput : Vector2.zero;
            input = Vector2.ClampMagnitude(input, 1f);

            bool sprinting = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed && input.y > 0.1f;
            float speed = sprinting ? sprintSpeed : walkSpeed;

            // Move relative to where the camera is actually looking, not the body root, so walking
            // always tracks the view direction even while any residual look offset settles out.
            Transform facing = _camera != null ? _camera.transform : transform;
            Vector3 flatForward = Vector3.ProjectOnPlane(facing.forward, Vector3.up).normalized;
            Vector3 flatRight = Vector3.ProjectOnPlane(facing.right, Vector3.up).normalized;

            Vector3 moveDir = flatRight * input.x + flatForward * input.y;

            if (_controller.isGrounded)
            {
                _verticalVelocity = -1f;

                if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }
            else
            {
                _verticalVelocity += gravity * Time.deltaTime;
            }

            Vector3 velocity = moveDir * speed + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }
    }
}
