using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardSlot : MonoBehaviour
{
    public RectTransform rect;
    public bool IsOccupied = false;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {


    }
    void Awake()
    {
        rect = GetComponent<RectTransform>();

        // デバッグ用: スロット位置を目視で確認
        // var img = gameObject.AddComponent<Image>();
        // img.color = new Color(1f, 0f, 0f, 0.1f); // 赤っぽい透明
    }
}
