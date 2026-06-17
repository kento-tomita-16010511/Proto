using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// シーンのライフサイクルイベントを定義するインターフェース。
/// ~~ScenePresenter に実装し、BaseScene から委譲で呼ばれる。
/// </summary>
public interface ISceneLifecycle
{
    /// <summary>
    /// シーンフェードイン完了後に呼ばれる。
    /// BGM再生・初期アニメーションの開始などに使用する。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    UniTask OnAfterFadeInAsync(CancellationToken ct);

    /// <summary>
    /// シーンフェードアウト開始前に呼ばれる。
    /// SEの停止・後処理などに使用する。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    UniTask OnBeforeFadeOutAsync(CancellationToken ct);
}
