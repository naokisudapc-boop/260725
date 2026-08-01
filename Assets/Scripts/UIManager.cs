using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    private Camera mainCamera;
    private Canvas screenCanvas;
    
    // キャラクターとテキストのペアを管理するリスト
    private List<(Transform target, RectTransform labelRt, Vector3 offset)> trackedLabels = new List<(Transform, RectTransform, Vector3)>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        mainCamera = Camera.main;
        CreateScreenCanvas();
    }

    // 画面の最前面に「絶対に地面に隠れないCanvas」を1つだけ作る
    private void CreateScreenCanvas()
    {
        GameObject canvasObj = new GameObject("UIScreenCanvas");
        canvasObj.transform.SetParent(this.transform);

        screenCanvas = canvasObj.AddComponent<Canvas>();
        screenCanvas.renderMode = RenderMode.ScreenSpaceOverlay; // 👈 これが絶対に最前面に出る魔法
        screenCanvas.sortingOrder = 9999; // 圧倒的最前面

        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
    }

    /// <summary>
    /// 最前面Canvasの中に名前テキストを生成し、追跡リストに登録します。
    /// </summary>
    public TMP_Text CreateNameLabel(Transform characterTransform, string name, Vector3 offset = default)
    {
        if (offset == default)
        {
            offset = new Vector3(0, 1.5f, 0); // キャラクターの頭上のオフセット
        }

        if (mainCamera == null) mainCamera = Camera.main;

        // 画面最前面のCanvasの中にテキスト用オブジェクトを作る
        GameObject textObj = new GameObject("NameLabel_Screen");
        textObj.transform.SetParent(screenCanvas.transform, false);

        // UI専用のテキストコンポーネントをアタッチ
        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = name;
        text.fontSize = 24f; // 画面空間用なので大きめのフォントサイズにします
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;

        RectTransform rt = textObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200, 50);

        // 毎フレーム位置を同期するためにリストに登録
        trackedLabels.Add((characterTransform, rt, offset));

        return text;
    }

    private void LateUpdate()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        // リストに登録されたすべての名前の位置を、キャラクターの頭上に同期する
        for (int i = trackedLabels.Count - 1; i >= 0; i--)
        {
            var item = trackedLabels[i];

            // キャラクターが削除されていたらリストから消す（エラー対策）
            if (item.target == null || item.labelRt == null)
            {
                trackedLabels.RemoveAt(i);
                continue;
            }

            // キャラクターの「ゲーム世界での3D座標（頭上）」を計算
            Vector3 worldPos = item.target.position + item.offset;
            
            // それを「画面上の2D座標」に変換して、UIの位置に代入する
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);
            
            // カメラの裏側にいる場合は非表示にする
            if (screenPos.z < 0)
            {
                item.labelRt.gameObject.SetActive(false);
            }
            else
            {
                item.labelRt.gameObject.SetActive(true);
                item.labelRt.position = screenPos;
            }
        }
    }

    public void UpdateNameLabel(TMP_Text label, string newName)
    {
        if (label != null) label.text = newName;
    }
}