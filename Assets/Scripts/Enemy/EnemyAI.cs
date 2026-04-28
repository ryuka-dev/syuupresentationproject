using UnityEngine;

/// <summary>
/// 敌人AI控制 - 处理寻路和目标跟踪
/// </summary>
public class EnemyAI : MonoBehaviour
{
    [Header("检测")]
    public FOVDetector fovDetector;
    public Transform head;                     // 头部位置（用于眼睛位置）

    [Header("移动")]
    public float moveSpeed = 3f;
    public float stoppingDistance = 1f;
    public Rigidbody rb;

    [Header("转向")]
    public float rotationSpeed = 5f;

    [Header("动画")]
    public Animator animator;

    private Transform currentTarget;
    private Vector3 moveDirection = Vector3.zero;

void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (fovDetector == null) fovDetector = GetComponent<FOVDetector>();
        if (head == null) head = GetComponentInChildren<Transform>();
        // rb は Start() で取得（Spawner から AddComponent 後に Rigidbody が付く場合があるため）
    }

void Start()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
    }


    void Update()
    {
        // 扫描视锥内的目标
        var players = FindObjectsOfType<FactionComponent>();
        currentTarget = null;
        foreach (var fc in players) {
            if (fovDetector.CanSeeTarget(fc.transform)) {
                currentTarget = fc.transform;
                break;
            }
        }

        // 更新动画参数
        float speed = moveDirection.magnitude;
        if (animator != null) animator.SetFloat("Speed", speed, 0.1f, Time.deltaTime);
    }

void FixedUpdate()
    {
        if (currentTarget != null)
        {
            float dist = Vector3.Distance(transform.position, currentTarget.position);
            if (dist > stoppingDistance)
            {
                moveDirection = (currentTarget.position - transform.position).normalized;
                rb.linearVelocity = new Vector3(moveDirection.x * moveSpeed, rb.linearVelocity.y, moveDirection.z * moveSpeed);

                // Y 轴をゼロにして水平方向のみ転向
                Vector3 lookDir = currentTarget.position - transform.position;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookDir);
                    transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
                }
            }
            else
            {
                moveDirection = Vector3.zero;
                rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            }
        }
        else
        {
            moveDirection = Vector3.zero;
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        }

        if (animator != null)
        {
            float speed = moveDirection.magnitude;
            animator.SetFloat("Speed", speed, 0.1f, Time.deltaTime);
        }
    }
}
