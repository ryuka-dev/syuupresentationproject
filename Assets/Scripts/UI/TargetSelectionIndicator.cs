using UnityEngine;

/// <summary>
/// 現在の選択目標の頭上に倒三角インジケーターを表示するコンポーネント。
/// Player に挂载し、PlayerTargeting.CurrentTarget を毎フレーム読む。
///
/// バグ修正 (v0.2)：
///   以前は LateUpdate 毎フレーム Collider bounds を再走査していたため、
///   アニメーション中に bounds.max.y が変動し、指示器が上下に頻闪していた。
///   現在は目標切換時に一度だけオフセットを計算し、以後は
///   target.position + _cachedWorldOffsetFromTarget で毎フレーム位置更新する。
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
    [SerializeField] private Vector3 targetOffset   = new Vector3(0f, 0.4f, 0f);
    [SerializeField] private float   fallbackHeight = 2f;

    [Header("表示制御")]
    [SerializeField] private bool hideWhenTargetDead = true;

    // ─── 运行时字段 ───────────────────────────────────────────────

    private GameObject _indicatorInstance;
    private Transform  _currentTarget;
    private Camera     _mainCamera;

    /// <summary>
    /// 目標切換時に一度だけ計算する「target.position → 頭上」のワールドオフセット。
    /// 毎フレーム Collider を再走査する代わりにこの値を使って位置を更新する。
    /// </summary>
    private Vector3 _cachedWorldOffsetFromTarget;

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
            ClearTarget();
            return;
        }

        Transform target = playerTargeting.CurrentTarget;

        // 目標が切り替わった場合 → オフセットを再計算
        if (target != _currentTarget)
        {
            _currentTarget = target;

            if (_currentTarget != null)
            {
                _cachedWorldOffsetFromTarget = CalculateWorldOffsetFromTarget(_currentTarget);
            }
            else
            {
                _cachedWorldOffsetFromTarget = Vector3.zero;
            }
        }

        // 目標なし
        if (_currentTarget == null)
        {
            HideIndicator();
            return;
        }

        // 目標が死亡している場合は非表示
        if (hideWhenTargetDead)
        {
            var health = _currentTarget.GetComponent<HealthComponent>();
            if (health != null && health.IsDead)
            {
                ClearTarget();
                return;
            }
        }

        // インジケーター インスタンスを確保
        if (!EnsureIndicatorInstance())
            return;

        // 毎フレームの位置更新：Collider 走査なし、キャッシュ済みオフセットを加算するだけ
        _indicatorInstance.transform.position = _currentTarget.position + _cachedWorldOffsetFromTarget;

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
    /// 目標切換時に一度だけ呼ばれる。
    /// target.position を基準にした「頭上への相対オフセット」を返す。
    /// 非 Trigger Collider の bounds.max.y を優先し、
    /// Collider が見つからない場合は fallbackHeight を使用する。
    /// 毎フレーム呼ばない。
    /// </summary>
    private Vector3 CalculateWorldOffsetFromTarget(Transform target)
    {
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

        if (hasBounds)
        {
            // 頭上位置 = (target.x, colliderbounds.max.y, target.z) + targetOffset
            Vector3 headPosition = new Vector3(
                target.position.x,
                combined.max.y,
                target.position.z
            ) + targetOffset;

            // target.position からの相対オフセットとして返す
            return headPosition - target.position;
        }
        else
        {
            // Collider なし / 全て Trigger → fallback
            return Vector3.up * fallbackHeight + targetOffset;
        }
    }

    /// <summary>
    /// インジケーターを TextMeshPro 世界文字に適した方法でカメラに向ける。
    /// transform.forward = camera.forward 方式（DamageNumberPopup と統一）。
    /// </summary>
    private void FaceCamera()
    {
        if (_indicatorInstance == null)
            return;

        if (_mainCamera == null)
            _mainCamera = Camera.main;

        if (_mainCamera == null)
            return;

        _indicatorInstance.transform.forward = _mainCamera.transform.forward;
    }

    /// <summary>
    /// 目標が消えた / 死亡した場合にキャッシュをリセットして非表示にする。
    /// 次回同一目標を選び直した場合に高さを再計算できるよう _currentTarget も null にする。
    /// </summary>
    private void ClearTarget()
    {
        _currentTarget               = null;
        _cachedWorldOffsetFromTarget = Vector3.zero;
        HideIndicator();
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
