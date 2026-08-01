using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// NPC 仕様の Player ヘルパーキャラクター用の制御スクリプト。
/// 見た目は Player と同じですが、独立した NPC として動作し、
/// 他の NPC と同じアニメーターコントローラー（MoveX / MoveY / Blend）を使用します。
/// アニメーションパラメータは InputX/InputY ではなく MoveX/MoveY に送信します。
///
/// 追加仕様：誕生（Instantiate / Init）直後から、周囲の木を自動検索して
/// 自律的に伐採し続ける「全自動ヘルパーAI」を備えます。
/// Eキーやプレイヤーの接近判定には一切依存しません。
/// </summary>
public class NPCPlayerHelper : MonoBehaviour
{
    public Gender gender = Gender.Male;
    [SerializeField] private float _moveSpeed = 5;
    [SerializeField] private RuntimeAnimatorController correctPlayerController; // 正しい NPC 用コントローラー

    [Header("Auto Lumberjack AI Settings")]
    [SerializeField] private float _detectRange = 999f;   // 検索範囲（無限大に近い値で全木対象）
    [SerializeField] private float _reachDistance = 1.2f; // 伐採開始の至近距離
    [SerializeField] private float _chopDuration = 2.0f;  // 伐採演出（オノを構えた待機）時間
    [SerializeField] private string _treeTag = "Tree";    // 伐採対象のタグ
    [SerializeField] private string _treesRootName = "Trees"; // 木をまとめる親オブジェクト名

    private Rigidbody2D _rb;
    private Animator _anim;
    private Vector2 _moveInput;
    private TMP_Text _nameLabel;

    // プレイヤー死亡時に後継キャラクター（新しい操作対象）として選出されたかを示すフラグ
    [HideInInspector] public bool isSuccessor = false;

    // 死亡済みかを示すフラグ（おばけ接触時の多重死亡呼び出しを防ぐ）
    [HideInInspector] public bool isDead = false;

    // --- 伐採AIの内部状態 ---
    private enum LumberState { Idle, Moving, Chopping }
    private LumberState _lumberState = LumberState.Idle;
    private Transform _targetTree;
    private Transform _manualTarget; // 手動で指定された優先ターゲット
    private Coroutine _lumberRoutine;
    private float _fixedZ; // Z軸を固定するための基準値

    void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        // Animator が子オブジェクトにある場合も考慮し、InChildren で取得する
        _anim = GetComponentInChildren<Animator>();

        // 初期化の最終地点で、正しい NPC 用コントローラーを強制的に上書き代入。
        if (_anim != null && correctPlayerController != null)
        {
            _anim.runtimeAnimatorController = correctPlayerController;
        }

        if (_anim != null && HasParameter(_anim, "Gender"))
        {
            _anim.SetInteger("Gender", (int)gender);
        }
        // コントローラーを正しいものに差し替えた直後、MoveX/MoveY の初期値を反映
        if (_anim != null)
        {
            _anim.SetFloat("MoveX", 0f);
            _anim.SetFloat("MoveY", 0f);
            _anim.SetFloat("Blend", 0f);
        }

        // 衝突時のZ軸回転を完全にロック（プレハブ設定に加えコード側でも補強）
        if (_rb != null)
        {
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        // Z軸を固定（2Dトップビューで重なりを防ぐ）
        _fixedZ = transform.position.z;
        KeepZFixed();

        // 斧（ono）を装備状態にする（プレハブの子オブジェクトとして既に配置済み）
        Transform weponHolder = transform.Find("WeponHolder");
        if (weponHolder != null)
        {
            weponHolder.gameObject.SetActive(true);
            Transform ono = weponHolder.Find("ono");
            if (ono != null) ono.gameObject.SetActive(true);
        }

        Debug.Log($"[NPCPlayerHelper] Start: Animator {(_anim != null ? "取得OK" : "が見つかりません！")} (name={gameObject.name})");

        // Generate and display name
        string characterName = NameGenerator.GetRandomName(gender);
        if (UIManager.Instance != null)
        {
            Vector3 nameOffset = new Vector3(0f, 1.0f, -0.1f);
            _nameLabel = UIManager.Instance.CreateNameLabel(transform, characterName, nameOffset);
        }

        // ★誕生直後から自動で伐採モードを開始（指示待ちなし）
        StartLumberAI();

        // 男性の場合は操作切り替えシステム（プレイヤー死亡時の交代）が検知できるよう "Player" タグを設定
        if (gender == Gender.Male)
        {
            gameObject.tag = "Player";
        }
    }

