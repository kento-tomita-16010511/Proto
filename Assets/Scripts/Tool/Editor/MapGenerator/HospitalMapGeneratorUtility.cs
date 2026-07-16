using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// レイアウト定義からシーン上に病院マップのGameObject群を生成するユーティリティクラス。
/// 床・壁・ドアはすべて設定アセットのPrefab参照から生成する(Prefab差し替えで見た目を変更できる)。
/// 状態は持たず、メソッドのみを提供する。
/// </summary>
public static class HospitalMapGeneratorUtility
{
    /// <summary>生成するマップのルートGameObject名</summary>
    public const string RootObjectName = "HospitalMap";

    /// <summary>エリア名ラベルの床面からの高さ(m)</summary>
    private const float LabelHeight = 0.05f;

    /// <summary>玄関の開口部(玄関廊下の南端セル(13,2)の南辺)。設計書 3-2 準拠</summary>
    private static readonly WallEdge EntranceEdge = new WallEdge(13, 2, false);

    /// <summary>
    /// 設定アセットに基づいてマップを生成し、ルートGameObjectを返す。
    /// </summary>
    /// <param name="config">マップ生成設定</param>
    /// <returns>生成したマップのルートGameObject</returns>
    public static GameObject Generate(HospitalMapConfigModel config)
    {
        var layout = HospitalMapLayoutUtility.CreateDefaultLayout();
        var zoneGrid = HospitalMapLayoutUtility.BuildZoneGrid(layout);
        var walkwayCells = HospitalMapLayoutUtility.CollectWalkwayCells(layout);
        var random = new System.Random(config.RandomSeed);
        var root = new GameObject(RootObjectName);
        var doorEdges = SelectDoorEdges(layout, walkwayCells, random);
        CreateAreas(root.transform, layout, config);
        CreateWalls(root.transform, zoneGrid, doorEdges, config);
        return root;
    }

    /// <summary>玄関と各部屋のドア位置を選定する。部屋のドアは通行ゾーンに面した辺からシード付き乱数で1箇所選ぶ</summary>
    private static HashSet<WallEdge> SelectDoorEdges(HospitalMapLayout layout, HashSet<Vector2Int> walkwayCells, System.Random random)
    {
        var doorEdges = new HashSet<WallEdge> { EntranceEdge };
        layout.Areas
            .Where(area => area.Category == MapAreaCategory.Room)
            .ToList()
            .ForEach(room => AddRoomDoorEdge(doorEdges, room, walkwayCells, random));
        return doorEdges;
    }

    /// <summary>部屋1件のドア位置をドア候補から選んで追加する</summary>
    private static void AddRoomDoorEdge(HashSet<WallEdge> doorEdges, MapAreaDefinition room, HashSet<Vector2Int> walkwayCells, System.Random random)
    {
        var candidates = HospitalMapLayoutUtility.FindWalkwayEdges(room.Bounds, walkwayCells);
        if (candidates.Count == 0) return;

        doorEdges.Add(candidates[random.Next(candidates.Count)]);
    }

    /// <summary>全エリアの床・天井・ラベルを生成する</summary>
    private static void CreateAreas(Transform root, HospitalMapLayout layout, HospitalMapConfigModel config)
    {
        var areasParent = CreateChild(root, "Areas");
        var usedCells = new HashSet<Vector2Int>();
        foreach (var area in layout.Areas)
        {
            CreateAreaObject(areasParent, area, usedCells, config);
        }
    }

    /// <summary>エリア1件分の床・天井・ラベルを生成する。廊下交差部の重複セルはスキップする</summary>
    private static void CreateAreaObject(Transform parent, MapAreaDefinition area, HashSet<Vector2Int> usedCells, HospitalMapConfigModel config)
    {
        var areaRoot = CreateChild(parent, area.Name);
        var newCells = HospitalMapLayoutUtility.EnumerateCells(area.Bounds)
            .Where(usedCells.Add)
            .ToList();
        newCells.ForEach(cell => CreateFloor(areaRoot, cell, config));
        if (config.GenerateCeiling)
        {
            newCells.ForEach(cell => CreateCeiling(areaRoot, cell, config));
        }
        if (config.GenerateAreaLabels && area.Category != MapAreaCategory.Corridor)
        {
            CreateAreaLabel(areaRoot, area, config);
        }
    }

    /// <summary>セル1個分の床を生成する</summary>
    private static void CreateFloor(Transform parent, Vector2Int cell, HospitalMapConfigModel config)
    {
        var position = CellCenter(cell, config.CellSize);
        InstantiatePlaced(config.FloorPrefab, parent, position, Quaternion.identity, $"Floor_{cell.x}_{cell.y}");
    }

