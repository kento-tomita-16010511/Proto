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
    // 右クリック=グラップル。押した瞬間に発射、押している間は接続維持、離すと切断。
    public bool GrapplePressedThisFrame { get; private set; }
    public bool GrappleHeld { get; private set; }
    // 右クリック=Net（蜘蛛の巣）。押した瞬間に射出する。
    public bool NetPressedThisFrame { get; private set; }
    public bool JumpPressedThisFrame { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // 毎フレームの入力ポーリングは Update を使わず EveryUpdate で行う（CLAUDE.md 規約）
        Observable.EveryUpdate()
            .Subscribe(_ => Tick())
            .AddTo(this);
    }

    /// <summary>毎フレームの入力取得処理。EveryUpdate から呼ばれる。</summary>
    private void Tick()
    {
        if(enabled == false) return; // InputManager 自体が無効化されている場合は何もしない

        OnEscapePressed();

        if (!IsEnabled)
        {
            MoveInput = Vector3.zero;
            LookDelta = Vector2.zero;
            AttackPressedThisFrame = false;
            GrapplePressedThisFrame = false;
            GrappleHeld = false;
            NetPressedThisFrame = false;
            JumpPressedThisFrame = false;
            return;
        }

        MoveInput = ReadMove();
        LookDelta = ReadLook();
        AttackPressedThisFrame = ReadAttack();
        GrapplePressedThisFrame = ReadGrapplePressed();
        GrappleHeld = ReadGrappleHeld();
        NetPressedThisFrame = ReadNetPressed();
        JumpPressedThisFrame = ReadJump();
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

    /// <summary>グラップル発射（右クリック / ゲームパッド西ボタンを押した瞬間）。</summary>
    private bool ReadGrapplePressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        bool mouseClick = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        bool gamepadWest = Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame;
        return mouseClick || gamepadWest;
#else
        return Input.GetMouseButtonDown(1);
#endif
    }

    /// <summary>グラップル接続維持（右クリック / ゲームパッド西ボタンを押している間）。離すと切断。</summary>
    private bool ReadGrappleHeld()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        bool mouseHeld = Mouse.current != null && Mouse.current.rightButton.isPressed;
        bool gamepadWest = Gamepad.current != null && Gamepad.current.buttonWest.isPressed;
        return mouseHeld || gamepadWest;
#else
        return Input.GetMouseButton(1);
#endif
    }

    /// <summary>Net 射出（右クリック / ゲームパッド西ボタンを押した瞬間）。</summary>
    private bool ReadNetPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        bool mouseClick = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        bool gamepadWest = Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame;
        return mouseClick || gamepadWest;
#else
        return Input.GetMouseButtonDown(1);
#endif
    }

    private bool ReadJump()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        bool key = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        bool gamepadNorth = Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame;
        return key || gamepadNorth;
#else
        return Input.GetKeyDown(KeyCode.Space);
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
