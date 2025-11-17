using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CardView : MonoBehaviour, IPointerClickHandler
{
    public Image cardImage;
    public Sprite backSprite;
    public Card CardData { get; private set; }
    public bool IsFaceUp { get; private set; } = true;
    public bool IsSelected { get; private set; } = false;

    public bool isPlayable = true;

    private Vector3 originalPosition;

    private Coroutine moveCoroutine;



    Vector3 initialLocalPos;
    private HumanPlayer humanPlayer;  // ★追加：選択状態を渡す相手

    void Awake()
    {
        if (cardImage == null) cardImage = GetComponent<Image>();

        // ★ HumanPlayer をシーン内から探す
        GameManager gm = FindObjectOfType<GameManager>();
        if (gm != null)
            humanPlayer = gm.humanPlayer; // GameManagerが管理しているHumanPlayerを取得
    }
    void Start()
    {
        initialLocalPos = transform.localPosition;
    }

    public void SetCard(Card card)
    {
        CardData = card;
        IsFaceUp = true;
        UpdateSprite();
    }

    public void SetFaceDown()
    {
        CardData = null;
        IsFaceUp = false;
        if (backSprite != null) cardImage.sprite = backSprite;
    }

    public void FlipFaceUp(Card card)
    {
        CardData = card;
        StartCoroutine(FlipAnimation(card));
    }

    IEnumerator FlipAnimation(Card card)
    {
        float t = 0f; float dur = 0.15f;
        while (t < dur)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(Vector3.one, new Vector3(0f, 1f, 1f), t / dur);
            yield return null;
        }
        CardData = card;
        UpdateSprite();
        t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(new Vector3(0f, 1f, 1f), Vector3.one, t / dur);
            yield return null;
        }
    }

    void UpdateSprite()
    {
        if (!IsFaceUp)
        {
            if (backSprite != null) cardImage.sprite = backSprite;
            return;
        }
        if (CardData == null)
        {
            cardImage.sprite = backSprite;
            return;
        }

        var sprite = Resources.Load<Sprite>(CardData.SpritePath);
        if (sprite != null) cardImage.sprite = sprite;
        else
        {
            // Debug.LogWarning("Card sprite not found: " + CardData.SpritePath);
            cardImage.sprite = backSprite;
        }
    }

    // クリックで選択（上にポップ）
    //public void OnPointerClick(PointerEventData eventData)
    //{
    //    ToggleSelect();
    //}

    public void ToggleSelect()
    {
        if (CardData == null) return;

        IsSelected = !IsSelected;
        float targetOffsetY = IsSelected ? 25f : 0f; // 選択時に25上に

        // コルーチンで滑らかにYのみ移動
        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        moveCoroutine = StartCoroutine(MoveCardYOffset(targetOffsetY, 0.15f));

        // HumanPlayerの選択リストに反映
        if (humanPlayer != null)
        {
            if (IsSelected)
            {
                if (!humanPlayer.SelectedCards.Contains(CardData))
                    humanPlayer.SelectedCards.Add(CardData);
            }
            else
            {
                humanPlayer.SelectedCards.Remove(CardData);
            }
        }
    }

    // === カードを指定位置へアニメーション移動 ===
    public IEnumerator MoveTo(Vector3 targetWorldPos, float duration)
    {
        Vector3 start = transform.position; // ワールド座標で取得
        float elapsed = 0;
        while (elapsed < duration)
        {
            transform.position = Vector3.Lerp(start, targetWorldPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = targetWorldPos;
    }

    public void SetSelected(bool value)
    {
        IsSelected = value;

        // もし見た目に反映するための処理があるならここで
        // 例：選択時に少し上に浮かせる UI など
        transform.localPosition = new Vector3(
            transform.localPosition.x,
            value ? 30f : 0f,
            transform.localPosition.z
        );
    }

    public void SetPlayable(bool canPlay)
    {
        // Debug.Log($"SetPlayable() called for {CardData.Suit}{CardData.Rank} - canPlay={canPlay}");
        var image = GetComponent<Image>();

        if (canPlay)
        {
            // 通常の色に戻す
            image.color = Color.white;
            var cg = GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = true;
        }
        else
        {
            // グレー化（絵柄や数字は見える）
            image.color = new Color(0.6f, 0.6f, 0.6f, 1f); // ← 明るさ調整OK
            var cg = GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = false;
        }
    }
    private IEnumerator MoveCardY(float targetY, float duration)
    {
        Vector3 startPos = transform.localPosition;
        Vector3 endPos = new Vector3(startPos.x, initialLocalPos.y + targetY, startPos.z);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            transform.localPosition = Vector3.Lerp(startPos, endPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localPosition = endPos;
    }
    private IEnumerator MoveCardYOffset(float offsetY, float duration)
    {
        Vector3 startPos = transform.localPosition;
        Vector3 endPos = new Vector3(initialLocalPos.x, initialLocalPos.y + offsetY, initialLocalPos.z);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            transform.localPosition = Vector3.Lerp(startPos, endPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localPosition = endPos;
    }

    // === 場に出た後はクリック・操作無効化 ===
    public void DisableInteraction()
    {
        // クリック無効化（raycastを遮断）
        var cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;

        // ボタンがあれば操作無効化
        var button = GetComponent<Button>();
        if (button != null)
            button.interactable = false;

        // Colliderがあれば当たり判定を無効化
        var collider = GetComponent<Collider2D>();
        if (collider != null)
            collider.enabled = false;

        // もう選択できないので選択解除
        IsSelected = false;
    }




    public void OnPointerClick(PointerEventData eventData)
    {
        // 出せないカードなら何もしない
        if (!isPlayable) return;

        ToggleSelect();
    }
}