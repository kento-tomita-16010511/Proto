using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

public class Player : MonoBehaviour
{
    [Tooltip("移動速度（m/s）")]
    public float speed = 5f;

    [Tooltip("移動をプレイヤーの向きに合わせるための参照（未設定時はこの GameObject の Transform を使用）")]
    public Transform orientation;

    [Header("Effect Settings")]
    [Tooltip("生成するエフェクトのプレハブ")]
    public GameObject effectPrefab;
    [Tooltip("同時に存在できる最大数")]
    public int maxEffectCount = 5;
    [Tooltip("エフェクトが自動消滅するまでの時間(秒)")]
    public float effectLifetime = 2.0f;
    [Tooltip("エフェクトを表示するCanvas（RectTransform）")]
    public RectTransform uiRoot;

    private Rigidbody _rb;
    private List<GameObject> _activeEffects = new List<GameObject>();

    /// <summary>
    /// コンポーネントの初期化と参照の解決を行います。
    /// </summary>
    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        if (orientation == null) orientation = transform;
    }

    /// <summary>
    /// 毎フレームの入力監視と、Rigidbody を使用しない場合の移動処理を行います。
    /// </summary>
    void Update()
    {
        // 攻撃ボタン（クリック）が押されたらエフェクトを生成して発火
        if (IsAttackPressed() && effectPrefab != null)
        {
            SpawnEffect();
        }

        if (_rb == null)
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

    /// <summary>
    /// エフェクトの生成、リスト管理、および再生開始を行います。
    /// 最大数に達している場合は古いエフェクトを破棄します。
    /// </summary>
    private void SpawnEffect()
    {
        SoundManager.Instance.PlaySEWithRandomPitch("bite");
        // 1. 寿命などで既に消滅したエフェクトの参照をリストから削除
        _activeEffects.RemoveAll(e => e == null);

        // 2. 最大数を超えている場合は、一番古いエフェクトを即座に破棄して枠を空ける
        if (_activeEffects.Count >= maxEffectCount)
        {
            if (_activeEffects[0] != null) Destroy(_activeEffects[0]);
            _activeEffects.RemoveAt(0);
        }

        // 3. エフェクトを生成し、Canvas(uiRoot) の子要素にする
        GameObject effect = Instantiate(effectPrefab, uiRoot);
        _activeEffects.Add(effect);

        // 4. エフェクトの発火（ParticleController または Animator）
        if (effect.TryGetComponent<ParticleController>(out var controller))
        {
            // ParticleController が付いている場合はランダム回転などのロジックを実行
            controller.PlayParticle();
            // パーティクルの場合は指定秒数で破棄
            Destroy(effect, effectLifetime);
        }
        else if (effect.TryGetComponent<Animator>(out var animator))
        {
            // Animator のみの場合は直接 "bite" ステートを再生
            animator.Play("bite", 0, 0f);
            // 非同期でアニメーション終了を待機して破棄
            WaitAndDestroy(effect, animator).Forget();
        }
        else
        {
            // コンポーネントがない場合のフォールバック
            Destroy(effect, effectLifetime);
        }
    }

    private async UniTask WaitAndDestroy(GameObject target, Animator animator)
    {
        // ターゲットが破棄されたらタスクを自動中断するトークン
        var token = target.GetCancellationTokenOnDestroy();

        // アニメーションの計算が開始されるまで待機
        await UniTask.Yield(token);

        // 対象が削除されず、かつアニメーションが再生中の間ループで待機
        // normalizedTime が 1.0 を超えたら再生終了とみなす
        while (animator != null && animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1.0f)
        {
            await UniTask.Yield(token);
        }

        if (target != null) Destroy(target);
    }

    /// <summary>
    /// 物理演算（Rigidbody）に基づいた移動処理を一定間隔で実行します。
    /// </summary>
    void FixedUpdate()
    {
        // Rigidbody があれば物理移動
        if (_rb != null)
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
                Vector3 target = _rb.position + move * speed * Time.fixedDeltaTime;
                _rb.MovePosition(target);
            }
        }
    }

    /// <summary>
    /// 入力デバイスから移動ベクトルを取得します。
    /// 新旧両方の Input System に対応しています。
    /// </summary>
    /// <returns>正規化された入力方向（XZ平面）</returns>
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

    /// <summary>
    /// 攻撃ボタン（マウスの左クリック）が押されたかどうかを判定します。
    /// </summary>
    /// <returns>押された瞬間のフレームであれば true</returns>
    bool IsAttackPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        bool mouseClick = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool gamepadSouth = Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
        return mouseClick || gamepadSouth;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }
}
