using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// タイトル画面の UI 表示を担当する View クラス。
/// ロジックは持たず、イベント通知と表示操作のみを行う。
/// </summary>
public class TitleScreenView : MonoBehaviour
{
    /// <summary>ロゴ用 CanvasGroup。</summary>
    [SerializeField] private CanvasGroup logoGroup;

    /// <summary>ボタン群用 CanvasGroup。</summary>
    [SerializeField] private CanvasGroup buttonGroup;

    /// <summary>ゲームスタートボタン。</summary>
    [SerializeField] private Button startButton;

    /// <summary>ゲーム終了ボタン（省略可）。</summary>
    [SerializeField] private Button quitButton;

    private readonly Subject<Unit> _onStartButtonClicked = new Subject<Unit>();
    private readonly Subject<Unit> _onQuitButtonClicked  = new Subject<Unit>();

    /// <summary>スタートボタンが押されたときに発行されるストリーム。</summary>
    public IObservable<Unit> OnStartButtonClicked => _onStartButtonClicked;

    /// <summary>終了ボタンが押されたときに発行されるストリーム。</summary>
    public IObservable<Unit> OnQuitButtonClicked  => _onQuitButtonClicked;

    /// <summary>起動時に全 CanvasGroup を非表示・非インタラクティブ状態に初期化する。</summary>
    private void Awake()
    {
        ResetGroup(logoGroup);
        ResetGroup(buttonGroup);
    }

    /// <summary>ボタンのクリックイベントを UniRx ストリームに変換する。</summary>
    private void Start()
    {
        startButton.onClick.AsObservable()
            .Subscribe(_ => _onStartButtonClicked.OnNext(Unit.Default))
            .AddTo(this);

        if (quitButton != null)
            quitButton.onClick.AsObservable()
                .Subscribe(_ => _onQuitButtonClicked.OnNext(Unit.Default))
                .AddTo(this);
    }

    /// <summary>ボタン群のインタラクティブ状態を設定する。</summary>
    /// <param name="value">true で有効、false で無効。</param>
    public void SetButtonInteractable(bool value)
    {
        if (buttonGroup == null) return;
        buttonGroup.interactable   = value;
        buttonGroup.blocksRaycasts = value;
    }

    /// <summary>ロゴをフェードインする。</summary>
    /// <param name="duration">フェード秒数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    public async UniTask FadeLogoInAsync(float duration, CancellationToken ct)
    {
        await FadeGroupAsync(logoGroup, 0f, 1f, duration, ct);
    }

    /// <summary>ボタン群をフェードインし、インタラクティブ状態にする。</summary>
    /// <param name="duration">フェード秒数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    public async UniTask FadeButtonsInAsync(float duration, CancellationToken ct)
    {
        await FadeGroupAsync(buttonGroup, 0f, 1f, duration, ct);
        SetButtonInteractable(true);
    }

    /// <summary>この View 全体をフェードアウトする。</summary>
    /// <param name="duration">フェード秒数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    public async UniTask FadeAllOutAsync(float duration, CancellationToken ct)
    {
        var root = GetComponent<CanvasGroup>();
        if (root != null)
        {
            await FadeGroupAsync(root, root.alpha, 0f, duration, ct);
            return;
        }
        var t1 = FadeGroupAsync(logoGroup,   logoGroup   != null ? logoGroup.alpha   : 1f, 0f, duration, ct);
        var t2 = FadeGroupAsync(buttonGroup, buttonGroup != null ? buttonGroup.alpha : 1f, 0f, duration, ct);
        await UniTask.WhenAll(t1, t2);
    }

    /// <summary>CanvasGroup を指定時間でフェードさせるユーティリティ。</summary>
    private static async UniTask FadeGroupAsync(
        CanvasGroup g, float from, float to, float duration, CancellationToken ct)
    {
        if (g == null) return;
        float elapsed = 0f;
        g.alpha = from;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            g.alpha  = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }
        g.alpha = to;
    }

    /// <summary>CanvasGroup を alpha=0・非インタラクティブにリセットする。</summary>
    private static void ResetGroup(CanvasGroup g)
    {
        if (g == null) return;
        g.alpha          = 0f;
        g.interactable   = false;
        g.blocksRaycasts = false;
    }
}
