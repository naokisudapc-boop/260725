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

    [Header("Combat Settings (Ally Auto-Defense)")]
    [Tooltip("周囲の敵（Ghost/Enemy）を索敵する範囲。ThiefNPCのnormalDetectionRange相当")]
    [SerializeField] private float _combatDetectionRange = 5.0f;
    [SerializeField] private float _attackRange = 1.5f;
    [SerializeField] private float _combatMoveSpeed = 2.5f;
    [SerializeField] private float _attackCooldown = 1.2f;

    [Header("Attack Command Settings")]
    [Tooltip("味方への攻撃指示キー（ThiefNPCのattackCommandKeyと同じQキーを共有）")]
    [SerializeField] private KeyCode _attackCommandKey = KeyCode.Q;

    private Rigidbody2D _rb;
    private Animator _anim;
    private Vector2 _moveInput;
    private TMP_Text _nameLabel;

    // 戦闘用：斧（WeponHolder/ono）に自動アタッチされる Hammer コンポーネント
    private Hammer _hammerComponent;
    private Transform _targetEnemy;
    private float _nextAttackTime = 0f;

    // Qキーでの攻撃指示に関する状態（ThiefNPC.CommandAttack()と同じ考え方）
    private bool _isCommandedToAttack = false;
    private bool _isGatheringToPlayer = false;
    private Transform _playerTransform;

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

        // 戦闘（自衛AI／操作キャラクター昇格時の斧攻撃）に使う Hammer コンポーネントを用意
        EnsureHammerWeapon();

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

        // 注意：以前はここで gender == Male の場合に強制的に tag = "Player" を
        // 設定していたが、これだと味方の男性NPCヘルパーが複数いる場合に全員が
        // 同時に "Player" タグを持つことになり、FindWithTag("Player") を使う
        // 各所（追従・索敵など）が実際に操作中でないNPCを誤検出する原因になる。
        // また CharacterHealth.isPlayer と食い違うため削除した。
        // "Player" タグは GameManager.SelectNextPlayer が実際に操作対象へ
        // 昇格させたときにのみ設定される。
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

        // 戦闘に使う Hammer コンポーネントを用意（Startで未生成だった場合の保険も兼ねる）
        EnsureHammerWeapon();

        // ★Init 完了直後も自動で伐採モードを開始
        StartLumberAI();

        // 注意：tag を強制的に "Player" にする処理は Start() 側と同じ理由で削除。
        // GameManager.SelectNextPlayer が実際に操作対象を昇格させる際に
        // 自分でタグを "Player" に設定するので、ここでは何もしない。
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

            // 伐採演出：_chopDuration の間、斧（Hammer）を繰り返し振る
            float chopStartTime = Time.time;
            while (Time.time - chopStartTime < _chopDuration)
            {
                if (_hammerComponent != null)
                {
                    // SwingAndSpinHammer は1回あたり約0.3秒（Hammer.cs側で定義）。
                    // それを完了まで待ってから次の振りへ移る＝連続で振り続ける演出になる。
                    yield return StartCoroutine(_hammerComponent.SwingAndSpinHammer());
                }
                else
                {
                    // Hammer が見つからない場合は従来通りただ待つだけにフォールバック
                    yield return new WaitForSeconds(_chopDuration);
                    break;
                }
            }

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

    // ============================================================
    //  戦闘AI（ThiefNPC同様の自衛索敵・攻撃）
    // ============================================================

    /// <summary>
    /// WeponHolder/ono に Hammer コンポーネント（と攻撃判定用の Collider2D）を
    /// 自動で用意する。ThiefNPC は既にプレハブ上で Hammer を持っているが、
    /// NPCPlayerHelper 側にはまだ無いため、ここで補完する。
    /// これにより、自衛AIの攻撃（hammerComponent.ExecuteAttack）に加えて、
    /// 操作キャラクターへ昇格した際に GameManager.SelectNextPlayer が行う
    /// GetComponentInChildren&lt;Hammer&gt;() 経由の PlayerAttack への武器設定も
    /// 自動的に機能するようになる。
    /// </summary>
    private void EnsureHammerWeapon()
    {
        Transform ono = transform.Find("WeponHolder/ono");
        if (ono == null) return;

        // Hammer.Awake() は Collider2D の存在を前提にしているため、
        // Hammer をアタッチする前に Collider2D を用意しておく
        Collider2D onoCollider = ono.GetComponent<Collider2D>();
        if (onoCollider == null)
        {
            CircleCollider2D circle = ono.gameObject.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = 0.4f;
        }

        _hammerComponent = ono.GetComponent<Hammer>();
        if (_hammerComponent == null)
        {
            _hammerComponent = ono.gameObject.AddComponent<Hammer>();
        }
    }

    /// <summary>
    /// 周囲の敵（Ghost / Enemy タグ）を索敵する。ThiefNPC.FindGhost() と同じロジック。
    /// </summary>
    private void FindNearbyEnemy()
    {
        float minDistance = _combatDetectionRange;
        _targetEnemy = null;

        GameObject[] ghosts = GameObject.FindGameObjectsWithTag("Ghost");
        foreach (GameObject g in ghosts)
        {
            var ghostHealth = g.GetComponent<CharacterHealth>();
            if (ghostHealth != null && ghostHealth.isDead) continue;
            float dist = Vector2.Distance(transform.position, g.transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                _targetEnemy = g.transform;
            }
        }

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject e in enemies)
        {
            var enemyHealth = e.GetComponent<EnemyHealth>();
            if (enemyHealth != null && enemyHealth.isDead) continue;
            float dist = Vector2.Distance(transform.position, e.transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                _targetEnemy = e.transform;
            }
        }
    }

    /// <summary>
    /// 敵に接近し、射程内に入ったら斧（Hammer）で攻撃する。ThiefNPC.ExecuteCombatAI() 相当。
    /// </summary>
    private void ExecuteCombatAI()
    {
        if (_targetEnemy == null) return;

        float distance = Vector2.Distance(transform.position, _targetEnemy.position);
        Vector3 moveDirection = ((Vector3)_targetEnemy.position - transform.position).normalized;

        if (distance > _attackRange)
        {
            if (_rb != null) _rb.linearVelocity = Vector2.zero;
            Vector3 nextPosition = Vector2.MoveTowards(transform.position, _targetEnemy.position, _combatMoveSpeed * Time.deltaTime);
            transform.position = nextPosition;
            UpdateCombatAnimator(moveDirection, _combatMoveSpeed);
        }
        else
        {
            if (_rb != null) _rb.linearVelocity = Vector2.zero;
            UpdateCombatAnimator(Vector2.zero, 0f);

            if (Time.time >= _nextAttackTime)
            {
                if (_hammerComponent != null)
                {
                    _hammerComponent.ExecuteAttack();
                    _nextAttackTime = Time.time + _attackCooldown;
                }
            }
        }
    }

    /// <summary>
    /// 戦闘移動中のアニメーションパラメータ（MoveX/MoveY/Speed/Blend）を更新する。
    /// SetMoveInput は _moveInput（Rigidbody駆動のWalk()用）も書き換えてしまうため、
    /// transform.position を直接動かす戦闘中はこちらを使う。
    /// </summary>
    private void UpdateCombatAnimator(Vector2 moveDirection, float speedValue)
    {
        if (_anim == null) return;
        _anim.SetFloat("MoveX", moveDirection.x);
        _anim.SetFloat("MoveY", moveDirection.y);
        if (HasParameter(_anim, "Speed"))
        {
            _anim.SetFloat("Speed", speedValue);
        }
        _anim.SetFloat("Blend", speedValue);
    }

    /// <summary>
    /// Qキーによる攻撃指示（ThiefNPC.CommandAttack()と同じ考え方）。
    /// 即座に索敵を行い、敵が見つからなければプレイヤーの位置へ集合する。
    /// </summary>
    public void CommandAttack()
    {
        _isCommandedToAttack = true;

        FindNearbyEnemy();

        if (_targetEnemy == null)
        {
            FindPlayerTransform();
            if (_playerTransform != null)
            {
                _isGatheringToPlayer = true;
            }
        }
        else
        {
            _isGatheringToPlayer = false;
        }
    }

    /// <summary>
    /// 攻撃指示をキャンセルし、通常の伐採AIへ復帰させる。
    /// </summary>
    public void CancelAttackCommand()
    {
        _isCommandedToAttack = false;
        _isGatheringToPlayer = false;
        _targetEnemy = null;
    }

    private void FindPlayerTransform()
    {
        // "Player" タグがついているオブジェクトを操作中のプレイヤーとして探す
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            _playerTransform = playerObj.transform;
        }
    }

    /// <summary>
    /// 攻撃指示を受けたが周囲に敵がいない場合、プレイヤーの位置まで集合する。
    /// </summary>
    private void GatherToPlayer()
    {
        if (_playerTransform == null)
        {
            FindPlayerTransform();
            if (_playerTransform == null)
            {
                _isGatheringToPlayer = false;
                return;
            }
        }

        float distance = Vector2.Distance(transform.position, _playerTransform.position);

        if (distance <= _attackRange)
        {
            // プレイヤーの近くまで来たら停止して待機（次の索敵で敵が見つかれば戦闘へ移行）
            if (_rb != null) _rb.linearVelocity = Vector2.zero;
            UpdateCombatAnimator(Vector2.zero, 0f);
            return;
        }

        if (_rb != null) _rb.linearVelocity = Vector2.zero;
        Vector3 moveDirection = ((Vector3)_playerTransform.position - transform.position).normalized;
        Vector3 nextPosition = Vector2.MoveTowards(transform.position, _playerTransform.position, _combatMoveSpeed * Time.deltaTime);
        transform.position = nextPosition;
        UpdateCombatAnimator(moveDirection, _combatMoveSpeed);
    }

    void Update()
    {
        // 死亡後は何もしない（自律AI・戦闘・伐採のいずれも停止）
        if (isDead) return;

        // プレイヤー入力は受け付けず、伐採AIが駆動する。
        // （AIの移動入力は LumberjackLoop 内で SetMoveInput 経由で設定される）
        KeepZFixed();

        // --- Qキー：攻撃指示（ThiefNPCと同じキーを共有） ---
        if (Input.GetKeyDown(_attackCommandKey))
        {
            CommandAttack();
        }

        // --- 敵の索敵（ThiefNPC同様、自衛のため最優先で対応） ---
        FindNearbyEnemy();

        if (_targetEnemy != null)
        {
            // 戦闘中は伐採AIを一時停止し、敵に対処する
            if (_lumberRoutine != null)
            {
                StopLumberAI();
            }
            _isGatheringToPlayer = false;
            ExecuteCombatAI();
            return;
        }
        else if (_isGatheringToPlayer)
        {
            // 攻撃指示は出たが敵がいない：プレイヤーの位置へ集合する
            if (_lumberRoutine != null)
            {
                StopLumberAI();
            }
            GatherToPlayer();
            return;
        }
        else if (_lumberRoutine == null)
        {
            // 敵がいなくなったら伐採AIへ復帰
            StartLumberAI();
        }

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
            NPCPlayerHelper[] npcs = FindObjectsByType<NPCPlayerHelper>();
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
        // Qキーによる攻撃指示状態もリセット
        _isCommandedToAttack = false;
        _isGatheringToPlayer = false;
        _targetEnemy = null;
        // 移動入力とアニメーションリセット
        SetMoveInput(Vector2.zero);
        // Rigidbodyの速度をゼロに
        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
        }
    }
}