using UnityEngine;

public class IntegratedCameraController : MonoBehaviour
{
    [Header("Target Focus")]

    public Transform targetFocus;

    [Header("Speeds")]
    public float touchpadZoomSpeed = 900f;
    public float panSpeed = 2.0f;
    public float mouseDragSensitivity = 300f;

    [Header("Touch Speeds (mobile)")]
    [Tooltip("Sensitivity for rotating with a single-finger drag.")]
    public float touchRotateSensitivity = 0.25f;
    [Tooltip("Sensitivity for pinch-to-zoom.")]
    public float touchZoomSpeed = 0.02f;
    [Tooltip("Sensitivity for panning with a two-finger drag.")]
    public float touchPanSpeed = 0.0025f;

    [Header("Camera range")]
    public float minDistance = 0.8f;
    public float maxDistance = 27000f;
    public float minPitch = -85f;
    public float maxPitch = 85f;
    public float minYaw = -1800f;
    public float maxYaw = 1800f;

    [Header("Smoothing")]
    public float rotationSmoothTime = 0.18f;
    public float zoomSmoothTime = 1.2f;
    public float panSmoothTime = 0.22f;

    private const float DefaultXRot = 32.20003f;
    private const float DefaultYRot = -150.2999f;
    private const float DefaultDistance = 16.08f;
    private static readonly Vector3 DefaultPanOffset = Vector3.zero;

    private float targetXRot = DefaultXRot;
    private float targetYRot = DefaultYRot;
    private float targetDistance = DefaultDistance;
    private Vector3 targetPanOffset = DefaultPanOffset;

    private float currentXRot = DefaultXRot;
    private float currentYRot = DefaultYRot;
    private float currentDistance = DefaultDistance;
    private Vector3 currentPanOffset = DefaultPanOffset;

    private float xRotVelocity, yRotVelocity, distanceVelocity;
    private Vector3 panVelocity;

    private Vector3 calculatedCenterPoint;

    void Start()
    {
        if (targetFocus == null)
        {
            GameObject model = GameObject.Find("NetworkOrigin");
            if (model == null) model = GameObject.Find("NeuralNetwork");

            if (model != null) targetFocus = model.transform;
        }

        if (targetFocus != null)
        {
            calculatedCenterPoint = CalculateAbsoluteCenter(targetFocus);
        }
    }

    void LateUpdate()
    {
        if (targetFocus == null) return;

        calculatedCenterPoint = CalculateAbsoluteCenter(targetFocus);

        bool mouseOverUI = UnityEngine.EventSystems.EventSystem.current != null
            && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        if (!mouseOverUI && Input.GetMouseButton(0))
        {
            targetYRot += Input.GetAxis("Mouse X") * mouseDragSensitivity;
            targetXRot -= Input.GetAxis("Mouse Y") * mouseDragSensitivity;
        }

        if (!mouseOverUI && Input.GetMouseButton(1))
        {
            Vector3 right = transform.right;
            Vector3 up = transform.up;
            targetPanOffset -= right * Input.GetAxis("Mouse X") * panSpeed * targetDistance;
            targetPanOffset -= up * Input.GetAxis("Mouse Y") * panSpeed * targetDistance;
        }

        if (!mouseOverUI)
        {
            float scrollDelta = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scrollDelta) > 0.001f)
            {
                targetDistance -= scrollDelta * touchpadZoomSpeed;
            }
        }

        HandleTouchInput();

        targetXRot = Mathf.Clamp(targetXRot, minPitch, maxPitch);
        targetYRot = Mathf.Clamp(targetYRot, minYaw, maxYaw);
        targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);

        currentXRot = Mathf.SmoothDampAngle(currentXRot, targetXRot, ref xRotVelocity, rotationSmoothTime);
        currentYRot = Mathf.SmoothDampAngle(currentYRot, targetYRot, ref yRotVelocity, rotationSmoothTime);
        currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref distanceVelocity, 1f / zoomSmoothTime);
        currentPanOffset = Vector3.SmoothDamp(currentPanOffset, targetPanOffset, ref panVelocity, panSmoothTime);

        Quaternion targetRotation = Quaternion.Euler(currentXRot, currentYRot, 0);
        Vector3 reverseDistance = new Vector3(0f, 0f, -currentDistance);

        transform.rotation = targetRotation;

        transform.position = targetRotation * reverseDistance + calculatedCenterPoint + currentPanOffset;
    }

    private void HandleTouchInput()
    {
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);

            if (IsTouchOverUI(touch.fingerId)) return;

            if (touch.phase == TouchPhase.Moved)
            {
                targetYRot += touch.deltaPosition.x * touchRotateSensitivity;
                targetXRot -= touch.deltaPosition.y * touchRotateSensitivity;
            }
        }
        else if (Input.touchCount == 2)
        {
            Touch touchZero = Input.GetTouch(0);
            Touch touchOne = Input.GetTouch(1);

            if (IsTouchOverUI(touchZero.fingerId) || IsTouchOverUI(touchOne.fingerId)) return;

            Vector2 touchZeroPreviousPosition = touchZero.position - touchZero.deltaPosition;
            Vector2 touchOnePreviousPosition = touchOne.position - touchOne.deltaPosition;

            float previousDistance = (touchZeroPreviousPosition - touchOnePreviousPosition).magnitude;
            float currentDistanceBetweenFingers = (touchZero.position - touchOne.position).magnitude;
            float pinchDelta = currentDistanceBetweenFingers - previousDistance;

            targetDistance -= pinchDelta * touchZoomSpeed;

            Vector2 averageDelta = (touchZero.deltaPosition + touchOne.deltaPosition) * 0.5f;
            Vector3 right = transform.right;
            Vector3 up = transform.up;
            targetPanOffset -= right * averageDelta.x * touchPanSpeed * targetDistance;
            targetPanOffset -= up * averageDelta.y * touchPanSpeed * targetDistance;
        }
    }

    private bool IsTouchOverUI(int fingerId)
    {
        return UnityEngine.EventSystems.EventSystem.current != null
            && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(fingerId);
    }

    public void ResetToDefaultView()
    {
        ResetToDefaultView(DefaultXRot, DefaultYRot, DefaultDistance, DefaultPanOffset);
    }

    public void ResetToDefaultView(float xRot, float yRot, float distance, Vector3 panOffset = default)
    {
        targetXRot = xRot;
        targetYRot = yRot;
        targetDistance = distance;
        targetPanOffset = panOffset;

        currentXRot = xRot;
        currentYRot = yRot;
        currentDistance = distance;
        currentPanOffset = panOffset;

        xRotVelocity = 0f;
        yRotVelocity = 0f;
        distanceVelocity = 0f;
        panVelocity = Vector3.zero;
    }

    private Vector3 CalculateAbsoluteCenter(Transform parentTransform)
    {
        if (parentTransform.childCount == 0) return parentTransform.position;

        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;

        foreach (Transform child in parentTransform)
        {
            if (!hasBounds)
            {
                bounds = new Bounds(child.position, Vector3.zero);
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(child.position);
            }
        }

        return hasBounds ? bounds.center : parentTransform.position;
    }
}
