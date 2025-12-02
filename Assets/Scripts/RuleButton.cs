using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class NewBehaviourScript : MonoBehaviour
{
    public Button targetButton;
    public Color changedColor = Color.yellow;

    private Color originalColor;
    private bool isChanged = false;

    // Start is called before the first frame update
    void Start()
    {
        // 最初の色を保存
        ColorBlock cb = targetButton.colors;
        originalColor = cb.normalColor;
    }

    public void ToggleColor()
    {
        ColorBlock cb = targetButton.colors;

        if (isChanged)
        {
            // 元の色に戻す
            cb.normalColor = originalColor;
            cb.highlightedColor = originalColor;
            cb.pressedColor = originalColor;
            cb.selectedColor = originalColor;
            isChanged = false;
        }
        else
        {
            // 色を変更
            cb.normalColor = changedColor;
            cb.highlightedColor = changedColor;
            cb.pressedColor = changedColor;
            cb.selectedColor = changedColor;
            isChanged = true;
        }

        targetButton.colors = cb;
    }

// Update is called once per frame
void Update()
    {
        
    }
}
