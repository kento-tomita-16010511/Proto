using UnityEngine;

/// <summary>
/// Rigidbody の状態（速度、角速度、Kinematic状態）を保存・復元するヘルパークラス。
/// IFreezable を実装しており、MainSceneActivatorPresenter から一括制御される。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class RigidbodyFreezer : MonoBehaviour, IFreezable
{
    private Rigidbody _rb;
    private Vector3 _savedVelocity;
    private Vector3 _savedAngularVelocity;
    private bool _wasKinematic;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    public void Freeze()
    {
        if (_rb == null) return;

        _wasKinematic = _rb.isKinematic;
        _savedVelocity = _rb.linearVelocity;
        _savedAngularVelocity = _rb.angularVelocity;

        _rb.isKinematic = true;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
    }

    public void Unfreeze()
    {
        if (_rb == null) return;

        _rb.isKinematic = _wasKinematic;
        _rb.linearVelocity = _savedVelocity;
        _rb.angularVelocity = _savedAngularVelocity;
    }
}