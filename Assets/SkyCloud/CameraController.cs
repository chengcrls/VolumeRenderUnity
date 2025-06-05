using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 10f;
    public float fastMoveSpeed = 50f;
    public float smoothTime = 0.1f;
    
    [Header("Rotation Settings")]
    public float mouseSensitivity = 2f;
    public bool invertY = false;
    
    [Header("Zoom Settings")]
    public float zoomSpeed = 10f;
    public float minZoom = 1f;
    public float maxZoom = 100f;
    
    [Header("Focus Settings")]
    public float focusSpeed = 5f;
    
    // Private variables
    private Vector3 targetPosition;
    private Vector3 currentVelocity;
    private float rotationX = 0f;
    private float rotationY = 0f;
    private bool isRotating = false;
    private bool isPanning = false;
    private Vector3 lastMousePosition;
    
    private Camera cam;
    
    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            cam = Camera.main;
        }
        
        targetPosition = transform.position;
        
        // 初始化旋转角度
        Vector3 euler = transform.eulerAngles;
        rotationX = euler.x;
        rotationY = euler.y;
        
        // 锁定光标（可选）
        // Cursor.lockState = CursorLockMode.None;
    }
    
    void Update()
    {
        HandleMouseInput();
        HandleKeyboardInput();
        HandleZoom();
        
        // 平滑移动到目标位置
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothTime);
    }
    
    void HandleMouseInput()
    {
        // 右键旋转（类似Editor的Alt+左键）
        if (Input.GetMouseButtonDown(1))
        {
            isRotating = true;
            lastMousePosition = Input.mousePosition;
        }
        else if (Input.GetMouseButtonUp(1))
        {
            isRotating = false;
        }
        
        // 中键平移
        if (Input.GetMouseButtonDown(2))
        {
            isPanning = true;
            lastMousePosition = Input.mousePosition;
        }
        else if (Input.GetMouseButtonUp(2))
        {
            isPanning = false;
        }
        
        // 执行旋转
        if (isRotating)
        {
            Vector3 mouseDelta = Input.mousePosition - lastMousePosition;
            
            rotationY += mouseDelta.x * mouseSensitivity * 0.1f;
            rotationX -= mouseDelta.y * mouseSensitivity * 0.1f * (invertY ? -1 : 1);
            
            // 限制垂直旋转角度
            rotationX = Mathf.Clamp(rotationX, -90f, 90f);
            
            transform.rotation = Quaternion.Euler(rotationX, rotationY, 0f);
            
            lastMousePosition = Input.mousePosition;
        }
        
        // 执行平移
        if (isPanning)
        {
            Vector3 mouseDelta = Input.mousePosition - lastMousePosition;
            
            // 根据摄像机距离调节平移速度
            float distance = Vector3.Distance(transform.position, Vector3.zero);
            float panSpeed = distance * 0.001f;
            
            Vector3 move = new Vector3(-mouseDelta.x * panSpeed, -mouseDelta.y * panSpeed, 0);
            move = transform.TransformDirection(move);
            
            targetPosition += move;
            
            lastMousePosition = Input.mousePosition;
        }
    }
    
    void HandleKeyboardInput()
    {
        Vector3 moveDirection = Vector3.zero;
        
        // WASD 移动
        if (Input.GetKey(KeyCode.W))
            moveDirection += transform.forward;
        if (Input.GetKey(KeyCode.S))
            moveDirection -= transform.forward;
        if (Input.GetKey(KeyCode.A))
            moveDirection -= transform.right;
        if (Input.GetKey(KeyCode.D))
            moveDirection += transform.right;
        
        // QE 上下移动
        if (Input.GetKey(KeyCode.Q))
            moveDirection -= transform.up;
        if (Input.GetKey(KeyCode.E))
            moveDirection += transform.up;
        
        // 按住Shift加速
        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? fastMoveSpeed : moveSpeed;
        
        if (moveDirection != Vector3.zero)
        {
            targetPosition += moveDirection.normalized * currentSpeed * Time.deltaTime;
        }
    }
    
    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        
        if (scroll != 0f)
        {
            // 正交摄像机调整size，透视摄像机移动位置
            if (cam.orthographic)
            {
                cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - scroll * zoomSpeed, minZoom, maxZoom);
            }
            else
            {
                Vector3 zoomDirection = transform.forward * scroll * zoomSpeed;
                targetPosition += zoomDirection;
            }
        }
    }
    
    /// <summary>
    /// 聚焦到指定物体（类似Editor的F键）
    /// </summary>
    /// <param name="target">目标物体</param>
    public void FocusOnTarget(Transform target)
    {
        if (target == null) return;
        
        Bounds bounds = GetBounds(target);
        FocusOnBounds(bounds);
    }
    
    /// <summary>
    /// 聚焦到指定位置
    /// </summary>
    /// <param name="position">目标位置</param>
    public void FocusOnPosition(Vector3 position)
    {
        Vector3 direction = transform.forward;
        float distance = 10f; // 默认距离
        
        targetPosition = position - direction * distance;
    }
    
    /// <summary>
    /// 聚焦到包围盒
    /// </summary>
    /// <param name="bounds">包围盒</param>
    public void FocusOnBounds(Bounds bounds)
    {
        float distance = bounds.size.magnitude;
        Vector3 direction = transform.forward;
        
        targetPosition = bounds.center - direction * distance * 1.5f;
    }
    
    /// <summary>
    /// 获取物体的包围盒
    /// </summary>
    /// <param name="target">目标物体</param>
    /// <returns>包围盒</returns>
    private Bounds GetBounds(Transform target)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
        {
            return renderer.bounds;
        }
        
        // 如果没有渲染器，返回默认包围盒
        return new Bounds(target.position, Vector3.one);
    }
    
    /// <summary>
    /// 重置摄像机到默认位置
    /// </summary>
    public void ResetCamera()
    {
        targetPosition = new Vector3(0, 0, -10);
        transform.rotation = Quaternion.identity;
        rotationX = 0f;
        rotationY = 0f;
        
        if (cam.orthographic)
        {
            cam.orthographicSize = 5f;
        }
    }
    
    void OnGUI()
    {
        // 显示控制说明
        GUI.Box(new Rect(10, 10, 300, 150), "摄像机控制说明");
        GUI.Label(new Rect(20, 35, 280, 20), "右键拖拽: 旋转视角");
        GUI.Label(new Rect(20, 55, 280, 20), "中键拖拽: 平移视角");
        GUI.Label(new Rect(20, 75, 280, 20), "滚轮: 缩放");
        GUI.Label(new Rect(20, 95, 280, 20), "WASD: 移动 (Shift加速)");
        GUI.Label(new Rect(20, 115, 280, 20), "QE: 上下移动");
        GUI.Label(new Rect(20, 135, 280, 20), "按R键重置摄像机");
        
        // R键重置
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetCamera();
        }
    }
}