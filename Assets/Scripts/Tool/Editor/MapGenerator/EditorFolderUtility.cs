using UnityEditor;

/// <summary>
/// エディタ用アセットフォルダの作成を補助するユーティリティクラス。
/// 状態は持たず、メソッドのみを提供する。
/// </summary>
public static class EditorFolderUtility
{
    /// <summary>
    /// "Assets/..." 形式のフォルダパスを(途中の階層も含めて)無ければ作成する。
    /// </summary>
    /// <param name="folderPath">作成するフォルダパス</param>
    public static void EnsureFolder(string folderPath)
    {
        var segments = folderPath.Split('/');
        var current = segments[0];
        for (var i = 1; i < segments.Length; i++)
        {
            CreateIfMissing(current, segments[i]);
            current = $"{current}/{segments[i]}";
        }
    }

    /// <summary>指定フォルダが無ければ作成する</summary>
    private static void CreateIfMissing(string parent, string child)
    {
        if (AssetDatabase.IsValidFolder($"{parent}/{child}")) return;

        AssetDatabase.CreateFolder(parent, child);
    }
}
