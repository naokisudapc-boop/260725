using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ResourceUIManager : MonoBehaviour
{
    public static ResourceUIManager Instance;

    public TextMeshProUGUI ironCountText; 

    private int ironCount = 0;

    // Public read‑only accessor for debugging / external checks
    public int IronCount => ironCount;

    // Allow external scripts to modify the iron count (positive or negative)
    public void AddIron(int amount)
    {
        ironCount += amount;
        UpdateUI();
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ★このメソッドの中身を書き換えました
    private void UpdateUI()
    {
        if (ironCountText != null)
        {
            // 文字列補間を使って「Iron : 数値」の形にします
            ironCountText.text = $"Iron : {ironCount}";
        }
    }
}