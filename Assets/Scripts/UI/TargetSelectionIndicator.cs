using UnityEngine;

/// <summary>
/// 現在の選択目標の頭上に倒三角インジケーターを表示するコンポーネント。
/// Player に挂载し、PlayerTargeting.CurrentTarget を毎フレーム読む。
///
/// 使い方：
///   1. Player の GameObject にアタッチ。
///   2. Inspector で indicatorPrefab に倒三角 Prefab を指定。
///   3. targetOffset / fallbackHeight で表示位置を調整。
///
/// 目標選択ロジック・Raycast・陣営判定は一切行わない。
/// インジケーターは初回表示時のみ Instantiate し、以後は SetActive で表示/非表示を切り替える。
/// </summary>
public class TargetSelectionIndicator : MonoBehaviour
{
    // ─── Inspector 設定 ───────────────────────────────────────────

    [Header("参照")]
    [SerializeField] private PlayerTargeting playerTargeting;
    [SerializeField] private GameObject      indicatorPrefab;

    [Header("頭上位置設定")]
    [SerializeField] private Vector3 targetOffset          = new Vector3(0f, 0.4f, 0f);
    [SerializeField] private float   fallbackHeight        = 2f;

    [Header("表示制御")]
    [SerializeField] private bool hideWhenTargetDead = true;

    // ─── 运行时字段 ───────────────────────────────────────────────

    private GameObject _indicatorInstance;
    private Transform  _currentTarget;
    private Camera     _mainCamera;

    // prefab 未設定 Warning を一度だけ出すためのフラグ
    private bool _prefabWarningShown;

    // ─── Unity 生命周期 ───────────────────────────────────────────

    private void Awake()
    {
        if (playerTargeting == null)
            playerTargeting = GetComponent<PlayerTargeting>();

        if (playerTargeting == null)
            Debug.LogWarning("[TargetSelectionIndicator] PlayerTargeting not found. Indicator will not function.");

        _mainCamera = Camera.main;
    }

    private void OnDisable()
    {
        HideIndicator();
    }

    private void OnDestroy()
    {
        if (_indicatorInstance != null)
            Destroy(_indicatorInstance);
    }

    private void LateUpdate()
    {
        // PlayerTargeting が見つからない場合は非表示
        if (playerTargeting == null)
        {
            HideIndicator();
            return;
        }

        Transform target = playerTargeting.CurrentTarget;

        // 目標なし
        if (target == null)
        {
            HideIndicator();
            return;
        }

        // 目標が死亡している場合は非表示
        if (hideWhenTargetDead)
        {
            var health = target.GetComponent<HealthComponent>();
            if (health != null && health.IsDead)
            {
                HideIndicator();
                return;
            }
        }

        // インジケーター インスタンスを確保
        if (!EnsureIndicatorInstance())
            return;

        // 位置更新
        _indicatorInstance.transform.position = CalculateIndicatorPosition(target);

        // カメラに向ける
        FaceCamera();

        // 表示
        if (!_indicatorInstance.activeSelf)
            _indicatorInstance.SetActive(true);
    }

    // ─── Private メソッド ─────────────────────────────────────────

    /// <summary>
    /// インジケーター インスタンスを初回のみ生成する。
    /// 既に存在する場合はそのまま true を返す。
    /// prefab 未設定の場合は Warning を一度出して false を返す。
    /// </summary>
    private bool EnsureIndicatorInstance()
    {
        if (_indicatorInstance != null)
            return true;

        if (indicatorPrefab == null)
        {
            if (!_prefabWarningShown)
            {
                Debug.LogWarning("[TargetSelectionIndicator] indicatorPrefab is not assigned.");
                _prefabWarningShown = true;
            }
            return false;
        }

        _indicatorInstance = Instantiate(indicatorPrefab);
        _indicatorInstance.SetActive(false);
        return true;
    }

    /// <summary>
    /// 目標の頭上座標を計算する。
    /// 非 Trigger Collider の bounds.max.y を優先し、
    /// Collider が見つからない場合は fallbackHeight を使用する。
    /// </summary>
    private Vector3 CalculateIndicatorPosition(Transform target)
    {
        // 自身と子の全 Collider を収集（Trigger を除外）
        Collider[] colliders = target.GetComponentsInChildren<Collider>();

        bool   hasBounds = false;
        Bounds combined  = new Bounds();

        foreach (var col in colliders)
        {
            if (col.isTrigger) continue;

            if (!hasBounds)
            {
                combined  = col.bounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(col.bounds);
            }
        }

        Vector3 basePos;
        if (hasBounds)
        {
            // bounds の上端 (max.y) をベースに、XZ は target.position に合わせる
            basePos = new Vector3(target.position.x, combined.max.y, target.position.z);
        }
        else
        {
            // Collider なし / 全て Trigger → fallback
            basePos = target.position + Vector3.up * fallbackHeight;
        }

        return basePos + targetOffset;
    }

    /// <summary>
    /// インジケーターをカメラ正面に向ける（Billboard）。
    /// </summary>
    private void FaceCamera()
    {
        if (_mainCamera == null)
            _mainCamera = Camera.main;

        if (_mainCamera == null || _indicatorInstance == null)
            return;

        Vector3 direction = _indicatorInstance.transform.position - _mainCamera.transform.position;
        if (direction.sqrMagnitude > 0.0001f)
            _indicatorInstance.transform.rotation = Quaternion.LookRotation(direction);
    }

    /// <summary>
    /// インジケーターを非表示にする。インスタンスは破棄しない。
    /// </summary>
    private void HideIndicator()
    {
        if (_indicatorInstance != null && _indicatorInstance.activeSelf)
            _indicatorInstance.SetActive(false);
    }
}
