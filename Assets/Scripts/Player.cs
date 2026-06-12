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
    private bool _inputEnabled = true;

    public void SetInputEnabled(bool enabled)
    {
        _inputEnabled = enabled;
        if (!enabled && _animator != null)
        {
            _animator.SetBool("IsMoving", false);
            _animator.SetTrigger("GameOverTrigger");
        }
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
        if (!_inputEnabled) return;

        // 移動入力の取得 → IsMoving を毎フレーム更新
        Vector3 input = GetInput();
        bool isMoving = input.sqrMagnitude > 0.01f;
        _animator?.SetBool("IsMoving", isMoving);

        // 攻撃入力
        if (IsAttackPressed() && effectPrefab != null)
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

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (Mouse.current == null) return;
        Vector2 screenPos = Mouse.current.position.ReadValue();
#else
        Vector2 screenPos = Input.mousePosition;
#endif

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
        if (!_inputEnabled || _rb == null) return;
        Vector3 input = GetInput();
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

    Vector3 GetInput()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Vector2 move = Vector2.zero;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)  move.x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) move.x += 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)    move.y += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)  move.y -= 1f;
        }
        if (Gamepad.current != null) move += Gamepad.current.leftStick.ReadValue();
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

    bool IsAttackPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        bool mouseClick  = Mouse.current   != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool gamepadSouth = Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
        return mouseClick || gamepadSouth;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }
}
