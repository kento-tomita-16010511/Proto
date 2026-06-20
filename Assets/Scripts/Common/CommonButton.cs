using UnityEngine;
using UnityEngine.UI;
using UniRx;

/// <summary>
/// UnityEngine.UI.Button のクリックイベントを購読し、共通のSEを再生するラッパークラス。
/// </summary>
public class CommonButton : Button
{
    [SerializeField] private SEEnum _seType = SEEnum.Button;

#pragma warning disable CS0114 // メンバーは継承されたメンバーを非表示にします。override キーワードがありません
    private void Awake()
#pragma warning restore CS0114 // メンバーは継承されたメンバーを非表示にします。override キーワードがありません
    {
        // Button コンポーネントを取得し、クリックイベントを UniRx で購読
        this.OnClickAsObservable()
            .Subscribe(_ => PlayClickSound())
            .AddTo(this);
    }

    private void PlayClickSound()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(_seType);
        }
    }
}