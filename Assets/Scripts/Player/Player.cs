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
    [Tooltip("Net の射出口（蜘蛛の口元）。未設定時はプレイヤー本体中心から射出する")]
    [SerializeField] private Transform webMuzzle;

    [Header("Jump Settings")]
    [Tooltip("ジャンプの最高到達高さ（m）")]
    [SerializeField] private float jumpHeight = 1.5f;
    [Tooltip("重力加速度（負の値）")]
    [SerializeField] private float gravity = -20f;

    [Header("Grapple / Momentum Settings")]
    [Tooltip("グラップリングフック。右クリックで発射し、外れた後の速度（慣性）は Player が保持する")]
    [SerializeField] private GrappleController grapple;
    [Tooltip("空中での方向転換のしやすさ（加速度）。慣性を残しつつ少しだけ操作を効かせる")]
    [SerializeField] private float airControlAccel = 18f;
    [Tooltip("着地時、通常速度を超える慣性が残っている間の減速（スライド感）。大きいほど早く止まる")]
    [SerializeField] private float slideFriction = 12f;

    private CharacterController _controller;

    /// <summary>ワールド空間のフル速度ベクトル（m/s）。グラップルの慣性をここに保持して持ち越す。</summary>
    private Vector3 _velocity;

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
        _velocity = Vector3.down * 2f; // 接地スナップ値でリセット（速度・重力蓄積をクリア）
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

        // 攻撃モーション中は新たなアクションを受け付けない
        if (!_actionLocked && attack && effectPrefab != null)
        {
            BeginAction();
            SpawnEffect();
            _animator?.SetTrigger("AttackTrigger");
        }

        // --- グラップル発射 / 切断（右クリック）---------------------------------
        bool grapplePressed = InputManager.Instance?.GrapplePressedThisFrame ?? false;
        bool grappleHeld = InputManager.Instance?.GrappleHeld ?? false;
        if (grapple != null)
        {
            if (grapplePressed) grapple.TryAttach();
            if (!grappleHeld) grapple.Detach();
        }
        bool grappling = grapple != null && grapple.IsAttached;

        // --- 速度の更新（グラップル中 / 接地 / 空中 の 3 状態）------------------
        float dt = Time.deltaTime;
        if (grappling)
        {
            // グラップルが速度を全面的に駆動する。外れた瞬間の速度が _velocity に残り慣性になる。
            _velocity = grapple.ComputeVelocity(_velocity, input, dt);
        }
        else
        {
            UpdateGroundedOrAirborne(input, isMoving, dt);
        }

        // 1 回の CharacterController.Move で適用する。
        // transform.Translate と Move を混在させると衝突解決（overlap recovery）と競合するため Move に統一。
        if (_controller != null)
            _controller.Move(_velocity * dt);
        else
            transform.Translate(_velocity * dt, Space.World);
    }

    /// <summary>
    /// グラップルしていない時の速度更新。接地中は機敏な直接制御、空中はグラップル等の慣性を保持しつつ
    /// 弱いエアコントロールで方向だけ調整する。これにより「スイング→リリース→大ジャンプ／スライド」が繋がる。
    /// </summary>
    private void UpdateGroundedOrAirborne(Vector3 input, bool isMoving, float dt)
    {
        if (_controller == null) return;

        bool grounded = _controller.isGrounded;

        // 垂直（重力・ジャンプ）
        if (grounded && _velocity.y < 0f)
            _velocity.y = -2f; // 接地を安定させる軽い押し付け

        bool jump = InputManager.Instance?.JumpPressedThisFrame ?? false;
        if (grounded && jump)
        {
            _animator?.SetTrigger("Jump");
            // v = sqrt(2 * g * h)。グラップルの水平慣性は維持したまま上向き初速だけ与える＝大ジャンプ。
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            grounded = false; // この後の水平処理を空中側（慣性保持）に倒す
        }
        _velocity.y += gravity * dt;

        // 入力から望む水平方向（カメラ基準）
        Vector3 wish = Vector3.zero;
        if (isMoving)
        {
            Vector3 forward = orientation.forward;
            Vector3 right = orientation.right;
            forward.y = 0f; right.y = 0f;
            forward.Normalize(); right.Normalize();
            wish = (forward * input.z + right * input.x).normalized * speed;
        }

        Vector3 horiz = new Vector3(_velocity.x, 0f, _velocity.z);
        float curSpeed = horiz.magnitude;

        if (grounded)
        {
            if (curSpeed > speed + 0.1f)
            {
                // 通常速度を超える慣性が残っている → スライドとして摩擦で減速しつつ入力で寄せる
                horiz = Vector3.MoveTowards(horiz, wish, slideFriction * dt);
            }
            else
            {
                // 通常の機敏な接地移動：水平を入力で直接上書き
                horiz = wish;
            }
        }
        else if (isMoving)
        {
            // 空中：慣性を消さずに、望む方向へ弱く加速（エアコントロール）。
            // 現在速度か通常速度の大きい方を上限にして、慣性での高速を削らない。
            float cap = Mathf.Max(curSpeed, speed);
            horiz += wish.normalized * airControlAccel * dt;
            horiz = Vector3.ClampMagnitude(horiz, cap);
        }

        _velocity.x = horiz.x;
        _velocity.z = horiz.z;
    }

    /// <summary>
    /// プレイヤー前方に蜘蛛の巣エフェクトを生成する。
    /// Net アクション発火時に呼ばれる（検証用に public）。
    /// スタン時間は衝突した敵の EnemyState（EnemyModel）が保持するため、ここで渡す必要はない。
    /// </summary>
    public void SpawnWebImpact()
    {
        if (webImpactPrefab == null) return;

        // 射出方向はキャラクター（蜘蛛）本体の水平な正面。カメラ(orientation)は
        // pitch を含み見下ろし時に水平成分が不安定になるため、本体 transform.forward を使う。
        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = orientation != null ? orientation.forward : Vector3.forward;
        fwd.Normalize();

        // 射出口は蜘蛛の口元（webMuzzle）。未設定時のみ本体中心にフォールバックする。
        // 本体中心はプレイヤールート（コライダー中心）で蜘蛛より高く、空中から出ているように
        // 見えてしまうため、口元の Transform を基準にする。
        Vector3 origin = webMuzzle != null ? webMuzzle.position : transform.position;
        Vector3 pos = origin + fwd * webSpawnDistance;

        // プレイヤー本体を親にして生成する。これにより発射後にプレイヤーが移動・回転しても
        // ネットが常に蜘蛛の真正面（一定のローカルオフセット）に追従する。
        // ※前進させたい場合は world 空間の弾になり真正面から外れるため、
        //   FX 側の WebStunEffect.moveSpeed は 0 にしている。
        Instantiate(webImpactPrefab, pos, Quaternion.LookRotation(fwd), null);
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
                AnimatorClipInfo[] clipInfo = _animator.GetCurrentAnimatorClipInfo(0);
                string clipName = clipInfo[0].clip.name;
                if (enemy != null && attackCollider.bounds.Intersects(other.collider.bounds) && clipName == "Attack")
                {
                    enemy.TakeDamage(attackDamage);
                }
                break;
        }
    }

    private void SpawnEffect()
    {
        SoundManager.Instance.PlaySEWithRandomPitch(SEEnum.Bite);
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
