using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 任意のボタンに添付して設定パネルを開くコネクタ。
/// TitleScene・MainScene 共通で使用するシンプルなブリッジ。
/// </summary>
[RequireComponent(typeof(Button))]
public class SettingsOpenButton : MonoBehaviour
{
    /// <summary>開く対象の SettingsPresenter。</summary>
    [SerializeField] private SettingsPresenter settingsPresenter;

    private void Start()
    {
        var ct = this.GetCancellationTokenOnDestroy();
        GetComponent<Button>()
            .onClick.AsObservable()
            .Subscribe(_ => settingsPresenter?.OpenAsync(ct).Forget())
            .AddTo(this);
    }
}
