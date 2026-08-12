using UnityEngine;
using System.Collections;

/// <summary>
/// 矢の被弾処理を共通化するコンポーネント
/// FarmingNPCやThiefNPCなどの味方NPCで使用される
/// </summary>
public class ArrowHitHandler : MonoBehaviour
{
    [Header("Retreat Settings")]
    [Tooltip("退却時の移動速度")]
    public float retreatMoveSpeed = 1.5f;
    
    [Tooltip("退却完了の判定距離")]
    public float retreatStopDistance = 0.5f;
    
    [Header("References")]
    [Tooltip("退却先となる鍛冶工房（Blacksmith）")]
    public Transform blacksmithPosition;
    
    // 矢の被弾回数
    private int arrowHitCount = 0;
    
    // 退却中フラグ
    private bool isRetreating = false;
    
    // 退却用のRigidbody2D
    private Rigidbody2D rb;
    
    // 退却中の元の速度を保存
    private float originalMoveSpeed;
    
    // CharacterHealth参照
    private CharacterHealth characterHealth;
    
    // 退却中に作業を中断させないためのフラグ
    private bool wasWorkingBeforeRetreat = false;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        characterHealth = GetComponent<CharacterHealth>();
        
        // 鍛冶工房が設定されていない場合、シーン内を検索
        if (blacksmithPosition == null)
        {
            FindBlacksmith();
        }
    }
    
    /// <summary>
    /// シーン内からBlacksmithタグのオブジェクトを検索
    /// </summary>
    private void FindBlacksmith()
    {
        GameObject blacksmithObj = GameObject.FindGameObjectWithTag("Blacksmith");
        if (blacksmithObj != null)
        {
            blacksmithPosition = blacksmithObj.transform;
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: Blacksmithタグのオブジェクトが見つかりません。退却処理は無効化されます。");
        }
    }
    
    /// <summary>
    /// 頭部への命中。退却・被弾回数のカウントに関わらず即座に死亡させる。
    /// </summary>
    public void OnHeadshot()
    {
        if (characterHealth != null && !characterHealth.isDead)
        {
            Debug.Log($"🎯 {gameObject.name} に頭部命中！ 即死。");
            characterHealth.Die();
        }
    }

    /// <summary>
    /// 矢が当たったときの処理
    /// 外部から呼び出すメインメソッド
    /// </summary>
    public void OnHitByArrow()
    {
        arrowHitCount++;
        
        if (arrowHitCount == 1)
        {
            // 1本目：退却開始
            StartRetreat();
        }
        else if (arrowHitCount >= 2)
        {
            // 2本目以上：死亡処理
            HandleDeath();
        }
    }
    
    /// <summary>
    /// 退却処理を開始
    /// </summary>
    private void StartRetreat()
    {
        if (isRetreating) return;
        
        isRetreating = true;
        
        // 退却中の作業を中断
        wasWorkingBeforeRetreat = IsCurrentlyWorking();
        InterruptCurrentWork();
        
        // 退却開始
        StartCoroutine(RetreatToBlacksmith());
    }
    
    /// <summary>
    /// 退却中の作業を中断する
    /// </summary>
    private void InterruptCurrentWork()
    {
        // FarmingNPCの場合
        var farmingNPC = GetComponent<FarmingNPC>();
        if (farmingNPC != null)
        {
            // 作業中フラグをチェックしている場合は中断
        }
        
        // ThiefNPCの場合
        var thiefNPC = GetComponent<ThiefNPC>();
        if (thiefNPC != null)
        {
            // 採掘中や戦闘中の場合は中断
        }
    }
    
    /// <summary>
    /// 鍛冶工房へ退却するコルーチン
    /// </summary>
    private IEnumerator RetreatToBlacksmith()
    {
        if (blacksmithPosition == null)
        {
            Debug.LogWarning($"{gameObject.name}: 退却先が設定されていません。");
            yield break;
        }
        
        // 物理演算を一時的に無効化して位置ベースの移動に切り替え
        if (rb != null)
        {
            rb.simulated = false;
        }
        
        while (isRetreating)
        {
            Vector3 direction = (blacksmithPosition.position - transform.position).normalized;
            Vector3 nextPosition = Vector3.MoveTowards(transform.position, blacksmithPosition.position, retreatMoveSpeed * Time.deltaTime);
            
            // アニメーション更新
            UpdateAnimatorForMovement(direction, retreatMoveSpeed);
            
            transform.position = nextPosition;
            
            // 退却完了判定
            if (Vector3.Distance(transform.position, blacksmithPosition.position) <= retreatStopDistance)
            {
                // 退却完了時はSpeedを0にして待機モーションへ
                UpdateAnimatorForMovement(Vector3.zero, 0f);
                isRetreating = false;
                break;
            }
            
            yield return null;
        }
        
        // 退却完了後の処理
        OnRetreatComplete();
    }
    
    /// <summary>
    /// 退却完了後の処理
    /// </summary>
    private void OnRetreatComplete()
    {
        // 物理演算を再有効化
        if (rb != null)
        {
            rb.simulated = true;
        }
        
        // 作業を再開（必要に応じて）
        // 退却先で一定時間待機など
    }
    
    /// <summary>
    /// 死亡処理
    /// </summary>
    private void HandleDeath()
    {
        if (characterHealth != null && !characterHealth.isDead)
        {
            characterHealth.Die();
        }
    }
    
    /// <summary>
    /// 現在作業中かどうかを判定
    /// </summary>
    private bool IsCurrentlyWorking()
    {
        var farmingNPC = GetComponent<FarmingNPC>();
        if (farmingNPC != null && farmingNPC.IsBusy)
        {
            return true;
        }
        
        var thiefNPC = GetComponent<ThiefNPC>();
        if (thiefNPC != null)
        {
            // 採掘中かどうかチェック
            // ここではisMiningフラグをチェック
            return false; // 実装はThiefNPC側で適切に
        }
        
        return false;
    }
    
    /// <summary>
    /// 移動用のアニメーションを更新
    /// </summary>
    private void UpdateAnimatorForMovement(Vector3 direction, float speed)
    {
        // NPCPlayerHelper/BowManNPC 等は実際に使うAnimatorが子オブジェクトにあるため、
        // GetComponent ではなく GetComponentInChildren で探す（ThiefNPC/FarmingNPCのように
        // 自分自身にAnimatorがある場合もこれで問題なく見つかる）
        Animator anim = GetComponentInChildren<Animator>();
        if (anim == null) return;

        // Controllerが割り当てられていないAnimatorに対して parameters にアクセスすると
        // 「Animator is not playing an AnimatorController」という警告が出るため、先に確認する。
        if (anim.runtimeAnimatorController == null) return;

        // Animator.SetFloat は存在しないパラメータを渡しても例外を投げず、
        // コンソールに警告を出すだけなので、事前に存在確認してから呼び出す。
        if (HasParameter(anim, "InputX")) anim.SetFloat("InputX", direction.x);
        if (HasParameter(anim, "InputY")) anim.SetFloat("InputY", direction.y);
        if (HasParameter(anim, "Speed")) anim.SetFloat("Speed", speed);
    }

    /// <summary>
    /// 指定した名前のパラメータがAnimatorに存在するかを確認する
    /// （FarmingNPC.HasParameterと同じロジック）
    /// </summary>
    private bool HasParameter(Animator anim, string paramName)
    {
        foreach (AnimatorControllerParameter param in anim.parameters)
        {
            if (param.name == paramName) return true;
        }
        return false;
    }
    
    /// <summary>
    /// 矢の被弾回数を取得
    /// </summary>
    public int GetArrowHitCount()
    {
        return arrowHitCount;
    }
    
    /// <summary>
    /// 退却中かどうかを取得
    /// </summary>
    public bool IsRetreating()
    {
        return isRetreating;
    }
    
    /// <summary>
    /// 退却をキャンセル（必要な場合）
    /// </summary>
    public void CancelRetreat()
    {
        isRetreating = false;
        if (rb != null)
        {
            rb.simulated = true;
        }
    }
}