    /// <summary>セル1個分の天井を生成する</summary>
    private static void CreateCeiling(Transform parent, Vector2Int cell, HospitalMapConfigModel config)
    {
        var position = CellCenter(cell, config.CellSize) + Vector3.up * config.WallHeight;
        InstantiatePlaced(config.CeilingPrefab, parent, position, Quaternion.identity, $"Ceiling_{cell.x}_{cell.y}");
    }

    /// <summary>エリア名を床面に表示するラベルを生成する(レイアウト確認用)</summary>
    private static void CreateAreaLabel(Transform parent, MapAreaDefinition area, HospitalMapConfigModel config)
    {
        var labelObject = new GameObject("Label");
        labelObject.transform.SetParent(parent);
        labelObject.transform.position = AreaCenter(area.Bounds, config.CellSize) + Vector3.up * LabelHeight;
        labelObject.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
        var textMesh = labelObject.AddComponent<TextMesh>();
        textMesh.text = area.Name;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.fontSize = 48;
        textMesh.characterSize = 0.12f;
        textMesh.color = Color.black;
    }

    /// <summary>ゾーン境界すべてに壁またはドアを生成する</summary>
    private static void CreateWalls(Transform root, int[,] zoneGrid, HashSet<WallEdge> doorEdges, HospitalMapConfigModel config)
    {
        var wallsParent = CreateChild(root, "Walls");
        var width = zoneGrid.GetLength(0);
        var height = zoneGrid.GetLength(1);
        for (var x = 0; x <= width; x++)
        {
            for (var z = 0; z <= height; z++)
            {
                TryCreateEdgeObject(wallsParent, zoneGrid, doorEdges, new WallEdge(x, z, true), config);
                TryCreateEdgeObject(wallsParent, zoneGrid, doorEdges, new WallEdge(x, z, false), config);
            }
        }
    }

    /// <summary>辺の両側のゾーンが異なる場合のみ、壁またはドアを生成する</summary>
    private static void TryCreateEdgeObject(Transform parent, int[,] zoneGrid, HashSet<WallEdge> doorEdges, WallEdge edge, HospitalMapConfigModel config)
    {
        var zoneA = ZoneAt(zoneGrid, edge.IsVertical ? edge.X - 1 : edge.X, edge.IsVertical ? edge.Z : edge.Z - 1);
        var zoneB = ZoneAt(zoneGrid, edge.X, edge.Z);
        if (zoneA == zoneB) return;

        var isDoor = doorEdges.Contains(edge);
        var prefab = isDoor ? config.DoorPrefab : config.WallPrefab;
        var name = $"{(isDoor ? "Door" : "Wall")}_{edge.X}_{edge.Z}_{(edge.IsVertical ? "V" : "H")}";
        InstantiatePlaced(prefab, parent, EdgeCenter(edge, config.CellSize), EdgeRotation(edge), name);
    }

    /// <summary>グリッド範囲内ならゾーンIDを、範囲外なら屋外IDを返す</summary>
    private static int ZoneAt(int[,] zoneGrid, int x, int z)
    {
        var isInside = x >= 0 && x < zoneGrid.GetLength(0) && z >= 0 && z < zoneGrid.GetLength(1);
        return isInside ? zoneGrid[x, z] : HospitalMapLayoutUtility.EmptyZoneId;
    }

    /// <summary>セル中心のワールド座標(床面)を返す</summary>
    private static Vector3 CellCenter(Vector2Int cell, float cellSize)
    {
        return new Vector3((cell.x + 0.5f) * cellSize, 0f, (cell.y + 0.5f) * cellSize);
    }

    /// <summary>エリア矩形の中心のワールド座標(床面)を返す</summary>
    private static Vector3 AreaCenter(RectInt bounds, float cellSize)
    {
        return new Vector3(bounds.center.x * cellSize, 0f, bounds.center.y * cellSize);
    }

    /// <summary>辺の中点のワールド座標(床面)を返す</summary>
    private static Vector3 EdgeCenter(WallEdge edge, float cellSize)
    {
        return edge.IsVertical
            ? new Vector3(edge.X * cellSize, 0f, (edge.Z + 0.5f) * cellSize)
            : new Vector3((edge.X + 0.5f) * cellSize, 0f, edge.Z * cellSize);
    }

    /// <summary>辺の向きに応じた壁・ドアの回転を返す(Prefabは東西方向を基準とする)</summary>
    private static Quaternion EdgeRotation(WallEdge edge)
    {
        return edge.IsVertical ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
    }

    /// <summary>空の子GameObjectを生成して返す</summary>
    private static Transform CreateChild(Transform parent, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent);
        return child.transform;
    }

    /// <summary>Prefabをシーンに配置する。Prefabアセット以外が指定された場合は複製で代替する</summary>
    private static void InstantiatePlaced(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation, string name)
    {
        var instance = PrefabUtility.IsPartOfPrefabAsset(prefab)
            ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent)
            : Object.Instantiate(prefab, parent);
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.name = name;
    }
}
