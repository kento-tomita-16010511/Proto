using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 設定画面の UI 表示を担う View クラス。
/// BGM/SE スライダーの値変化とパネルの開閉イベントを公開する。
/// </summary>
public class SettingsView : MonoBehaviour
{
    /// <summary>パネル全体の表示・ブロック制御用 CanvasGroup。</summary>
    [SerializeField] private CanvasGroup canvasGroup;

    /// <summary>BGM 音量スライダー。</summary>
    [SerializeField] private Slider bgmSlider;

    /// <summary>SE 音量スライダー。</summary>
    [SerializeField] private Slider seSlider;

    /// <summary>パネルを閉じるボタン。</summary>
    [SerializeField] private Button closeButton;

    private readonly Subject<float> _onBGMVolumeChanged = new Subject<float>();
    private readonly Subject<float> _onSEVolumeChanged  = new Subject<float>();
    private readonly Subject<Unit>  _onCloseClicked     = new Subject<Unit>();

    /// <summary>BGM スライダー値が変化したときに発行されるストリーム（0〜1）。</summary>
    public IObservable<float> OnBGMVolumeChanged => _onBGMVolumeChanged;

    /// <summary>SE スライダー値が変化したときに発行されるストリーム（0〜1）。</summary>
    public IObservable<float> OnSEVolumeChanged => _onSEVolumeChanged;

    /// <summary>閉じるボタンが押されたときに発行されるストリーム。</summary>
    public IObservable<Unit> OnCloseClicked => _onCloseClicked;

    /// <summary>初期状態では非表示・非インタラクティブに設定する。</summary>
    private void Awake()
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha          = 0f;
        canvasGroup.interactable   = false;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>スライダーとボタンのイベントを UniRx ストリームに変換する。</summary>
    private void Start()
    {
        if (bgmSlider != null)
            bgmSlider.OnValueChangedAsObservable()
                .Subscribe(v => _onBGMVolumeChanged.OnNext(v))
                .AddTo(this);

        if (seSlider != null)
            seSlider.OnValueChangedAsObservable()
                .Subscribe(v => _onSEVolumeChanged.OnNext(v))
                .AddTo(this);

        if (closeButton != null)
            closeButton.onClick.AsObservable()
                .Subscribe(_ => _onCloseClicked.OnNext(Unit.Default))
                .AddTo(this);
    }

    /// <summary>BGM スライダーの値をイベント発火なしで設定する。</summary>
    public void SetBGMSlider(float value)
    {
        if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(Mathf.Clamp01(value));
    }

    /// <summary>SE スライダーの値をイベント発火なしで設定する。</summary>
    public void SetSESlider(float value)
    {
        if (seSlider != null) seSlider.SetValueWithoutNotify(Mathf.Clamp01(value));
    }

    /// <summary>パネルをフェードインして操作可能にする。</summary>
    public async UniTask FadeInAsync(float duration, CancellationToken ct)
    {
        if (canvasGroup == null) return;
        canvasGroup.blocksRaycasts = true;
        float elapsed = 0f;
        canvasGroup.alpha = 0f;
        while (elapsed < duration)
        {
            elapsed           += Time.deltaTime;
            canvasGroup.alpha  = Mathf.Clamp01(elapsed / duration);
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }
        canvasGroup.alpha        = 1f;
        canvasGroup.interactable = true;
    }

    /// <summary>パネルをフェードアウトして非インタラクティブにする。</summary>
    public async UniTask FadeOutAsync(float duration, CancellationToken ct)
    {
        if (canvasGroup == null) return;
        canvasGroup.interactable = false;
        float elapsed = 0f;
        float start   = canvasGroup.alpha;
        while (elapsed < duration)
        {
            elapsed           += Time.deltaTime;
            canvasGroup.alpha  = Mathf.Lerp(start, 0f, Mathf.Clamp01(elapsed / duration));
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }
        canvasGroup.alpha          = 0f;
        canvasGroup.blocksRaycasts = false;
    }
}
