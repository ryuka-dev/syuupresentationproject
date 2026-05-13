using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 传统 RPG 摄像机
///   左键拖拽 → 摄像机绕玩家自由旋转，角色方向不变
///   右键拖拽 → 摄像机旋转的同时，角色 Y 轴与摄像机 Yaw 保持同步
///   滚轮     → 推拉距离
///   拖拽时隐藏鼠标，松开后恢复显示。
/// </summary>
public class RPGCameraController : MonoBehaviour
{
    [Header("目标")]
    public Transform target;
    public Vector3 targetOffset = new Vector3(0f, 1.0f, 0f);

    [Header("距离")]
    public float distance    = 6f;
    public float minDistance = 2f;
    public float maxDistance = 14f;
    public float zoomSpeed   = 3f;

    [Header("旋转")]
    public float rotationSpeed = 200f;
    public float minPitch      = 10f;
    public float maxPitch      = 75f;

    [Header("平滑")]
    public float followSmooth = 10f;

    private float _yaw;
    private float _pitch = 30f;

    // Cursor 管理
    private bool _isCameraDragging = false;

    void Start()
    {
        var angles = transform.eulerAngles;
        _yaw   = angles.y;
        _pitch = angles.x > 180f ? angles.x - 360f : angles.x;
        _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

        // 初期状態でカーソルを表示
        SetCursorVisible(true);
    }

void LateUpdate()
    {
        if (target == null) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        // 右键のみドラッグ対象（左键は鏡頭回転に参加しない）
        bool rmb = mouse.rightButton.isPressed;

        if (rmb && !_isCameraDragging)
        {
            _isCameraDragging = true;
            SetCursorVisible(false);
        }
        else if (!rmb && _isCameraDragging)
        {
            _isCameraDragging = false;
            SetCursorVisible(true);
        }

        if (rmb)
        {
            Vector2 delta = mouse.delta.ReadValue();
            _yaw   += delta.x * rotationSpeed * Time.deltaTime;
            _pitch -= delta.y * rotationSpeed * Time.deltaTime;
            _pitch  = Mathf.Clamp(_pitch, minPitch, maxPitch);
            // Player 朝向は PlayerController が移动方向に合わせて制御する
        }

        // 滚轮缩放
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
            distance = Mathf.Clamp(distance - scroll * zoomSpeed * 0.01f, minDistance, maxDistance);

        // 计算并平滑更新摄像机位置
        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3    lookAt   = target.position + targetOffset;
        Vector3    desired  = lookAt - rotation * Vector3.forward * distance;

        transform.position = Vector3.Lerp(transform.position, desired, followSmooth * Time.deltaTime);
        transform.LookAt(lookAt);
    }

    void OnDisable()
    {
        // 無効化時（プレイヤー死亡など）に必ずカーソルを復元する
        _isCameraDragging = false;
        SetCursorVisible(true);
    }

    void OnDestroy()
    {
        _isCameraDragging = false;
        SetCursorVisible(true);
    }

    private void SetCursorVisible(bool visible)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = visible;
    }
}
