/// <summary>
/// 戦闘テキストの表示名を保持する軽量構造体。
/// 将来的にローカライズキーを使った多言語対応が可能な設計。
/// 現状は fallbackText を返す。
/// </summary>
[System.Serializable]
public struct CombatTextSourceLabel
{
    /// <summary>
    /// ローカライズシステム向けのキー（例: "skill.player.radiant_riposte.name"）。
    /// 将来的に Localization テーブルのキーとして使用する想定。
    /// </summary>
    public string localizationKey;

    /// <summary>
    /// ローカライズが未実装の間に表示するフォールバックテキスト。
    /// </summary>
    public string fallbackText;

    /// <summary>
    /// 現在の表示テキストを返す。
    /// fallbackText が空でない場合はそちらを優先する。
    /// 将来的にここで Localization lookup に切り替え可能。
    /// </summary>
    public string GetDisplayText()
    {
        return string.IsNullOrEmpty(fallbackText) ? localizationKey : fallbackText;
    }
}
