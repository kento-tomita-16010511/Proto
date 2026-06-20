using UnityEngine;
using UnityEngine.UI;
using UniRx;

/// <summary>
/// UnityEngine.UI.Button のクリックイベントを購読し、SEEnum.Button を再生するラッパークラス。
/// 既存のボタンコンポーネントと同じ GameObject にアタッチして使用します。
/// </summary>
[RequireComponent(typeof(Button))]
public class SoundButton : MonoBehaviour
{
    [SerializeField] private SEEnum seType = SEEnum.Button;

    private void Start()
    {
        // Button コンポーネントを取得し、クリックイベントを UniRx で購読
        var button = GetComponent<Button>();
        button.OnClickAsObservable()
            .Subscribe(_ => PlayClickSound())
            .AddTo(this);
    }

    private void PlayClickSound()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(seType);
        }
    }
}