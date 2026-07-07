/// <summary>
/// ネット（蜘蛛の巣）などでスタン（行動停止）させられる対象が実装するインターフェース。
/// EnemyPresenter / TigerPresenter が実装し、WebStunEffect はこの型を通じてスタンを与える。
/// </summary>
public interface IStunnable
{
    /// <summary>
    /// スタン状態にする。停止時間や揺れ演出の値は実装側が EnemyState 等から決定する。
    /// </summary>
    void Stun();
}
