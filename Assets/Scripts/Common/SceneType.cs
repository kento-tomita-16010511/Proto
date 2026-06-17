/// <summary>
/// プロジェクト内の全シーンを定義するEnum。
/// シーン遷移の指定には文字列ではなく、必ずこの SceneType を使用する。
/// シーンを追加した場合はここに追記し、BuildSettings にも必ず追加すること。
/// </summary>
public enum SceneType
{
    /// <summary>タイトルシーン。</summary>
    Title,

    /// <summary>メインゲームシーン。</summary>
    Main,

    /// <summary>リザルトシーン。</summary>
    Result,
}
