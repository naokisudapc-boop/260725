using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;  //シーン遷移のために必要

public class TitleManager : MonoBehaviour
{
    ///<summary>    
    ///ButtonUIのOnClick()に設定することで、ボタンが押されたときに呼び出されるメソッド
    /// アクセス演算子はpublicにする必要がある
    /// </summary>
    
    public void OnClickStageGoButton()
    {
        //SceneManager.LoadScene("GameScene"); //シーン名を指定して遷移する場合
        SceneManager.LoadScene("SampleScene"); 
    }
}
