using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChanger : MonoBehaviour
{

    // 「みんなで遊ぶ」ボタンを押したときに呼ぶ関数
    public void GoToMinnaDe()
    {
        SceneManager.LoadScene("minnade");
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
