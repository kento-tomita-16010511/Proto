using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// メインゲーム画面の UI 表示を担当する View クラス。
/// フェード演出やカメラ初期化など、画面表示に関わる操作を提供する。
/// </summary>
public class MainSceneView : MonoBehaviour
{
    /// <summary>メインカメラ。</summary>
    [SerializeField] private Camera mainCamera;

    /// <summary>ゲーム UI の CanvasGroup。</summary>
    [SerializeField] private CanvasGroup gameUI;

    /// <summary>
    /// カメラのみ表示してゲーム UI を非表示にする。
    /// TitleScene 表示中に MainScene 側を初期化するために呼ぶ。
    /// </summary>
    public void ShowCameraOnly()
    {
        if (mainCamera != null)
        {
            mainCamera.depth   = 0;
            mainCamera.enabled = true;
        }
        if (gameUI != null)
        {
            gameUI.alpha          = 0f;
            gameUI.interactable   = false;
            gameUI.blocksRaycasts = false;
        }
    }

    /// <summary>ゲーム UI をフェードインし、インタラクティブ状態にする。</summary>
    /// <param name="duration">フェード秒数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    public async UniTask FadeGameUIInAsync(float duration, CancellationToken ct)
    {
        if (gameUI == null) return;
        float elapsed = 0f;
        gameUI.alpha = 0f;
        while (elapsed < duration)
        {
            elapsed    += Time.deltaTime;
            gameUI.alpha = Mathf.Lerp(0f, 1f, Mathf.Clamp01(elapsed / duration));
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }
        gameUI.alpha          = 1f;
        gameUI.interactable   = true;
        gameUI.blocksRaycasts = true;
    }
}
