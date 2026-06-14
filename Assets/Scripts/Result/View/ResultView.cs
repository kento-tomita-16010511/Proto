using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// リザルト画面の UI 表示を担う View クラス。
/// ボタン押下をストリームで公開し、スコア・タイム表示メソッドを提供する。
/// </summary>
public class ResultView : MonoBehaviour
{
    /// <summary>「タイトルへ戻る」ボタン。</summary>
    [SerializeField] private Button returnButton;

    /// <summary>「リスタート」ボタン。</summary>
    [SerializeField] private Button restartButton;

    /// <summary>スコア表示テキスト。</summary>
    [SerializeField] private TextMeshProUGUI scoreText;

    /// <summary>タイム表示テキスト。</summary>
    [SerializeField] private TextMeshProUGUI timeText;

    /// <summary>フェードアウト用 CanvasGroup（ResultCanvas のルートに配置）。</summary>
    [SerializeField] private CanvasGroup canvasGroup;

    private readonly Subject<Unit> _onReturnToTitleClicked = new Subject<Unit>();
    private readonly Subject<Unit> _onRestartClicked       = new Subject<Unit>();

    /// <summary>「タイトルへ戻る」ボタン押下ストリーム。</summary>
    public IObservable<Unit> OnReturnToTitleClicked => _onReturnToTitleClicked;

    /// <summary>「リスタート」ボタン押下ストリーム。</summary>
    public IObservable<Unit> OnRestartClicked => _onRestartClicked;

    /// <summary>ボタンのクリックイベントを UniRx ストリームに変換する。</summary>
    private void Start()
    {
        returnButton.onClick.AsObservable()
            .Subscribe(_ => _onReturnToTitleClicked.OnNext(Unit.Default))
            .AddTo(this);

        restartButton.onClick.AsObservable()
            .Subscribe(_ => _onRestartClicked.OnNext(Unit.Default))
            .AddTo(this);
    }

    /// <summary>スコアを表示する。</summary>
    /// <param name="score">表示するスコア値。</param>
    public void SetScore(int score)
    {
        if (scoreText != null) scoreText.text = "SCORE: " + score;
    }

    /// <summary>経過タイムを表示する。</summary>
    /// <param name="time">表示するタイム（秒）。</param>
    public void SetTime(float time)
    {
        if (timeText != null) timeText.text = string.Format("TIME: {0:F2}", time);
    }

    /// <summary>リザルト Canvas 全体をフェードアウトする。</summary>
    /// <param name="duration">フェード秒数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    public async UniTask FadeAllOutAsync(float duration, CancellationToken ct)
    {
        if (canvasGroup == null) return;
        float elapsed = 0f;
        float start   = canvasGroup.alpha;
        while (elapsed < duration)
        {
            elapsed          += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, 0f, Mathf.Clamp01(elapsed / duration));
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }
        canvasGroup.alpha = 0f;
    }

    /// <summary>ResultScene ロード時に Canvas をフェードインする。</summary>
    /// <param name="duration">フェード秒数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    public async UniTask FadeInAsync(float duration, CancellationToken ct)
    {
        if (canvasGroup == null) return;
        float elapsed = 0f;
        canvasGroup.alpha = 0f;
        while (elapsed < duration)
        {
            elapsed          += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }
        canvasGroup.alpha = 1f;
    }
}
