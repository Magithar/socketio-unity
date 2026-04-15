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

#if UNITY_ANDROID || UNITY_IOS
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Moved)
            {
                h += Mathf.Clamp(touch.deltaPosition.x * 0.1f, -1f, 1f);
                v += Mathf.Clamp(touch.deltaPosition.y * 0.1f, -1f, 1f);
            }
        }
#endif

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
