using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 病院マップをエディタ上で生成するツールウィンドウ。
/// Docs/MapDesign.md のレイアウトに基づき、廊下骨格→部屋の順でマップを構築する。
/// メニュー: Tools > Hospital Map Generator
/// </summary>
public class HospitalMapGeneratorWindow : EditorWindow
{
    /// <summary>設定アセットの既定保存パス</summary>
    private const string ConfigAssetPath = "Assets/ScriptableObjects/Map/HospitalMapConfig.asset";

    /// <summary>マップ生成設定アセット</summary>
    [SerializeField] private HospitalMapConfigModel _config;

    /// <summary>生成時に既存Terrainを非アクティブ化するかどうか</summary>
    [SerializeField] private bool _deactivateTerrain = true;

    /// <summary>生成時に既存のエリアPrefabを作り直すかどうか(OFFなら編集済みPrefabを保持して再利用)</summary>
    [SerializeField] private bool _overwriteAreaPrefabs = false;

    /// <summary>ウィンドウのスクロール位置</summary>
    private Vector2 _scrollPosition;

    /// <summary>ウィンドウを開く</summary>
    [MenuItem("Tools/Hospital Map Generator")]
    public static void ShowWindow()
    {
        GetWindow<HospitalMapGeneratorWindow>("Hospital Map Generator");
    }

