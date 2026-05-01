using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 最小关卡流程控制器。
/// 职责：击杀指定数量敌人后胜利 / 玩家死亡后失败 / 按 R 重开当前场景 / 显示简易 UI。
/// 挂载到场景中的 LevelObjectiveManager GameObject 上。
/// </summary>
public class LevelObjectiveManager : MonoBehaviour
{
    [Header("关卡配置")]
    [SerializeField] private HealthComponent playerHealth;
    [SerializeField] private List<HealthComponent> enemyHealthComponents = new();
    [SerializeField] private int requiredKills = 3;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private TextMeshProUGUI restartHintText;

    private int _defeatedCount;
    private bool _isLevelEnded;

    // 防止同一敌人 OnDied 被重复计数
    private readonly HashSet<HealthComponent> _countedEnemies = new();

    // -------------------------------------------------------
    // 生命周期
    // -------------------------------------------------------

    private void Start()
    {
        UpdateUI();
    }

    private void OnEnable()
    {
        if (playerHealth != null)
            playerHealth.OnDied += HandlePlayerDied;

        foreach (var enemy in enemyHealthComponents)
        {
            if (enemy != null)
                enemy.OnDied += MakeEnemyDiedHandler(enemy);
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnDied -= HandlePlayerDied;

        foreach (var enemy in enemyHealthComponents)
        {
            if (enemy != null)
                enemy.OnDied -= MakeEnemyDiedHandler(enemy);
        }
    }

    private void Update()
    {
        if (!_isLevelEnded) return;

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
        {
            Debug.Log("[LevelObjectiveManager] Restarting level...");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    // -------------------------------------------------------
    // UI
    // -------------------------------------------------------

    private void UpdateUI()
    {
        if (progressText != null)
            progressText.text = "目标：击败 " + _defeatedCount + " / " + requiredKills + " 个敌人";

        if (!_isLevelEnded)
        {
            if (resultText != null)      resultText.gameObject.SetActive(false);
            if (restartHintText != null) restartHintText.gameObject.SetActive(false);
        }
    }

    private void ShowVictory()
    {
        if (resultText != null)
        {
            resultText.gameObject.SetActive(true);
            resultText.text = "Victory!";
        }
        if (restartHintText != null)
        {
            restartHintText.gameObject.SetActive(true);
            restartHintText.text = "按 R 重新开始";
        }
    }

    private void ShowGameOver()
    {
        if (resultText != null)
        {
            resultText.gameObject.SetActive(true);
            resultText.text = "Game Over";
        }
        if (restartHintText != null)
        {
            restartHintText.gameObject.SetActive(true);;
            restartHintText.text = "按 R 重新开始";
        }
    }

    // -------------------------------------------------------
    // 事件处理
    // -------------------------------------------------------

    private void HandlePlayerDied()
    {
        if (_isLevelEnded) return;
        _isLevelEnded = true;
        Debug.Log("[LevelObjectiveManager] Game Over!");
        ShowGameOver();
    }

    private System.Action MakeEnemyDiedHandler(HealthComponent enemy)
    {
        return () => HandleEnemyDied(enemy);
    }

    private void HandleEnemyDied(HealthComponent enemy)
    {
        if (_isLevelEnded) return;
        if (_countedEnemies.Contains(enemy)) return;

        _countedEnemies.Add(enemy);
        _defeatedCount++;

        Debug.Log("[LevelObjectiveManager] Enemy defeated: " + _defeatedCount + "/" + requiredKills);
        UpdateUI();

        if (_defeatedCount >= requiredKills)
        {
            _isLevelEnded = true;
            Debug.Log("[LevelObjectiveManager] Victory!");
            ShowVictory();
        }
    }

    /// <summary>
    /// 动态注册敌人（供 SkeletonSpawner 等运行时生成器调用）。
    /// </summary>
    public void RegisterEnemy(HealthComponent enemy)
    {
        if (enemy == null || _isLevelEnded) return;
        if (enemyHealthComponents.Contains(enemy)) return;
        enemyHealthComponents.Add(enemy);
        enemy.OnDied += MakeEnemyDiedHandler(enemy);
    }
}
