using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public class Player : MonoBehaviour, IFreezable
{
    [Tooltip("移動速度（m/s）")]
    public float speed = 5f;

    [Tooltip("移動をプレイヤーの向きに合わせるための参照")]
    public Transform orientation;

    [Header("Effect Settings")]
    public GameObject effectPrefab;
    public int maxEffectCount = 5;
    public float effectLifetime = 2.0f;
    public RectTransform uiRoot;

    [Header("Attack Settings")]
    [Tooltip("攻撃が届く最大距離")]
    public float attackRange = 50f;
    [Tooltip("1回の攻撃ダメージ")]
    public int attackDamage = 100;

    [Tooltip("攻撃 / Net モーション中ロックの最大時間（秒）。アニメ終了検知のフェイルセーフ。")]
    public float maxActionDuration = 2f;

    [Header("Net (Web) Settings")]
    [Tooltip("プレイヤーの調整可能ステータス（Net の停止時間などを保持）")]
    [SerializeField] private PlayerStats stats;
    [Tooltip("Net で生成する蜘蛛の巣エフェクト（FX_SpiderWeb_Impact）")]
    [SerializeField] private GameObject webImpactPrefab;
    [Tooltip("エフェクトを生成する前方距離（m）")]
    [SerializeField] private float webSpawnDistance = 1.5f;

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

    /// <summary>Intimidation（威嚇）アニメーションを再生する。リザルト演出で呼ぶ。</summary>
    public void PlayIntimidation()
    {
        if (_animator != null)
            _animator.SetTrigger("IntimidationTrigger");
    }

    void Awake()
    {
        // Player 本体ではなく、Spider モデル側の Animator（コントローラ付き）を取得する
        foreach (var a in GetComponentsInChildren<Animator>(true))
        {
            if (a.runtimeAnimatorController != null) { _animator = a; break; }
        }
        _animator?.SetTrigger("Idle");
        if (orientation == null) orientation = transform;
    }

    void Update()
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
            TryHitEnemy();
            _animator?.SetTrigger("AttackTrigger");
        }
        else if (!_actionLocked && net)
        {
            BeginAction();
            _animator?.SetTrigger("Net");
            SpawnWebImpact();
        }

        if (isMoving)
        {
            Vector3 forward = orientation.forward;
            Vector3 right = orientation.right;
            forward.y = 0f; right.y = 0f;
            forward.Normalize(); right.Normalize();
            Vector3 move = (forward * input.z + right * input.x).normalized;
            transform.Translate(move * speed * Time.deltaTime, Space.World);
        }
    }

    /// <summary>
    /// プレイヤー前方に蜘蛛の巣エフェクトを生成し、停止時間を渡す。
    /// Net アクション発火時に呼ばれる（検証用に public）。
    /// </summary>
    public void SpawnWebImpact()
    {
        if (webImpactPrefab == null) return;

        Vector3 fwd = orientation != null ? orientation.forward : transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = transform.forward;
        fwd.Normalize();

        Vector3 pos = transform.position + fwd * webSpawnDistance;
        var go = Instantiate(webImpactPrefab, pos, Quaternion.LookRotation(fwd));

        var web = go.GetComponent<WebStunEffect>();
        if (web != null) web.Initialize(stats != null ? stats.WebStunDuration : 0f);
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

    private void TryHitEnemy()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector2 screenPos = InputManager.Instance?.MouseScreenPosition ?? Vector2.zero;
        Ray ray = cam.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, attackRange))
        {
            var enemy = hit.collider.GetComponent<EnemyBase>();
            if (enemy != null)
                enemy.TakeDamage(attackDamage);
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
        // アニメーションの停止
        if (_animator != null)
        {
            _animator.speed = 0f;
            _animator.gameObject.SetActive(false);
        }

        // Update / FixedUpdate を停止
        this.enabled = false;
    }

    public void Unfreeze()
    {
        // アニメーションの再開
        if (_animator != null)
        {
            _animator.speed = 1f;
            _animator.gameObject.SetActive(true);
        }

        // Update / FixedUpdate を再開
        this.enabled = true;
    }
}
