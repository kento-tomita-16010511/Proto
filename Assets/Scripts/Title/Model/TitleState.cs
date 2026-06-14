using UnityEngine;
using UniRx;

/// <summary>タイトルシーンの状態を管理する Model クラス（ScriptableObject）。</summary>
public enum ScenePhase { Title, Transitioning, InGame }

/// <summary>
/// タイトル遷移フェーズを ReactiveProperty で保持する ScriptableObject。
/// Presenter が Subscribe してフェーズ変化に反応できる。
/// </summary>
[CreateAssetMenu(fileName = "TitleState", menuName = "Game/TitleState")]
public class TitleState : ScriptableObject
{
    private readonly ReactiveProperty<ScenePhase> _currentPhase =
        new ReactiveProperty<ScenePhase>(ScenePhase.Title);

    /// <summary>現在のシーンフェーズ（読み取り専用の ReactiveProperty）。</summary>
    public IReadOnlyReactiveProperty<ScenePhase> CurrentPhase => _currentPhase;

    /// <summary>シーン遷移中かどうか。</summary>
    public bool IsTransitioning => _currentPhase.Value == ScenePhase.Transitioning;

    /// <summary>フェーズを設定する。</summary>
    /// <param name="phase">設定するフェーズ。</param>
    public void SetPhase(ScenePhase phase) => _currentPhase.Value = phase;

    /// <summary>ScriptableObject が有効化されたときにフェーズをリセットする。</summary>
    private void OnEnable() => _currentPhase.Value = ScenePhase.Title;
}
