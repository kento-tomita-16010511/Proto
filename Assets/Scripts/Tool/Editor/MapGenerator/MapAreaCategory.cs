/// <summary>
/// マップ上のエリア種別を定義するEnum。
/// </summary>
public enum MapAreaCategory
{
    /// <summary>廊下(幅3mの通行路)</summary>
    Corridor,

    /// <summary>中央ホールなどの広い通行空間</summary>
    Hall,

    /// <summary>ドアで廊下・ホールと接続される部屋</summary>
    Room,
}
