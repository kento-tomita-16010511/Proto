/// <summary>
/// グリッドセル境界の1辺を表す構造体。壁・ドアの配置位置の特定に使う。
/// IsVertical=true はセル(X-1,Z)とセル(X,Z)の間の辺(南北方向に伸びる辺)、
/// false はセル(X,Z-1)とセル(X,Z)の間の辺(東西方向に伸びる辺)を表す。
/// </summary>
public readonly struct WallEdge
{
    /// <summary>辺の基準セルX座標</summary>
    public readonly int X;

    /// <summary>辺の基準セルZ座標</summary>
    public readonly int Z;

    /// <summary>南北方向に伸びる辺かどうか</summary>
    public readonly bool IsVertical;

    /// <summary>
    /// 辺を生成する。
    /// </summary>
    /// <param name="x">辺の基準セルX座標</param>
    /// <param name="z">辺の基準セルZ座標</param>
    /// <param name="isVertical">南北方向に伸びる辺かどうか</param>
    public WallEdge(int x, int z, bool isVertical)
    {
        X = x;
        Z = z;
        IsVertical = isVertical;
    }
}
