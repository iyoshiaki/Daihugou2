using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChanger : MonoBehaviour
{

    // u???V?v{^?????
    public void GoToMinnaDe(string minnade)
    {
        SoloRuleSettings.SetSoloMode(false);
        SceneManager.LoadScene("minnade");
    }

    // u[?v{^?????
    public void GoToRulesettings(string Rulesettings)
    {
        SoloRuleSettings.SetSoloMode(false);
        SceneManager.LoadScene("Rulesettings");
    }

    // u?v{^?????
    public void GoTomodesentaku(string modesentaku)
    {
        SoloRuleSettings.SetSoloMode(false);
        SceneManager.LoadScene("SoloRule");
    }

    // [hI?A[???u?v{^?????
    public void BackTotitle()
    {
        SoloRuleSettings.SetSoloMode(false);
        SceneManager.LoadScene("title");
    }

    // ?????u?v{^?????
    public void BackTomodesentaku()
    {
        SoloRuleSettings.SetSoloMode(false);
        SceneManager.LoadScene("modesentaku");
    }

    // IWi[{^?????
    public void GoTooriginal()
    {
        SoloRuleSettings.SetSoloMode(false);
        SceneManager.LoadScene("original");
    }

    // u???V?vIWi[{^?????
    public void GoToSoloOriginal()
    {
        SoloRuleSettings.SetSoloMode(true);
        SceneManager.LoadScene("SoloOriginal");
    }

    // ?????u?v{^?????
    public void BackToRulesetteings()
    {
        SoloRuleSettings.SetSoloMode(false);
        SceneManager.LoadScene("Rulesettings");
    }

    // u???V?v?????
    public void GoToSoloRule()
    {
        SoloRuleSettings.SetSoloMode(true);
        SceneManager.LoadScene("SoloRule");
    }

    // u???V?vIWi[??????
    public void BackToSoloRule()
    {
        SoloRuleSettings.SetSoloMode(true);
        SceneManager.LoadScene("SoloRule");
    }

    // u???V?vJn! ?????
    public void GoToMainScene()
    {
        SoloRuleSettings.SetSoloMode(true);
        SceneManager.LoadScene("MainScene");
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