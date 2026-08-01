using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;

public class PlayerSpawnerNPC : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject girlPrefab; // 女008_一般人 プレハブ（Playerを呼べる特別な女の子）
    [SerializeField] private RuntimeAnimatorController correctPlayerController; // 正しい Player 用 Animator Controller
    private Transform spawnPlace;

    [Header("Interaction Settings")]
    [SerializeField] private float interactRange = 3.0f;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Spawn Visual Settings")]
    [SerializeField] private Sprite backSprite; // 失敗時に一瞬表示する後ろ向きスプライト

    private Transform playerTransform;
    private FarmingNPC currentNPCData;
    private SpriteRenderer spriteRenderer;
    private Animator animator; // Animatorとの競合を防ぐため取得
    private bool isVisualPlaying = false; // 演出中に連続入力を防ぐフラグ
    private bool pendingSpawnAsPlayer = false; // 演出後にスポーンする種別を保持

    void Start()
    {
        currentNPCData = GetComponent<FarmingNPC>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) playerTransform = playerObj.transform;

        // 出現位置を "aesthetic (1)"（鍛冶屋）に設定。見つからない場合は
        // フォールバックとしてこの女性NPC自身の位置を使用する。
        GameObject blacksmithObj = GameObject.Find("aesthetic (1)");
        if (blacksmithObj != null)
        {
            spawnPlace = blacksmithObj.transform;
        }
        else
        {
            Debug.LogWarning("[PlayerSpawnerNPC] 'aesthetic (1)' が見つからないため、自身の位置をスポーン地点にします。");
            spawnPlace = transform;
        }

        if (playerPrefab == null)
        {
            PlayerSpawnerNPC[] allSpawners = Object.FindObjectsByType<PlayerSpawnerNPC>(FindObjectsSortMode.None);
            foreach (var spawner in allSpawners)
            {
                if (!spawner.gameObject.name.Contains("(Clone)") && spawner.playerPrefab != null)
                {
                    this.playerPrefab = spawner.playerPrefab;
                    break;
                }
            }
        }

        if (girlPrefab == null)
        {
            PlayerSpawnerNPC[] allSpawners = Object.FindObjectsByType<PlayerSpawnerNPC>(FindObjectsSortMode.None);
            foreach (var spawner in allSpawners)
            {
                if (!spawner.gameObject.name.Contains("(Clone)") && spawner.girlPrefab != null)
                {
                    this.girlPrefab = spawner.girlPrefab;
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
                TrySpawnNewPlayer();
            }
        }
    }

    private void TrySpawnNewPlayer()
    {
        if (GameManager.Instance == null) return;

        // このスポーンで何を生むかを事前に決定（50% で Player / 50% で女の子）
        bool spawnAsPlayer = Random.value < 0.5f;

        // 共通コスト：Food は必ず -1。Wood/Iron は Player の場合のみ消費。
        if (GameManager.Instance.ConsumeResourcesForSpawn(spawnAsPlayer))
        {
            // 演出後に実際に何をスポーンするかを渡すため、フラグを保持する
            pendingSpawnAsPlayer = spawnAsPlayer;
            StartCoroutine(DoSuccessVisual());
        }
        else
        {
            Debug.LogWarning($"[PlayerSpawnerNPC] スポーンに必要な資源が不足しています！");
            StartCoroutine(DoFailureVisual());
        }
    }

    // 成功時：左右にぶるぶる震えてから、ぴょんと跳ねる演出（NPCSpawner 準拠）
    private IEnumerator DoSuccessVisual()
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

        // 3. 実際のスポーンを実行（50% で Player、50% で女の子自身）
        SpawnCharacter(pendingSpawnAsPlayer);
        isVisualPlaying = false;
    }

    // 失敗時：後ろを向くスプライトを一瞬表示する演出（NPCSpawner 準拠）
    private IEnumerator DoFailureVisual()
    {
        if (spriteRenderer == null || backSprite == null)
        {
            Debug.LogWarning("[PlayerSpawnerNPC] SpriteRenderer または BackSprite が未設定のため失敗演出をスキップします。");
            yield break;
        }

        isVisualPlaying = true;
        Sprite originalSprite = spriteRenderer.sprite;

        if (animator != null) animator.enabled = false;

        spriteRenderer.sprite = backSprite;

        yield return new WaitForSeconds(0.5f);

        spriteRenderer.sprite = originalSprite;
        if (animator != null) animator.enabled = true;

        isVisualPlaying = false;
    }

    private void SpawnCharacter(bool spawnAsPlayer)
    {
        if (spawnPlace == null) return;

        if (spawnAsPlayer)
        {
            if (playerPrefab == null) return;

            GameObject newPlayer = Instantiate(playerPrefab, spawnPlace.position, Quaternion.identity);

            int cloneCount = Object.FindObjectsByType<PlayerSpawnerNPC>(FindObjectsSortMode.None).Length;
            newPlayer.name = $"PlayerHelper_Clone_{cloneCount}_{newPlayer.name.Replace("(Clone)", "")}";

            // NPC 仕様の Player ヘルパーは、味方（Ally）として扱う
            newPlayer.tag = "Ally";

            // 正しい NPC 用 Animator Controller を強制的に上書き代入。
            // Unity の自動生成や他処理による上書きを力技でねじ伏せる。
            ForceAssignPlayerController(newPlayer);

            // 衝突時のZ軸回転を完全にロック
            Rigidbody2D playerRb = newPlayer.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                playerRb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }

            CharacterHealth newHealth = newPlayer.GetComponent<CharacterHealth>();
            if (newHealth != null)
            {
                newHealth.isPlayer = false;       // 操作は別キャラのまま
                newHealth.isControllable = false;
            }

            // 新しい味方がスポーンしたので人口カウンターを更新
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RecountPopulation();
            }

            Debug.Log($"[PlayerSpawnerNPC] プレイヤーをスポーンしました: {newPlayer.name}");
        }
        else
        {
            if (girlPrefab == null) return;

            GameObject newGirl = Instantiate(girlPrefab, spawnPlace.position, Quaternion.identity);

            int cloneCount = Object.FindObjectsByType<PlayerSpawnerNPC>(FindObjectsSortMode.None).Length;
            newGirl.name = $"Girl_Clone_{cloneCount}_{newGirl.name.Replace("(Clone)", "")}";

            // 女の子は Player を呼べる特別な味方（Ally）として扱う
            newGirl.tag = "Ally";

            // 女性NPCであることを明示的に確定（誤って Male になるのを防止）
            FarmingNPC girlFarming = newGirl.GetComponent<FarmingNPC>();
            if (girlFarming != null)
            {
                girlFarming.gender = Gender.Female;
                // 誕生時に 1〜99% のランダムなスポーンレートを割り当て
                int randomSpawnRate = Random.Range(1, 100);
                girlFarming.SetSpawnRate(randomSpawnRate);
            }

            CharacterHealth newHealth = newGirl.GetComponent<CharacterHealth>();
            if (newHealth != null)
            {
                newHealth.isPlayer = false;
                newHealth.isControllable = false;
            }

            // 新しい味方がスポーンしたので人口カウンターを更新
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RecountPopulation();
            }

            Debug.Log($"[PlayerSpawnerNPC] 女008_一般人（Playerを呼べる女の子）をスポーンしました: {newGirl.name}");
        }
    }

    /// <summary>
    /// 生成したキャラクターに、インスペクターで指定した正しい Player 用 Animator
    /// Controller を強制的に上書き代入します。Unity の自動生成や他スクリプトによる
    /// 上書きを無視して、確実に InputX / InputY 等のパラメータを持つコントローラーを
    /// 設定します。
    /// </summary>
    private void ForceAssignPlayerController(GameObject character)
    {
        Animator anim = character.GetComponentInChildren<Animator>();
        if (anim == null) return;

        if (correctPlayerController != null)
        {
            // 強制的に正しいコントローラーを上書き（上書き不可の形でねじ伏せる）
            anim.runtimeAnimatorController = correctPlayerController;
            Debug.Log($"[PlayerSpawnerNPC] Animator Controller を正しい Player 用に強制代入しました: {character.name}");
        }
        else
        {
            Debug.LogWarning("[PlayerSpawnerNPC] correctPlayerController が未設定のため、強制代入できませんでした。インスペクターで設定してください。");
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}