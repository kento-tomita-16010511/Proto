using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// MainScene のアクティベーションを管理する Presenter クラス。
/// TitleScene 表示中は FreezeAll で全ロジックを停止し、
/// 遷移完了後にカウントダウン演出を経て入力を有効化する。
/// </summary>
public class MainSceneActivator : MonoBehaviour
{
    /// <summary>停止対象となるゲームオブジェクトのルート群。</summary>
    [SerializeField] private GameObject[] logicRoots;

    /// <summary>停止状態を保持する ScriptableObject。</summary>
    [SerializeField] private FreezeState freezeState;

    /// <summary>入力の有効 / 無効を切り替える View。</summary>
    [SerializeField] private InputGuardView inputGuard;

    /// <summary>MainScene のゲーム UI を制御する View。</summary>
    [SerializeField] private MainSceneView mainSceneView;

    /// <summary>シーン遷移後のカウントダウン演出を担う Presenter（省略時はすぐ入力有効化）。</summary>
    [SerializeField] private CountdownPresenter countdownPresenter;

    private bool _isFrozen;

    /// <summary>起動時にカメラのみ表示し、ロジックを停止する。</summary>
    private void Awake()
    {
        mainSceneView?.ShowCameraOnly();
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

        freezeState.Clear();

        if (logicRoots != null)
        {
            foreach (var root in logicRoots)
            {
                if (root == null) continue;

                foreach (var b in root.GetComponentsInChildren<Behaviour>(true))
                {
                    if (b is Camera || b is MainSceneActivator) continue;
                    freezeState.Behaviours.Add(
                        new FreezeState.Entry { component = b, wasEnabled = b.enabled });
                    b.enabled = false;
                }

                foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
                {
                    freezeState.Rigidbodies.Add(
                        new FreezeState.RbEntry { rb = rb, wasKinematic = rb.isKinematic });
                    rb.isKinematic = true;
                    rb.Sleep();
                }
            }
        }

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

        foreach (var e in freezeState.Behaviours)
            if (e.component != null) e.component.enabled = e.wasEnabled;

        foreach (var e in freezeState.Rigidbodies)
        {
            if (e.rb == null) continue;
            e.rb.isKinematic = e.wasKinematic;
            e.rb.WakeUp();
        }

        freezeState.Clear();
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
