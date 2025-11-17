using UnityEngine;
using UnityEngine.UI; 

public class RuleToggle : MonoBehaviour
{
    

    [Header("ルール設定")]
    public string ruleName = "革命"; 
    public bool isRuleOn = false;    

    [Header("色設定")]
    public Color colorOn = new Color(1f, 0.8f, 0f, 1f); 
    public Color colorOff = Color.white;                 

    
    private Image buttonImage; 

    void Awake()
    {
        
        buttonImage = GetComponent<Image>();

        
        UpdateVisual();
    }

    
    public void ToggleRule()
    {
        
        isRuleOn = !isRuleOn;

        
        UpdateVisual();

       
        Debug.Log(ruleName + " の設定を " + (isRuleOn ? "オン" : "オフ") + " に切り替えました。");
    }

    
    private void UpdateVisual()
    {
        if (buttonImage != null)
        {
            if (isRuleOn)
            {
                buttonImage.color = colorOn;
            }
            else
            {
                buttonImage.color = colorOff;
            }
        }
    }
}