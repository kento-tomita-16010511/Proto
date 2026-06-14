using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

/// <summary>
/// メインゲームのライフサイクルを管理するクラス。
/// タイムアップを検知し、リザルト演出と ResultScene への遷移を行う。
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("References")]
    public TimerController timerController;
    public EnemySpawner    enemySpawner;
    public Player          player;

    [Header("Result")]
    [SerializeField] private ResultState resultState;
    [SerializeField] private GameConfig  config;

    [Header("Result Camera")]
    [Tooltip("ゲーム終了時にメインカメラを移動させる位置・向きを示す Transform。")]
    [SerializeField] private Transform resultCameraAnchor;

    private Camera _mainCamera;

    private void Start()
    {
        _mainCamera = Camera.main;
        if (timerController != null)
            timerController.OnTimeUp += HandleTimeUp;
    }

    private void OnDestroy()
    {
        if (timerController != null)
            timerController.OnTimeUp -= HandleTimeUp;
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
            config.ResultSceneName,
            fromSceneName: null,        // MainScene はアンロードしない（Spider を映すため）
            config.FadeOutDuration,
            fadeOut: null,
            ct
        ).Forget();
    }
}
