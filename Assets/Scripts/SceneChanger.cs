using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChanger : MonoBehaviour
{

    // 「みんなで遊ぶ」ボタンを押したときに呼ぶ関数
    public void GoToMinnaDe(string minnade)
    {
        SceneManager.LoadScene("minnade");
    }

    // 「ルール設定」ボタンを押したときに呼ぶ関数
    public void GoToRulesettings(string Rulesettings)
    {
        SceneManager.LoadScene("Rulesettings");
    }

    // 「対戦」ボタンを押したときに呼ぶ関数
    public void GoTomodesentaku(string modesentaku)
    {
        SceneManager.LoadScene("modesentaku");
    }

    // モード選択画面、ルール設定画面の「戻る」ボタンを押したときに呼ぶ関数
    public void BackTotitle()
    {
        SceneManager.LoadScene("title");
    }

    // みんなで画面の「戻る」ボタンを押したときに呼ぶ関数
    public void BackTomodesentaku()
    {
        SceneManager.LoadScene("modesentaku");
    }

    // オリジナルルールボタンを押したときに呼ぶ関数
    public void GoTooriginal()
    {
        SceneManager.LoadScene("original");
    }

    // みんなで画面の「戻る」ボタンを押したときに呼ぶ関数
    public void BackToRulesetteings()
    {
        SceneManager.LoadScene("Rulesettings");
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
