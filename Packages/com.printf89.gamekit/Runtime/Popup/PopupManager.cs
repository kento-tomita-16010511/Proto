using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ポップアップを生成・表示する常駐シングルトン（PersistentScene に配置する）。
/// 専用の Screen Space - Overlay Canvas を子に動的生成して持ち、
/// どのシーンからでも ShowAsync で同一 API で表示できる。
/// 表示・破棄のみを担当し、ポーズ等のゲーム状態は呼び出し側が制御する。
/// </summary>
public class PopupManager : MonoBehaviour
{
    /// <summary>シングルトンインスタンス。</summary>
    public static PopupManager Instance { get; private set; }

    /// <summary>ポップアップ用 Canvas の sortingOrder。常に最前面に出すため大きめにする。</summary>
    [SerializeField] private int sortingOrder = 1000;

    private Transform _canvasRoot;

    /// <summary>シングルトンを確立し、ポップアップ用 Canvas を生成する。</summary>
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        CreateCanvas();
    }

    /// <summary>子に Screen Space - Overlay の Canvas を生成する。</summary>
    private void CreateCanvas()
    {
        var go = new GameObject("PopupCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        _canvasRoot = go.transform;
    }

    /// <summary>
    /// ポップアップ prefab を Canvas 下に生成し、フェードインして表示する。
    /// </summary>
    /// <typeparam name="T">PopupBase を継承したポップアップ型。</typeparam>
    /// <param name="prefab">生成するポップアップ prefab。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>生成・表示したポップアップインスタンス。</returns>
    public async UniTask<T> ShowAsync<T>(T prefab, CancellationToken ct) where T : PopupBase
    {
        var instance = Instantiate(prefab, _canvasRoot);
        await instance.OpenAsync(ct);
        return instance;
    }
}
