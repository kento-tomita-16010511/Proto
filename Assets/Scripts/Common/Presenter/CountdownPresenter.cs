using System.Threading;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

/// <summary>
/// カウントダウン演出のタイミングを制御する Presenter クラス。
/// MainSceneActivator から PlayAsync() を呼ばれ、GO!! 表示と同時に入力とタイマーを有効化する。
/// </summary>
public class CountdownPresenter : MonoBehaviour
{
    /// <summary>カウントダウン演出の View。</summary>
    [SerializeField] private CountdownView countdownView;

    /// <summary>GO!! のタイミングで入力を有効化するための View。</summary>
    [SerializeField] private InputGuardView inputGuard;

    private TimerController _timerController;

    /// <summary>同一 GO にアタッチされた TimerController を取得する。</summary>
    private void Awake()
    {
        _timerController = GetComponent<TimerController>();
    }

    /// <summary>GO!! 表示イベントを入力有効化とタイマー開始にバインドする。</summary>
    private void Start()
    {
        countdownView.OnGoDisplayed
            .Subscribe(_ =>
            {
                inputGuard?.EnableInput();
                _timerController?.StartTimer();
                SoundManager.Instance.PlayBGM(BGMEnum.MAIN, loop: true);
            })
            .AddTo(this);
    }

    /// <summary>
    /// 3→2→1→GO!! を再生する。MainSceneActivator.OnSceneTransitionComplete() から呼ぶ。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    public async UniTask PlayAsync(CancellationToken ct)
    {
        // カウントダウン開始と同時に SE を再生する（3→2→1→GO!! の演出に合わせる）。
        SoundManager.Instance?.PlaySE(SEEnum.Countdown);
        await countdownView.PlayCountAsync(ct);
    }
}
