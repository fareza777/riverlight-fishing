using UnityEngine;
using UnityEngine.InputSystem;

namespace CozyFishing.Core
{
    /// <summary>
    /// Single source of player input. Merges keyboard / gamepad with the on-screen touch controls
    /// (virtual joystick + context button), so gameplay code never cares which one is used.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameInput : MonoBehaviour
    {
        public static GameInput Instance { get; private set; }

        InputAction move;
        InputAction action;
        InputAction cancel;
        InputAction lookKeys;
        InputAction lookStick;
        InputAction mouseDelta;
        InputAction mouseRight;
        float touchLook;
        float touchLookFrame;

        // Written by the touch UI; promoted to per-frame flags in Update (runs before gameplay) so every reader sees them.
        Vector2 touchMove;
        bool touchActionHeld;
        bool pendingPress, pendingRelease, pendingCancel;
        bool touchPressed, touchReleased, touchCancel;

        public Vector2 Move
        {
            get
            {
                Vector2 v = move.ReadValue<Vector2>();
                return v.sqrMagnitude >= touchMove.sqrMagnitude ? v : touchMove;
            }
        }

        public bool ActionHeld => action.IsPressed() || touchActionHeld;
        public bool ActionPressed => action.WasPressedThisFrame() || touchPressed;
        public bool ActionReleased => action.WasReleasedThisFrame() || touchReleased;
        public bool CancelPressed => cancel.WasPressedThisFrame() || touchCancel;
        public bool UsingGamepad { get; private set; }
        public bool UsingTouch { get; private set; }

        /// <summary>Camera yaw input in degrees for this frame (mouse right-drag, Z/C keys, right stick, screen drag).</summary>
        public float LookYaw
        {
            get
            {
                float dt = Time.unscaledDeltaTime;
                float v = lookKeys.ReadValue<float>() * 110f * dt + lookStick.ReadValue<Vector2>().x * 140f * dt;
                if (mouseRight.IsPressed()) v += mouseDelta.ReadValue<Vector2>().x * 0.25f;
                return v + touchLookFrame;
            }
        }

        public void AddTouchLook(float degrees) => touchLook += degrees;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            UsingTouch = Application.isMobilePlatform;

            move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");

            action = new InputAction("Action", InputActionType.Button);
            action.AddBinding("<Keyboard>/space");
            action.AddBinding("<Keyboard>/e");
            action.AddBinding("<Gamepad>/buttonSouth");

            cancel = new InputAction("Cancel", InputActionType.Button);
            cancel.AddBinding("<Keyboard>/escape");
            cancel.AddBinding("<Keyboard>/q");
            cancel.AddBinding("<Gamepad>/buttonEast");

            lookKeys = new InputAction("LookKeys", InputActionType.Value);
            lookKeys.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/z").With("Positive", "<Keyboard>/c");
            lookStick = new InputAction("LookStick", InputActionType.Value, "<Gamepad>/rightStick");
            mouseDelta = new InputAction("MouseDelta", InputActionType.Value, "<Mouse>/delta");
            mouseRight = new InputAction("MouseRight", InputActionType.Button, "<Mouse>/rightButton");

            move.performed += ctx => SetDevice(ctx.control.device);
            action.performed += ctx => SetDevice(ctx.control.device);
        }

        void SetDevice(InputDevice device)
        {
            UsingGamepad = device is Gamepad;
            UsingTouch = false;
        }

        // ------------------------------------------------------------ touch UI hooks
        public void SetTouchMove(Vector2 value)
        {
            touchMove = Vector2.ClampMagnitude(value, 1f);
            if (value.sqrMagnitude > 0.01f) UsingTouch = true;
        }

        public void SetTouchAction(bool down)
        {
            if (down == touchActionHeld) return;
            touchActionHeld = down;
            if (down) pendingPress = true;
            else pendingRelease = true;
            UsingTouch = true;
        }

        public void PressTouchCancel() => pendingCancel = true;

        void Update()
        {
            // Drags accumulated since last frame become this frame's look input for every reader.
            touchLookFrame = touchLook;
            touchLook = 0f;
            touchPressed = pendingPress;
            touchReleased = pendingRelease;
            touchCancel = pendingCancel;
            pendingPress = pendingRelease = pendingCancel = false;
        }



        void OnEnable()
        {
            move?.Enable();
            action?.Enable();
            cancel?.Enable();
            lookKeys?.Enable(); lookStick?.Enable(); mouseDelta?.Enable(); mouseRight?.Enable();
        }

        void OnDisable()
        {
            move?.Disable();
            action?.Disable();
            cancel?.Disable();
            lookKeys?.Disable(); lookStick?.Disable(); mouseDelta?.Disable(); mouseRight?.Disable();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            move?.Dispose();
            action?.Dispose();
            cancel?.Dispose();
            lookKeys?.Dispose(); lookStick?.Dispose(); mouseDelta?.Dispose(); mouseRight?.Dispose();
        }
    }
}
