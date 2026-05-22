using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player ↔ Enemy の物理碰撞忽略スクリプト。
/// EnemyBase.prefab と SkeletonEnemy.prefab に挂载。
///
/// Start 时に本敌人の非 Trigger Collider と Player の非 Trigger Collider を取得し、
/// 全ペアに対して Physics.IgnoreCollision(enemy, player, true) を呼ぶ。
///
/// 【影響しないもの】
/// - Raycast（PlayerTargeting のマウスクリック・Tab 選択は引き続き有効）
/// - Trigger Collider（拾取・検測・攻撃判定等）
/// - 敵人同士の碰撞
/// - 伤害・HP・掉落・Hikari・スキル等の全ゲームロジック
///
/// 【キャッシュ戦略】
/// PlayerController 参照と Player Collider 配列を static で共有し、
/// 各敵 Prefab の Start 時に一度だけ Find を実行する。
/// Play Mode 再入時は Unity のオブジェクトライフサイクルで自動的に null になり、
/// 次の Start 時に再取得される。
/// </summary>
public class EnemyPlayerCollisionIgnore : MonoBehaviour
{
    // ─── 静的キャッシュ（全敵インスタンスで共有） ─────────────────
    private static PlayerController _cachedPlayer;
    private static Collider[]       _cachedPlayerColliders;

    // ─── インスタンス変数 ─────────────────────────────────────
    private Collider[] _myColliders;

    // ─── Unity 生命周期 ───────────────────────────────────────
    private void Start()
    {
        // 自身の非 Trigger Collider を収集（子オブジェクト含む）
        _myColliders = CollectNonTriggerColliders(gameObject);
        if (_myColliders.Length == 0)
        {
            Debug.LogWarning($"[EnemyPlayerCollisionIgnore] {gameObject.name}: 非 Trigger Collider が見つかりません。スキップします。");
            return;
        }

        // キャッシュが有効なら即時適用
        if (IsPlayerCacheValid())
        {
            ApplyIgnoreCollision();
            return;
        }

        // 初回 or キャッシュ無効 → 再取得
        RefreshPlayerCache();

        if (IsPlayerCacheValid())
        {
            ApplyIgnoreCollision();
        }
        else
        {
            // Player が Start 時点でまだ存在しない場合（まれ）→ 短いコルーチンで 1 回リトライ
            StartCoroutine(RetryAfterDelay());
        }
    }

    // ─── キャッシュ管理 ───────────────────────────────────────

    /// <summary>静的キャッシュが有効かどうかを確認する。</summary>
    private static bool IsPlayerCacheValid()
    {
        // Unity オブジェクトは Play Mode 終了で null になるため、これで再入時の残留を検知できる。
        return _cachedPlayer != null
            && _cachedPlayerColliders != null
            && _cachedPlayerColliders.Length > 0;
    }

    /// <summary>シーン内の PlayerController を探し、静的キャッシュを更新する。</summary>
    private static void RefreshPlayerCache()
    {
        var pc = Object.FindFirstObjectByType<PlayerController>();
        if (pc == null)
        {
            _cachedPlayer          = null;
            _cachedPlayerColliders = null;
            return;
        }

        _cachedPlayer          = pc;
        _cachedPlayerColliders = CollectNonTriggerColliders(pc.gameObject);

        if (_cachedPlayerColliders.Length == 0)
            Debug.LogWarning("[EnemyPlayerCollisionIgnore] Player 上に非 Trigger Collider が見つかりません。");
    }

    // ─── Collider 収集 ────────────────────────────────────────

    /// <summary>指定 GameObject（子オブジェクト含む）の非 Trigger Collider 配列を返す。</summary>
    private static Collider[] CollectNonTriggerColliders(GameObject go)
    {
        var all  = go.GetComponentsInChildren<Collider>(true);
        var list = new List<Collider>(all.Length);
        foreach (var c in all)
            if (!c.isTrigger) list.Add(c);
        return list.ToArray();
    }

    // ─── Physics.IgnoreCollision 適用 ────────────────────────

    /// <summary>
    /// 自身の全非 Trigger Collider × Player の全非 Trigger Collider に対して
    /// Physics.IgnoreCollision(enemy, player, true) を呼び出す。
    /// </summary>
    private void ApplyIgnoreCollision()
    {
        foreach (var ec in _myColliders)
        {
            if (ec == null) continue;
            foreach (var pc in _cachedPlayerColliders)
            {
                if (pc == null) continue;
                Physics.IgnoreCollision(ec, pc, true);
            }
        }
        Debug.Log($"[EnemyPlayerCollisionIgnore] {gameObject.name}: Player との物理碰撞を無効化しました。");
    }

    // ─── リトライ ─────────────────────────────────────────────

    /// <summary>
    /// Start 時に Player が見つからなかった場合の 1 回限りのリトライ。
    /// 永続的なループは行わない。
    /// </summary>
    private IEnumerator RetryAfterDelay()
    {
        yield return new WaitForSeconds(0.5f);

        RefreshPlayerCache();

        if (IsPlayerCacheValid())
            ApplyIgnoreCollision();
        else
            Debug.LogWarning($"[EnemyPlayerCollisionIgnore] {gameObject.name}: リトライ後も Player が見つかりませんでした。碰撞フィルタ未適用。");
    }
}
