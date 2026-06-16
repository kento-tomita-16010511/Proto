using UnityEngine;
using System.Linq;

/// <summary>
/// 入力の有効 / 無効を切り替える View クラス。
/// CameraController と InputManager を個別に制御し、MainSceneActivator から呼び出される。
/// </summary>
public class InputGuardView : MonoBehaviour
{
    /// <summary>無効化対象のカメラコントローラー群。</summary>
    [SerializeField] private Behaviour[] cameraControllers;

    /// <summary>入力を集中管理する InputManager。</summary>
    [SerializeField] private InputManager inputManager;

    /// <summary>全入力を無効化する。</summary>
    public void DisableInput() => SetInputEnabled(false);

    /// <summary>全入力を有効化する。</summary>
    public void EnableInput() => SetInputEnabled(true);

    /// <summary>InputManager とカメラ操作の有効 / 無効をまとめて切り替える。</summary>
    private void SetInputEnabled(bool isEnabled)
    {
        if (inputManager != null) inputManager.IsEnabled = isEnabled;
        cameraControllers
            .Where(c => c != null)
            .ToList()
            .ForEach(c => c.enabled = isEnabled);
    }
}
