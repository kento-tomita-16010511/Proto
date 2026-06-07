using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

public class Player : MonoBehaviour
{
    [Tooltip("移動速度（m/s）")]
    public float speed = 5f;

    [Tooltip("移動をプレイヤーの向きに合わせるための参照（未設定時はこの GameObject の Transform を使用）")]
    public Transform orientation;

    Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (orientation == null) orientation = transform;
    }

    void Update()
    {
        // Rigidbody がアタッチされていない場合は Transform を直接移動
        if (rb == null)
        {
            Vector3 input = GetInput();
            if (input.sqrMagnitude > 0f)
            {
                // カメラや向きの参照から、水平方向のみのベクトルを取り出します
                Vector3 forward = orientation.forward;
                Vector3 right = orientation.right;
                forward.y = 0f;
                right.y = 0f;
                forward.Normalize();
                right.Normalize();

                Vector3 move = (forward * input.z + right * input.x).normalized;
                transform.Translate(move * speed * Time.deltaTime, Space.World);
            }
        }
    }

    void FixedUpdate()
    {
        // Rigidbody があれば物理移動
        if (rb != null)
        {
            Vector3 input = GetInput();
            if (input.sqrMagnitude > 0f)
            {
                // リジッドボディがある場合も同様に、水平方向の移動を計算します
                Vector3 forward = orientation.forward;
                Vector3 right = orientation.right;
                forward.y = 0f;
                right.y = 0f;
                forward.Normalize();
                right.Normalize();

                Vector3 move = (forward * input.z + right * input.x).normalized;
                Vector3 target = rb.position + move * speed * Time.fixedDeltaTime;
                rb.MovePosition(target);
            }
        }
    }

    // WASD / 矢印キー に対応した入力を返す
    Vector3 GetInput()
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
        if (Gamepad.current != null)
        {
            move += Gamepad.current.leftStick.ReadValue();
        }
        Vector3 dirNew = new Vector3(move.x, 0f, move.y);
        if (dirNew.sqrMagnitude > 1f) dirNew.Normalize();
        return dirNew;
#else
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 dir = new Vector3(h, 0f, v);
        if (dir.sqrMagnitude > 1f) dir.Normalize();
        return dir;
#endif
    }
}
