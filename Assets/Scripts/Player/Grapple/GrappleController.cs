using UnityEngine;

/// <summary>
/// Apex Legends パスファインダー風グラップリングフック。
///
/// 設計の要点:
/// - 物理基盤は Rigidbody ではなく、Player と同じく CharacterController（速度ベース）。
///   本クラスは「現在の速度ベクトルを受け取り、グラップル中の新しい速度ベクトルを返す」
///   純粋な速度計算器として振る舞う（ComputeVelocity）。実際の移動適用（Move）は Player 側。
/// - これによりフックが外れた瞬間の速度がそのまま Player._velocity に残り、
///   スイングの慣性（モメンタム）が大ジャンプ・スライドへ受け継がれる。
///
/// 4 つの仕様の対応:
///   (1) 狙った壁/地面に引っかける …… TryAttach() の SphereCast（照準=カメラ前方）
///   (2) 入力＋視点で振り子スイング …… ComputeVelocity の「接線方向加速」＋重力＋距離拘束
///   (3) 外れた後も慣性維持           …… 速度を返すだけで止めない。Player が airborne で保持
///   (4) 最大射程＋折れ角で自動切断   …… maxRange / breakAngle による Detach
/// </summary>
[DisallowMultipleComponent]
public class GrappleController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("照準の基準（カメラ）。ここの forward 方向へフックを撃つ。未設定なら Camera.main")]
    [SerializeField] private Transform aimSource;
    [Tooltip("ロープの根本（蜘蛛の口元など）。見た目のロープ始点。未設定なら本体中心")]
    [SerializeField] private Transform hookOrigin;
    [Tooltip("移動入力をワールド方向へ変換する基準（カメラ基準の向き）。未設定なら aimSource")]
    [SerializeField] private Transform orientation;

    [Header("Hook")]
    [Tooltip("フックが届く最大射程（m）")]
    [SerializeField] private float maxRange = 35f;
    [Tooltip("引っかけ判定の太さ（SphereCast 半径）。多少狙いがズレても刺さる")]
    [SerializeField] private float aimRadius = 0.4f;
    [Tooltip("フックが刺さる対象レイヤー")]
    [SerializeField] private LayerMask grappleMask = ~0;

    [Header("Swing Physics")]
    [Tooltip("グラップル中に掛かる重力（負の値）。Player の重力と別に調整できる")]
    [SerializeField] private float gravity = -22f;
    [Tooltip("アンカーへ引き寄せる加速度。Apex の「ぐいっと寄る」感。大きいほど直線的")]
    [SerializeField] private float reelAccel = 28f;
    [Tooltip("移動入力で振り子をこぐ接線方向の加速度。スイングの大きさを決める主役")]
    [SerializeField] private float swingAccel = 45f;
    [Tooltip("ロープが伸びきった時に張力で引き戻すバネ強度（拘束の硬さ）")]
    [SerializeField] private float ropeStiffness = 30f;
    [Tooltip("グラップル中の最大速度（m/s）のクランプ")]
    [SerializeField] private float maxSpeed = 40f;
    [Tooltip("これ以上アンカーに近づいたら自動で外す（到達判定）")]
    [SerializeField] private float minDistance = 2.5f;

    [Header("Auto Release")]
    [Tooltip("ロープが真下から測ってこの角度を超えて折れたら自動切断（度）。" +
             "0=真下、90=水平、>90=アンカーより上。スイング頂点での自動リリースに使う")]
    [SerializeField] private float breakAngle = 95f;
    [Tooltip("掴んでからの最大グラップル時間（秒）。フェイルセーフ")]
    [SerializeField] private float maxGrappleTime = 4f;

    [Header("Rope Visual")]
    [Tooltip("ロープ描画用 LineRenderer（任意）。未設定なら描画なしで物理だけ動く")]
    [SerializeField] private LineRenderer rope;

    /// <summary>現在フックが刺さっているか。Player はこれを見て移動制御を切り替える。</summary>
    public bool IsAttached { get; private set; }

    private Vector3 _anchor;     // フックの固定点（ワールド）
    private float _ropeLength;   // 拘束する基準ロープ長
    private float _attachTime;   // 掴んだ時刻

    private void Awake()
    {
        if (aimSource == null && Camera.main != null) aimSource = Camera.main.transform;
        if (orientation == null) orientation = aimSource;
        if (hookOrigin == null) hookOrigin = transform;
        if (rope != null) rope.enabled = false;
    }

    /// <summary>
    /// 照準方向へフックを撃ち、当たれば取り付ける。右クリック押下フレームで Player から呼ぶ。
    /// </summary>
    public void TryAttach()
    {
        if (IsAttached || aimSource == null) return;

        Ray ray = new Ray(aimSource.position, aimSource.forward);
        // SphereCast で多少の狙いズレを許容（Apex の吸い付き感）
        if (!Physics.SphereCast(ray, aimRadius, out RaycastHit hit, maxRange, grappleMask, QueryTriggerInteraction.Ignore))
            return;

        _anchor = hit.point;
        _ropeLength = Vector3.Distance(transform.position, _anchor);
        _attachTime = Time.time;
        IsAttached = true;

        if (rope != null)
        {
            rope.positionCount = 2;
            rope.enabled = true;
        }
    }

    /// <summary>フックを外す。右クリックを離した時／自動切断時に呼ぶ。速度はそのまま残す。</summary>
    public void Detach()
    {
        if (!IsAttached) return;
        IsAttached = false;
        if (rope != null) rope.enabled = false;
    }

    /// <summary>
    /// グラップル中の 1 フレーム分の速度を計算して返す。Player.Tick から毎フレーム呼ぶ。
    /// 戻り値の速度をそのまま CharacterController.Move へ渡し、外れた後も保持することで慣性が残る。
    /// </summary>
    /// <param name="velocity">現在の速度（前フレームの結果）</param>
    /// <param name="moveInput">移動入力（x=左右, z=前後 のローカル値）</param>
    /// <param name="dt">Time.deltaTime</param>
    public Vector3 ComputeVelocity(Vector3 velocity, Vector3 moveInput, float dt)
    {
        if (!IsAttached) return velocity;

        Vector3 toPlayer = transform.position - _anchor; // アンカー→プレイヤー
        float dist = toPlayer.magnitude;
        if (dist < 0.001f) { Detach(); return velocity; }
        Vector3 ropeDir = toPlayer / dist; // アンカーから見たプレイヤー方向（=ロープの向き）

        // --- 自動切断の判定（仕様4）------------------------------------------
        // 真下(Vector3.down)とロープの角度。スイングで上へ振れるほど角度が増える。
        float bend = Vector3.Angle(ropeDir, Vector3.down);
        if (dist <= minDistance ||                       // アンカーに到達
            bend > breakAngle ||                          // 折れ角オーバー
            Time.time - _attachTime > maxGrappleTime)     // 時間切れ
        {
            Detach();
            return velocity;
        }

        // --- 重力（振り子を下へ引く力）---------------------------------------
        velocity += Vector3.up * gravity * dt;

        // --- アンカーへの引き寄せ（Apex の加速する寄り）-----------------------
        velocity += -ropeDir * reelAccel * dt;

        // --- 入力による振り子こぎ（仕様2）-----------------------------------
        // 視点基準の移動方向を、ロープに垂直な接線平面へ射影して加速する。
        // これで「進みたい方向へ大きくカーブ・加速」できる。
        if (moveInput.sqrMagnitude > 0.01f && orientation != null)
        {
            Vector3 fwd = orientation.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = orientation.right; right.y = 0f; right.Normalize();
            Vector3 wish = (fwd * moveInput.z + right * moveInput.x);
            Vector3 tangent = Vector3.ProjectOnPlane(wish, ropeDir); // ロープに垂直な成分のみ
            if (tangent.sqrMagnitude > 0.0001f)
                velocity += tangent.normalized * swingAccel * dt;
        }

        // --- 距離拘束（ロープが伸びきったら張力で支点運動にする）---------------
        if (dist > _ropeLength)
        {
            // ロープより外へ出る速度（外向き成分）を打ち消す → 接線方向だけ残り振り子になる
            float radialOut = Vector3.Dot(velocity, ropeDir);
            if (radialOut > 0f) velocity -= ropeDir * radialOut;

            // 伸びた分をバネで引き戻す（CharacterController なので位置スナップではなく速度で補正）
            float stretch = dist - _ropeLength;
            velocity += -ropeDir * stretch * ropeStiffness * dt;
        }

        velocity = Vector3.ClampMagnitude(velocity, maxSpeed);

        UpdateRope();
        return velocity;
    }

    /// <summary>ロープの見た目を更新する。Player.Tick（EveryUpdate）経由で毎フレーム呼ばれる。</summary>
    private void UpdateRope()
    {
        if (rope == null || !rope.enabled) return;
        rope.SetPosition(0, hookOrigin != null ? hookOrigin.position : transform.position);
        rope.SetPosition(1, _anchor);
    }
}
