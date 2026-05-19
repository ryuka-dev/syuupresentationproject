using UnityEngine;

/// <summary>
/// 伤害飘字生成器。
/// 挂在带 HealthComponent 的对象上，监听 OnDamaged 事件，
/// 在对象头顶附近生成 DamageNumberPopup。
///
/// 使用方法：
///   1. 挂载到玩家 / 敌人等带 HealthComponent 的 GameObject。
///   2. 在 Inspector 中指定 popupPrefab（DamageNumberPopup Prefab）。
///   3. 可调整 popupOffset / randomHorizontalOffset 控制弹出位置。
///
/// 不计算伤害，不修改血量，不区分玩家 / 敌人 / 颜色（第一版）。
/// </summary>
public class DamageNumberSpawner : MonoBehaviour
{
    [Header("飘字 Prefab（需指定 DamageNumberPopup Prefab）")]
    [SerializeField] private DamageNumberPopup popupPrefab;

    [Header("治疗飘字 Prefab（可选，留空时 fallback 到 popupPrefab）")]
    [SerializeField] private DamageNumberPopup healingPopupPrefab;


    [Header("生成位置")]
    [SerializeField] private Vector3 popupOffset          = new Vector3(0f, 2f, 0f);
    [SerializeField] private float   randomHorizontalOffset = 0.25f;

    // ─── 运行时缓存 ───────────────────────────────────────────────

    private HealthComponent _health;
    private Camera          _mainCamera;

    // ─── Unity 生命周期 ───────────────────────────────────────────

    private void Awake()
    {
        _health = GetComponent<HealthComponent>();
        if (_health == null)
            Debug.LogWarning($"[DamageNumberSpawner] HealthComponent not found on {gameObject.name}. Spawner will not function.");

        _mainCamera = Camera.main;
    }

    private void OnEnable()
    {
        if (_health != null)
        {
            _health.OnDamaged += HandleDamaged;
            _health.OnHealed  += HandleHealed;
        }
    }

    private void OnDisable()
    {
        if (_health != null)
        {
            _health.OnDamaged -= HandleDamaged;
            _health.OnHealed  -= HandleHealed;
        }
    }

    // ─── イベントハンドラ ─────────────────────────────────────────

    /// <summary>
    /// HealthComponent.OnDamaged から呼ばれる。
    /// damage は最終実際ダメージ（減伤適用済み）。
    /// </summary>
    private void HandleDamaged(float damage, Transform attacker)
    {
        if (damage <= 0f) return;

        if (popupPrefab == null)
        {
            Debug.LogWarning($"[DamageNumberSpawner] popupPrefab is not assigned on {gameObject.name}.");
            return;
        }

        // 生成位置 = 対象位置 + オフセット + ランダム水平ゆれ
        Vector3 randomH = new Vector3(
            Random.Range(-randomHorizontalOffset, randomHorizontalOffset),
            0f,
            Random.Range(-randomHorizontalOffset, randomHorizontalOffset)
        );
        Vector3 spawnPos = transform.position + popupOffset + randomH;

        // 生成 & 初期化
        DamageNumberPopup popup = Instantiate(popupPrefab, spawnPos, Quaternion.identity);
        popup.Initialize(damage, _mainCamera);
    }


    /// <summary>
    /// HealthComponent.OnHealed から呼ばれる。
    /// actualHealAmount は実際に回復した HP（0 超の保証あり）。
    /// </summary>
    private void HandleHealed(float actualHealAmount, Transform healer)
    {
        // 使用するプレハブを決定（healingPopupPrefab 優先、なければ popupPrefab にフォールバック）
        DamageNumberPopup prefabToUse = healingPopupPrefab != null ? healingPopupPrefab : popupPrefab;

        if (prefabToUse == null)
        {
            Debug.LogWarning($"[DamageNumberSpawner] 治疗飘字 Prefab 未设置（healingPopupPrefab 和 popupPrefab 均为空）on {gameObject.name}.");
            return;
        }

        Vector3 randomH = new Vector3(
            Random.Range(-randomHorizontalOffset, randomHorizontalOffset),
            0f,
            Random.Range(-randomHorizontalOffset, randomHorizontalOffset)
        );
        Vector3 spawnPos = transform.position + popupOffset + randomH;

        DamageNumberPopup popup = Instantiate(prefabToUse, spawnPos, Quaternion.identity);
        popup.Initialize(actualHealAmount, _mainCamera);
    }
}
