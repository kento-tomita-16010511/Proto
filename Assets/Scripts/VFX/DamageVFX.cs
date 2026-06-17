using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// 敵がダメージを受けて死亡する際のVFXを制御するクラス
/// </summary>
public class DamageVFX : MonoBehaviour
{
    [Header("同期するレンダラーとVFX")]
    public SkinnedMeshRenderer myRenderer;
    public VisualEffect shatterVFX;

    /// <summary>
    /// 砕け散る VFX を再生し、本体メッシュを非表示にして演出時間だけ待機する。
    /// 本体（敵ルート）の破棄は呼び出し側（EnemyPresenter.OnDie）が担うため、
    /// ここでは破棄を行わない。Collider は同じ GameObject に存在する場合のみ無効化する。
    /// </summary>
    public async UniTask DieAsync()
    {
        // VFX 用 GameObject はデフォルトで非アクティブな場合があるため、再生前に有効化する。
        // これを忘れると shatterVFX.Play() が走っても描画されない。
        gameObject.SetActive(true);

        if (shatterVFX != null && myRenderer != null)
        {
            // 1. VFXに敵のメッシュ情報を送る
            shatterVFX.SetSkinnedMeshRenderer("EnemyMesh", myRenderer);

            // 2. 敵のマテリアルからメインテクスチャを取得してVFXに送る
            Texture mainTex = myRenderer.material.mainTexture;
            if (mainTex != null)
            {
                shatterVFX.SetTexture("EnemyTexture", mainTex);
            }

            // 3. VFXを再生
            shatterVFX.Play();
        }

        // 4. 敵の本体（メッシュ）を一瞬で非表示にする
        if (myRenderer != null) myRenderer.enabled = false;

        // 5. VFX の再生時間だけ待機（当たり判定の無効化と本体破棄は呼び出し側が行う）
        await UniTask.Delay(3000);
    }
}
