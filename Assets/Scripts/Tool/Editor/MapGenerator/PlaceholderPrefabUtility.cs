using UnityEditor;
using UnityEngine;

/// <summary>
/// マップ生成用の仮Prefab(床・壁・ドア・天井)とマテリアルを自動生成するユーティリティクラス。
/// 生成した仮Prefabは後で本番アセットに差し替える前提のグレーボックス用アセット。
/// 状態は持たず、メソッドのみを提供する。
/// </summary>
public static class PlaceholderPrefabUtility
{
    /// <summary>仮アセットの保存先フォルダ</summary>
    private const string FolderPath = "Assets/Prefab/Map/Placeholder";

    /// <summary>床・天井の厚み(m)</summary>
    private const float SlabThickness = 0.1f;

    /// <summary>壁の厚み(m)</summary>
    private const float WallThickness = 0.15f;

    /// <summary>ドア開口部の幅(m)</summary>
    private const float DoorOpeningWidth = 1.8f;

    /// <summary>ドア開口部の高さ(m)</summary>
    private const float DoorOpeningHeight = 2.1f;

    /// <summary>
    /// 仮Prefab一式を生成し、設定アセットに割り当てる。
    /// </summary>
    /// <param name="config">割り当て先の設定アセット</param>
    public static void CreateAndAssign(HospitalMapConfigModel config)
    {
        EnsureFolders();
        var floorMaterial = CreateMaterialAsset("M_PlaceholderFloor", new Color(0.55f, 0.55f, 0.52f));
        var wallMaterial = CreateMaterialAsset("M_PlaceholderWall", new Color(0.85f, 0.82f, 0.72f));
        var doorMaterial = CreateMaterialAsset("M_PlaceholderDoor", new Color(0.35f, 0.25f, 0.20f));
        var floor = CreateFloorPrefab(config.CellSize, floorMaterial);
        var wall = CreateWallPrefab(config.CellSize, config.WallHeight, wallMaterial);
        var door = CreateDoorPrefab(config.CellSize, config.WallHeight, doorMaterial);
        var ceiling = CreateCeilingPrefab(config.CellSize, wallMaterial);
        AssignToConfig(config, floor, wall, door, ceiling);
    }

    /// <summary>保存先フォルダを(無ければ)作成する</summary>
    private static void EnsureFolders()
    {
        CreateFolderIfMissing("Assets", "Prefab");
        CreateFolderIfMissing("Assets/Prefab", "Map");
        CreateFolderIfMissing("Assets/Prefab/Map", "Placeholder");
    }

    /// <summary>指定フォルダが無ければ作成する</summary>
    private static void CreateFolderIfMissing(string parent, string child)
    {
        if (AssetDatabase.IsValidFolder($"{parent}/{child}")) return;

        AssetDatabase.CreateFolder(parent, child);
    }

    /// <summary>単色マテリアルアセットを生成する(既存があれば再利用)</summary>
    private static Material CreateMaterialAsset(string name, Color color)
    {
        var path = $"{FolderPath}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var material = new Material(shader != null ? shader : Shader.Find("Standard"));
        material.color = color;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    /// <summary>床タイルPrefabを生成する(原点=床面中央、上面がy=0)</summary>
    private static GameObject CreateFloorPrefab(float cellSize, Material material)
    {
        var root = new GameObject("FloorTile");
        CreateCubeChild(root.transform, material,
            new Vector3(cellSize, SlabThickness, cellSize),
            new Vector3(0f, -SlabThickness * 0.5f, 0f));
        return SaveAsPrefabAsset(root, "FloorTile");
    }

    /// <summary>壁Prefabを生成する(原点=下端中央、長さは東西方向)</summary>
    private static GameObject CreateWallPrefab(float cellSize, float wallHeight, Material material)
    {
        var root = new GameObject("Wall");
        CreateCubeChild(root.transform, material,
            new Vector3(cellSize, wallHeight, WallThickness),
            new Vector3(0f, wallHeight * 0.5f, 0f));
        return SaveAsPrefabAsset(root, "Wall");
    }

    /// <summary>ドア枠Prefabを生成する(原点=下端中央。左右の柱と鴨居で開口部を作る)</summary>
    private static GameObject CreateDoorPrefab(float cellSize, float wallHeight, Material material)
    {
        var root = new GameObject("DoorFrame");
        var postWidth = (cellSize - DoorOpeningWidth) * 0.5f;
        var postOffset = (cellSize - postWidth) * 0.5f;
        var postScale = new Vector3(postWidth, wallHeight, WallThickness);
        CreateCubeChild(root.transform, material, postScale, new Vector3(-postOffset, wallHeight * 0.5f, 0f));
        CreateCubeChild(root.transform, material, postScale, new Vector3(postOffset, wallHeight * 0.5f, 0f));
        CreateCubeChild(root.transform, material,
            new Vector3(DoorOpeningWidth, wallHeight - DoorOpeningHeight, WallThickness),
            new Vector3(0f, (wallHeight + DoorOpeningHeight) * 0.5f, 0f));
        return SaveAsPrefabAsset(root, "DoorFrame");
    }

    /// <summary>天井Prefabを生成する(原点=天井面中央、下面が原点の高さ)</summary>
    private static GameObject CreateCeilingPrefab(float cellSize, Material material)
    {
        var root = new GameObject("CeilingTile");
        CreateCubeChild(root.transform, material,
            new Vector3(cellSize, SlabThickness, cellSize),
            new Vector3(0f, SlabThickness * 0.5f, 0f));
        return SaveAsPrefabAsset(root, "CeilingTile");
    }

    /// <summary>マテリアルを適用したCubeを子として生成する</summary>
    private static void CreateCubeChild(Transform parent, Material material, Vector3 scale, Vector3 localPosition)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(parent);
        cube.transform.localScale = scale;
        cube.transform.localPosition = localPosition;
        cube.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    /// <summary>一時オブジェクトをPrefabアセットとして保存し、シーンから削除する</summary>
    private static GameObject SaveAsPrefabAsset(GameObject temporary, string name)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(temporary, $"{FolderPath}/{name}.prefab");
        Object.DestroyImmediate(temporary);
        return prefab;
    }

    /// <summary>生成したPrefabを設定アセットのシリアライズフィールドへ割り当てる</summary>
    private static void AssignToConfig(HospitalMapConfigModel config, GameObject floor, GameObject wall, GameObject door, GameObject ceiling)
    {
        var serialized = new SerializedObject(config);
        serialized.FindProperty("_floorPrefab").objectReferenceValue = floor;
        serialized.FindProperty("_wallPrefab").objectReferenceValue = wall;
        serialized.FindProperty("_doorPrefab").objectReferenceValue = door;
        serialized.FindProperty("_ceilingPrefab").objectReferenceValue = ceiling;
        serialized.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }
}
