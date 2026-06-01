using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>単一 Buff アイコン表示コンポーネント。Icon が null の場合はプレースホルダ色で表示。</summary>
public class PlayerBuffIconUI : MonoBehaviour
{
    [SerializeField] private Image    iconImage;
    [SerializeField] private TMP_Text timerText;

    private static readonly Color PlaceholderColor = new Color(0.55f, 0.8f, 1f, 0.9f);

    private string _boundBuffId;
    public string BoundBuffId => _boundBuffId;

    public void Bind(string buffId, PlayerBuffRuntime buff)
    {
        _boundBuffId = buffId;
        if (iconImage != null)
        {
            if (buff.Icon != null)
            {
                iconImage.sprite = buff.Icon;
                iconImage.color  = Color.white;
            }
            else
            {
                iconImage.sprite = null;
                iconImage.color  = PlaceholderColor; // icon なし時のプレースホルダ色
            }
        }
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
