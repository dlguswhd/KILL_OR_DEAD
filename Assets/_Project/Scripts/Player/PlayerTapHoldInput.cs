/*
작성자: 이준호
이 코드는 플레이어가 버튼을 누르고 있던 시간을 기반으로 입력을 탭·홀드·취소로 판정하고, 그 결과만 이벤트로 전달하는 역할을 담당한다.
- 누르는 순간 Press 이벤트를 내보낸다(누름 인식용 — 예: 홀드 게이지 UI 시작, 탭 동작만 대상의 즉시 실행)
- 누른 시간 < tapHoldBoundaryTime → 떼면 탭
- tapHoldBoundaryTime 통과 후, 다시 holdRequiredTime만큼 더 누르면 → 홀드 발동(떼지 않아도)
- 그 사이(경계 통과 ~ 홀드 발동 전)에 떼면 → 취소
입력이 잠기면(InputEnabled=false 또는 액션 비활성) 진행 중인 판정은 이벤트 없이 취소된다.
실제 동작(상호작용 등) 실행은 이 이벤트를 받는 쪽이 담당한다.
*/
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KillOrDead.Player
{
    public class PlayerTapHoldInput : MonoBehaviour
    {
        [Tooltip("탭/홀드를 판정할 입력 액션 이름. 벤더 애셋은 고치지 않고 런타임에 찾는다.")]
        [SerializeField] private string actionName = "Interact";

        [Header("판정 시간")]
        [Tooltip("이 시간 미만 눌렀다 떼면 탭, 이상이면 홀드 판정 구간으로 본다. 플레이테스트 후 조정한다.")]
        [SerializeField, Min(0.01f)] private float tapHoldBoundaryTime = 0.3f;

        [Tooltip("탭/홀드 경계를 통과한 뒤 추가로 이 시간을 채우면 홀드가 발동한다. 키를 떼지 않아도 되고, 동작별로 조절한다.")]
        [SerializeField, Min(0.01f)] private float holdRequiredTime = 1.0f;

        [Tooltip("탭/홀드/취소가 날 때마다 콘솔에 찍어 확인한다.")]
        [SerializeField] private bool logEvents;

        private PlayerInput _playerInput;
        private InputAction _action;

        private float _pressTime;
        private bool _holdFired;
        private bool _inputEnabled = true;
        private Coroutine _holdRoutine;

        // 외부(상호작용 포인트 등)가 홀드 요구 시간을 덮어쓸 때 쓴다. null이면 인스펙터 기본값을 쓴다.
        private float? _holdRequiredTimeOverride;

        // 이 컴포넌트의 입력 판정을 켜고 끄는 스위치. 외부 시스템이 잠금/해제한다.
        // 꺼지는 순간 진행 중인 누름/홀드 판정을 이벤트 없이 취소한다.
        public bool InputEnabled
        {
            get => _inputEnabled;
            set
            {
                if (_inputEnabled == value) return;
                _inputEnabled = value;

                if (!value && IsKeyHeld)
                {
                    Log("Forced cancel (input disabled)");
                    CancelCurrentInput();
                }
            }
        }

        // 지금 키를 누르고 있는지 판단하는 변수.
        public bool IsKeyHeld { get; private set; }

        // 지금까지 키를 누르고 있던 시간(초). 안 누르고 있으면 0. 요청 시 계산한다.
        public float KeyHeldTime => IsKeyHeld ? Time.time - _pressTime : 0f;

        // 이 입력에 적용할 홀드 요구 시간(초). 기본은 인스펙터의 holdRequiredTime이고,
        // 상호작용 포인트처럼 동작별로 다른 값이 필요한 쪽이 설정하면 그 값이 우선한다.
        public float HoldRequiredTime
        {
            get => _holdRequiredTimeOverride ?? holdRequiredTime;
            set => _holdRequiredTimeOverride = Mathf.Max(0.01f, value);
        }

        // 인스펙터에 설정된 기본 홀드 요구 시간(초).
        public float DefaultHoldRequiredTime => holdRequiredTime;

        // 외부에서 덮어쓴 값을 지우고 인스펙터 기본값으로 되돌린다.
        public void ResetHoldRequiredTime()
        {
            _holdRequiredTimeOverride = null;
        }

        // 키를 누르기 시작한 순간을 알린다. 누름을 인식해야 하는 곳(홀드 게이지 UI 시작, 탭 동작만 대상의 즉시 실행 등)에서 쓴다. 진행 중 시간은 KeyHeldTime으로 읽는다.
        public event Action Press;

        // 짧게 눌렀다 뗐을 때(판정 시간 미만)
        public event Action Tap;

        // 홀드 요구 시간에 도달했을 때. 키를 계속 누른 상태에서 발동.
        public event Action Hold;

        // 홀드로 판정됐지만 요구 시간 전에 뗐을 때(홀드 동작이지만 중단 판정)
        public event Action Cancelled;

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();

            if (_playerInput == null || _playerInput.actions == null)
            {
                Debug.LogWarning($"PlayerTapHoldInput: PlayerInput을 찾지 못해 '{actionName}' 액션을 읽을 수 없습니다.", this);
                return;
            }

            _action = _playerInput.actions.FindAction(actionName);
            if (_action == null)
            {
                Debug.LogWarning($"PlayerTapHoldInput: '{actionName}' 액션을 찾지 못했습니다.", this);
            }
        }

        private void OnEnable()
        {
            if (_action == null) return;

            _action.started += OnActionStarted;
            _action.canceled += OnActionCanceled;
        }

        private void OnDisable()
        {
            if (_action != null)
            {
                _action.started -= OnActionStarted;
                _action.canceled -= OnActionCanceled;
            }

            CancelCurrentInput();
        }

        // 누름 시작: 시각을 기록하고 홀드 타이머를 건다.
        private void OnActionStarted(InputAction.CallbackContext context)
        {
            if (!_inputEnabled) return;

            _pressTime = Time.time;
            _holdFired = false;
            IsKeyHeld = true;

            Log("Press");
            Press?.Invoke();

            if (_holdRoutine != null) StopCoroutine(_holdRoutine);
            _holdRoutine = StartCoroutine(HoldRoutine());
        }

        // 뗌: 홀드가 이미 발동했으면 종료만 하고, 아니면 탭/취소로 분기한다.
        private void OnActionCanceled(InputAction.CallbackContext context)
        {
            if (!IsKeyHeld) return;

            // 잠금/액션 비활성에 의한 강제 취소는 탭·홀드·취소 어느 이벤트도 내보내지 않는다.
            if (!_inputEnabled || !_action.enabled)
            {
                Log("Forced cancel (action disabled)");
                CancelCurrentInput();
                return;
            }

            EndPress();
        }

        // 홀드 발동 전담. 경계 시간을 통과한 뒤 요구 시간에 도달하면 키를 떼지 않아도 발동한다.
        private IEnumerator HoldRoutine()
        {
            yield return new WaitForSeconds(tapHoldBoundaryTime + HoldRequiredTime);

            if (!IsKeyHeld || _holdFired) yield break;

            _holdFired = true;
            Log("Hold");
            Hold?.Invoke();
        }

        private void EndPress()
        {
            if (!_holdFired)
            {
                float heldTime = Time.time - _pressTime;

                // 탭: 유지 시간이 경계 미만
                if (heldTime < tapHoldBoundaryTime)
                {
                    Log($"Tap ({heldTime:0.00}s)");
                    Tap?.Invoke();
                }
                // 취소: 경계를 넘겼지만 홀드 요구 시간 전에 뗌
                else
                {
                    Log($"Cancelled ({heldTime:0.00}s)");
                    Cancelled?.Invoke();
                }
            }

            CancelCurrentInput();
        }

        // 진행 중인 판정을 이벤트 없이 정리한다. 여러 번 불려도 안전하다(멱등).
        private void CancelCurrentInput()
        {
            if (_holdRoutine != null)
            {
                StopCoroutine(_holdRoutine);
                _holdRoutine = null;
            }

            ResetState();
        }

        // 누름 상태를 초기값으로 되돌린다.(관련 변수 초기화) 탭/홀드/취소로 판정이 끝났거나 강제 취소된 뒤 다음 입력을 새로 받을 수 있게 하기 위함.
        private void ResetState()
        {
            IsKeyHeld = false;
            _holdFired = false;
        }

        private void Log(string message)
        {
            if (logEvents) Debug.Log($"PlayerTapHoldInput: {message}", this);
        }
    }
}
