using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UniRx;
using System.Linq;

public class Player : MonoBehaviour, IFreezable
{
    [Tooltip("移動速度（m/s）")]
    [SerializeField] private float speed = 5f;

    [Tooltip("移動をプレイヤーの向きに合わせるための参照")]
    [SerializeField] private Transform orientation;

    [Header("Effect Settings")]
    [SerializeField] private GameObject effectPrefab;
    [SerializeField] private int maxEffectCount = 5;
    [SerializeField] private float effectLifetime = 2.0f;
    [SerializeField] private RectTransform uiRoot;

    [Header("Attack Settings")]
    [Tooltip("攻撃が届く最大距離")]
    [SerializeField] private CapsuleCollider attackCollider;
    [Tooltip("1回の攻撃ダメージ")]
    [SerializeField] private int attackDamage = 100;

    [Tooltip("攻撃 / Net モーション中ロックの最大時間（秒）。アニメ終了検知のフェイルセーフ。")]
    [SerializeField] private float maxActionDuration = 2f;

    [Header("Net (Web) Settings")]
    [Tooltip("Net で生成する蜘蛛の巣エフェクト（FX_SpiderWeb_Impact）")]
    [SerializeField] private GameObject webImpactPrefab;
    [Tooltip("エフェクトを生成する前方距離（m）")]
    [SerializeField] private float webSpawnDistance = 1.5f;

    [Header("Jump Settings")]
    [Tooltip("ジャンプの最高到達高さ（m）")]
    [SerializeField] private float jumpHeight = 1.5f;
    [Tooltip("重力加速度（負の値）")]
    [SerializeField] private float gravity = -20f;

    private CharacterController _controller;
    private float _verticalVelocity;

    /// <summary>Freeze 中かどうか。EveryUpdate ストリームをスキップさせるフラグ。</summary>
    private bool _frozen;

    private Animator _animator;
    private List<GameObject> _activeEffects = new List<GameObject>();

    /// <summary>攻撃 / Net モーション再生中で次のアクションを受け付けないか。</summary>
    private bool _actionLocked;

    /// <summary>ロック後にアクションステートへ実際に入ったか（遷移ラグ対策）。</summary>
    private bool _enteredAction;

    /// <summary>アクション開始時刻。フェイルセーフのタイムアウト判定に使う。</summary>
    private float _actionStartTime;

    public void SetInputEnabled(bool enabled)
    {
        if (InputManager.Instance != null)
            if (!enabled && _animator != null)
            {
                _animator.SetBool("IsMoving", false);
            }
    }

    /// <summary>
    /// Intimidation（威嚇）アニメーションを再生する。リザルト演出で呼ぶ。
    /// タイムアップ時にキー押しっぱなしで遷移しても移動が続かないよう、
    /// _frozen = true で Tick() を即停止し CharacterController への Move 呼び出しを断つ。
    /// Freeze() と異なりアニメーターは止めない。
    /// </summary>
    public void PlayIntimidation()
    {
        _frozen = true;
        _verticalVelocity = -2f; // 接地スナップ値でリセット（重力蓄積をクリア）
        if (_animator != null)
        {
            _animator.SetBool("IsMoving", false);
            _animator.SetTrigger("IntimidationTrigger");
        }
    }

    void Awake()
    {
        _controller = GetComponent<CharacterController>();

        // Player 本体ではなく、Spider モデル側の Animator（コントローラ付き）を取得する
        _animator = GetComponentsInChildren<Animator>(true)
            .FirstOrDefault(a => a.runtimeAnimatorController != null);
        _animator?.SetTrigger("Idle");
        if (orientation == null) orientation = transform;

        // 毎フレーム処理は Update を使わず EveryUpdate で行う（CLAUDE.md 規約）。
        // Freeze 中は _frozen で処理をスキップする。
        Observable.EveryUpdate()
            .Where(_ => !_frozen)
            .Subscribe(_ => Tick())
            .AddTo(this);
    }

    /// <summary>毎フレームの移動・アクション・ジャンプ処理。EveryUpdate から呼ばれる。</summary>
    private void Tick()
    {
        UpdateActionLock();

        Vector3 input = InputManager.Instance?.MoveInput ?? Vector3.zero;
        bool isMoving = input.sqrMagnitude > 0.01f;
        _animator?.SetBool("IsMoving", isMoving);

        bool attack = InputManager.Instance?.AttackPressedThisFrame ?? false;
        bool net = InputManager.Instance?.NetPressedThisFrame ?? false;

        // 攻撃 / Net モーション中は新たなアクションを受け付けない
        if (!_actionLocked && attack && effectPrefab != null)
        {
            BeginAction();
            SpawnEffect();
            _animator?.SetTrigger("AttackTrigger");
        }
        else if (!_actionLocked && net)
        {
            BeginAction();
            _animator?.SetTrigger("Net");
            SpawnWebImpact();
        }

        // 水平移動量（カメラ基準）を算出する
        Vector3 horizontal = Vector3.zero;
        if (isMoving)
        {
            Vector3 forward = orientation.forward;
            Vector3 right = orientation.right;
            forward.y = 0f; right.y = 0f;
            forward.Normalize(); right.Normalize();
            horizontal = (forward * input.z + right * input.x).normalized * speed;
        }

        // ジャンプ / 重力で垂直速度を更新する
        UpdateVerticalVelocity();

        // 水平 + 垂直を 1 回の CharacterController.Move で適用する。
        // transform.Translate と Move を混在させると CharacterController の衝突解決
        // （overlap recovery）と競合して移動できなくなるため、必ず Move に統一する。
        Vector3 velocity = horizontal + Vector3.up * _verticalVelocity;
        if (_controller != null)
            _controller.Move(velocity * Time.deltaTime);
        else
            transform.Translate(horizontal * Time.deltaTime, Space.World);
    }

