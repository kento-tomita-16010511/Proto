using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

/// <summary>
/// 都度生成・破棄されるポップアップ UI の基底クラス。
/// PopupManager によって Instantiate され、OpenAsync でフェードイン、
/// RequestClose でフェードアウトして自身を破棄する。
/// フェードは Time.unscaledDeltaTime を使うため、ポーズ中（timeScale=0）でも進行する。
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public abstract class PopupBase : MonoBehaviour
{
    /// <summary>表示・ブロック制御用 CanvasGroup。未設定時は自動取得する。</summary>
    [SerializeField] private CanvasGroup canvasGroup;

    /// <summary>フェードイン・アウトの所要時間（秒）。</summary>
    [SerializeField] private float fadeDuration = 0.15f;

    private readonly Subject<Unit> _onClosed = new Subject<Unit>();

    /// <summary>ポップアップが閉じて破棄される直前に発行されるストリーム。</summary>
    public IObservable<Unit> OnClosed => _onClosed;

    private bool _isClosing;

    /// <summary>CanvasGroup を取得し、初期状態を非表示・非インタラクティブにする。</summary>
    protected virtual void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// フェードインして操作可能にする。生成直後に PopupManager から呼ばれる。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    public async UniTask OpenAsync(CancellationToken ct)
    {
        canvasGroup.blocksRaycasts = true;
        OnOpened();

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
    }

    /// <summary>
    /// フェードアウトして自身を破棄するよう要求する。多重呼び出しは無視される。
    /// </summary>
    public void RequestClose()
    {
        if (_isClosing) return;
        _isClosing = true;
        CloseAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    /// <summary>フェードアウト後に OnClosed を発行し GameObject を破棄する。</summary>
    private async UniTask CloseAsync(CancellationToken ct)
    {
        canvasGroup.interactable = false;
        float start = canvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, 0f, Mathf.Clamp01(elapsed / fadeDuration));
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }
        OnClosing();
        _onClosed.OnNext(Unit.Default);
        _onClosed.OnCompleted();
        Destroy(gameObject);
    }

    /// <summary>フェードイン開始時のフック。派生クラスで初期化に使う。</summary>
    protected virtual void OnOpened() { }

    /// <summary>破棄直前のフック。派生クラスで後始末に使う。</summary>
    protected virtual void OnClosing() { }
}
