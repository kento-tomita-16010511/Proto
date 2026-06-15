using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

public class CameraController : MonoBehaviour
{
    [Tooltip("カメラの感度")]
    public float mouseSensitivity = 10f;
    [Tooltip("カメラが回転させる対象（プレイヤーの Transform）")]
    public Transform playerBody;
    [Tooltip("上下回転の最小角度（度）")]
    public float minPitch = -80f;
    [Tooltip("上下回転の最大角度（度）")]
    public float maxPitch = 80f;
    [Tooltip("マウスY軸を反転する")]
    public bool invertY = false;
    [Tooltip("カーソルをロックする")]
    public bool lockCursor = true;

    float pitch = 0f; // 上下回転（カメラ）

    void Start()
    {
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        if (playerBody == null && transform.parent != null)
        {
            playerBody = transform.parent;
        }
    }

    void Update()
    {
        if (!(InputManager.Instance?.IsEnabled ?? false)) return;

        Vector2 delta = GetMouseDelta();
        if (delta == Vector2.zero) return;

        // マウスのデルタ値に Time.deltaTime を掛けると、フレームレートが高いほど回転が遅くなってしまうため削除します。
        // 感度は mouseSensitivity で調整するようにします。
        float mouseX = delta.x * mouseSensitivity * 0.1f;
        float mouseY = delta.y * mouseSensitivity * 0.1f;

        // Y軸反転オプション
        if (invertY) mouseY = -mouseY;

        // プレイヤー回転（左右）はプレイヤー本体に適用
        if (playerBody != null)
        {
            playerBody.Rotate(Vector3.up * mouseX, Space.World);
        }
        else
        {
            transform.Rotate(Vector3.up * mouseX, Space.World);
        }

        // ピッチ（上下）はカメラ自身で管理してクランプ
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    Vector2 GetMouseDelta()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Vector2 delta = Vector2.zero;
        if (Mouse.current != null)
        {
            delta = Mouse.current.delta.ReadValue();
        }
        else if (Pointer.current != null)
        {
            delta = Pointer.current.delta.ReadValue();
        }
        return delta;
#else
        float dx = Input.GetAxisRaw("Mouse X");
        float dy = Input.GetAxisRaw("Mouse Y");
        return new Vector2(dx, dy);
#endif
    }
}
