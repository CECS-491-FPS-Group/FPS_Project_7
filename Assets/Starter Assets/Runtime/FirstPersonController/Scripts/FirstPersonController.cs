using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
	public class FirstPersonController : MonoBehaviour
	{
		[Header("Player")]
		[Tooltip("Move speed of the character in m/s")]
		public float MoveSpeed = 4.0f;
		[Tooltip("Sprint speed of the character in m/s")]
		public float SprintSpeed = 6.0f;
        [Tooltip("Crouch speed of the character in m/s")]
        public float CrouchSpeed = 1.0f;
        [Tooltip("Rotation speed of the character")]
		public float RotationSpeed = 1.0f;
		[Tooltip("Acceleration and deceleration")]
		public float SpeedChangeRate = 10.0f;

        [Space(10)]
        public float JumpHeight = 1.2f;
        public float Gravity = -15.0f;

        [Space(10)]
        public float JumpTimeout = 0.1f;
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]
        public bool Grounded = true;
        public float GroundedOffset = -0.14f;
        public float GroundedRadius = 0.5f;
        public LayerMask GroundLayers;

        [Header("Cinemachine")]
        public GameObject CinemachineCameraTarget;
        public float TopClamp = 90.0f;
        public float BottomClamp = -90.0f;

        private float _cinemachineTargetPitch;
        private float _speed;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;

		// timeout deltatime
		private float _jumpTimeoutDelta;
		private float _fallTimeoutDelta;

		private float m_FieldOfView; 
		private float old_FieldOfView;
        private Vector3 old_Scale;
        private GameObject player_object;


#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
        private PlayerInput _playerInput;
#endif

        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private GameObject _mainCamera;
        private PlayerBoosts _playerBoosts;

        private const float _threshold = 0.01f;

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
                return false;
