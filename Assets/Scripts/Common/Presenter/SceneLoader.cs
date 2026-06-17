using System;
using System.Linq;
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
    /// <param name="callerCt">呼び出し元（TitlePresenter）のキャンセルトークン。</param>
    public async UniTask TransitionToMainAsync(
        GameConfig config, TitleScreenView fromView, CancellationToken callerCt)
    {
        if (_isTransitioning) return;
        _isTransitioning = true;

        try
        {
            var toSceneName = config.MainSceneName;
            var fromSceneName = fromView != null ? fromView.gameObject.scene.name : null;
            float halfDuration = config.FadeOutDuration * 0.5f;

            // フェードアウトは呼び出し元（TitlePresenter）のトークンで実行
            if (fromView != null) await fromView.FadeAllOutAsync(halfDuration, callerCt);

            await UniTask.NextFrame(callerCt);

            // MainScene の UI をフェードインし、起点(BaseScene)経由でライフサイクルを呼ぶ
            var mainScene = FindInScene<BaseScene>(toSceneName);
            var toView = FindInScene<MainSceneView>(toSceneName);

            if (toView != null) await toView.FadeGameUIInAsync(halfDuration, callerCt);
            if (mainScene != null)
                await mainScene.OnAfterFadeInAsync(callerCt);
            else
                FindInScene<MainSceneActivatorPresenter>(toSceneName)?.OnSceneTransitionComplete();

            // TitleScene をアンロード。
            // アンロード時に TitlePresenter が破棄され callerCt が失効するため、
            // 自身（PersistentScene 常駐）の生存トークンを使う。
            if (!string.IsNullOrEmpty(fromSceneName))
            {
                var ct = this.GetCancellationTokenOnDestroy();
                await SceneManager.UnloadSceneAsync(fromSceneName).ToUniTask(cancellationToken: ct);
            }
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
    /// <param name="callerCt">呼び出し元（ResultPresenter）のキャンセルトークン。</param>
    public async UniTask TransitionFromResultAsync(
        string toSceneName,
        string resultSceneName,
        string mainSceneName,
        float fadeDuration,
        Func<CancellationToken, UniTask> fadeOut,
        CancellationToken callerCt)
    {
        if (_isTransitioning) return;
        _isTransitioning = true;

        try
        {
            // フェードアウトは呼び出し元（ResultPresenter）のトークンで実行
            if (fadeOut != null) await fadeOut(callerCt);

            // ResultScene をアンロードすると ResultPresenter が破棄され callerCt が失効する。
            // 以降は自身（PersistentScene 常駐）の生存トークンを使う。
            // ※ countdown 修正と同じ根本原因：呼び出し元シーンの破棄でトークンが切れる。
            var ct = this.GetCancellationTokenOnDestroy();

            // ResultScene と MainScene を両方アンロード → MainScene を初期状態で再生成する
            await SceneManager.UnloadSceneAsync(resultSceneName).ToUniTask(cancellationToken: ct);
            await SceneManager.UnloadSceneAsync(mainSceneName).ToUniTask(cancellationToken: ct);

            // 遷移先をロード
            await SceneManager.LoadSceneAsync(toSceneName, LoadSceneMode.Additive)
                              .ToUniTask(cancellationToken: ct);

            if (toSceneName == mainSceneName)
            {
                // リスタート: 新しく生成された MainScene でカウントダウンから再開
                await UniTask.NextFrame(ct);
                var mainScene = FindInScene<BaseScene>(toSceneName);
                var toView = FindInScene<MainSceneView>(toSceneName);
                if (toView != null) await toView.FadeGameUIInAsync(fadeDuration * 0.5f, ct);
                if (mainScene != null)
                    await mainScene.OnAfterFadeInAsync(ct);
                else
                    FindInScene<MainSceneActivatorPresenter>(toSceneName)?.OnSceneTransitionComplete();
            }
            else
            {
                // タイトルへ戻る: 新しい MainScene を背景として additive ロード（初期化済み状態）
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
        return scene.GetRootGameObjects()
            .Select(root => root.GetComponentInChildren<T>(true))
            .FirstOrDefault(comp => comp != null);
    }
}
