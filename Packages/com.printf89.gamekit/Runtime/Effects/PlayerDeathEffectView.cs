using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// プレイヤー被撃破時の画面演出。
/// 画面全体を薄い半透明の赤でフラッシュさせ、同時にカメラを減衰シェイクする（スクリーンシェイク）。
/// オーバーレイ UI は実行時に Screen Space - Overlay の Canvas として自前生成するため、
/// シーン側への事前配置は不要。再生完了後に自身を破棄する。
/// </summary>
public class PlayerDeathEffectView : MonoBehaviour
{
    /// <summary>全画面の赤オーバーレイ画像。</summary>
    private Image _overlay;

    /// <summary>オーバーレイの不透明度を制御する CanvasGroup。</summary>
    private CanvasGroup _group;

    /// <summary>
    /// 赤フラッシュ＋カメラシェイク演出を生成して再生し、完了後に自身を破棄する。
    /// </summary>
    /// <param name="camera">シェイク対象のカメラ（localPosition を揺らす）。null ならフラッシュのみ。</param>
    /// <param name="flashColor">赤フラッシュの色。アルファをピーク不透明度として使う。</param>
    /// <param name="duration">演出全体の長さ（秒）。</param>
    /// <param name="shakeAmplitude">シェイクの振幅（m）。</param>
    /// <param name="shakeFrequency">シェイクの周波数（Hz）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    public static UniTask PlayAsync(
        Camera camera,
        Color flashColor,
        float duration,
        float shakeAmplitude,
        float shakeFrequency,
        CancellationToken ct)
    {
        var go = new GameObject("PlayerDeathEffect");
        var fx = go.AddComponent<PlayerDeathEffectView>();
        fx.Build(flashColor);
        return fx.RunAsync(camera, flashColor.a, duration, shakeAmplitude, shakeFrequency, ct);
    }

    /// <summary>オーバーレイ用の Canvas / Image / CanvasGroup を構築する。</summary>
    /// <param name="flashColor">オーバーレイの色（アルファは CanvasGroup 側で制御するため 1 に正規化）。</param>
    private void Build(Color flashColor)
    {
        // 最前面に出すための Screen Space - Overlay キャンバス。
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue; // 既存 UI より手前に表示する
        gameObject.AddComponent<CanvasScaler>();

        var imgGo = new GameObject("RedOverlay");
        imgGo.transform.SetParent(transform, false);

        _overlay = imgGo.AddComponent<Image>();
        _overlay.color = new Color(flashColor.r, flashColor.g, flashColor.b, 1f);
        _overlay.raycastTarget = false; // 入力を奪わない

        var rt = _overlay.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _group = imgGo.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.interactable = false;
        _group.blocksRaycasts = false;
    }

    /// <summary>
    /// フラッシュとシェイクを再生する。
    /// 不透明度は 0→ピーク→0、シェイク振幅は時間で減衰させ、最後にカメラ位置を元へ戻して破棄する。
    /// </summary>
    private async UniTask RunAsync(
        Camera camera, float peakAlpha, float duration,
        float amplitude, float frequency, CancellationToken ct)
    {
        Transform camT = camera != null ? camera.transform : null;
        Vector3 originalLocalPos = camT != null ? camT.localPosition : Vector3.zero;

        // 立ち上がりを素早く、その後ゆっくり引かせる（前半でピーク、後半で 0 へ）。
        float rise = duration * 0.3f;

        try
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                // 赤フラッシュの不透明度。
                float alpha = elapsed < rise
                    ? Mathf.Lerp(0f, peakAlpha, elapsed / rise)
                    : Mathf.Lerp(peakAlpha, 0f, (elapsed - rise) / (duration - rise));
                if (_group != null) _group.alpha = alpha;

                // 減衰する正弦波でカメラ localPosition をオフセット。
                if (camT != null)
                {
                    float damper = 1f - (elapsed / duration);
                    float t = elapsed * frequency * Mathf.PI * 2f;
                    float ox = Mathf.Sin(t) * amplitude * damper;
                    float oy = Mathf.Cos(t * 1.3f) * amplitude * damper;
                    camT.localPosition = originalLocalPos + new Vector3(ox, oy, 0f);
                }

                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }
        finally
        {
            if (camT != null) camT.localPosition = originalLocalPos;
            if (this != null) Destroy(gameObject);
        }
    }
}