    /// <summary>
    /// (Re)initializes references. Call this after the component is added to a
    /// character at runtime (e.g. when control is handed over to a new NPC) so
    /// it correctly picks up the new Rigidbody2D / Animator and can move.
    /// </summary>
    public void Init()
    {
        _rb = GetComponent<Rigidbody2D>();
        _anim = GetComponentInChildren<Animator>();

        // 初期化の最終地点で、正しい NPC 用コントローラーを強制的に上書き代入。
        if (_anim != null && correctPlayerController != null)
        {
            _anim.runtimeAnimatorController = correctPlayerController;
        }

        if (_anim != null && HasParameter(_anim, "Gender"))
        {
            _anim.SetInteger("Gender", (int)gender);
        }
        if (_anim != null)
        {
            _anim.SetFloat("MoveX", 0f);
            _anim.SetFloat("MoveY", 0f);
            _anim.SetFloat("Blend", 0f);
        }
        Debug.Log($"[NPCPlayerHelper] Init: Animator {(_anim != null ? "取得OK" : "が見つかりません！")} (name={gameObject.name})");

        if (_rb != null)
        {
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            _rb.linearVelocity = Vector2.zero;
        }

        // Z軸を固定
        _fixedZ = transform.position.z;
        KeepZFixed();

        // ★Init 完了直後も自動で伐採モードを開始
        StartLumberAI();

        // 男性の場合は操作切り替えシステム（プレイヤー死亡時の交代）が検知できるよう "Player" タグを設定
        if (gender == Gender.Male)
        {
            gameObject.tag = "Player";
        }
    }

    private bool HasParameter(Animator anim, string paramName)
    {
        if (anim == null) return false;
        foreach (AnimatorControllerParameter param in anim.parameters)
        {
            if (param.name == paramName) return true;
        }
        return false;
    }

    /// <summary>
    /// 手動で優先ターゲットとして設定する。
    /// </summary>
    /// <param name="target">対象の木 Transform</param>
    public void SetManualTarget(Transform target)
    {
        _manualTarget = target;
        Debug.Log($"[NPCPlayerHelper] Manual target set to {target?.name}");
    }

    // ============================================================
    //  全自動伐採AI
    // ============================================================

    /// <summary>
    /// 伐採AIを起動する。すでに動作中なら再起動しない。
    /// </summary>
    public void StartLumberAI()
    {
        if (_lumberRoutine != null) return; // 多重起動防止
        _lumberState = LumberState.Idle;
        _lumberRoutine = StartCoroutine(LumberjackLoop());
        Debug.Log($"[NPCPlayerHelper] 伐採AI 開始 (name={gameObject.name})");
    }

    /// <summary>
    /// 伐採AIを停止する（外部から制御を奪う場合など）。
    /// </summary>
    public void StopLumberAI()
    {
        if (_lumberRoutine != null)
        {
            StopCoroutine(_lumberRoutine);
            _lumberRoutine = null;
        }
        _targetTree = null;
        _lumberState = LumberState.Idle;
        SetMoveInput(Vector2.zero);
    }

