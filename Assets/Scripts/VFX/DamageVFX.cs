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

    public async UniTask DieAsync()
    {

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

        gameObject.SetActive(true);

        // 4. 敵の本体（メッシュ）を一瞬で非表示にする
        myRenderer.enabled = false;

        // 5. 当たり判定などを消す
        GetComponent<Collider>().enabled = false;

        // 6. 後処理（3秒後にオブジェクトを完全に削除）
        Destroy(gameObject, 3f);

        await UniTask.Delay(3000);

        gameObject.SetActive(false);

    }
}
