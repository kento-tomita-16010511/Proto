using UniRx;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 音量設定ポップアップ。PopupBase を継承し、BGM/SE スライダーと閉じるボタンを持つ。
/// 開いたとき SoundManager の現在値にスライダーを同期し、変更を SoundManager へ反映する。
/// （旧 SettingsView + SettingsPresenter を統合したもの。）
/// </summary>
public class SettingsPopup : PopupBase
{
    /// <summary>BGM 音量スライダー。</summary>
    [SerializeField] private Slider bgmSlider;

    /// <summary>SE 音量スライダー。</summary>
    [SerializeField] private Slider seSlider;

    /// <summary>パネルを閉じるボタン。</summary>
    [SerializeField] private Button closeButton;

    /// <summary>スライダー・ボタンのイベントを購読する。</summary>
    protected override void Awake()
    {
        base.Awake();

        // Skip(1): OnValueChangedAsObservable は購読時に現在値を流すため、
        // prefab 既定値が SoundManager に書き込まれるのを防ぐ。
        if (bgmSlider != null)
            bgmSlider.OnValueChangedAsObservable()
                .Skip(1)
                .Subscribe(v => SoundManager.Instance?.SetBGMVolume(v))
                .AddTo(this);

        if (seSlider != null)
            seSlider.OnValueChangedAsObservable()
                .Skip(1)
                .Subscribe(v => SoundManager.Instance?.SetSEVolume(v))
                .AddTo(this);

        if (closeButton != null)
            closeButton.onClick.AsObservable()
                .Subscribe(_ => RequestClose())
                .AddTo(this);
    }

    /// <summary>開いた瞬間にスライダーを現在の音量へ同期する（イベント発火なし）。</summary>
    protected override void OnOpened()
    {
        if (SoundManager.Instance == null) return;
        if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(Mathf.Clamp01(SoundManager.Instance.BGMVolume));
        if (seSlider != null) seSlider.SetValueWithoutNotify(Mathf.Clamp01(SoundManager.Instance.SEVolume));
    }
}
