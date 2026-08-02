using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;

public class NPCSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject maleNpcPrefab;
    public GameObject femaleNpcPrefab;
    private Transform blacksmithPlace; 
    
    [Header("Interaction Settings")]
    [SerializeField] private float interactRange = 3.0f;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Spawn Visual Settings")]
    [SerializeField] private Sprite backSprite; // 失敗時に一瞬表示する後ろ向きスプライト

    private Transform playerTransform;
    private FarmingNPC currentNPCData;
    private SpriteRenderer spriteRenderer;
    private Animator animator; // ★追加：Animatorとの競合を防ぐため取得
    private bool isVisualPlaying = false; // 演出中に連続入力を防ぐフラグ

    void Start()
    {
        currentNPCData = GetComponent<FarmingNPC>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>(); // ★追加

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) playerTransform = playerObj.transform;

        GameObject blacksmithObj = GameObject.Find("aesthetic");
        if (blacksmithObj != null) blacksmithPlace = blacksmithObj.transform;

        if (maleNpcPrefab == null || femaleNpcPrefab == null)
        {
            NPCSpawner[] allSpawners = Object.FindObjectsByType<NPCSpawner>();
            foreach (var spawner in allSpawners)
            {
                if (!spawner.gameObject.name.Contains("(Clone)") && spawner.maleNpcPrefab != null && spawner.femaleNpcPrefab != null)
                {
                    this.maleNpcPrefab = spawner.maleNpcPrefab;
                    this.femaleNpcPrefab = spawner.femaleNpcPrefab;
                    break;
                }
            }
        }
    }

    void Update()
    {
        if (currentNPCData == null || currentNPCData.gender != Gender.Female) return;
        if (isVisualPlaying) return; 

        GameObject currentPlayerObj = GameObject.FindWithTag("Player");
        if (currentPlayerObj == null) return;

        float distance = Vector2.Distance(transform.position, currentPlayerObj.transform.position);

        if (Input.GetKeyDown(interactKey))
        {
            if (distance <= interactRange)
            {
                TrySpawnNewNPC();
            }
        }
    }

    private void TrySpawnNewNPC()
    {
        if (GameManager.Instance == null) return;

        bool hasEnoughWood = GameManager.Instance.wood >= 1;
        bool hasEnoughIron = GameManager.Instance.iron >= 1;
        bool hasEnoughFood = GameManager.Instance.food >= 1;

        if (!hasEnoughFood || !hasEnoughWood || !hasEnoughIron)
        {
            Debug.LogWarning($"[NPCSpawner] スポーンに必要な資源が不足しています！");
            StartCoroutine(DoFailureVisual());
            return; 
        }

        Gender chosenGender = (Random.Range(0, 2) == 0) ? Gender.Male : Gender.Female;
        float checkRate = currentNPCData.femaleSpawnRateAttribute > 0 ? currentNPCData.femaleSpawnRateAttribute : 100f;
        float randomValue = Random.Range(0f, 100f);

        if (randomValue <= checkRate)
        {
            if (GameManager.Instance.CheckAndConsumeResourcesForNPC(chosenGender))
            {
                StartCoroutine(DoSuccessVisual(chosenGender));
            }
            else
            {
                StartCoroutine(DoFailureVisual());
            }
        }
        else
        {
            StartCoroutine(DoFailureVisual());
        }
    }

    // ★成功時：左右にぶるぶる震えてから、ぴょんと跳ねる演出
    private IEnumerator DoSuccessVisual(Gender chosenGender)
    {
        isVisualPlaying = true;
        Vector3 originalPos = transform.position;

        // 1. 左右にぶるぶる震える（0.3秒間）
        float shakeDuration = 0.3f;
        float shakeTimer = 0f;
        float shakeAmount = 0.08f; 
        while (shakeTimer < shakeDuration)
        {
            float offsetX = Random.Range(-shakeAmount, shakeAmount);
            transform.position = originalPos + new Vector3(offsetX, 0f, 0f);
            shakeTimer += Time.deltaTime;
            yield return null;
        }
        transform.position = originalPos; 

        yield return new WaitForSeconds(0.05f);

        // 2. ぴょんと上下に飛び跳ねる（放物線運動）
        float jumpDuration = 0.4f;
        float jumpTimer = 0f;
        float jumpHeight = 0.6f; 
        while (jumpTimer < jumpDuration)
        {
            float normalizedTime = jumpTimer / jumpDuration;
            float height = Mathf.Sin(normalizedTime * Mathf.PI) * jumpHeight;
            transform.position = originalPos + new Vector3(0f, height, 0f);
            
            jumpTimer += Time.deltaTime;
            yield return null;
        }
        transform.position = originalPos; 

        // 3. 実際のスポーンを実行
        SpawnNPC(chosenGender);
        isVisualPlaying = false;
    }

    // ★失敗時：後ろを向くスプライトを一瞬表示する演出（Animator上書き対策版）
    private IEnumerator DoFailureVisual()
    {
        if (spriteRenderer == null || backSprite == null)
        {
            Debug.LogWarning("[NPCSpawner] SpriteRenderer または BackSprite が未設定のため失敗演出をスキップします。");
            yield break;
        }

        isVisualPlaying = true;
        Sprite originalSprite = spriteRenderer.sprite;

        // ★Animatorが動いている場合、スプライトの強制書き換えを維持するために一時停止する
        if (animator != null) animator.enabled = false;

        // 後ろ向きスプライトに差し替え
        spriteRenderer.sprite = backSprite;

        // 0.5秒間ガッカリして後ろを向く
        yield return new WaitForSeconds(0.5f);

        // 元のスプライトに戻して、Animatorを再開
        spriteRenderer.sprite = originalSprite;
        if (animator != null) animator.enabled = true;

        isVisualPlaying = false;
    }

    private void SpawnNPC(Gender chosenGender)
    {
        if (maleNpcPrefab == null || femaleNpcPrefab == null || blacksmithPlace == null) return;

        GameObject prefabToSpawn = (chosenGender == Gender.Male) ? maleNpcPrefab : femaleNpcPrefab;
        GameObject newNPC = Instantiate(prefabToSpawn, blacksmithPlace.position, Quaternion.identity);
        
        int cloneCount = Object.FindObjectsByType<NPCSpawner>().Length;
        newNPC.name = $"{chosenGender}_Clone_{cloneCount}_{newNPC.name.Replace("(Clone)", "")}";

        NPCSpawner newNPCSpawner = newNPC.GetComponent<NPCSpawner>();
        if (newNPCSpawner != null)
        {
            newNPCSpawner.maleNpcPrefab = this.maleNpcPrefab;
            newNPCSpawner.femaleNpcPrefab = this.femaleNpcPrefab;
            newNPCSpawner.backSprite = this.backSprite; 
        }

        FarmingNPC newNPCScript = newNPC.GetComponent<FarmingNPC>();
        if (newNPCScript != null)
        {
            newNPCScript.gender = chosenGender;
            // 女性NPCの場合は明示的に Female を確定（誤って Male になるのを防止）
            if (chosenGender == Gender.Female)
            {
                newNPCScript.gender = Gender.Female;
                // 誕生時に 1〜99% のランダムなスポーンレートを割り当て
                int randomSpawnRate = Random.Range(1, 100);
                newNPCScript.SetSpawnRate(randomSpawnRate);
            }
        }

        // New ally spawned: refresh the population counter
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RecountPopulation();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}