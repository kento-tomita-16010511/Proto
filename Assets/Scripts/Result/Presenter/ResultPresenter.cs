using System.Threading;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

/// <summary>
/// リザルト画面のボタン操作とシーン遷移を担う Presenter クラス。
/// ResultView のストリームを購読し、SceneLoader 経由でシーンを切り替える。
/// </summary>
public class ResultPresenter : MonoBehaviour, ISceneLifecycle
{
    /// <summary>リザルト画面の View。</summary>
    [SerializeField] private ResultView view;

    /// <summary>スコア・タイムデータの Model（ScriptableObject）。</summary>
    [SerializeField] private ResultState resultState;

    /// <summary>ゲーム設定 Model。遷移先シーン名を取得する。</summary>
    [SerializeField] private GameConfig config;

    /// <summary>ResultScene をフェードインし、ボタン購読と ResultState の表示バインドを開始する。</summary>
    private async UniTaskVoid Start()
    {
        var ct = this.GetCancellationTokenOnDestroy();
        await view.FadeInAsync(config.FadeOutDuration, ct);
        await OnAfterFadeInAsync(ct);
    }

    /// <summary>
    /// フェードイン完了後の処理：スコア/タイムのバインドとボタン購読を行う。ISceneLifecycle 実装。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    public async UniTask OnAfterFadeInAsync(CancellationToken ct)
    {
        // スコア・タイムを View にバインド
        resultState.Score
            .Subscribe(s => view.SetScore(s))
            .AddTo(this);

        resultState.ElapsedTime
            .Subscribe(t => view.SetTime(t))
            .AddTo(this);

        // タイトルへ戻る
        view.OnReturnToTitleClicked
            .Subscribe(_ => TransitionTo(config.TitleSceneName, ct))
            .AddTo(this);

        // リスタート
        view.OnRestartClicked
            .Subscribe(_ => TransitionTo(config.MainSceneName, ct))
            .AddTo(this);

        await UniTask.CompletedTask;
    }

    /// <summary>
    /// フェードアウト開始前の処理。現状は特になし（後処理が必要になればここに追加）。ISceneLifecycle 実装。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    public async UniTask OnBeforeFadeOutAsync(CancellationToken ct)
    {
        await UniTask.CompletedTask;
    }

    /// <summary>
    /// ResultScene と MainScene の両方をアンロードして指定シーンへ遷移する。
    /// </summary>
    private void TransitionTo(string toSceneName, CancellationToken ct)
    {
        SceneLoader.Instance?.TransitionFromResultAsync(
            toSceneName,
            config.ResultSceneName,
            config.MainSceneName,
            config.FadeOutDuration,
            c => view.FadeAllOutAsync(config.FadeOutDuration, c),
            ct
        ).Forget();
    }
}
