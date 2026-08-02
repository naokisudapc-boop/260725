using UnityEngine;
using TMPro;

public class NewMonoBehaviourScript : MonoBehaviour
{
    public Gender gender;
    [SerializeField] private float _moveSpeed = 5;
    [SerializeField] private RuntimeAnimatorController correctPlayerController; // 正しい Player 用 Animator Controller
    private Rigidbody2D _rb;
    private Animator _anim;
    private Vector2 _moveInput;
    private TMP_Text _nameLabel;

    void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        // Animator が子オブジェクトにある場合も考慮し、InChildren で取得する
        _anim = GetComponentInChildren<Animator>();
        if (_anim != null && HasParameter(_anim, "Gender"))
        {
            _anim.SetInteger("Gender", (int)gender);
        }
        Debug.Log($"[NewMonoBehaviourScript] Start: Animator {( _anim != null ? "取得OK" : "が見つかりません！")} (name={gameObject.name})");

        // Generate and display name
        string characterName = NameGenerator.GetRandomName(gender);
        if (UIManager.Instance != null)
        {
        // 【移動スクリプトのStart内をこのように修正】
        // X=0, Y=1.0f(頭上), Z=-0.1f(地面やキャラより手前) に強制固定する
        Vector3 nameOffset = new Vector3(0f, 1.0f, -0.1f);
        _nameLabel = UIManager.Instance.CreateNameLabel(transform, characterName, nameOffset);
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
        // Animator が子オブジェクトにある場合も考慮し、InChildren で取得する
        _anim = GetComponentInChildren<Animator>();

        // 初期化の最終地点で、正しい Player 用コントローラーを強制的に上書き代入。
        // Unity の自動生成や他処理による "Walk" 専用コントローラーへの上書きをねじ伏せる。
        if (_anim != null && correctPlayerController != null)
        {
            _anim.runtimeAnimatorController = correctPlayerController;
        }

        if (_anim != null && HasParameter(_anim, "Gender"))
        {
            _anim.SetInteger("Gender", (int)gender);
        }
        // コントローラーを正しいものに差し替えた直後、InputX/InputY の初期値を
        // 確実に反映させる（0 でリセット）。
        if (_anim != null)
        {
            _anim.SetFloat("InputX", 0f);
            _anim.SetFloat("InputY", 0f);
            _anim.SetFloat("Speed", 0f);
        }
        Debug.Log($"[NewMonoBehaviourScript] Init: Animator {( _anim != null ? "取得OK" : "が見つかりません！")} (name={gameObject.name})");

        // Prevent spinning when colliding with obstacles
        if (_rb != null)
        {
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            _rb.linearVelocity = Vector2.zero;
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

    void Update()
    {
        GetInput();
    }

    private void GetInput()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");

        _moveInput = new Vector2(x, y).normalized;

        if (_anim == null) return;

        // Animator.SetFloat は存在しないパラメータを渡しても例外を投げず、
        // コンソールに警告を出すだけなので、事前に存在確認してから呼び出す。
        if (HasParameter(_anim, "InputX")) _anim.SetFloat("InputX", x);
        if (HasParameter(_anim, "InputY")) _anim.SetFloat("InputY", y);
        if (HasParameter(_anim, "Speed")) _anim.SetFloat("Speed", _moveInput.magnitude);

        if (_moveInput.magnitude > 0.1f)
        {
            if (HasParameter(_anim, "LastInputX")) _anim.SetFloat("LastInputX", x);
            if (HasParameter(_anim, "LastInputY")) _anim.SetFloat("LastInputY", y);
        }
    }

    void FixedUpdate()
    {
        Walk();
    }

    private void Walk()
    {
        if (_rb == null) return;

        // When there is no input, force the velocity to zero so the character
        // stops instantly instead of sliding with inertia after a collision.
        if (_moveInput == Vector2.zero)
        {
            _rb.linearVelocity = Vector2.zero;
        }
        else
        {
            _rb.linearVelocity = _moveInput * _moveSpeed;
        }
    }
}