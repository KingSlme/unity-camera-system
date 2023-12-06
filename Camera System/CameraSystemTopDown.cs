using UnityEngine;
using Cinemachine;

public class CameraSystemTopDown : MonoBehaviour
{   
    private enum ZoomType
    {
        FOV,
        Forward,
        Y
    }

    [SerializeField] private CinemachineVirtualCamera _cinemachineVirtualCamera;
    private CinemachineTransposer _cinemachineTransposer;
    private const float EPSILON = 0.01f;

    [Header("Movement")]
    [SerializeField] [Range(10.0f, 100.0f)] private float _movementSpeed = 50.0f;
    [SerializeField] private float _dragPanSpeed = 1.0f;
    [SerializeField] private bool _enableKeyboardMovement = true;
    [SerializeField] private bool _enableEdgeScrolling = false;
    [SerializeField] private bool _enableDragPanning = true;
    private int _edgeScrollBuffer = 20;
    private bool _isDragging = false;
    private Vector2 _lastMousePosition;

    [Header("Rotation")]
    [SerializeField] private KeyCode _rotateLeftKey = KeyCode.Q;
    [SerializeField] private KeyCode _rotateRightKey = KeyCode.E;
    [SerializeField] [Range(50.0f, 360.0f)] private float _rotatationSpeed = 100.0f;
    [SerializeField] [Range(5.0f, 15.0f)] private float _rotationSnapSpeed = 10.0f;
    [SerializeField] private bool _enableRotationSnapping = false;
    private Quaternion targetRotation = Quaternion.identity;
    private bool _isSnapping = false;

    [Header("Zoom Settings")]
    [SerializeField] private ZoomType _zoomType;
    [SerializeField] [Tooltip("Amount of zoom per scroll")] [Range(1.0f, 5.0f)] private float _zoomAmount = 3.0f;
    [SerializeField] [Tooltip("Transition speed of the zoom")] [Range(10.0f, 50.0f)] private float _zoomSpeed = 10.0f;
    [SerializeField] private bool _enableZoom = true;

    [Header("FOV Zoom")]
    [SerializeField] [Range(10.0f, 100.0f)] private float _fovMin = 10.0f;
    [SerializeField] [Range(10.0f, 100.0f)] private float _fovMax = 60.0f;
    private float _targetFOV;
    
    [Header("Forward Zoom")]
    [SerializeField] [Range(10.0f, 100.0f)] private float _followOffsetMin = 10.0f;
    [SerializeField] [Range(10.0f, 100.0f)] private float _followOffsetMax = 100.0f;
    private Vector3 _followOffset;

    [Header("Y Zoom")]
    [SerializeField] [Range(10.0f, 100.0f)] private float _followOffsetMinY = 10.0f;
    [SerializeField] [Range(10.0f, 100.0f)] private float _followOffsetMaxY = 1000f;

    [Header("Camera Bounds")]
    [SerializeField] private Vector3 _cameraOrigin = Vector3.zero;
    [SerializeField] [Range(10.0f, 1000.0f)] private float _cameraBoundX = 100.0f;
    [SerializeField] [Range(10.0f, 1000.0f)] private float _cameraBoundY = 100.0f;
    [SerializeField] [Range(10.0f, 1000.0f)] private float _cameraBoundZ = 100.0f;
    [SerializeField] private bool _enableCameraBounds = true;

    private void Awake()
    {   
        _cinemachineTransposer = _cinemachineVirtualCamera.GetCinemachineComponent<CinemachineTransposer>();
    }

    private void Start()
    {
        InitializeCameraSystem();
        CheckWarnings();
    }

    private void Update()
    {
        HandleMovement();
        HandleRotation();
        HandleZoom();
    }

