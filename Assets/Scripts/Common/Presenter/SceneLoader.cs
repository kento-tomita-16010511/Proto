using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// シーンのロードとフェード演出を一括して管理する Presenter クラス（シングルトン）。
/// PersistentScene に配置し、ゲーム全体のシーン遷移を担う。
/// </summary>
public class SceneLoader : MonoBehaviour
{
    /// <summary>シングルトンインスタンス。</summary>
    public static SceneLoader Instance { get; private set; }

    private bool _isTransitioning;

    /// <summary>重複インスタンスを排除し、シングルトンを確立する。</summary>
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// TitleScene からメインゲームへフェード遷移する。
    /// MainScene は起動時に additive ロード済みのため、フェードと UI 表示のみ行う。
    /// </summary>
    /// <param name="config">シーン名やフェード時間を含む設定 ScriptableObject。</param>
    /// <param name="fromView">フェードアウトを実行する TitleScreenView。</param>
    /// <param name="ct">キャンセルトークン。</param>
    public async UniTask TransitionToMainAsync(
        GameConfig config, TitleScreenView fromView, CancellationToken ct)
    {
        if (_isTransitioning) return;
        _isTransitioning = true;

        try
        {
            var   toSceneName   = config.MainSceneName;
            var   fromSceneName = fromView != null ? fromView.gameObject.scene.name : null;
            float halfDuration  = config.FadeOutDuration * 0.5f;

            // TitleScene をフェードアウト
            if (fromView != null) await fromView.FadeAllOutAsync(halfDuration, ct);

            await UniTask.NextFrame(ct);

            // MainScene の UI をフェードインしてカウントダウンを開始
            var activator = FindInScene<MainSceneActivator>(toSceneName);
            var toView    = FindInScene<MainSceneView>(toSceneName);

            if (toView != null) await toView.FadeGameUIInAsync(halfDuration, ct);
            activator?.OnSceneTransitionComplete();

            // TitleScene をアンロード
            if (!string.IsNullOrEmpty(fromSceneName))
                await SceneManager.UnloadSceneAsync(fromSceneName).ToUniTask(cancellationToken: ct);
        }
        finally
        {
            _isTransitioning = false;
        }
    }

    /// <summary>
    /// 汎用シーン遷移。fromSceneName をアンロードし toSceneName を additive でロードする。
    /// fromSceneName が null または空の場合はアンロードしない。
    /// </summary>
    public async UniTask GenericTransitionAsync(
        string toSceneName,
        string fromSceneName,
        float fadeDuration,
        Func<CancellationToken, UniTask> fadeOut,
        CancellationToken ct)
    {
        if (_isTransitioning) return;
        _isTransitioning = true;

        try
        {
            if (fadeOut != null) await fadeOut(ct);

            await SceneManager.LoadSceneAsync(toSceneName, LoadSceneMode.Additive)
                              .ToUniTask(cancellationToken: ct);

            if (!string.IsNullOrEmpty(fromSceneName))
                await SceneManager.UnloadSceneAsync(fromSceneName)
                                  .ToUniTask(cancellationToken: ct);
        }
        finally
        {
            _isTransitioning = false;
        }
    }

    /// <summary>
    /// ResultScene から退場する遷移。
    /// ResultScene と MainScene を両方アンロードしてから toSceneName へ遷移する。
    /// toSceneName が MainSceneName の場合はリスタート（カウントダウンから再開）、
    /// それ以外（TitleScene 等）の場合は MainScene を背景として再ロードしてタイトルへ戻る。
    /// </summary>
    /// <param name="toSceneName">遷移先シーン名。</param>
    /// <param name="resultSceneName">アンロードする ResultScene 名。</param>
    /// <param name="mainSceneName">アンロードする MainScene 名。</param>
    /// <param name="fadeDuration">フェード時間（秒）。</param>
    /// <param name="fadeOut">フェードアウト処理。</param>
    /// <param name="ct">キャンセルトークン。</param>
    public async UniTask TransitionFromResultAsync(
        string toSceneName,
        string resultSceneName,
        string mainSceneName,
        float fadeDuration,
        Func<CancellationToken, UniTask> fadeOut,
        CancellationToken ct)
    {
        if (_isTransitioning) return;
        _isTransitioning = true;

        try
        {
            if (fadeOut != null) await fadeOut(ct);

            // ResultScene と MainScene を両方アンロード
            await SceneManager.UnloadSceneAsync(resultSceneName).ToUniTask(cancellationToken: ct);
            await SceneManager.UnloadSceneAsync(mainSceneName).ToUniTask(cancellationToken: ct);

            // 遷移先をロード
            await SceneManager.LoadSceneAsync(toSceneName, LoadSceneMode.Additive)
                              .ToUniTask(cancellationToken: ct);

            if (toSceneName == mainSceneName)
            {
                // リスタート: カウントダウンから再開
                await UniTask.NextFrame(ct);
                var activator = FindInScene<MainSceneActivator>(toSceneName);
                var toView    = FindInScene<MainSceneView>(toSceneName);
                if (toView != null) await toView.FadeGameUIInAsync(fadeDuration * 0.5f, ct);
                activator?.OnSceneTransitionComplete();
            }
            else
            {
                // タイトルへ戻る: MainScene を背景として再ロード
                await SceneManager.LoadSceneAsync(mainSceneName, LoadSceneMode.Additive)
                                  .ToUniTask(cancellationToken: ct);
                SceneManager.SetActiveScene(SceneManager.GetSceneByName(toSceneName));
            }
        }
        finally
        {
            _isTransitioning = false;
        }
    }

    /// <summary>指定シーン内から特定コンポーネントを検索する。</summary>
    private static T FindInScene<T>(string sceneName) where T : Component
    {
        var scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid()) return null;
        foreach (var root in scene.GetRootGameObjects())
        {
            var comp = root.GetComponentInChildren<T>(true);
            if (comp != null) return comp;
        }
        return null;
    }
}
