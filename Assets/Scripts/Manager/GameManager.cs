using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UniRx;
using unityroom.Api;

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

    [Header("Player Death Effect")]
    [Tooltip("被撃破時の全画面フラッシュ色。アルファがピーク不透明度（薄い半透明の赤）。")]
    [SerializeField] private Color deathFlashColor = new Color(1f, 0f, 0f, 0.35f);
    [Tooltip("被撃破演出（フラッシュ＋シェイク）の長さ（秒）。")]
    [SerializeField] private float deathEffectDuration = 0.6f;
    [Tooltip("被撃破シェイクの振幅（m）。")]
    [SerializeField] private float deathShakeAmplitude = 0.2f;
    [Tooltip("被撃破シェイクの周波数（Hz）。")]
    [SerializeField] private float deathShakeFrequency = 25f;

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

    /// <summary>
    /// unityroom へスコアを送信済みかどうか。
    /// SendScore の二重送信（ランキング画面のフリーズ原因）を防ぐためのガードフラグ。
    /// </summary>
    private bool _isScoreSent;

    /// <summary>unityroom のスコアボードNo。</summary>
    private const int UnityroomBoardNo = 1;

    private void Start()
    {
        _mainCamera = Camera.main;
        if (timerController != null)
            timerController.OnTimeUp
                .Subscribe(_ => HandleTimeUp())
                .AddTo(this);

        // 寅などの攻撃でプレイヤーが死亡したらリザルトへ即時遷移する。
        if (player != null)
            player.OnDeath
                .Subscribe(_ => HandlePlayerDeath())
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

    /// <summary>プレイヤー死亡を検知したらリザルトへの即時遷移を開始する。</summary>
    private void HandlePlayerDeath()
    {
        if (_isGameOver) return;
        HandlePlayerDeathAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    /// <summary>
    /// プレイヤー死亡時の処理。タイムアップと異なり演出・遅延を挟まず、
    /// スコア確定後ただちに ResultScene へ遷移する。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    private async UniTaskVoid HandlePlayerDeathAsync(CancellationToken ct)
    {
        _isGameOver = true;

        // 入力・タイマー・スポーンを停止する。
        mainSceneActivator?.DisableInput();
        timerController?.Pause();
        enemySpawner?.StopSpawning();

        // スコアと経過タイムを ResultState に書き込む。
        var finalScore = ScoreManager.Instance?.Score ?? 0;
        if (resultState != null)
        {
            resultState.SetScore(finalScore);
            resultState.SetElapsedTime(timerController?.ElapsedTime ?? 0f);
        }

        // unityroom のスコアボードへ送信（ゲーム終了時に1回だけ）。
        SendScoreToUnityroom(finalScore);

        // リザルト中は入力を完全に無効化する。
        if (InputManager.Instance != null) InputManager.Instance.enabled = false;

        // 被撃破演出：全画面を薄い赤でフラッシュしつつカメラをシェイクする。
        // 入力（CameraController）は DisableInput 済みのためシェイクは上書きされない。
        await PlayerDeathEffectView.PlayAsync(
            _mainCamera, deathFlashColor, deathEffectDuration,
            deathShakeAmplitude, deathShakeFrequency, ct);

        // 演出後に ResultScene を additive でロード（MainScene はアンロードしない）。
        SceneLoader.Instance?.GenericTransitionAsync(
            config.GetSceneName(SceneType.Result),
            fromSceneName: null,
            config.FadeOutDuration,
            fadeOut: null,
            ct
        ).Forget();
    }

    /// <summary>
    /// unityroom のスコアボードへスコアを送信する。
    /// _isScoreSent ガードにより、ゲーム終了時に必ず1回だけ送信される。
    /// エディタ実行時は実際には送信されず、コンソールにログが出るのみ（ライブラリ仕様）。
    /// </summary>
    /// <param name="score">送信するスコア（int を float にキャストして渡す）</param>
    private void SendScoreToUnityroom(int score)
    {
        if (_isScoreSent) return;
        if (UnityroomApiClient.Instance == null) return;

        _isScoreSent = true;
        UnityroomApiClient.Instance.SendScore(UnityroomBoardNo, (float)score, ScoreboardWriteMode.HighScoreDesc);
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
        var finalScore = ScoreManager.Instance?.Score ?? 0;
        if (resultState != null)
        {
            resultState.SetScore(finalScore);
            resultState.SetElapsedTime(timerController?.ElapsedTime ?? 0f);
        }

        // 2-1. unityroom のスコアボードへスコアを送信する（ゲーム終了時に1回だけ）
        SendScoreToUnityroom(finalScore);

        // 3. カメラを Player から切り離してリザルト用位置へ移動
        if (_mainCamera != null && resultCameraAnchor != null)
        {
            _mainCamera.transform.SetParent(null);
            _mainCamera.transform.SetPositionAndRotation(
                resultCameraAnchor.position, resultCameraAnchor.rotation);
        }

        // 4. Spider に Intimidation アニメーションを再生させる
        player?.PlayIntimidation();

        // リザルト中は入力を完全に無効化する
        InputManager.Instance.enabled = false;

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
