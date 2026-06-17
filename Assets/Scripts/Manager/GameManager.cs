using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UniRx;

/// <summary>
/// メインゲームのライフサイクルを管理するクラス。
/// タイムアップを検知し、リザルト演出と ResultScene への遷移を行う。
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TimerController timerController;
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private Player player;

    [Header("Result")]
    [SerializeField] private ResultState resultState;
    [SerializeField] private GameConfig config;

    [Header("Result Camera")]
    [Tooltip("ゲーム終了時にメインカメラを移動させる位置・向きを示す Transform。")]
    [SerializeField] private Transform resultCameraAnchor;

    /// <summary>設定ポップアップの prefab。ESC 押下時に都度生成する。</summary>
    [SerializeField] private SettingsPopup settingsPopupPrefab;

    [SerializeField] private MainSceneActivatorPresenter mainSceneActivator;

    private Camera _mainCamera;

    /// <summary>現在表示中の設定ポップアップ（未表示なら null）。</summary>
    private SettingsPopup _activePopup;

    /// <summary>
    /// タイムアップ後に true になる。ESC によるポーズを封じ、
    /// Result 画面中に設定ポップアップが開くのを防ぐ。
    /// </summary>
    private bool _isGameOver;

    private void Start()
    {
        _mainCamera = Camera.main;
        if (timerController != null)
            timerController.OnTimeUp
                .Subscribe(_ => HandleTimeUp())
                .AddTo(this);

        // Skip(1) で ReactiveProperty の購読時初期値発火を無視する。
        // これを怠ると MainScene ロード時（タイトル画面背景）に初期値 false が流れ、
        // else ブランチの UnfreezeAll() が走って入力が早期有効化されてしまう。
        InputManager.Instance.IsEscapePressed
            .Skip(1)
            .Subscribe(isPressed =>
            {
                // ゲームオーバー後は ESC によるポーズを無効化する
                if (_isGameOver) return;
                if (isPressed) EnterPause(); else ExitPause();
            })
            .AddTo(this);
    }

    /// <summary>
    /// ポーズに入る。タイマー停止 + ロジック Freeze を行い、設定ポップアップを表示する。
    /// ポーズ処理は GameManager が担当し、ポップアップの生成・表示は PopupManager に委譲する（機能分離）。
    /// </summary>
    private void EnterPause()
    {
        // Time.timeScale は使用禁止のため、タイマーは Pause() で個別に停止する（CLAUDE.md 規約）。
        timerController?.Pause();
        mainSceneActivator?.FreezeAll();
        OpenSettingsAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    /// <summary>ポーズを解除する。設定ポップアップを閉じ、タイマー / ロジックを再開する。</summary>
    private void ExitPause()
    {
        _activePopup?.RequestClose();
        timerController?.Resume();
        mainSceneActivator?.UnfreezeAll();
    }

    /// <summary>設定ポップアップを生成・表示し、閉じられたら ESC トグル状態を同期する。</summary>
    private async UniTaskVoid OpenSettingsAsync(CancellationToken ct)
    {
        if (_activePopup != null || PopupManager.Instance == null || settingsPopupPrefab == null) return;

        _activePopup = await PopupManager.Instance.ShowAsync(settingsPopupPrefab, ct);
        _activePopup.OnClosed
            .Subscribe(_ =>
            {
                _activePopup = null;
                // 閉じるボタンで閉じた場合に ESC トグル状態を false へ同期する。
                if (InputManager.Instance != null)
                    InputManager.Instance.IsEscapePressed.Value = false;
            })
            .AddTo(this);
    }

    private void HandleTimeUp()
    {
        HandleTimeUpAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    /// <summary>
    /// タイムアップ後の演出と ResultScene への遷移を非同期で実行する。
    /// </summary>
    private async UniTaskVoid HandleTimeUpAsync(CancellationToken ct)
    {
        _isGameOver = true;

        // MainScene が「プレイ中」でなくなったので入力を無効化する。
        // FreezeAll は使わない——Player のアニメーション（Intimidation）を維持するため。
        mainSceneActivator?.DisableInput();

        // 1. ゲームプレイを即時停止
        player?.SetInputEnabled(false);
        enemySpawner?.StopSpawning();

        // 2. スコアと経過タイムを ResultState に書き込む
        if (resultState != null)
        {
            resultState.SetScore(ScoreManager.Instance?.Score ?? 0);
            resultState.SetElapsedTime(timerController?.ElapsedTime ?? 0f);
        }

        // 3. カメラを Player から切り離してリザルト用位置へ移動
        if (_mainCamera != null && resultCameraAnchor != null)
        {
            _mainCamera.transform.SetParent(null);
            _mainCamera.transform.SetPositionAndRotation(
                resultCameraAnchor.position, resultCameraAnchor.rotation);
        }

        // 4. Spider に Intimidation アニメーションを再生させる
        player?.PlayIntimidation();

        // 5. 演出のための間を置いてから ResultScene を additive でロード
        await UniTask.Delay(System.TimeSpan.FromSeconds(0.5f), cancellationToken: ct);

        SceneLoader.Instance?.GenericTransitionAsync(
            config.GetSceneName(SceneType.Result),
            fromSceneName: null,        // MainScene はアンロードしない（Spider を映すため）
            config.FadeOutDuration,
            fadeOut: null,
            ct
        ).Forget();
    }
}
