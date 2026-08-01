using UnityEngine;
using UnityEngine.Tilemaps;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // Public read-only accessors for debugging resource values
    public int wood => currentWoodCount;
    // Iron is tracked by ResourceUIManager; expose its value for debugging
    public int iron => ResourceUIManager.Instance != null ? ResourceUIManager.Instance.IronCount : 0;
    public int food => currentFoodCount;

    [Header("Wood Settings")]
    [SerializeField] private TextMeshProUGUI woodText;
    private int currentWoodCount = 0;

    [Header("Iron Settings")]
    [SerializeField] private TextMeshProUGUI ironText;
    private int currentIronCount = 0;

    [Header("Food Settings")]
    [SerializeField] private Tilemap farmTilemap;
    [SerializeField] private int foodMultiplier = 2;
    [SerializeField] private TextMeshProUGUI foodText;
    private int currentFoodCount = 0;
    // 死亡還元など、畑カウントとは別に加算される Food ボーナス。
    // 畑の水やり状態と同期する際のベース値に加算される。
    private int foodBonus = 0;

    [Header("Population Settings")]
    [SerializeField] private TextMeshProUGUI populationText;
    [SerializeField] private string[] allyTags = { "Player", "Ally" };
    private int currentPopulation = 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        UpdateWoodUI();
        UpdateIronUI();
        UpdateFoodCount();

        // Delay the initial population count by one frame so that all ally
        // NPCs (Ally) have finished their spawn/initialization before counting.
        StartCoroutine(DelayedInitialCount());
    }

    private System.Collections.IEnumerator DelayedInitialCount()
    {
        // Wait a short moment so that all ally NPCs (Ally) have finished their
        // Awake/Start initialization and tag assignment before we count them.
        yield return new WaitForSeconds(0.1f);
        RecountPopulation();
    }

    // --- Population Logic ---
    /// <summary>
    /// Recalculates the current population by counting all alive ally characters
    /// (GameObjects with an ally tag that have a CharacterHealth component not dead).
    /// Call this whenever an ally spawns or dies.
    /// </summary>
    public void RecountPopulation()
    {
        int count = 0;
        CharacterHealth[] allCharacters = Object.FindObjectsByType<CharacterHealth>(FindObjectsSortMode.None);

        foreach (CharacterHealth ch in allCharacters)
        {
            if (ch.isDead) continue;

            // Check if this character carries an ally tag
            foreach (string tag in allyTags)
            {
                if (ch.gameObject.CompareTag(tag))
                {
                    count++;
                    break;
                }
            }
        }

        currentPopulation = count;
        UpdatePopulationUI();
    }

    private void UpdatePopulationUI()
    {
        if (populationText != null) populationText.text = "Population: " + currentPopulation;
    }

    // --- Wood Logic ---
    public void AddTreeCount(int amount = 1)
    {
        currentWoodCount += amount;
        UpdateWoodUI();
    }

    private void UpdateWoodUI()
    {
        if (woodText != null) woodText.text = "Wood: " + currentWoodCount;
    }

    // --- Iron Logic ---
    public void AddIronCount(int amount = 1)
    {
        currentIronCount += amount;
        UpdateIronUI();
    }

    private void UpdateIronUI()
    {
        if (ironText != null) ironText.text = "Iron: " + currentIronCount;
    }

    // --- Food Logic ---
    public void OnFieldUpdated()
    {
        UpdateFoodCount();
    }

    /// <summary>
    /// Adds food to the current stock (e.g. when an ally character dies and is
    /// converted into a food resource). The value is stored as a bonus and then
    /// re-synced together with the watered-farm count so the Food total always
    /// equals "watered farms × multiplier + bonus" (no infinite growth).
    /// </summary>
    public void AddFood(int amount = 1)
    {
        foodBonus += amount;
        UpdateFoodCount();
    }

    /// <summary>
    /// Re-syncs the Food value to the actual number of watered farm tiles in the
    /// scene (× multiplier) plus any accumulated bonus. This is the single source
    /// of truth for the Food total, so calling it on every farm-state change keeps
    /// the UI consistent and prevents Food from growing when an NPC simply moves
    /// between fields.
    /// </summary>
    public void UpdateFoodByWateredFarms()
    {
        UpdateFoodCount();
    }

    private void UpdateFoodCount()
    {
        if (farmTilemap == null) return;

        int wateredCount = 0;
        BoundsInt bounds = farmTilemap.cellBounds;

        foreach (var pos in bounds.allPositionsWithin)
        {
            TileBase tile = farmTilemap.GetTile(pos);
            if (tile is FarmTileData farmTile)
            {
                if (farmTile.status == FarmTileData.TileStatus.Watered)
                {
                    wateredCount++;
                }
            }
        }

        // Food = (watered farms × multiplier) + bonus. Assigning (not adding)
        // guarantees the total always matches the scene state.
        currentFoodCount = wateredCount * foodMultiplier + foodBonus;
        if (foodText != null) foodText.text = "Food: " + currentFoodCount;
    }

    // --- NPC Spawn Resource Check ---
    // Updated to support gender‑based resource costs
    public bool CheckAndConsumeResourcesForNPC(Gender gender)
    {
        // Use the iron count from ResourceUIManager to stay in sync with UI
        int ironCount = ResourceUIManager.Instance != null ? ResourceUIManager.Instance.IronCount : 0;

        if (gender == Gender.Female)
        {
            // Female NPCs only require Food
            if (currentFoodCount >= 1)
            {
                currentFoodCount -= 1;
                if (foodText != null) foodText.text = "Food: " + currentFoodCount;
                return true;
            }
            return false;
        }
        else // Male or default
        {
            if (currentWoodCount >= 1 && ironCount >= 1 && currentFoodCount >= 1)
            {
                currentWoodCount -= 1;
                // Deduct iron via ResourceUIManager to keep UI consistent
                if (ResourceUIManager.Instance != null)
                {
                    ResourceUIManager.Instance.AddIron(-1);
                }

                // Food is calculated from watered tiles; we simply allow the spawn.
                currentFoodCount -= 1;

                UpdateWoodUI();
                // No need to call UpdateIronUI() because ResourceUIManager handles its own UI update
                if (foodText != null) foodText.text = "Food: " + currentFoodCount;

                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Consumes the resources required to spawn a character.
    /// Common rule: Food is ALWAYS consumed by 1, regardless of what spawns.
    /// Wood and Iron are consumed ONLY when spawning a Player (isPlayer == true);
    /// a female NPC (girl) spawns for free (Wood/Iron untouched).
    /// Returns true if the spawn is allowed (enough Food), false otherwise.
    /// </summary>
    public bool ConsumeResourcesForSpawn(bool isPlayer)
    {
        // Food is a mandatory common cost for every spawn.
        if (currentFoodCount < 1)
        {
            return false;
        }

        currentFoodCount -= 1;
        if (foodText != null) foodText.text = "Food: " + currentFoodCount;

        // Wood and Iron are consumed only when a Player is spawned.
        if (isPlayer)
        {
            int ironCount = ResourceUIManager.Instance != null ? ResourceUIManager.Instance.IronCount : 0;

            if (currentWoodCount >= 1 && ironCount >= 1)
            {
                currentWoodCount -= 1;
                if (ResourceUIManager.Instance != null)
                {
                    ResourceUIManager.Instance.AddIron(-1);
                }
                UpdateWoodUI();
            }
            else
            {
                // Not enough Wood/Iron for a Player spawn: refund Food and deny.
                currentFoodCount += 1;
                if (foodText != null) foodText.text = "Food: " + currentFoodCount;
                return false;
            }
        }

        return true;
    }

    public void ReplacePlayer(CharacterHealth deadPlayer)
    {
        CharacterHealth[] allCharacters = Object.FindObjectsByType<CharacterHealth>(FindObjectsSortMode.None);
        CharacterHealth nextPlayer = null;

        foreach (CharacterHealth ch in allCharacters)
        {
            if (ch != deadPlayer && !ch.isDead)
            {
                // 次の操作キャラクター候補を決定
                nextPlayer = ch;
                break;
            }
        }

        SelectNextPlayer(nextPlayer);
    }

    /// <summary>
    /// NPCPlayerHelper 等、CharacterHealth を持たない後継キャラクターが死亡した際の
    /// 操作権限移譲用オーバーロード。死亡キャラクター（GameObject）を除外して
    /// 次の生存 CharacterHealth を新しいプレイヤーとして選出する。
    /// </summary>
    public void ReplacePlayer(GameObject deadPlayerObj)
    {
        CharacterHealth[] allCharacters = Object.FindObjectsByType<CharacterHealth>(FindObjectsSortMode.None);
        CharacterHealth nextPlayer = null;

        foreach (CharacterHealth ch in allCharacters)
        {
            if (ch.gameObject != deadPlayerObj && !ch.isDead)
            {
                // 次の操作キャラクター候補を決定
                nextPlayer = ch;
                break;
            }
        }

        SelectNextPlayer(nextPlayer);
    }

    /// <summary>
    /// 次の操作キャラクターを決定し、操作権限を付与する共通処理。
    /// </summary>
    private void SelectNextPlayer(CharacterHealth nextPlayer)
    {
        // ▼▼▼ 追加：直前まで操作していた既存のプレイヤーたちのフラグ・タグをリセットする ▼▼▼
        CharacterHealth[] allCharacters = Object.FindObjectsByType<CharacterHealth>(FindObjectsSortMode.None);
        foreach (CharacterHealth ch in allCharacters)
        {
            if (ch.isPlayer)
            {
                ch.isPlayer = false;
                ch.isControllable = false;
                
                // タグが "Player" だったものを "Ally" などにリセット
                if (ch.gameObject.CompareTag("Player"))
                {
                    ch.gameObject.tag = "Ally";
                }
            }
        }
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

        if (nextPlayer != null)
        {
            // プレイヤーフラグと操作権限を付与
            nextPlayer.isPlayer = true;
            nextPlayer.isControllable = true;
            // タグをPlayerに変更し、カメラやシステムが自動検知できるようにする
            nextPlayer.gameObject.tag = "Player";

            // Stop any automatic NPC movement logic (e.g. ThiefNPC) so it does
            // not keep overwriting the Rigidbody velocity while the player is
            // controlling this character.
            MonoBehaviour[] scripts = nextPlayer.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour script in scripts)
            {
                // Keep GameManager, the player control script, the attack script
                // and CharacterHealth enabled; disable everything else that could
                // drive movement or auto-combat.
                if (script != this &&
                    script.GetType() != typeof(NewMonoBehaviourScript) &&
                    script.GetType() != typeof(PlayerAttack) &&
                    script.GetType() != typeof(CharacterHealth))
                {
                    script.enabled = false;
                }
            }

            // Ensure the new character has the manual movement script so the
            // player can walk with arrow keys / WASD. Attach it if missing,
            // otherwise just enable it, then (re)initialize its references.
            NewMonoBehaviourScript playerMove = nextPlayer.GetComponent<NewMonoBehaviourScript>();
            if (playerMove == null)
            {
                playerMove = nextPlayer.gameObject.AddComponent<NewMonoBehaviourScript>();
            }
            else
            {
                playerMove.enabled = true;
            }

            // Inherit the gender from the NPC's FarmingNPC data so the animator
            // and name generation stay consistent.
            FarmingNPC npcData = nextPlayer.GetComponent<FarmingNPC>();
            if (npcData != null)
            {
                playerMove.gender = npcData.gender;
            }
            playerMove.Init();

            // Configure the Rigidbody so collisions don't cause spinning or
            // uncontrolled sliding while under player control.
            Rigidbody2D rb = nextPlayer.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                rb.linearVelocity = Vector2.zero;
            }

            // Ensure the new character can perform a manual attack (Space key).
            // Attach PlayerAttack if missing, then point it at this character's
            // weapon (hammer) so the attack animation and hit detection work.
            PlayerAttack playerAttack = nextPlayer.GetComponent<PlayerAttack>();
            if (playerAttack == null)
            {
                playerAttack = nextPlayer.gameObject.AddComponent<PlayerAttack>();
            }
            else
            {
                playerAttack.enabled = true;
            }

            // Resolve the weapon transform from the ThiefNPC's hammer reference.
            Hammer hammer = nextPlayer.GetComponentInChildren<Hammer>();
            if (hammer != null)
            {
                playerAttack.SetAttackPoint(hammer.transform);
            }
            else
            {
                Debug.LogWarning("【攻撃設定】新しいプレイヤーに Hammer が見つかりませんでした。攻撃が正しく動作しない可能性があります。");
            }

            Debug.Log($"【プレイヤー切り替え成功】新しいプレイヤーは {nextPlayer.gameObject.name} です。");
        }
        else
        {
            Debug.LogWarning("【ゲームオーバー】次に操作できる生存NPCが残っていません！");
        }
    }
}