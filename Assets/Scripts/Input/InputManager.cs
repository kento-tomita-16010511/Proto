using UniRx;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    public bool IsEnabled { get; set; } = false;
    public ReactiveProperty<bool> IsEscapePressed { get; private set; } = new ReactiveProperty<bool>(false);

    public Vector3 MoveInput { get; private set; }
    public Vector2 LookDelta { get; private set; }
    public bool AttackPressedThisFrame { get; private set; }
    public bool NetPressedThisFrame { get; private set; }
    public Vector2 MouseScreenPosition { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        // マウス座標はIsEnabledに関わらず常に更新（Raycast用）
        MouseScreenPosition = ReadMouseScreenPosition();
        OnEscapePressed();

        if (!IsEnabled)
        {
            MoveInput = Vector3.zero;
            LookDelta = Vector2.zero;
            AttackPressedThisFrame = false;
            NetPressedThisFrame = false;
            return;
        }

        MoveInput = ReadMove();
        LookDelta = ReadLook();
        AttackPressedThisFrame = ReadAttack();
        NetPressedThisFrame = ReadNet();
    }

    private void OnEscapePressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            IsEscapePressed.Value = !IsEscapePressed.Value;
        }
#else
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            IsEscapePressed.Value = !IsEscapePressed.Value;
        }
#endif
    }

    private Vector3 ReadMove()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Vector2 move = Vector2.zero;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) move.x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) move.x += 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) move.y += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) move.y -= 1f;
        }
        if (Gamepad.current != null) move += Gamepad.current.leftStick.ReadValue();
        var dir = new Vector3(move.x, 0f, move.y);
        if (dir.sqrMagnitude > 1f) dir.Normalize();
        return dir;
#else
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        var dir = new Vector3(h, 0f, v);
        if (dir.sqrMagnitude > 1f) dir.Normalize();
        return dir;
#endif
    }

    private Vector2 ReadLook()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (Mouse.current != null) return Mouse.current.delta.ReadValue();
        if (Pointer.current != null) return Pointer.current.delta.ReadValue();
        return Vector2.zero;
#else
        return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
#endif
    }

    private bool ReadAttack()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        bool mouseClick = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool gamepadSouth = Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
        return mouseClick || gamepadSouth;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    private bool ReadNet()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        bool mouseClick = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        bool gamepadWest = Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame;
        return mouseClick || gamepadWest;
#else
        return Input.GetMouseButtonDown(1);
#endif
    }

    private Vector2 ReadMouseScreenPosition()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current?.position.ReadValue() ?? Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }
}