    /// <summary>
    /// メインループ：木を探して→移動して→伐採する をシーンに木がなくなるまで繰り返す。
    /// </summary>
    private IEnumerator LumberjackLoop()
    {
        while (true)
        {
            // 1. 手動ターゲットが設定されていれば最優先で使用
            if (_manualTarget != null && _manualTarget.gameObject.activeInHierarchy)
            {
                _targetTree = _manualTarget;
            }
            else
            {
                // 手動ターゲットが無効になったらクリア
                if (_manualTarget != null && !_manualTarget.gameObject.activeInHierarchy)
                {
                    _manualTarget = null;
                }
                _targetTree = FindNearestTree();
            }

            if (_targetTree == null)
            {
                // 木がない場合は Idle 状態で待機
                _lumberState = LumberState.Idle;
                SetMoveInput(Vector2.zero);
                yield return new WaitForSeconds(0.5f); // 少し待って再検索
                continue;
            }

            // 2. 木の至近距離まで移動
            _lumberState = LumberState.Moving;
            while (_targetTree != null)
            {
                // ターゲットが破棄されていないか確認
                if (_targetTree == null || !_targetTree.gameObject.activeInHierarchy)
                {
                    break;
                }

                float distance = Vector2.Distance(transform.position, _targetTree.position);
                if (distance <= _reachDistance)
                {
                    break; // 到達：移動終了
                }

                // 木へ向かう方向を計算し移動＋アニメーション同期
                Vector2 dir = ((Vector2)_targetTree.position - (Vector2)transform.position).normalized;
                SetMoveInput(dir);

                KeepZFixed();
                yield return null;
            }

            // ターゲットが消えていたら次のループへ
            if (_targetTree == null || !_targetTree.gameObject.activeInHierarchy)
            {
                SetMoveInput(Vector2.zero);
                yield return null;
                continue;
            }

            // 3. 至近距離に到達：移動停止して伐採演出
            _lumberState = LumberState.Chopping;
            SetMoveInput(Vector2.zero);
            KeepZFixed();

            Transform treeToCut = _targetTree;
            yield return new WaitForSeconds(_chopDuration);

            // 4. 木を破棄（TreeController が GameManager の木材を加算）
            if (treeToCut != null)
            {
                TreeController tc = treeToCut.GetComponent<TreeController>();
                if (tc != null)
                {
                    // TreeController の伐採処理を呼び出し（木材加算＋破棄/切り株化）
                    tc.TakeDamage(999);
                }
                else
                {
                    // TreeController がない場合は直接破棄し、木材を加算
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.AddTreeCount(1);
                    }
                    Destroy(treeToCut.gameObject);
                }
            }

            _targetTree = null;
            _manualTarget = null; // 手動ターゲットをクリア
            // 次の木を探すためにループ先頭へ
            yield return null;
        }
    }

    /// <summary>
    /// タグ "Tree" または "Trees" の子オブジェクトから、最も近い伐採可能な木を返す。
    /// </summary>
    private Transform FindNearestTree()
    {
        Transform nearest = null;
        float minDist = _detectRange;

        // 方法A: タグ "Tree" を持つオブジェクトを検索
        GameObject[] taggedTrees = GameObject.FindGameObjectsWithTag(_treeTag);
        foreach (GameObject t in taggedTrees)
        {
            if (!t.activeInHierarchy) continue;
            // すでに切り株化（非アクティブ化）されていないかは上で判定済み
            float dist = Vector2.Distance(transform.position, t.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = t.transform;
            }
        }

        // 方法B: "Trees" という名前の親オブジェクトの子を検索（タグがなくても対応）
        GameObject treesRoot = GameObject.Find(_treesRootName);
        if (treesRoot != null)
        {
            foreach (Transform child in treesRoot.transform)
            {
                if (!child.gameObject.activeInHierarchy) continue;
                float dist = Vector2.Distance(transform.position, child.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = child;
                }
            }
        }

        return nearest;
    }

    /// <summary>
    /// 移動方向を設定し、MoveX / MoveY / Speed アニメーションパラメータを同期する。
    /// </summary>
    private void SetMoveInput(Vector2 input)
    {
        _moveInput = input;

        if (_anim != null)
        {
            _anim.SetFloat("MoveX", input.x);
            _anim.SetFloat("MoveY", input.y);
            // Speed パラメータ（歩行アニメーション再生用）を同期
            if (HasParameter(_anim, "Speed"))
            {
                _anim.SetFloat("Speed", input.magnitude);
            }
            // 既存の Blend パラメータも互換性のため同期
            _anim.SetFloat("Blend", input.magnitude);
        }
    }

    /// <summary>
    /// Z軸を固定値に戻す（物理・アニメーションでずれないよう毎フレーム補正）。
    /// </summary>
    private void KeepZFixed()
    {
        Vector3 p = transform.position;
        p.z = _fixedZ;
        transform.position = p;
    }

    void Update()
    {
        // プレイヤー入力は受け付けず、伐採AIが駆動する。
        // （AIの移動入力は LumberjackLoop 内で SetMoveInput 経由で設定される）
        KeepZFixed();

        // --- 手動ターゲット設定（右クリック） ---
        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.transform.CompareTag(_treeTag))
                {
                    SetManualTarget(hit.transform);
                }
            }
        }

        // --- 一括キャンセル（Xキー） ---
        if (Input.GetKeyDown(KeyCode.X))
        {
            // すべてのNPCPlayerHelperにキャンセル指令を出す
            NPCPlayerHelper[] npcs = FindObjectsOfType<NPCPlayerHelper>();
            foreach (var npc in npcs)
            {
                npc.CancelAll();
            }
        }

        // --- プレイヤー入力の復帰処理（AIが駆動していないとき）---
        if (_lumberState != LumberState.Moving)
        {
            float moveX = Input.GetAxisRaw("Horizontal");
            float moveY = Input.GetAxisRaw("Vertical");
            Vector2 manualInput = new Vector2(moveX, moveY);
            // 斜め入力時に長さが 1 を超えないよう正規化し、上下左右と同じ移動速度にする
            if (manualInput.magnitude > 1f)
            {
                manualInput = manualInput.normalized;
            }
            SetMoveInput(manualInput);
        }
    }

    void FixedUpdate()
    {
        Walk();
    }

    private void Walk()
    {
        if (_rb == null) return;

        if (_moveInput == Vector2.zero)
        {
            _rb.linearVelocity = Vector2.zero;
        }
        else
        {
            // 斜め入力時でも長さを 1 に正規化し、上下左右と同じ移動速度にする
            _rb.linearVelocity = _moveInput.normalized * _moveSpeed;
        }
    }

    void OnDestroy()
    {
        if (_lumberRoutine != null)
        {
            StopCoroutine(_lumberRoutine);
            _lumberRoutine = null;
        }
    }

    /// <summary>
    /// おばけ（敵）に接触した際などに呼び出され、このキャラクターを死亡させる。
    /// 元のプレイヤー（CharacterHealth）と同様に、死亡演出・食料還元・
    /// 次の操作キャラクターへの交代処理（GameManager.ReplacePlayer）を実行する。
    /// </summary>
    public void Die()
    {
        if (isDead) return;
        isDead = true;

        // 伐採AIを停止
        StopLumberAI();

        // 味方サイド（Player / Ally）が死亡した場合、その命を食料リソースとして +1 還元する。
        bool wasAlly = gameObject.CompareTag("Player") || gameObject.CompareTag("Ally");
        if (wasAlly && GameManager.Instance != null)
        {
            GameManager.Instance.AddFood(1);
        }

        // 死亡が確定した時点で、自分はもうプレイヤーのタグを返上する
        gameObject.tag = "Untagged";

        // 1. Disable collider
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // 2. Disable Animator to freeze animation instantly
        Animator anim = GetComponent<Animator>();
        if (anim != null) anim.enabled = false;

        // 3. Stop physics and velocity completely
        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.bodyType = RigidbodyType2D.Kinematic;
        }

        // 4. Automatically find and disable any movement scripts attached to this object
        MonoBehaviour[] scripts = GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            if (script != this && script.GetType() != typeof(GameManager))
            {
                script.enabled = false;
            }
        }

        // 5. Notify GameManager to recount the population (this character is now dead)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RecountPopulation();
        }

        // 死亡演出を開始し、終了後に次のキャラクターへ切り替える
        StartCoroutine(DeathAnimationRoutine());
    }

    private System.Collections.IEnumerator DeathAnimationRoutine()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        Vector3 originalLocalPosition = transform.localPosition;

        // 1. スプライトを真っ赤にする
        if (sr != null)
        {
            sr.color = Color.red;
        }

        // 2. 90度回転して横倒しにする
        transform.rotation = Quaternion.Euler(0, 0, 90f);

        float shakeDuration = 0.5f;
        float shakeMagnitude = 0.1f;
        float elapsed = 0.0f;

        // 3. 横倒しになった状態でぶるぶる痙攣させる
        while (elapsed < shakeDuration)
        {
            float offsetX = Random.Range(-1f, 1f) * shakeMagnitude;
            float offsetY = Random.Range(-1f, 1f) * shakeMagnitude;

            transform.localPosition = originalLocalPosition + (transform.right * offsetX) + (transform.up * offsetY);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 最終位置リセット
        transform.localPosition = originalLocalPosition;

        // 4. 自分が操作キャラクター（後継として選出された状態）だった場合のみ、
        // 次のキャラクターへ切り替える。自律AIのまま死んだ場合は交代しない。
        if (isSuccessor && GameManager.Instance != null)
        {
            GameManager.Instance.ReplacePlayer(this.gameObject);
        }
    }

    /// <summary>
    /// すべてのNPCPlayerHelperをキャンセルし、待機状態に戻す。
    /// </summary>
    public void CancelAll()
    {
        // コルーチン停止
        if (_lumberRoutine != null)
        {
            StopCoroutine(_lumberRoutine);
            _lumberRoutine = null;
        }
        // ターゲットクリア
        _targetTree = null;
        _manualTarget = null;
        _lumberState = LumberState.Idle;
        // 移動入力とアニメーションリセット
        SetMoveInput(Vector2.zero);
        // Rigidbodyの速度をゼロに
        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
        }
    }
}