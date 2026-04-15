using Mirror;
using UnityEngine;

/// <summary>
/// Simple WASD movement for the Mirror player prefab.
///
/// Extends NetworkBehaviour so it can check isLocalPlayer —
/// only the owning client processes input. Mirror's NetworkTransform
/// replicates the resulting position to all other peers automatically.
///
/// Player colour (red = local, blue = remote) is set here.
/// Name label display is handled by PlayerIdentityBridge.
///
/// Attach alongside NetworkIdentity, NetworkTransform, and PlayerIdentityBridge
/// on the Mirror player prefab.
/// </summary>
public class MirrorPlayerController : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float boundsLimit = 10f;

    // Same colours as PlayerSync sample — red = you, blue = others.
    private static readonly Color LocalColor  = new Color(1f, 0.103f, 0f); // red
    private static readonly Color RemoteColor = new Color(0f, 0.349f, 1f); // blue

    public override void OnStartClient()
    {
        var rend = GetComponentInChildren<Renderer>();
        if (rend != null)
            rend.material.color = isLocalPlayer ? LocalColor : RemoteColor;
    }

    public override void OnStartLocalPlayer()
    {
        // Re-apply red in case OnStartClient fired before isLocalPlayer was set.
        var rend = GetComponentInChildren<Renderer>();
        if (rend != null)
            rend.material.color = LocalColor;
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 move = new Vector3(h, 0f, v).normalized;
        transform.Translate(move * moveSpeed * Time.deltaTime, Space.World);

        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, -boundsLimit, boundsLimit);
        pos.z = Mathf.Clamp(pos.z, -boundsLimit, boundsLimit);
        transform.position = pos;
    }
}
