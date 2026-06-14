using System.Threading;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

/// <summary>
/// 設定画面のロジックを担う Presenter クラス。
/// SettingsView のストリームを購読して SoundManager に音量変更を伝え、
/// PlayerPrefs への永続化も行う。
/// </summary>
public class SettingsPresenter : MonoBehaviour
{
    /// <summary>設定画面の View。</summary>
    [SerializeField] private SettingsView view;

    /// <summary>フェード時間などを参照するゲーム設定 ScriptableObject。</summary>
    [SerializeField] private GameConfig config;

    /// <summary>スライダーイベントを SoundManager にバインドする。</summary>
    private void Start()
    {
        var ct = this.GetCancellationTokenOnDestroy();

        view.OnBGMVolumeChanged
            .Subscribe(v => SoundManager.Instance?.SetBGMVolume(v))
            .AddTo(this);

        view.OnSEVolumeChanged
            .Subscribe(v => SoundManager.Instance?.SetSEVolume(v))
            .AddTo(this);

        // UniTaskVoid は fire-and-forget のため .Forget() 不要
        view.OnCloseClicked
            .Subscribe(_ => CloseAsync(ct).Forget())
            .AddTo(this);
    }

    /// <summary>
    /// 設定パネルを開く。各シーンの Opener から呼び出す。
    /// スライダーを現在の SoundManager の値に同期してからフェードインする。
    /// </summary>
    public async UniTask OpenAsync(CancellationToken ct)
    {
        if (SoundManager.Instance != null)
        {
            view.SetBGMSlider(SoundManager.Instance.BGMVolume);
            view.SetSESlider(SoundManager.Instance.SEVolume);
        }
        await view.FadeInAsync(config != null ? config.FadeInDuration * 0.5f : 0.3f, ct);
    }

    /// <summary>
    /// 設定パネルを閉じる。フェードアウトしてから完了する。
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    private async UniTask CloseAsync(CancellationToken ct)
    {
        await view.FadeOutAsync(config != null ? config.FadeInDuration * 0.5f : 0.3f, ct);
    }
}
