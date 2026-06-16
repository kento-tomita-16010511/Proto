using System;
using UnityEngine;
using UniRx;

/// <summary>
/// リザルト画面に表示するスコアと経過時間を保持する ScriptableObject。
/// 将来的なスコアシステムの差し替えを想定したプレースホルダ。
/// </summary>
[CreateAssetMenu(fileName = "ResultState", menuName = "Game/ResultState")]
public class ResultState : ScriptableObject
{
    private readonly ReactiveProperty<int>   _score       = new ReactiveProperty<int>(0);
    private readonly ReactiveProperty<float> _elapsedTime = new ReactiveProperty<float>(0f);

    /// <summary>現在のスコア（プレースホルダ）。</summary>
    public IReadOnlyReactiveProperty<int>   Score       => _score;

    /// <summary>経過タイム（プレースホルダ）。</summary>
    public IReadOnlyReactiveProperty<float> ElapsedTime => _elapsedTime;

    /// <summary>スコアを設定する。</summary>
    /// <param name="value">設定するスコア値。</param>
    public void SetScore(int value) => _score.Value = value;

    /// <summary>経過タイムを設定する。</summary>
    /// <param name="value">設定するタイム（秒）。</param>
    public void SetElapsedTime(float value) => _elapsedTime.Value = value;

    /// <summary>プレイ開始時にリセットする。</summary>
    private void OnEnable()
    {
        _score.Value       = 0;
        _elapsedTime.Value = 0f;
    }
}
