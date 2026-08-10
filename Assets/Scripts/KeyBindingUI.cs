using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 操作キー一覧を画面に表示するUI。
/// 操作キャラクター交代のKキーは意図的に一覧から除外している。
/// 既存のCanvasがシーンにあればそこに追加し、無ければ自前でCanvasを作成する。
/// </summary>
public class KeyBindingUI : MonoBehaviour
{
    [Header("Toggle Settings")]
    [Tooltip("キー一覧の表示/非表示を切り替えるキー")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
    [SerializeField] private bool visibleByDefault = true;

    private GameObject panelObj;

    void Start()
    {
        BuildUI();
        SetVisible(visibleByDefault);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey) && panelObj != null)
        {
            SetVisible(!panelObj.activeSelf);
        }
    }

    private void SetVisible(bool visible)
    {
        if (panelObj != null) panelObj.SetActive(visible);
    }

    private void BuildUI()
    {
        // 既存のCanvasがあればそれを使う。無ければ自前で作成する。
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("KeyBindingCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        panelObj = new GameObject("KeyBindingPanel");
        panelObj.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1, 1);
        panelRect.anchorMax = new Vector2(1, 1);
        panelRect.pivot = new Vector2(1, 1);
        panelRect.anchoredPosition = new Vector2(-12, -12);
        panelRect.sizeDelta = new Vector2(280, 280);

        Image background = panelObj.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.55f);

        GameObject textObj = new GameObject("KeyBindingText");
        textObj.transform.SetParent(panelObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12, 12);
        textRect.offsetMax = new Vector2(-12, -12);

        Text listText = textObj.AddComponent<Text>();
        listText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        listText.fontSize = 15;
        listText.color = Color.white;
        listText.alignment = TextAnchor.UpperLeft;
        listText.horizontalOverflow = HorizontalWrapMode.Wrap;
        listText.verticalOverflow = VerticalWrapMode.Overflow;
        listText.text = BuildKeyBindingText();
    }

    /// <summary>
    /// 操作キー一覧の文字列を組み立てる。
    /// 操作キャラクター交代のKキーはここには含めない。
    /// </summary>
    private string BuildKeyBindingText()
    {
        return
            "【操作キー一覧】\n" +
            "移動：W / A / S / D（または矢印キー）\n" +
            "攻撃（斧）：Space\n" +
            "耕作：F\n" +
            "インタラクト：E\n" +
            "攻撃指示：Q\n" +
            "指示キャンセル：X\n" +
            "採掘一斉指示：M\n" +
            "左クリック(ドラッグ)：弓の照準・発射\n" +
            "右クリック：水やり指示 / 木の手動指定\n" +
            "\n" +
            $"（このパネルの表示切替：{toggleKey}）";
    }
}