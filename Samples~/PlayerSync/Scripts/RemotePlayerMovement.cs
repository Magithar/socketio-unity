using UnityEngine;

/// <summary>
/// Handles smooth interpolation of remote player movement.
/// Attached to RemotePlayer prefab to smoothly move towards network-synced positions.
/// </summary>
public class RemotePlayerMovement : MonoBehaviour
{
    [SerializeField] private float interpolationSpeed = 10f;

    private Vector3 targetPosition;

    private void Start()
    {
        targetPosition = transform.position;
    }

    private void Update()
    {
        // Smoothly interpolate towards target position
        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            Time.deltaTime * interpolationSpeed
        );
    }

    /// <summary>
    /// Sets the target position to interpolate towards.
    /// Called by PlayerSpawner when receiving position updates from the network.
    /// </summary>
    public void SetTargetPosition(Vector3 newPosition)
    {
        targetPosition = newPosition;
    }
}
