using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    public bool CanMove { get; set; }

    private void Start()
    {
        Debug.Log("PlayerController START - Script is running!");
    }

    private void Update()
    {
        if (!CanMove) return;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 move = new Vector3(h, 0f, v).normalized;

        transform.Translate(move * moveSpeed * Time.deltaTime, Space.World);

        ClampBounds();
    }

    private void ClampBounds()
    {
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, -10f, 10f);
        pos.z = Mathf.Clamp(pos.z, -10f, 10f);
        transform.position = pos;
    }
}
