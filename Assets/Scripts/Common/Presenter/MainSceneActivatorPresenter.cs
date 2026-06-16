using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// MainScene のアクティベーションを管理する Presenter クラス。
/// TitleScene 表示中は FreezeAll で全ロジックを停止し、
/// 遷移完了後にカウントダウン演出を経て入力を有効化する。
/// </summary>
public class MainSceneActivatorPresenter : MonoBehaviour
{
    /// <summary>停止対象となるゲームオブジェクトのルート群。</summary>
    [SerializeField] private GameObject[] logicRoots;

    /// <summary>入力の有効 / 無効を切り替える View。</summary>
    [SerializeField] private InputGuardView inputGuard;

    /// <summary>MainScene のゲーム UI を制御する View。</summary>
    [SerializeField] private MainSceneView mainSceneView;

    /// <summary>シーン遷移後のカウントダウン演出を担う Presenter（省略時はすぐ入力有効化）。</summary>
    [SerializeField] private CountdownPresenter countdownPresenter;

    private bool _isFrozen;
    private readonly List<IFreezable> _freezables = new List<IFreezable>();

    /// <summary>起動時にカメラのみ表示し、ロジックを停止する。</summary>
    private void Awake()
    {
        mainSceneView?.ShowCameraOnly();

        // logicRoots 配下から IFreezable を実装したコンポーネントを収集する（子要素含む）
        if (logicRoots != null)
        {
            _freezables.AddRange(
                logicRoots
                    .Where(root => root != null)
                    .SelectMany(root => root.GetComponentsInChildren<IFreezable>(true)));
        }

        FreezeAll();
    }

    /// <summary>
    /// ロジックルート以下の全 Behaviour と Rigidbody を停止し入力も無効化する。
    /// 二重呼び出しは _isFrozen ガードで無視される。
    /// </summary>
    public void FreezeAll()
    {
        if (_isFrozen) return;
        _isFrozen = true;

        // 各コンポーネント独自の停止処理を実行
        _freezables.Where(f => f != null).ToList().ForEach(f => f.Freeze());

        inputGuard?.DisableInput();
    }

    /// <summary>
    /// 停止した Behaviour / Rigidbody を元の状態に復元し、入力を再有効化する。
    /// FreezeAll が呼ばれていない場合は何もしない。
    /// </summary>
    public void UnfreezeAll()
    {
        UnfreezeExceptInput();
        inputGuard?.EnableInput();
    }

    /// <summary>
    /// Behaviour / Rigidbody を復元するが、入力は有効化しない。
    /// カウントダウン演出中に物理世界を動かしつつ入力を遅延させる場合に使用する。
    /// </summary>
    public void UnfreezeExceptInput()
    {
        if (!_isFrozen) return;
        _isFrozen = false;

        // 各コンポーネント独自の再開処理を実行
        _freezables.Where(f => f != null).ToList().ForEach(f => f.Unfreeze());
    }

    /// <summary>
    /// SceneLoader からシーン遷移完了時に呼び出される。
    /// CountdownPresenter が割り当てられていればカウントダウン後に入力有効化、
    /// なければ即座に UnfreezeAll する。
    /// </summary>
    public void OnSceneTransitionComplete()
    {
        if (countdownPresenter != null)
        {
            UnfreezeExceptInput();
            countdownPresenter.PlayAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }
        else
        {
            UnfreezeAll();
        }
    }
}
