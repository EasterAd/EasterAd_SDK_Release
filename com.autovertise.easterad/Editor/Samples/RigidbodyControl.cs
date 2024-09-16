using UnityEngine;

public class RigidbodyControl : MonoBehaviour
{
    public float speed = 5.0f; // Speed of the player movement
    public Transform cameraTransform; // Reference to the Camera's transform
    public Vector3 cameraOffset = new Vector3(0, 2, -2); // Offset of the camera from the player
    public float rotationSpeed = 1000.0f;
    public Camera usingCamera;
    public bool ignoreInput = false;

    public bool constrainX = false;
    public bool constrainY = false;

    private Rigidbody rb; // Reference to the Rigidbody component
    private Transform indicatorTransform;

    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>(); // Get the Rigidbody component attached to this GameObject
        indicatorTransform = transform.Find("Indicator");
        
        // Camera Init
        cameraTransform = usingCamera.transform;
        cameraTransform.parent = transform;
        cameraTransform.localPosition = cameraOffset;
        //cameraTransform.rotation = Quaternion.Euler(0.0f, 0.0f, 0.0f);
    }
    void Update()
    {
        if (ignoreInput) { return; }

        // Move rigid body object with wasd keys
        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        Vector3 movement = right * moveHorizontal + forward * moveVertical;

        rb.velocity = movement * speed + new Vector3(0.0f, rb.velocity.y, 0.0f); // Apply force to move the rigid body


        if (cameraTransform != null)
        {
            float mouseX = constrainX ? 0.0f : Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
            float mouseY = constrainY ? 0.0f : Input.GetAxis("Mouse Y") * rotationSpeed * Time.deltaTime;
            transform.Rotate(Vector3.up, mouseX);
            cameraTransform.Rotate(Vector3.right, -mouseY);
        }
        
        indicatorTransform.localRotation = Quaternion.AngleAxis(cameraTransform.localRotation.x * 180.0f, new Vector3(1.0f, 0.0f, 0.0f));
        indicatorTransform.localPosition = new Vector3(0.0f, 1.5f, 0.0f) + indicatorTransform.localRotation * new Vector3(0.0f, 0.0f, 0.15f);
    }
}
