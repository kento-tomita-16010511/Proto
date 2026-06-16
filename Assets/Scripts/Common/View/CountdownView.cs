using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UniRx;
using UnityEngine;

/// <summary>
/// カウントダウン演出（3→2→1→GO!!）の UI 表示を担う View クラス。
/// DOTween によるスケール・フェードアニメーションを実行し、
/// GO!! 表示タイミングを IObservable で通知する。
/// </summary>
public class CountdownView : MonoBehaviour
{
    /// <summary>カウントダウンテキストを表示する TextMeshProUGUI。</summary>
    [SerializeField] private TextMeshProUGUI countdownText;

    /// <summary>フェードアウト用 CanvasGroup。</summary>
    [SerializeField] private CanvasGroup canvasGroup;

    /// <summary>各数字のスケールアニメーション時間（秒）。</summary>
    [SerializeField] private float scaleInDuration = 0.3f;

    /// <summary>各数字の表示維持時間（秒）。</summary>
    [SerializeField] private float holdDuration = 0.5f;

    /// <summary>各数字のフェードアウト時間（秒）。</summary>
    [SerializeField] private float fadeOutDuration = 0.2f;

    /// <summary>GO!! の表示維持時間（秒）。</summary>
    [SerializeField] private float goDuration = 0.8f;

    private readonly Subject<Unit> _onGoDisplayed = new Subject<Unit>();

    /// <summary>GO!! が画面に表示されたタイミングで発行されるストリーム。</summary>
    public IObservable<Unit> OnGoDisplayed => _onGoDisplayed;

    /// <summary>コンポーネント初期化時にテキストを非表示にする。</summary>
    private void Awake()
    {
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        if (countdownText != null) countdownText.text = "";
    }

    /// <summary>
    /// 3→2→1→GO!! をアニメーション付きで順に表示する。
    /// GO!! 表示タイミングで OnGoDisplayed を発行する。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    public async UniTask PlayCountAsync(CancellationToken ct)
    {
        for (int i = 3; i >= 1; i--)
            await ShowStepAsync(i.ToString(), holdDuration, ct);

        await ShowGoAsync(ct);
    }

    /// <summary>1ステップ（数字）を表示・維持・フェードアウトする。</summary>
    private async UniTask ShowStepAsync(string text, float hold, CancellationToken ct)
    {
        SetupText(text);
        await AnimScaleInAsync(ct);
        await UniTask.Delay(System.TimeSpan.FromSeconds(hold), cancellationToken: ct);
        await FadeOutAsync(ct);
    }

    /// <summary>GO!! を表示し、OnGoDisplayed を発行してから一定時間後にフェードアウトする。</summary>
    private async UniTask ShowGoAsync(CancellationToken ct)
    {
        SetupText("GO!!");
        await AnimScaleInAsync(ct);
        _onGoDisplayed.OnNext(Unit.Default);          // 入力有効化トリガー
        await UniTask.Delay(System.TimeSpan.FromSeconds(goDuration), cancellationToken: ct);
        await FadeOutAsync(ct);
    }

    /// <summary>テキストを設定し、スケールと alpha を初期値にリセットする。</summary>
    private void SetupText(string text)
    {
        if (countdownText != null) countdownText.text = text;
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        countdownText.transform.localScale = Vector3.one * 1.5f;
    }

    /// <summary>テキストを 1.5x → 1.0x にスケールアニメーションする。</summary>
    private async UniTask AnimScaleInAsync(CancellationToken ct)
    {
        var tween = countdownText.transform
            .DOScale(1f, scaleInDuration)
            .SetEase(Ease.OutBack)
            .SetLink(gameObject);
        await AwaitTween(tween, ct);
    }

    /// <summary>CanvasGroup をフェードアウトする。</summary>
    private async UniTask FadeOutAsync(CancellationToken ct)
    {
        if (canvasGroup == null) return;
        var tween = canvasGroup
            .DOFade(0f, fadeOutDuration)
            .SetLink(gameObject);
        await AwaitTween(tween, ct);
    }

    /// <summary>Tween の完了を UniTask で待機する。キャンセル時は Tween を Kill する。</summary>
    private static UniTask AwaitTween(Tween tween, CancellationToken ct)
    {
        var tcs = new UniTaskCompletionSource();
        tween.OnComplete(() => tcs.TrySetResult())
             .OnKill(() => tcs.TrySetResult());
        ct.Register(() =>
        {
            tween.Kill();
            tcs.TrySetCanceled();
        });
        return tcs.Task;
    }
}
