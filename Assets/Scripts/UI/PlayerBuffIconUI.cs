using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>単一 Buff アイコン表示コンポーネント。</summary>
public class PlayerBuffIconUI : MonoBehaviour
{
    [SerializeField] private Image    iconImage;
    [SerializeField] private TMP_Text timerText;

    private string _boundBuffId;
    public string BoundBuffId => _boundBuffId;

    public void Bind(string buffId, PlayerBuffRuntime buff)
    {
        _boundBuffId = buffId;
        if (iconImage != null) iconImage.sprite = buff.Icon;
        UpdateTimer(buff);
    }

    public void UpdateDisplay(PlayerBuffRuntime buff)
    {
        if (buff != null) UpdateTimer(buff);
    }

    private void UpdateTimer(PlayerBuffRuntime buff)
    {
        if (timerText == null) return;
        timerText.text = buff.HasDuration
            ? (Mathf.CeilToInt(buff.RemainingTime) > 0 ? Mathf.CeilToInt(buff.RemainingTime).ToString() : "0")
            : "--";
    }
}
