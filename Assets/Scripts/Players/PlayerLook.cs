using UnityEngine;

public class HeadLook : MonoBehaviour
{
    [Header("Settings")]
    public float sensitivity = 100f;
    public bool invertY = true;
    public float minPitch = -40f;
    public float maxPitch = 40f;
    public float minYaw = -40f;
    public float maxYaw = 40f;
    public bool enableYaw = true;

    float pitch = 0f;
    float yaw = 0f;

    bool isLooking = false;
    private Camera mainCamera;

    void Start()
    {
        Debug.Log("MainCamera = " + mainCamera);
        Cursor.lockState = CursorLockMode.Locked;

        Vector3 e = transform.localEulerAngles;
        pitch = e.x;
        yaw = e.y;

        mainCamera = Camera.main;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            isLooking = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (Input.GetMouseButtonUp(1))
        {
            isLooking = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (isLooking)
        {
            float mouseX = Input.GetAxis("Mouse X") * sensitivity * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * sensitivity * Time.deltaTime;

            float yInput = invertY ? -mouseY : mouseY;

            pitch -= yInput;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            yaw += mouseX;
            yaw = Mathf.Clamp(yaw, minYaw, maxYaw);

            transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        ShootRay();
    }

    void ShootRay()
    {
        if (mainCamera == null)
        {
            Debug.Log($"[HeadLook] No Camera");
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Debug.DrawRay(ray.origin, ray.direction * 100f, Color.blue);

        if (Input.GetMouseButtonDown(0))
        {
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                Debug.Log("[HeadLook] Hit object: " + hit.collider.name);

                Item item = hit.collider.GetComponentInParent<Item>();
                if (item != null)
                {
                    item.UseItem();
                }


                Card card = hit.collider.GetComponentInParent<Card>();
                if (card != null)
                {
                    card.PlayCard();
                }
            }
            else
            {
                Debug.Log("[HeadLook] No hit");
            }

        }

    }
}
