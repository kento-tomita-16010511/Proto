using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 病院マップ全体のレイアウト(全エリア定義)を保持するデータクラス。
/// レイアウトの内容は HospitalMapLayoutUtility が Docs/MapDesign.md に基づいて構築する。
/// </summary>
public class HospitalMapLayout
{
    /// <summary>全エリア定義(廊下・ホール・部屋)</summary>
    private readonly List<MapAreaDefinition> _areas;

    /// <summary>
    /// レイアウトを生成する。
    /// </summary>
    /// <param name="areas">全エリア定義</param>
    public HospitalMapLayout(List<MapAreaDefinition> areas)
    {
        _areas = areas;
    }

    /// <summary>全エリア定義(読み取り専用)</summary>
    public IReadOnlyList<MapAreaDefinition> Areas => _areas;

    /// <summary>グリッド全体の幅(セル数)</summary>
    public int GridWidth => _areas.Max(area => area.Bounds.xMax);

    /// <summary>グリッド全体の高さ(セル数)</summary>
    public int GridHeight => _areas.Max(area => area.Bounds.yMax);
}