    private void OnDrawGizmos()
    {
        if (_enableCameraBounds)
        {   
            Gizmos.color =  Color.green;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 5.0f); // Camera System Location
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(_cameraOrigin, new Vector3(_cameraBoundX, _cameraBoundY, _cameraBoundZ)); // Camera System Bounds
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(_cameraOrigin, new Vector3(
                _cameraBoundX + Mathf.Abs(_cinemachineVirtualCamera.GetCinemachineComponent<CinemachineTransposer>().m_FollowOffset.x * 2.0f),
                _cameraBoundY + Mathf.Abs(_cinemachineVirtualCamera.GetCinemachineComponent<CinemachineTransposer>().m_FollowOffset.y * 2.0f),
                _cameraBoundZ + Mathf.Abs(_cinemachineVirtualCamera.GetCinemachineComponent<CinemachineTransposer>().m_FollowOffset.z * 2.0f)
            )); // Cinemachine Virtual Camera Bounds
        }
    }

    private void InitializeCameraSystem()
    {
        _targetFOV = _cinemachineVirtualCamera.m_Lens.FieldOfView;
        _followOffset = _cinemachineTransposer.m_FollowOffset;
    }

    private void HandleMovement()
    {
        if (_enableKeyboardMovement)
            HandleKeyboardMovement();
        if (_enableEdgeScrolling)
            HandleMovementEdgeScrolling();
        if (_enableDragPanning)
            HandleMovementDragPanning();
        if (_enableCameraBounds)
            ClampCamera();
    }

    private void HandleKeyboardMovement()
    {
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputZ = Input.GetAxisRaw("Vertical");
        Vector3 moveDirection = (inputX * _cinemachineVirtualCamera.transform.right + inputZ * _cinemachineVirtualCamera.transform.up).normalized;
        moveDirection.y = 0.0f;
        transform.Translate(moveDirection * _movementSpeed * Time.deltaTime, Space.World);
    }

    private void HandleMovementEdgeScrolling()
    {
        Vector3 moveDirection = Vector3.zero;
        if (_enableEdgeScrolling)
        {
            if (Input.mousePosition.x < _edgeScrollBuffer) moveDirection += -1.0f * _cinemachineVirtualCamera.transform.right; 
            if (Input.mousePosition.y < _edgeScrollBuffer) moveDirection += -1.0f * _cinemachineVirtualCamera.transform.up;
            if (Input.mousePosition.x > Screen.width - _edgeScrollBuffer) moveDirection += 1.0f * _cinemachineVirtualCamera.transform.right;
            if (Input.mousePosition.y > Screen.height - _edgeScrollBuffer) moveDirection += 1.0f * _cinemachineVirtualCamera.transform.up;
        }
        moveDirection.y = 0.0f;
        moveDirection = moveDirection.normalized;
        transform.Translate(moveDirection * _movementSpeed * Time.deltaTime, Space.World);
    }

    private void HandleMovementDragPanning()
    {   
        Vector3 moveDirection = Vector3.zero;
        if (Input.GetMouseButtonDown(1))
        {
            _isDragging = true;
            _lastMousePosition = Input.mousePosition;
        }
        if (Input.GetMouseButtonUp(1))
        {
            _isDragging = false;
        }
        if (_isDragging)
        {
            Vector2 mouseMovementDelta = (Vector2)Input.mousePosition - _lastMousePosition;
            moveDirection = mouseMovementDelta.x * -_dragPanSpeed * _cinemachineVirtualCamera.transform.right + mouseMovementDelta.y * -_dragPanSpeed * _cinemachineVirtualCamera.transform.up;
            _lastMousePosition = Input.mousePosition;
        }
        moveDirection.y = 0.0f;
        transform.Translate(moveDirection * _movementSpeed * Time.deltaTime, Space.World);
    }

    private void ClampCamera()
    {
        transform.position = new Vector3
        (
            Mathf.Clamp(transform.position.x, _cameraOrigin.x - (_cameraBoundX / 2.0f), _cameraOrigin.x + (_cameraBoundX / 2.0f)),
            Mathf.Clamp(transform.position.y, _cameraOrigin.y - (_cameraBoundY / 2.0f), _cameraOrigin.y + (_cameraBoundY / 2.0f)),
            Mathf.Clamp(transform.position.z, _cameraOrigin.z - (_cameraBoundZ / 2.0f), _cameraOrigin.z + (_cameraBoundZ / 2.0f))
        );
    }

    private void HandleRotation()
    {   
        if (_enableRotationSnapping)
        {
            if (!_isSnapping && Input.GetKeyDown(_rotateLeftKey))
            {
                _isSnapping = true;
                targetRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f) * transform.rotation;
            }
            if (!_isSnapping && Input.GetKeyDown(_rotateRightKey))
            {
                _isSnapping = true;
                targetRotation = Quaternion.Euler(0.0f, -90.0f, 0.0f) * transform.rotation;
            }
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, _rotationSnapSpeed * Time.deltaTime);
            // Ensure target rotation is reached
            if (Quaternion.Angle(transform.rotation, targetRotation) < 0.5f)
            {
                transform.rotation = targetRotation;
                _isSnapping = false;
            }
        }
        else
        {
            float rotateDirection = 0.0f;
            if (Input.GetKey(_rotateLeftKey)) rotateDirection++;
            if (Input.GetKey(_rotateRightKey)) rotateDirection--;
            transform.Rotate(0.0f, rotateDirection * _rotatationSpeed * Time.deltaTime, 0.0f);
        }
    }

    private void HandleZoom()
    {
        if (_enableZoom)
        {
            switch (_zoomType)
            {
                case ZoomType.FOV:
                    HandleFOVZoom();
                    break;
                case ZoomType.Forward:
                    HandleForwardZoom();
                    break;
                case ZoomType.Y:
                    HandleYZoom();
                    break;
            }
        }
    }

    private void HandleFOVZoom()
    {
        if (Input.mouseScrollDelta.y > 0.0f)
            _targetFOV -= _zoomAmount;
        if (Input.mouseScrollDelta.y < 0.0f)
            _targetFOV += _zoomAmount;

        _targetFOV = Mathf.Clamp(_targetFOV, _fovMin, _fovMax);
        _cinemachineVirtualCamera.m_Lens.FieldOfView = Mathf.Lerp(_cinemachineVirtualCamera.m_Lens.FieldOfView, _targetFOV, _zoomSpeed * Time.deltaTime);
    }

    private void HandleForwardZoom()
    {   
        Vector3 _zoomDirection = _followOffset.normalized;
        if (Input.mouseScrollDelta.y > 0.0f)
            _followOffset -= _zoomDirection * _zoomAmount;
        if (Input.mouseScrollDelta.y < 0.0f)
            _followOffset += _zoomDirection * _zoomAmount;

        if (_followOffset.magnitude < _followOffsetMin)
            _followOffset = _zoomDirection * _followOffsetMin;
        if (_followOffset.magnitude > _followOffsetMax)
            _followOffset = _zoomDirection * _followOffsetMax;
        _cinemachineTransposer.m_FollowOffset = Vector3.Lerp(_cinemachineTransposer.m_FollowOffset, _followOffset, _zoomSpeed * Time.deltaTime);
    }

    private void HandleYZoom()
    {
        if (Input.mouseScrollDelta.y > 0.0f)
            _followOffset.y -= _zoomAmount;
        if (Input.mouseScrollDelta.y < 0.0f)
            _followOffset.y += _zoomAmount;

        _followOffset.y = Mathf.Clamp(_followOffset.y, _followOffsetMinY, _followOffsetMaxY);
        _cinemachineTransposer.m_FollowOffset = Vector3.Lerp(_cinemachineTransposer.m_FollowOffset, _followOffset, _zoomSpeed * Time.deltaTime);
    }

    private void CheckWarnings()
    {
        if (_fovMin - EPSILON > _cinemachineVirtualCamera.m_Lens.FieldOfView)
            Debug.LogWarning($"_fovMin: {_fovMin} is greater than Cinemachine's Vertical FOV: {_cinemachineVirtualCamera.m_Lens.FieldOfView}");
        if (_fovMax + EPSILON < _cinemachineVirtualCamera.m_Lens.FieldOfView)
            Debug.LogWarning($"_fovMax: {_fovMax} is less than Cinemachine's Vertical FOV: {_cinemachineVirtualCamera.m_Lens.FieldOfView}");
        if (_followOffsetMin - EPSILON > _followOffset.magnitude)
            Debug.LogWarning($"_followOffsetMin: {_followOffsetMax} is greater than the magnitude of Cinemachine's Follow Offset: {_followOffset.magnitude}");
        if (_followOffsetMax + EPSILON < _followOffset.magnitude)
            Debug.LogWarning($"_followOffsetMaxt: {_followOffsetMax} is less than the magnitude of Cinemachine's Follow Offset: {_followOffset.magnitude}");
        if (_followOffsetMinY - EPSILON > _followOffset.y)
            Debug.LogWarning($"_followOffsetMinY: {_followOffsetMax} is greater than Cinemachine's Follow Offset Y: {_followOffset.y}");
        if (_followOffsetMaxY + EPSILON < _followOffset.y)
            Debug.LogWarning($"_followOffsetMaxY: {_followOffsetMax} is less than Cinemachine's Follow Offset Y: {_followOffset.y}");
    }
}