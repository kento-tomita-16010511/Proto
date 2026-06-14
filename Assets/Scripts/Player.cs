using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public class Player : MonoBehaviour
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

    private Rigidbody _rb;
    private Animator _animator;
    private List<GameObject> _activeEffects = new List<GameObject>();

    public void SetInputEnabled(bool enabled)
    {
        if (InputManager.Instance != null)
            InputManager.Instance.IsEnabled = enabled;
        if (!enabled && _animator != null)
        {
            _animator.SetBool("IsMoving", false);
            _animator.SetTrigger("GameOverTrigger");
        }
    }

/// <summary>Intimidation（威嚇）アニメーションを再生する。リザルト演出で呼ぶ。</summary>
    public void PlayIntimidation()
    {
        if (_animator != null)
            _animator.SetTrigger("IntimidationTrigger");
    }


    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        // Player 本体ではなく、Spider モデル側の Animator（コントローラ付き）を取得する
        foreach (var a in GetComponentsInChildren<Animator>(true))
        {
            if (a.runtimeAnimatorController != null) { _animator = a; break; }
        }
        if (orientation == null) orientation = transform;
    }

    void Update()
    {
        Vector3 input = InputManager.Instance?.MoveInput ?? Vector3.zero;
        bool isMoving = input.sqrMagnitude > 0.01f;
        _animator?.SetBool("IsMoving", isMoving);

        if ((InputManager.Instance?.AttackPressedThisFrame ?? false) && effectPrefab != null)
        {
            SpawnEffect();
            TryHitEnemy();
            _animator?.SetTrigger("AttackTrigger");
        }

        if (_rb == null && isMoving)
        {
            Vector3 forward = orientation.forward;
            Vector3 right   = orientation.right;
            forward.y = 0f; right.y = 0f;
            forward.Normalize(); right.Normalize();
            Vector3 move = (forward * input.z + right * input.x).normalized;
            transform.Translate(move * speed * Time.deltaTime, Space.World);
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

    void FixedUpdate()
    {
        if (_rb == null) return;
        Vector3 input = InputManager.Instance?.MoveInput ?? Vector3.zero;
        if (input.sqrMagnitude > 0f)
        {
            Vector3 forward = orientation.forward;
            Vector3 right   = orientation.right;
            forward.y = 0f; right.y = 0f;
            forward.Normalize(); right.Normalize();
            Vector3 move  = (forward * input.z + right * input.x).normalized;
            Vector3 tgt   = _rb.position + move * speed * Time.fixedDeltaTime;
            _rb.MovePosition(tgt);
        }
    }

}