    /// <summary>
    /// 接地判定に基づきジャンプ入力を処理し、重力で垂直速度（_verticalVelocity）を更新する。
    /// 実際の移動適用（Move）は Tick 側で水平移動とまとめて 1 回だけ行う。
    /// </summary>
    private void UpdateVerticalVelocity()
    {
        if (_controller == null) return;

        bool grounded = _controller.isGrounded;
        if (grounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f; // 接地を安定させるための軽い押し付け

        bool jump = InputManager.Instance?.JumpPressedThisFrame ?? false;
        if (grounded && jump)
        {
            // v = sqrt(2 * g * h) で目標高さに到達する初速を求める
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        _verticalVelocity += gravity * Time.deltaTime;
    }

    /// <summary>
    /// プレイヤー前方に蜘蛛の巣エフェクトを生成する。
    /// Net アクション発火時に呼ばれる（検証用に public）。
    /// スタン時間は衝突した敵の EnemyState（EnemyModel）が保持するため、ここで渡す必要はない。
    /// </summary>
    public void SpawnWebImpact()
    {
        if (webImpactPrefab == null) return;

        Vector3 fwd = orientation != null ? orientation.forward : transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = transform.forward;
        fwd.Normalize();

        Vector3 pos = transform.position + fwd * webSpawnDistance;
        Instantiate(webImpactPrefab, pos, Quaternion.LookRotation(fwd));
    }

    /// <summary>アクション（攻撃 / Net）を開始し、モーション完了までロックする。</summary>
    private void BeginAction()
    {
        _actionLocked = true;
        _enteredAction = false;
        _actionStartTime = Time.time;
    }

    /// <summary>
    /// アクションロックの解除を判定する。
    /// Attack / Net ステートに入った後、別ステートへ遷移完了したら解除する。
    /// maxActionDuration を超えた場合はフェイルセーフで強制解除する。
    /// </summary>
    private void UpdateActionLock()
    {
        if (!_actionLocked) return;

        if (_animator == null || Time.time - _actionStartTime > maxActionDuration)
        {
            _actionLocked = false;
            _enteredAction = false;
            return;
        }

        var st = _animator.GetCurrentAnimatorStateInfo(0);
        bool inAction = st.IsName("Attack") || st.IsName("Net");
        if (inAction)
        {
            _enteredAction = true;
        }
        else if (_enteredAction && !_animator.IsInTransition(0))
        {
            _actionLocked = false;
            _enteredAction = false;
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit other)
    {
        // 衝突したオブジェクトのタグに応じて処理を分岐する
        switch (other.gameObject.tag)
        {
            case "Enemy":
                // 敵にダメージを与える
                var enemy = other.collider.GetComponent<EnemyBasePresenter>();
                if (enemy != null && attackCollider.bounds.Intersects(other.collider.bounds))
                {
                    enemy.TakeDamage(attackDamage);
                }
                break;
        }
    }

    private void SpawnEffect()
    {
        SoundManager.Instance.PlaySEWithRandomPitch("bite");
        _activeEffects.RemoveAll(e => e == null);
        if (_activeEffects.Count >= maxEffectCount)
        {
            if (_activeEffects[0] != null) Destroy(_activeEffects[0]);
            _activeEffects.RemoveAt(0);
        }
        GameObject effect = Instantiate(effectPrefab, uiRoot);
        _activeEffects.Add(effect);
        if (effect.TryGetComponent<ParticleController>(out var controller))
        {
            controller.PlayParticle();
            Destroy(effect, effectLifetime);
        }
        else if (effect.TryGetComponent<Animator>(out var animator))
        {
            animator.Play("bite", 0, 0f);
            WaitAndDestroy(effect, animator).Forget();
        }
        else
        {
            Destroy(effect, effectLifetime);
        }
    }

    private async UniTask WaitAndDestroy(GameObject target, Animator animator)
    {
        var token = target.GetCancellationTokenOnDestroy();
        await UniTask.Yield(token);
        while (animator != null && animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1.0f)
            await UniTask.Yield(token);
        if (target != null) Destroy(target);
    }

    public void Freeze()
    {
        // 毎フレーム処理を停止（_frozen で EveryUpdate をスキップ）
        _frozen = true;
        this.enabled = false;

        // アニメーションの停止
        if (_animator != null)
        {
            _animator.speed = 0f;
            _animator.gameObject.SetActive(false);
        }
    }

    public void Unfreeze()
    {
        // 毎フレーム処理を再開
        _frozen = false;
        this.enabled = true;

        // アニメーションの再開
        if (_animator != null)
        {
            _animator.speed = 1f;
            _animator.gameObject.SetActive(true);
        }
    }
}
