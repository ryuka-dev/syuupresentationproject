using UnityEngine;

public class SkeletonSpawner : MonoBehaviour
{
    public GameObject skeletonPrefab;
    public Transform playerTransform;
    public float spawnRadius = 10f;
    public float minDistance = 3f;
    public int maxCount = 20;

    private int currentCount = 0;

    void Start()
    {
        if (playerTransform == null)
            playerTransform = GameObject.Find("Player")?.transform;

        // Prefab 包含所有组件，直接从 Resources 加载
        if (skeletonPrefab == null)
            skeletonPrefab = Resources.Load<GameObject>("SkeletonEnemy");

        if (skeletonPrefab == null)
            Debug.LogError("SkeletonSpawner: SkeletonEnemy prefab not found in Resources!");
    }

public bool SpawnSkeleton()
{
    if (playerTransform == null || skeletonPrefab == null) return false;
    if (currentCount >= maxCount) return false;

    Vector2 circle = Random.insideUnitCircle * spawnRadius;
    if (circle.magnitude < minDistance)
        circle = circle.normalized * minDistance;

    Vector3 spawnPos = playerTransform.position + new Vector3(circle.x, 0, circle.y);
    spawnPos.y = 0.5f;

    var skeleton = Instantiate(skeletonPrefab, spawnPos, Quaternion.identity);
    skeleton.name = "Skeleton_" + currentCount;
    currentCount++;

    // 动态注册到关卡管理器（如果存在）
    var levelManager = FindFirstObjectByType<LevelObjectiveManager>();
    if (levelManager != null)
    {
        var hc = skeleton.GetComponent<HealthComponent>();
        if (hc != null) levelManager.RegisterEnemy(hc);
    }

    return true;
}

    public int GetCount() => currentCount;

    public void ClearAll()
    {
        var all = FindObjectsOfType<EnemyAI>();
        foreach (var ai in all)
            if (ai.name.StartsWith("Skeleton_")) Destroy(ai.gameObject);
        currentCount = 0;
    }
}
