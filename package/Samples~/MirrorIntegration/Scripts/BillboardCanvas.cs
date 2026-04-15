using UnityEngine;

/// <summary>
/// Makes a World Space canvas always face the main camera.
/// Attach to the Canvas child of the RemotePlayer prefab.
/// </summary>
public class BillboardCanvas : MonoBehaviour
{
    private Camera _mainCamera;

    private void Start()
    {
        _mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) return;
        }

        transform.rotation = _mainCamera.transform.rotation;
    }
}
