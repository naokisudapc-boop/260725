using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;

public class PlayerSpawnerNPC : MonoBehaviour
{
    // すべてのスポーン処理で共有する通し番号。cloneCount を「シーン内の
    // PlayerSpawnerNPC の数」から計算すると、NPCを何体スポーンしても
    // この数自体は変わらないため同じ名前のクローンが量産されてしまっていた。
    // ここではスポーンが起きるたびに必ず1ずつ増える値を使い、名前の重複を防ぐ。
    private static int totalSpawnCount = 0;

    [Header("Spawn Settings")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject girlPrefab; // 女008_一般人 プレハブ（Playerを呼べる特別な女の子）
    [SerializeField] private RuntimeAnimatorController correctPlayerController; // 正しい Player 用 Animator Controller
    [Tooltip("新しいキャラクターがスポーンする位置。未設定の場合は自動的に鍛冶屋（aesthetic (1)）の位置、それも見つからなければ自身の位置が使われる")]
    [SerializeField] private Transform spawnPlace;

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

        // 出現位置がInspectorで未設定の場合のみ、自動的に "aesthetic (1)"（鍛冶屋）を探す。
        // 見つからない場合はフォールバックとしてこの女性NPC自身の位置を使用する。
        if (spawnPlace == null)
        {
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
        }

        if (playerPrefab == null)
        {
            PlayerSpawnerNPC[] allSpawners = Object.FindObjectsByType<PlayerSpawnerNPC>();
            string mySpeciesName = GetSpeciesName(gameObject.name);
            foreach (var spawner in allSpawners)
            {
                if (!spawner.gameObject.name.Contains("(Clone)")
                    && spawner.playerPrefab != null
                    && GetSpeciesName(spawner.gameObject.name) == mySpeciesName)
                {
                    this.playerPrefab = spawner.playerPrefab;
                    break;
                }
            }
        }

        if (girlPrefab == null)
        {
            PlayerSpawnerNPC[] allSpawners = Object.FindObjectsByType<PlayerSpawnerNPC>();
            string mySpeciesName = GetSpeciesName(gameObject.name);
            foreach (var spawner in allSpawners)
            {
                if (!spawner.gameObject.name.Contains("(Clone)")
                    && spawner.girlPrefab != null
                    && GetSpeciesName(spawner.gameObject.name) == mySpeciesName)
                {
                    this.girlPrefab = spawner.girlPrefab;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// GameObject名から「種族名（プレハブの素の名前）」を取り出す。
    /// 例："Girl_Clone_12_女008_一般人" → "女008_一般人"、"sample033_1" → "sample033_1"。
    /// これにより、女性NPCの種類ごとに異なる Player Prefab / Girl Prefab の組み合わせを
    /// 設定していても、Start() のフォールバック検索で別種族の設定を誤って
    /// 借りてしまわないようにする（例：sample033_1専用のBowManNPC/sample033_1ペアが、
    /// 女008_一般人側のPlayer/女008ペアに混ざってしまうのを防ぐ）。
    /// </summary>
    private static string GetSpeciesName(string objectName)
    {
        string n = objectName.Replace("(Clone)", "").Trim();
        // "Girl_Clone_12_女008_一般人" や "PlayerHelper_Clone_3_NPC_Player_Helper" のような
        // "何か_Clone_数字_元の名前" という命名規則から、末尾の元の名前部分だけを取り出す
        int cloneIdx = n.IndexOf("_Clone_");
        if (cloneIdx >= 0)
        {
            string afterClone = n.Substring(cloneIdx + "_Clone_".Length);
            int nextUnderscore = afterClone.IndexOf('_');
            if (nextUnderscore >= 0 && nextUnderscore + 1 < afterClone.Length)
            {
                return afterClone.Substring(nextUnderscore + 1);
            }
        }
        return n;
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
            if (distance <= interactRange && IsClosestFemaleSpawnerToPlayer(currentPlayerObj.transform, distance))
            {
                TrySpawnNewPlayer();
            }
        }
    }

    /// <summary>
    /// 射程内にいる女性NPC（PlayerSpawnerNPC）の中で、自分がプレイヤーに
    /// 最も近い1体かどうかを判定する。これにより、Eキー1回の入力で
    /// 近くの女性NPC全員が同時に反応してしまうのを防ぎ、最も近い1体だけに限定する。
    /// </summary>
    private bool IsClosestFemaleSpawnerToPlayer(Transform playerTransform, float myDistance)
    {
        PlayerSpawnerNPC[] allSpawners = Object.FindObjectsByType<PlayerSpawnerNPC>();
        foreach (var spawner in allSpawners)
        {
            if (spawner == this) continue;
            if (spawner.currentNPCData == null || spawner.currentNPCData.gender != Gender.Female) continue;
            if (spawner.isVisualPlaying) continue; // 演出中の子は候補から除外

            float otherDistance = Vector2.Distance(spawner.transform.position, playerTransform.position);
            if (otherDistance > spawner.interactRange) continue; // 射程外なら競合しない

            if (otherDistance < myDistance) return false; // 自分より近い子がいる

            // 距離が同じ場合は InstanceID で一意にタイブレークする
            if (Mathf.Approximately(otherDistance, myDistance) && spawner.GetInstanceID() < GetInstanceID())
            {
                return false;
            }
        }
        return true;
    }

    private void TrySpawnNewPlayer()
    {
        if (GameManager.Instance == null) return;

        // このスポーンで何を生むかを事前に決定（50% で Player / 50% で女の子）
        bool spawnAsPlayer = Random.value < 0.5f;

        // Player枠に入っているプレハブが BowManNPC かどうかで必要資源を切り替える。
        // 通常の Player 枠（NPCPlayerHelper 等）は Food + Wood + Iron が必要だが、
        // BowManNPC のスポーンは Food 1 + Wood 1 のみで良い（鉄は不要）。
        bool isBowManSpawn = spawnAsPlayer && playerPrefab != null && playerPrefab.GetComponent<BowManNPC>() != null;

        bool resourcesConsumed = isBowManSpawn
            ? GameManager.Instance.ConsumeResourcesForBowManSpawn()
            : GameManager.Instance.ConsumeResourcesForSpawn(spawnAsPlayer);

        if (resourcesConsumed)
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

            totalSpawnCount++;
            int cloneCount = totalSpawnCount;

            // NPC 仕様の Player ヘルパーは、味方（Ally）として扱う
            newPlayer.tag = "Ally";

            // NPCPlayerHelper（操作キャラクター交代の対象になる後継NPC）の場合のみ、
            // Player専用のセットアップ（Animator Controllerの強制上書き等）を行う。
            // BowManNPC等、それ以外の味方プレハブがこの枠に入っている場合は
            // 専用のAnimator Controllerを壊さないよう、このセットアップをスキップする。
            NPCPlayerHelper helperComponent = newPlayer.GetComponent<NPCPlayerHelper>();
            if (helperComponent != null)
            {
                newPlayer.name = $"PlayerHelper_Clone_{cloneCount}_{newPlayer.name.Replace("(Clone)", "")}";

                // 正しい NPC 用 Animator Controller を強制的に上書き代入。
                // Unity の自動生成や他処理による上書きを力技でねじ伏せる。
                ForceAssignPlayerController(newPlayer);
            }
            else
            {
                newPlayer.name = $"Ally_Clone_{cloneCount}_{newPlayer.name.Replace("(Clone)", "")}";
            }

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
                // アタッチされてはいてもデフォルトで無効化されていることがあるため、
                // 明示的に有効化する（死亡判定・操作キャラクター交代の対象にするため必須）
                newHealth.enabled = true;
            }

            // 新しい味方がスポーンしたので人口カウンターを更新
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RecountPopulation();
            }

            Debug.Log($"[PlayerSpawnerNPC] 味方をスポーンしました: {newPlayer.name}");
        }
        else
        {
            if (girlPrefab == null) return;

            GameObject newGirl = Instantiate(girlPrefab, spawnPlace.position, Quaternion.identity);

            totalSpawnCount++;
            int cloneCount = totalSpawnCount;
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