using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// タイトル画面の入力とシーン遷移ロジックを担う Presenter クラス。
/// TitleScreenView のボタンイベントを購読し、SceneLoader 経由で遷移を開始する。
/// </summary>
public class TitlePresenter : MonoBehaviour, ISceneLifecycle
{
    /// <summary>タイトル画面の View。</summary>
    [SerializeField] private TitleScreenView view;

    /// <summary>タイトルシーンの状態 Model（ScriptableObject）。</summary>
    [SerializeField] private TitleState titleState;

    /// <summary>ゲーム設定 Model（ScriptableObject）。</summary>
    [SerializeField] private GameConfig config;

    /// <summary>設定ポップアップの prefab。タイトルでは PopupManager 経由で都度生成する。</summary>
    [SerializeField] private SettingsPopup settingsPopupPrefab;

    private void Awake()
    {
        Debug.Log("[TitlePresenter] Awake scene=" + gameObject.scene.name);
    }

    private void OnEnable()
    {
        Debug.Log("[TitlePresenter] OnEnable");
    }

    private void OnDisable()
    {
        Debug.LogWarning("[TitlePresenter] OnDisable ← something disabled this!");
    }

    /// <summary>ボタンイベントの購読を開始し、イントロ演出を再生する。</summary>
    private void Start()
    {
        Debug.Log("[TitlePresenter] Start called");
        var ct = this.GetCancellationTokenOnDestroy();

        // デバッグ用：最初の3フレームをログ出力（Update の代替）
        Observable.EveryUpdate()
            .Take(3)
            .Subscribe(count => Debug.Log("[TitlePresenter] Update #" + (count + 1)))
            .AddTo(this);

        view.OnStartButtonClicked
            .Subscribe(_ => HandleStart(ct))
            .AddTo(this);

        view.OnQuitButtonClicked
            .Subscribe(_ => HandleQuit())
            .AddTo(this);

        view.OnSettingsButtonClicked
            .Subscribe(_ =>
            {
                if (settingsPopupPrefab != null)
                    PopupManager.Instance?.ShowAsync(settingsPopupPrefab, ct).Forget();
            })
            .AddTo(this);

        OnAfterFadeInAsync(ct).Forget();
    }

    /// <summary>
    /// フェードイン完了後の処理：ロゴ → ボタンの順にフェードインするイントロ演出。
    /// ISceneLifecycle 実装。BaseScene 経由、または Start から呼ばれる。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    public async UniTask OnAfterFadeInAsync(CancellationToken ct)
    {
        Debug.Log("[TitlePresenter] PlayIntroAsync START ct.IsCancellationRequested=" + ct.IsCancellationRequested);
        try
        {
            await view.FadeLogoInAsync(config.FadeInDuration, ct);
            await UniTask.Delay(TimeSpan.FromSeconds(config.ButtonFadeInDelay), cancellationToken: ct);
            await view.FadeButtonsInAsync(config.FadeInDuration, ct);
            SoundManager.Instance.PlayBGM(BGMEnum.TITLE);
        }
        catch (System.OperationCanceledException)
        {
            Debug.LogWarning("[TitlePresenter] PlayIntroAsync CANCELLED");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[TitlePresenter] PlayIntroAsync EXCEPTION: " + e);
        }
    }

    /// <summary>
    /// フェードアウト開始前の処理：ボタン操作を無効化する。ISceneLifecycle 実装。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    public async UniTask OnBeforeFadeOutAsync(CancellationToken ct)
    {
        view.SetButtonInteractable(false);
        await UniTask.CompletedTask;
    }

    /// <summary>スタートボタン押下時の処理。遷移中は無視する。</summary>
    /// <param name="ct">キャンセルトークン。</param>
    private void HandleStart(CancellationToken ct)
    {
        if (titleState.IsTransitioning) return;
        titleState.SetPhase(ScenePhase.Transitioning);
        OnBeforeFadeOutAsync(ct).Forget();
        SceneLoader.Instance?.TransitionToMainAsync(config, view, ct).Forget();
    }

    /// <summary>終了ボタン押下時の処理。エディタでは再生停止、ビルドではアプリ終了。</summary>
    private void HandleQuit()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
