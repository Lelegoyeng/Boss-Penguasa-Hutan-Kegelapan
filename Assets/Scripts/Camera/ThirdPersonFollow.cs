using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Vector3 offset = new Vector3(0, 2f, -4f);
    public float targetHeight = 1.7f;
    
    [Header("Follow Settings")]
    [Range(0.1f, 20f)] public float distance = 5f;
    [Range(0.1f, 20f)] public float minDistance = 2f;
    [Range(0.1f, 20f)] public float maxDistance = 10f;
    [Range(0.1f, 5f)] public float height = 2f;
    [Range(0.1f, 5f)] public float minHeight = 1f;
    [Range(0.1f, 5f)] public float maxHeight = 3f;
    
    [Header("Movement")]
    [Range(0.1f, 50f)] public float rotationSpeed = 5f;
    [Range(0.1f, 50f)] public float zoomSpeed = 10f;
    [Range(0.1f, 50f)] public float positionSmoothTime = 0.2f;
    [Range(0.1f, 50f)] public float rotationSmoothTime = 0.2f;
    
    [Header("Collision")]
    public LayerMask collisionLayers = -1;
    public float collisionOffset = 0.2f;
    public float cameraRadius = 0.3f;

    // Private variables
    private Vector3 _currentPosition;
    private Vector3 _positionVelocity;
    private float _currentRotationX;
    private float _currentRotationY;
    private float _currentDistance;
    private float _currentHeight;
    private float _zoomVelocity;
    private float _heightVelocity;
    private float _rotationX;
    private float _rotationY;
    private Vector3 _targetPosition;
    private PlayerControls _playerControls;

    private void Awake()
    {
        _playerControls = new PlayerControls();
    }

    private void OnEnable()
    {
        _playerControls.Player.Enable();
    }

    private void OnDisable()
    {
        _playerControls.Player.Disable();
    }

    private void Start()
    {
        if (!target) return;
        
        // Initialize current values
        Vector3 angles = transform.eulerAngles;
        _rotationX = angles.y;
        _rotationY = angles.x;
        _currentDistance = distance;
        _currentHeight = height;
        _currentPosition = transform.position;
    }

    private void LateUpdate()
    {
        if (!target) return;

        HandleInput();
        UpdatePosition();
        HandleCollision();
        UpdateCamera();
    }

    private void HandleInput()
    {
        // Rotate camera
        if (_playerControls.Player.EnableCameraRotation.IsPressed())
        {
            Vector2 lookInput = _playerControls.Player.Look.ReadValue<Vector2>();
            _rotationX += lookInput.x * rotationSpeed * Time.deltaTime;
            _rotationY -= lookInput.y * rotationSpeed * Time.deltaTime;
            _rotationY = Mathf.Clamp(_rotationY, -80f, 80f);
        }

        // Zoom with mouse wheel
        float scroll = _playerControls.Player.CameraZoom.ReadValue<Vector2>().y;
        distance = Mathf.Clamp(distance - scroll * zoomSpeed * Time.deltaTime, minDistance, maxDistance);
        _currentDistance = Mathf.SmoothDamp(_currentDistance, distance, ref _zoomVelocity, 0.1f);
        
        // Adjust height (remapping Q/E, as they are not in the input actions by default)
        if (Keyboard.current.qKey.isPressed) height = Mathf.Clamp(height - 1f * Time.deltaTime, minHeight, maxHeight);
        if (Keyboard.current.eKey.isPressed) height = Mathf.Clamp(height + 1f * Time.deltaTime, minHeight, maxHeight);
        _currentHeight = Mathf.SmoothDamp(_currentHeight, height, ref _heightVelocity, 0.3f);
    }

    private void UpdatePosition()
    {
        // Calculate target position based on rotation and distance
        Quaternion rotation = Quaternion.Euler(_rotationY, _rotationX, 0);
        _targetPosition = target.position + Vector3.up * targetHeight - (rotation * Vector3.forward * _currentDistance) + offset;
        
        // Smoothly move the camera
        _currentPosition = Vector3.SmoothDamp(transform.position, _targetPosition, ref _positionVelocity, positionSmoothTime);
    }

    private void HandleCollision()
    {
        if (collisionLayers == 0) return;
        
        // Cast a ray from target to camera position
        Vector3 rayOrigin = target.position + Vector3.up * targetHeight;
        Vector3 rayDirection = (_currentPosition - rayOrigin).normalized;
        float rayDistance = Vector3.Distance(rayOrigin, _currentPosition) + collisionOffset;

        if (Physics.SphereCast(rayOrigin, cameraRadius, rayDirection, out RaycastHit hit, rayDistance, collisionLayers))
        {
            // If we hit something, move the camera in front of the obstacle
            _currentPosition = hit.point + hit.normal * collisionOffset;
        }
    }

    private void UpdateCamera()
    {
        // Update transform
        transform.position = _currentPosition;
        
        // Calculate look at position (slightly above target)
        Vector3 lookAtPosition = target.position + Vector3.up * _currentHeight;
        
        // Smoothly rotate to look at target
        Quaternion targetRotation = Quaternion.LookRotation(lookAtPosition - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 
            rotationSmoothTime * Time.deltaTime * 10f);
    }

    // Draw camera frustum in editor for debugging
    private void OnDrawGizmos()
    {
        if (!target) return;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(target.position + Vector3.up * targetHeight, transform.position);
        Gizmos.DrawWireSphere(transform.position, 0.2f);
    }
}
