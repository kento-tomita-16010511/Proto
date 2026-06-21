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

    [SerializeField] private WebStunEffect _webStunEffect;

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
    }

    /// <summary>
    /// プレイヤー前方に WebStunEffect（蜘蛛の巣）を生成して射出する。
    /// 射出後は WebStunEffect 側の Rigidbody の重力に従って落下し、
    /// 当たった敵を WebStunEffect が EnemyPresenter.Stun() でスタンさせる。
    /// </summary>
    public void LaunchWebStun()
    {
        if (_webStunEffect == null) return;

        // 射出方向はキャラクター（蜘蛛）本体の水平な正面。カメラ(orientation)は
        // pitch を含み見下ろし時に水平成分が不安定になるため、本体 transform.forward を使う。
        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = orientation != null ? orientation.forward : Vector3.forward;
        fwd.Normalize();

        // 射出口は蜘蛛の口元（webMuzzle）。未設定時のみ本体中心にフォールバックする。
        Vector3 origin = webMuzzle != null ? webMuzzle.position : transform.position;
        Vector3 pos = origin + fwd * webSpawnDistance;

        // world 空間の弾として生成し、前方へ射出する（親を付けず重力で落下させる）。
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
