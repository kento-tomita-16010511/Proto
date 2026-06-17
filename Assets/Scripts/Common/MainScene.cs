using UnityEngine;

/// <summary>
/// メインゲームシーンの起点オブジェクト。
/// MainScene という名前の GameObject にアタッチし、Presenterへの参照を保持する。
/// ロジックは MainSceneActivatorPresenter（ISceneLifecycle 実装）に委譲する。
/// </summary>
public class MainScene : BaseScene
{
}
