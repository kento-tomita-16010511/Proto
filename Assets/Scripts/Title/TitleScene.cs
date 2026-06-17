using UnityEngine;

/// <summary>
/// タイトルシーンの起点オブジェクト。
/// TitleScene という名前の GameObject にアタッチし、Presenterへの参照を保持する。
/// ロジックは TitleScenePresenter（ISceneLifecycle 実装）に委譲する。
/// </summary>
public class TitleScene : BaseScene
{
}