    /// <summary>ウィンドウのGUIを描画する</summary>
    private void OnGUI()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        DrawConfigSection();
        DrawPlaceholderSection();
        DrawGenerateSection();
        DrawNavMeshSection();
        EditorGUILayout.EndScrollView();
    }

    /// <summary>設定アセットの指定・作成セクションを描画する</summary>
    private void DrawConfigSection()
    {
        EditorGUILayout.LabelField("1. 設定", EditorStyles.boldLabel);
        _config = EditorGUILayout.ObjectField("Config", _config, typeof(HospitalMapConfigModel), false) as HospitalMapConfigModel;
        if (GUILayout.Button("設定アセットを作成"))
        {
            CreateOrLoadConfigAsset();
        }
        EditorGUILayout.Space();
    }

    /// <summary>仮Prefab生成セクションを描画する</summary>
    private void DrawPlaceholderSection()
    {
        EditorGUILayout.LabelField("2. 仮Prefab", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(_config == null))
        {
            if (GUILayout.Button("仮Prefabを生成して割り当て"))
            {
                PlaceholderPrefabUtility.CreateAndAssign(_config);
            }
        }
        EditorGUILayout.Space();
    }

    /// <summary>検証・マップ生成セクションを描画する</summary>
    private void DrawGenerateSection()
    {
        EditorGUILayout.LabelField("3. マップ生成", EditorStyles.boldLabel);
        _deactivateTerrain = EditorGUILayout.Toggle("Terrainを非アクティブ化", _deactivateTerrain);
        _overwriteAreaPrefabs = EditorGUILayout.Toggle(
            new GUIContent("部屋Prefabを作り直す", "ONにすると既存のエリアPrefab(Assets/Prefab/Map/Areas)を作り直します。Prefabへの編集内容は失われます。OFFなら編集済みPrefabをそのまま再利用します。"),
            _overwriteAreaPrefabs);
        if (GUILayout.Button("レイアウトを検証"))
        {
            RunValidation();
        }
        using (new EditorGUI.DisabledScope(_config == null))
        {
            if (GUILayout.Button("マップを生成", GUILayout.Height(32)))
            {
                GenerateMap();
            }
        }
        EditorGUILayout.Space();
    }

    /// <summary>NavMesh再ベイクセクションを描画する</summary>
    private void DrawNavMeshSection()
    {
        EditorGUILayout.LabelField("4. NavMesh", EditorStyles.boldLabel);
        if (GUILayout.Button("NavMeshを再ベイク"))
        {
            BakeNavMesh();
        }
    }

    /// <summary>設定アセットを既定パスに作成する(既存があれば読み込む)</summary>
    private void CreateOrLoadConfigAsset()
    {
        var existing = AssetDatabase.LoadAssetAtPath<HospitalMapConfigModel>(ConfigAssetPath);
        if (existing != null)
        {
            _config = existing;
            return;
        }
        EditorFolderUtility.EnsureFolder("Assets/ScriptableObjects/Map");
        var config = CreateInstance<HospitalMapConfigModel>();
        AssetDatabase.CreateAsset(config, ConfigAssetPath);
        AssetDatabase.SaveAssets();
        _config = config;
    }

    /// <summary>レイアウトを検証し、結果をダイアログとConsoleに出力する</summary>
    private void RunValidation()
    {
        var layout = HospitalMapLayoutUtility.CreateDefaultLayout();
        var errors = HospitalMapLayoutUtility.Validate(layout);
        var cellSize = _config != null ? _config.CellSize : 3f;
        if (errors.Count == 0)
        {
            EditorUtility.DisplayDialog("検証OK", HospitalMapLayoutUtility.CreateSummary(layout, cellSize), "OK");
            return;
        }
        errors.ForEach(Debug.LogError);
        EditorUtility.DisplayDialog("検証エラー", $"{errors.Count}件のエラーがあります。Consoleを確認してください。", "OK");
    }

    /// <summary>マップを生成する。既存のマップは削除して再生成する</summary>
    private void GenerateMap()
    {
        if (!ValidateBeforeGenerate()) return;

        RemoveExistingMap();
        var root = HospitalMapGeneratorUtility.Generate(_config, _overwriteAreaPrefabs);
        Undo.RegisterCreatedObjectUndo(root, "Generate Hospital Map");
        if (_deactivateTerrain)
        {
            DeactivateTerrain();
        }
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"マップを生成しました。{HospitalMapLayoutUtility.CreateSummary(HospitalMapLayoutUtility.CreateDefaultLayout(), _config.CellSize)}");
    }

    /// <summary>生成前に設定とレイアウトを検証する。エラーがあれば中止する</summary>
    private bool ValidateBeforeGenerate()
    {
        var errors = CollectConfigErrors();
        errors.AddRange(HospitalMapLayoutUtility.Validate(HospitalMapLayoutUtility.CreateDefaultLayout()));
        if (errors.Count == 0) return true;

        errors.ForEach(Debug.LogError);
        EditorUtility.DisplayDialog("生成中止", $"{errors.Count}件のエラーがあります。Consoleを確認してください。", "OK");
        return false;
    }

    /// <summary>設定アセットの不備(Prefab未設定)を収集する</summary>
    private List<string> CollectConfigErrors()
    {
        var errors = new List<string>();
        if (_config.FloorPrefab == null) errors.Add("床Prefabが未設定です(「仮Prefabを生成して割り当て」を実行してください)");
        if (_config.WallPrefab == null) errors.Add("壁Prefabが未設定です");
        if (_config.DoorPrefab == null) errors.Add("ドアPrefabが未設定です");
        if (_config.GenerateCeiling && _config.CeilingPrefab == null) errors.Add("天井Prefabが未設定です");
        return errors;
    }

    /// <summary>シーン上の既存マップを削除する</summary>
    private static void RemoveExistingMap()
    {
        var existing = GameObject.Find(HospitalMapGeneratorUtility.RootObjectName);
        if (existing == null) return;

        Undo.DestroyObjectImmediate(existing);
    }

    /// <summary>シーン上のTerrainを非アクティブ化する</summary>
    private static void DeactivateTerrain()
    {
        var terrain = FindFirstObjectByType<Terrain>();
        if (terrain == null) return;

        Undo.RecordObject(terrain.gameObject, "Deactivate Terrain");
        terrain.gameObject.SetActive(false);
    }

    /// <summary>シーン内のNavMeshSurfaceでNavMeshを再ベイクする</summary>
    private static void BakeNavMesh()
    {
        var surface = FindFirstObjectByType<NavMeshSurface>();
        if (surface == null)
        {
            EditorUtility.DisplayDialog("NavMeshSurfaceなし", "シーン内にNavMeshSurfaceが見つかりません。", "OK");
            return;
        }
        surface.BuildNavMesh();
        EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
    }
}