#endif
            }
        }

        private void Awake()
        {
            if (_mainCamera == null)
            {
                _mainCamera =
                    GameObject.FindGameObjectWithTag("MainCamera");
            }

            _playerBoosts =
                GetComponent<PlayerBoosts>();
        }

        private void Start()
        {
            _controller =
                GetComponent<CharacterController>();

            _input =
                GetComponent<StarterAssetsInputs>();

#if ENABLE_INPUT_SYSTEM
            _playerInput =
                GetComponent<PlayerInput>();
#else
            Debug.LogError(
                "Starter Assets package is missing dependencies."
            );
#endif

			// reset our timeouts on start
			_jumpTimeoutDelta = JumpTimeout;
			_fallTimeoutDelta = FallTimeout;

			m_FieldOfView = 60.0f;
            old_FieldOfView = Camera.main.fieldOfView;
			player_object = GameObject.FindWithTag("Player");
        }

		private void Update()
		{
			JumpAndGravity();
			GroundedCheck();
			Move();
			SprintFOV();
			CrouchScale();
        }

        private void LateUpdate()
        {
            CameraRotation();
        }

        private void GroundedCheck()
        {
            Vector3 spherePosition =
                new Vector3(
                    transform.position.x,
                    transform.position.y - GroundedOffset,
                    transform.position.z
                );

            Grounded =
                Physics.CheckSphere(
                    spherePosition,
                    GroundedRadius,
                    GroundLayers,
                    QueryTriggerInteraction.Ignore
                );
        }

        private void CameraRotation()
        {
            if (_input.look.sqrMagnitude >= _threshold)
            {
                float deltaTimeMultiplier =
                    IsCurrentDeviceMouse
                        ? 1.0f
                        : Time.deltaTime;

                _cinemachineTargetPitch +=
                    _input.look.y *
                    RotationSpeed *
                    deltaTimeMultiplier;

                _rotationVelocity =
                    _input.look.x *
                    RotationSpeed *
                    deltaTimeMultiplier;

                _cinemachineTargetPitch =
                    ClampAngle(
                        _cinemachineTargetPitch,
                        BottomClamp,
                        TopClamp
                    );

                CinemachineCameraTarget.transform.localRotation =
                    Quaternion.Euler(
                        _cinemachineTargetPitch,
                        0.0f,
                        0.0f
                    );

                transform.Rotate(
                    Vector3.up *
                    _rotationVelocity
                );
            }
        }

		private void Move()
		{
			// set target speed based on move speed, sprint speed and if sprint is pressed
			float targetSpeed = _input.sprint ? SprintSpeed: _input.crouch ? CrouchSpeed : MoveSpeed;

			// a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

			// note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is no input, set the target speed to 0
			if (_input.move == Vector2.zero) targetSpeed = 0.0f;

			// a reference to the players current horizontal velocity
			float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

            float speedOffset = 0.1f;

            float inputMagnitude =
                _input.analogMovement
                    ? _input.move.magnitude
                    : 1f;

            if (
                currentHorizontalSpeed <
                    targetSpeed - speedOffset ||
                currentHorizontalSpeed >
                    targetSpeed + speedOffset
            )
            {
                _speed =
                    Mathf.Lerp(
                        currentHorizontalSpeed,
                        targetSpeed * inputMagnitude,
                        Time.deltaTime *
                        SpeedChangeRate
                    );

                _speed =
                    Mathf.Round(
                        _speed * 1000f
                    ) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            Vector3 inputDirection =
                new Vector3(
                    _input.move.x,
                    0.0f,
                    _input.move.y
                ).normalized;

            if (_input.move != Vector2.zero)
            {
                inputDirection =
                    transform.right * _input.move.x +
                    transform.forward * _input.move.y;
            }

            _controller.Move(
                inputDirection.normalized *
                (_speed * Time.deltaTime) +
                new Vector3(
                    0.0f,
                    _verticalVelocity,
                    0.0f
                ) *
                Time.deltaTime
            );
        }

        private void JumpAndGravity()
        {
            if (Grounded)
            {
                _fallTimeoutDelta = FallTimeout;

                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                if (
                    _input.jump &&
                    _jumpTimeoutDelta <= 0.0f
                )
                {
                    float currentJumpHeight =
                        JumpHeight;

                    if (_playerBoosts != null)
                    {
                        currentJumpHeight *=
                            _playerBoosts.JumpMultiplier;
                    }

                    _verticalVelocity =
                        Mathf.Sqrt(
                            currentJumpHeight *
                            -2f *
                            Gravity
                        );
                }

                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -=
                        Time.deltaTime;
                }
            }
            else
            {
                _jumpTimeoutDelta = JumpTimeout;

                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -=
                        Time.deltaTime;
                }

                _input.jump = false;
            }

			// apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
			if (_verticalVelocity < _terminalVelocity)
			{
				_verticalVelocity += Gravity * Time.deltaTime;
			}
		}

		private void SprintFOV()
		{
			if (_input.sprint == true)
			{
				Camera.main.fieldOfView = m_FieldOfView;
			}
			else if (_input.sprint == false)
			{
				Camera.main.fieldOfView = old_FieldOfView;
			}
		}

		private void CrouchScale()
		{
			if (_input.crouch == true)
			{
				player_object.transform.localScale = new Vector3(1f,0.4f,1f);
            }
            else if (_input.crouch == false)
            {
                player_object.transform.localScale = Vector3.one;
            }
        }

        private static float ClampAngle(
            float angle,
            float min,
            float max
        )
        {
            if (angle < -360f)
                angle += 360f;

            if (angle > 360f)
                angle -= 360f;

            return Mathf.Clamp(angle, min, max);
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen =
                new Color(
                    0.0f,
                    1.0f,
                    0.0f,
                    0.35f
                );

            Color transparentRed =
                new Color(
                    1.0f,
                    0.0f,
                    0.0f,
                    0.35f
                );

            Gizmos.color =
                Grounded
                    ? transparentGreen
                    : transparentRed;

            Gizmos.DrawSphere(
                new Vector3(
                    transform.position.x,
                    transform.position.y - GroundedOffset,
                    transform.position.z
                ),
                GroundedRadius
            );
        }
    }
}