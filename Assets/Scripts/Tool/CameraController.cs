using UnityEngine;
using UniRx;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

public class CameraController : MonoBehaviour
{
    [Tooltip("カメラの感度")]
    [SerializeField] private float mouseSensitivity = 10f;
    [Tooltip("カメラが回転させる対象（プレイヤーの Transform）")]
    [SerializeField] private Transform playerBody;
    [Tooltip("上下回転の最小角度（度）")]
    [SerializeField] private float minPitch = -80f;
    [Tooltip("上下回転の最大角度（度）")]
    [SerializeField] private float maxPitch = 80f;
    [Tooltip("マウスY軸を反転する")]
    [SerializeField] private bool invertY = false;
    [Tooltip("カーソルをロックする")]
    [SerializeField] private bool lockCursor = true;

    float pitch = 0f; // 上下回転（カメラ）

    void Start()
    {
        if (playerBody == null && transform.parent != null)
        {
            playerBody = transform.parent;
        }

        // 毎フレームのカメラ操作は Update を使わず EveryUpdate で行う（CLAUDE.md 規約）
        Observable.EveryUpdate()
            .Where(_ => InputManager.Instance?.IsEnabled ?? false)
            .Subscribe(_ => Tick())
            .AddTo(this);
    }

    // InputGuardView が enabled を切り替えるタイミングに合わせてカーソルロックを管理する。
    // Start ではなく OnEnable/OnDisable で行うことで、Result 画面など非プレイ中に
    // カーソルが中央固定のままになるのを防ぐ。
    private void OnEnable()
    {
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OnDisable()
    {
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    /// <summary>毎フレームのカメラ回転処理。EveryUpdate から入力有効時のみ呼ばれる。</summary>
    void Tick()
    {
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
