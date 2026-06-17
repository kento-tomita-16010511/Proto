using UnityEngine;

/// <summary>
/// ゲーム全体の設定値を保持する ScriptableObject。
/// シーン名やフェード時間など、複数の Presenter が参照する定数をまとめる。
/// </summary>
[CreateAssetMenu(fileName = "GameConfig", menuName = "Game/GameConfig")]
public class GameConfig : ScriptableObject
{
    /// <summary>タイトルシーンのビルド名。</summary>
    [SerializeField] private string titleSceneName = "TitleScene";

    /// <summary>メインゲームシーンのビルド名。</summary>
    [SerializeField] private string mainSceneName = "MainScene";

    /// <summary>パーシスタントシーンのビルド名。</summary>
    [SerializeField] private string persistentSceneName = "PersistentScene";

    /// <summary>リザルトシーンのビルド名。</summary>
    [SerializeField] private string resultSceneName = "ResultScene";

    /// <summary>フェードイン演出の秒数。</summary>
    [SerializeField] private float fadeInDuration = 0.8f;

    /// <summary>フェードアウト演出の秒数。</summary>
    [SerializeField] private float fadeOutDuration = 0.8f;

    /// <summary>ロゴフェード後にボタンをフェードインするまでの待機秒数。</summary>
    [SerializeField] private float buttonFadeInDelay = 0.6f;

    /// <inheritdoc cref="titleSceneName"/>
    public string TitleSceneName => titleSceneName;

    /// <inheritdoc cref="mainSceneName"/>
    public string MainSceneName => mainSceneName;

    /// <inheritdoc cref="persistentSceneName"/>
    public string PersistentSceneName => persistentSceneName;

    /// <inheritdoc cref="resultSceneName"/>
    public string ResultSceneName => resultSceneName;

    /// <inheritdoc cref="fadeInDuration"/>
    public float FadeInDuration => fadeInDuration;

    /// <inheritdoc cref="fadeOutDuration"/>
    public float FadeOutDuration => fadeOutDuration;

    /// <inheritdoc cref="buttonFadeInDelay"/>
    public float ButtonFadeInDelay => buttonFadeInDelay;

    /// <summary>
    /// SceneType に対応するシーンのビルド名を返す。
    /// シーン遷移は文字列直書きではなく SceneType + このメソッドで解決する。
    /// </summary>
    /// <param name="sceneType">対象シーン。</param>
    public string GetSceneName(SceneType sceneType)
    {
        switch (sceneType)
        {
            case SceneType.Title:  return titleSceneName;
            case SceneType.Main:   return mainSceneName;
            case SceneType.Result: return resultSceneName;
            default:               return null;
        }
    }
}
