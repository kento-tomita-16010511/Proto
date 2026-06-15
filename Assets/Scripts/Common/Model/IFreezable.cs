/// <summary>
/// 停止・再開処理を各クラスで独自に実装するためのインターフェース。
/// MainSceneActivatorPresenter によって呼び出される。
/// </summary>
public interface IFreezable
{
    /// <summary>
    /// ロジックや物理挙動を停止し、現在の状態を保存する。
    /// </summary>
    void Freeze();

    /// <summary>
    /// 保存された状態に基づき、ロジックや物理挙動を再開する。
    /// </summary>
    void Unfreeze();
}
