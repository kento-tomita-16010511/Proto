using UnityEngine;
using TMPro;
using System;
using UniRx;

/// <summary>ゲーム内タイマーの計測と表示を担うコンポーネント。</summary>
public class TimerController : MonoBehaviour
{
    /// <summary>カウントダウン開始秒数。</summary>
    [SerializeField] private float startSeconds = 60f;

    /// <summary>
    /// タイマーの残り時間を表示する TMP_Text。
    /// 省略時は同一 GO の TMP_Text を自動取得する。別 GO の TMP_Text も Inspector でアサイン可能。
    /// </summary>
    [SerializeField] private TMP_Text timerDisplay;

    /// <summary>タイムアップ時に発行されるイベント。</summary>
    public event Action OnTimeUp;

    /// <summary>現在の残り時間（秒）。</summary>
    public float Remaining => _remaining;

    /// <summary>タイマー開始からの経過時間（秒）。</summary>
    public float ElapsedTime => startSeconds - _remaining;

    private float _remaining;
    private bool  _running;

    private void Start()
    {
        if (timerDisplay == null) timerDisplay = GetComponent<TMP_Text>();
        _remaining = startSeconds;
        _running   = false;   // StartTimer() が呼ばれるまで計測しない
        UpdateDisplay();

        // 毎フレームのカウントダウンは Update を使わず EveryUpdate で行う（CLAUDE.md 規約）
        Observable.EveryUpdate()
            .Where(_ => _running)
            .Subscribe(_ => TickTimer())
            .AddTo(this);
    }

    /// <summary>
    /// タイマーを開始する。
    /// CountdownPresenter が GO!! 表示タイミングで呼び出す。
    /// </summary>
    public void StartTimer()
    {
        _remaining = startSeconds;
        _running   = true;
    }

    /// <summary>毎フレームのカウントダウン処理。EveryUpdate から _running 中のみ呼ばれる。</summary>
    private void TickTimer()
    {
        _remaining -= Time.deltaTime;
        if (_remaining <= 0f)
        {
            _remaining = 0f;
            _running   = false;
            OnTimeUp?.Invoke();
        }
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (timerDisplay != null)
            timerDisplay.text = Mathf.CeilToInt(_remaining).ToString();
    }
}
