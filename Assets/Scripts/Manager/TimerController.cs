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

    /// <summary>タイムアップ時に通知するイベント。</summary>
    public IObservable<Unit> OnTimeUp => _onTimeUp;
    private readonly Subject<Unit> _onTimeUp = new Subject<Unit>();

    /// <summary>現在の残り時間（秒）。</summary>
    public float Remaining => _remaining;

    /// <summary>タイマー開始からの経過時間（秒）。</summary>
    public float ElapsedTime => startSeconds - _remaining;

    private float _remaining;
    private bool  _running;

    /// <summary>一時停止中フラグ（残り時間は保持したままカウントを止める）。</summary>
    private bool  _paused;

    private void Start()
    {
        if (timerDisplay == null) timerDisplay = GetComponent<TMP_Text>();
        _remaining = startSeconds;
        _running   = false;   // StartTimer() が呼ばれるまで計測しない
        UpdateDisplay();

        _onTimeUp.AddTo(this);

        // 毎フレームのカウントダウンは Update を使わず EveryUpdate で行う（CLAUDE.md 規約）
        // 一時停止中（_paused）はカウントしない
        Observable.EveryUpdate()
            .Where(_ => _running && !_paused)
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

    /// <summary>カウントダウンを一時停止する（残り時間は保持）。設定ポップアップ表示中などに使う。</summary>
    public void Pause() => _paused = true;

    /// <summary>一時停止したカウントダウンを再開する。</summary>
    public void Resume() => _paused = false;

    /// <summary>毎フレームのカウントダウン処理。EveryUpdate から _running 中のみ呼ばれる。</summary>
    private void TickTimer()
    {
        _remaining -= Time.deltaTime;
        if (_remaining <= 0f)
        {
            _remaining = 0f;
            _running   = false;
            _onTimeUp.OnNext(Unit.Default);
        }
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (timerDisplay != null)
            timerDisplay.text = Mathf.CeilToInt(_remaining).ToString();
    }
}
