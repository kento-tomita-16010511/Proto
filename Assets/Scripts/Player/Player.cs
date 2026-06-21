using UnityEngine;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UniRx;
using System.Linq;
using System.Threading;

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

    [Header("Air / Momentum Settings")]
    [Tooltip("空中での方向転換のしやすさ（加速度）。慣性を残しつつ少しだけ操作を効かせる")]
    [SerializeField] private float airControlAccel = 18f;
    [Tooltip("着地時、通常速度を超える慣性が残っている間の減速（スライド感）。大きいほど早く止まる")]
    [SerializeField] private float slideFriction = 12f;

    [SerializeField] private WebStunEffect _webStunEffect;

    [SerializeField] private ParticleSystem _particleSystem;

    [Header("Health Settings")]
    [Tooltip("プレイヤーの最大HP。寅の攻撃ダメージがこれ以上なら一撃で死亡する。")]
    [SerializeField] private int maxHP = 100;

    /// <summary>Dissolve（崩壊）演出で溶かす対象のメッシュ。子の SkinnedMeshRenderer をアサインする。</summary>
    [SerializeField] private SkinnedMeshRenderer _skinnedMeshRenderer;

    /// <summary>撃破時に Dissolve（崩壊）させる秒数。</summary>
    [SerializeField] private float _dissolveDuration = 0.25f;

    /// <summary>シェーダーの _DissolveAmount プロパティID（Shader.PropertyToID でキャッシュ）。</summary>
    private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");

    /// <summary>現在のHP。</summary>
    public int CurrentHP { get; private set; }

    /// <summary>死亡時に1度だけ通知するイベント（GameManager がリザルト遷移を行う）。</summary>
    public IObservable<Unit> OnDeath => _onDeath;
    private readonly Subject<Unit> _onDeath = new Subject<Unit>();

    /// <summary>死亡済みかどうか。多重ダメージ・多重死亡を防ぐ。</summary>
    private bool _isDead;

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

    /// <summary>個体ごとのマテリアルインスタンス。共有マテリアルを汚さないよう初回アクセスでキャッシュする。</summary>
    private Material _dissolveMaterialInstance;


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
            // AnyState → Intimidation の遷移条件は GameOverTrigger。
            // Intimidation ステートは出口トランジションが無く、クリップも Loop 設定のため、
            // 一度入ればリザルト中ずっとループし続ける。
            _animator.SetTrigger("GameOverTrigger");
        }
    }

    void Awake()
    {
        _particleSystem.gameObject.SetActive(false);
        CurrentHP = maxHP;
        _onDeath.AddTo(this);
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

    /// <summary>
    /// メッシュの Dissolve（崩壊）演出を再生する。
    /// _DissolveAmount を 0→1 へ _dissolveDuration 秒かけて変化させ、完了後に本体を破棄する。
    /// （将来オブジェクトプール化する際は Destroy を SetActive(false) へ変更を検討）
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    public async UniTask PlayDissolveAsync(CancellationToken ct)
    {
        float elapsed = 0f;
        while (elapsed < _dissolveDuration)
        {
            elapsed += Time.deltaTime;
            SetDissolveAmount(Mathf.Clamp01(elapsed / _dissolveDuration));
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }
        SetDissolveAmount(1f);
    }

    /// <summary>
    /// Dissolve 量（0=通常表示 / 1=完全消滅）をマテリアルに設定する（表示操作のみ）。
    /// 他の敵に影響しないよう、共有マテリアルではなく個体インスタンス（renderer.material）に対して設定する。
    /// </summary>
    /// <param name="value">_DissolveAmount に設定する値（0〜1）。</param>
    public void SetDissolveAmount(float value)
    {
        if (_skinnedMeshRenderer == null) return;

        // 初回アクセスでマテリアルがインスタンス化される（共有マテリアルは書き換わらない）。
        if (_dissolveMaterialInstance == null)
            _dissolveMaterialInstance = _skinnedMeshRenderer.material;

        if (_dissolveMaterialInstance.HasProperty(DissolveAmountId))
            _dissolveMaterialInstance.SetFloat(DissolveAmountId, value);
    }

    /// <summary>
    /// 破壊エフェクト（砕け散る VFX）を再生する。
    /// DamageVFX 未アサインの場合は何もせず即完了する。
    /// </summary>
    public UniTask PlayDamageVFXAsync()
    {
        if (_particleSystem == null) return UniTask.CompletedTask;
        _particleSystem.gameObject.SetActive(true);
        _particleSystem.Play();
        return UniTask.CompletedTask;
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

        // 攻撃モーション中は新たなアクションを受け付けない
        if (!_actionLocked && attack && effectPrefab != null)
        {
            BeginAction();
            SpawnEffect();
            _animator?.SetTrigger("AttackTrigger");
        }
        // 右クリック=Net。蜘蛛の巣アニメーションを発火し、重力落下する Net を射出する。
        else if (!_actionLocked && net && _webStunEffect != null)
        {
            BeginAction();
            _animator?.SetTrigger("Net");
            LaunchWebStun();
        }

        // --- 速度の更新（接地中は直接制御、空中は慣性を保持しつつ弱いエアコントロール）---
        float dt = Time.deltaTime;
        UpdateGroundedOrAirborne(input, isMoving, dt);

        // 1 回の CharacterController.Move で適用する。
        // transform.Translate と Move を混在させると衝突解決（overlap recovery）と競合するため Move に統一。
        if (_controller != null)
            _controller.Move(_velocity * dt);
        else
            transform.Translate(_velocity * dt, Space.World);
    }

    /// <summary>
    /// 接地中は機敏な直接制御で水平移動・ジャンプを行い、空中では慣性を保持しつつ
    /// 弱いエアコントロールで方向だけ調整する。重力は常に加算する。
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
            // v = sqrt(2 * g * h)。上向き初速だけ与える。
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
            float cap = Mathf.Max(curSpeed, speed);
            horiz += wish.normalized * airControlAccel * dt;
            horiz = Vector3.ClampMagnitude(horiz, cap);
        }

        _velocity.x = horiz.x;
        _velocity.z = horiz.z;
    }

    /// <summary>
    /// プレイヤー前方に WebStunEffect（蜘蛛の巣）を生成して射出する。
    /// 射出後は WebStunEffect 側の Rigidbody の重力に従って落下し、
    /// 当たった敵を WebStunEffect が EnemyPresenter.Stun() でスタンさせる。
    /// </summary>
    public void LaunchWebStun()
    {
        if (_webStunEffect == null) return;

        // 射出方向は webMuzzle（カメラの子）の forward。カメラの上下角（pitch）を含むため、
        // 上を向けば射出も上向きになる。未設定時のみ本体正面へフォールバックする。
        Vector3 fwd = webMuzzle != null ? webMuzzle.forward : transform.forward;
        if (fwd.sqrMagnitude < 0.0001f) fwd = orientation != null ? orientation.forward : Vector3.forward;
        fwd.Normalize();

        // 射出口は蜘蛛の口元（webMuzzle）。未設定時のみ本体中心にフォールバックする。
        Vector3 origin = webMuzzle != null ? webMuzzle.position : transform.position;
        Vector3 pos = origin + fwd * webSpawnDistance;

        // world 空間の弾として生成し、照準方向へ射出する（親を付けず重力で落下させる）。
        WebStunEffect web = Instantiate(_webStunEffect, pos, Quaternion.LookRotation(fwd));
        web.Launch(fwd);
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

    /// <summary>
    /// ダメージを受ける処理（EnemyBasePresenter の死亡判定を移植）。
    /// HP が 0 以下になったら Die() を呼ぶ。
    /// </summary>
    /// <param name="amount">ダメージ量。</param>
    public void TakeDamage(int amount)
    {
        if (_isDead || CurrentHP <= 0) return;

        CurrentHP -= amount;
        if (CurrentHP <= 0) Die();
    }

    /// <summary>
    /// 死亡処理。死亡通知を1度だけ発行し、移動を停止する。
    /// リザルトシーンへの即時遷移は OnDeath を購読する GameManager 側で行う。
    /// </summary>
    public void Die()
    {
        if (_isDead) return;
        _isDead = true;
        CurrentHP = 0;

        // 移動・アクションを停止（接地スナップで速度をリセット）。
        _frozen = true;
        _velocity = Vector3.down * 2f;
        if (_animator != null) _animator.SetBool("IsMoving", false);
        PlayDissolveAsync(this.GetCancellationTokenOnDestroy()).Forget();
        _onDeath.OnNext(Unit.Default);
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
