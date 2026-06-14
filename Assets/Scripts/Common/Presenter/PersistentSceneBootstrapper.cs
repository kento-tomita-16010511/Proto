using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ゲーム起動時に必要なシーンを順次ロードする Presenter クラス。
/// PersistentScene → TitleScene → MainScene の順に additive ロードし、
/// MainScene のカメラを TitleScene 表示中の背景として利用する。
/// MainSceneActivator.Awake() が ShowCameraOnly() + FreezeAll() を実行するため、
/// カメラのみ描画・ゲームロジックは停止した状態でタイトル背景に使える。
/// </summary>
public class PersistentSceneBootstrapper : MonoBehaviour
{
    /// <summary>ゲーム設定 Model。シーン名などを取得する。</summary>
    [SerializeField] private GameConfig config;

    private async UniTask Start()
    {
        var ct = this.GetCancellationTokenOnDestroy();

        // 1. PersistentScene ロード（SceneLoader などシングルトンが Awake する）
        await SceneManager.LoadSceneAsync(config.PersistentSceneName, LoadSceneMode.Additive)
                          .ToUniTask(cancellationToken: ct);

        // 2. TitleScene ロード・アクティブ化
        await SceneManager.LoadSceneAsync(config.TitleSceneName, LoadSceneMode.Additive)
                          .ToUniTask(cancellationToken: ct);
        SceneManager.SetActiveScene(SceneManager.GetSceneByName(config.TitleSceneName));

        // 3. MainScene を即時 additive ロード（背景表示のため allowSceneActivation=true）
        await SceneManager.LoadSceneAsync(config.MainSceneName, LoadSceneMode.Additive)
                          .ToUniTask(cancellationToken: ct);
    }
}
