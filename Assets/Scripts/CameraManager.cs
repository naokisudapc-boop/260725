using UnityEngine;

public class CameraManager : MonoBehaviour
{
    [SerializeField] private Transform target;

    [Header("Camera Limits")]
    [SerializeField] private float _minX = -10f;
    [SerializeField] private float _maxX = 10f;
    [SerializeField] private float _minY = -10f;
    [SerializeField] private float _maxy = 10f;

    void LateUpdate()
    {
        // 常に "Player" タグを持つオブジェクトを自動で探してターゲットにする
        GameObject currentPlayer = GameObject.FindWithTag("Player");
        if (currentPlayer != null)
        {
            target = currentPlayer.transform;
        }

        // ターゲットが存在する場合は追従処理を行う
        if (target != null)
        {
            float targetX = Mathf.Clamp(target.position.x, _minX, _maxX);
            float targetY = Mathf.Clamp(target.position.y, _minY, _maxy);

            transform.position = new Vector3(targetX, targetY, transform.position.z);
        }
    }
}