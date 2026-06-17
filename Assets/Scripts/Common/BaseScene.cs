using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

/// <summary>
/// 全シーンの起点となる基底クラス。
/// 各シーンに1つだけ存在する ~~Scene オブジェクトにアタッチする。
/// ロジックは持たず、~~ScenePresenter（ISceneLifecycle 実装）に委譲する。
/// </summary>
public abstract class BaseScene : MonoBehaviour
{
    /// <summary>このシーンのPresenter参照。Inspector から設定する。</summary>
    [SerializeField] private MonoBehaviour _presenterBehaviour;

    /// <summary>ISceneLifecycle として Presenter を参照するプロパティ。</summary>
    private ISceneLifecycle Presenter => _presenterBehaviour as ISceneLifecycle;

    /// <summary>
    /// フェードイン完了後に Presenter へ委譲する。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    public async UniTask OnAfterFadeInAsync(CancellationToken ct)
    {
        if (Presenter == null) return;
        await Presenter.OnAfterFadeInAsync(ct);
    }

    /// <summary>
    /// フェードアウト前に Presenter へ委譲する。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    public async UniTask OnBeforeFadeOutAsync(CancellationToken ct)
    {
        if (Presenter == null) return;
        await Presenter.OnBeforeFadeOutAsync(ct);
    }
}
