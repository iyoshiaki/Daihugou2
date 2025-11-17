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
    private HumanPlayer humanPlayer;  // ���ǉ��F�I���Ԃ�n������

    void Awake()
    {
        if (cardImage == null) cardImage = GetComponent<Image>();

        // �� HumanPlayer ��V�[�������T��
        GameManager gm = FindObjectOfType<GameManager>();
        if (gm != null)
            humanPlayer = gm.humanPlayer; // GameManager���Ǘ����Ă���HumanPlayer��擾
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

    // �N���b�N�őI��i��Ƀ|�b�v�j
    //public void OnPointerClick(PointerEventData eventData)
    //{
    //    ToggleSelect();
    //}

    public void ToggleSelect()
    {
        if (CardData == null) return;

        IsSelected = !IsSelected;
        float targetOffsetY = IsSelected ? 25f : 0f; // �I�����25���

        // �R���[�`���Ŋ��炩��Y�݈̂ړ�
        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        moveCoroutine = StartCoroutine(MoveCardYOffset(targetOffsetY, 0.15f));

        // HumanPlayer�̑I����X�g�ɔ��f
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

    // === �J�[�h��w��ʒu�փA�j���[�V�����ړ� ===
    public IEnumerator MoveTo(Vector3 targetWorldPos, float duration)
    {
        Vector3 start = transform.position; // ���[���h���W�Ŏ擾
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

        // ��������ڂɔ��f���邽�߂̏���������Ȃ炱����
        // ��F�I����ɏ�����ɕ������� UI �Ȃ�
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
            // �ʏ�̐F�ɖ߂�
            image.color = Color.white;
            var cg = GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = true;
        }
        else
        {
            // �O���[���i�G���␔���͌�����j
            image.color = new Color(0.6f, 0.6f, 0.6f, 1f); // �� ���邳����OK
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

    // === ��ɏo����̓N���b�N�E���얳���� ===
    public void DisableInteraction()
    {
        // �N���b�N�������iraycast��Ւf�j
        var cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;

        // �{�^��������Α��얳����
        var button = GetComponent<Button>();
        if (button != null)
            button.interactable = false;

        // Collider������Γ����蔻��𖳌���
        var collider = GetComponent<Collider2D>();
        if (collider != null)
            collider.enabled = false;

        // ����I��ł��Ȃ��̂őI����
        IsSelected = false;
    }




    public void OnPointerClick(PointerEventData eventData)
    {
        // �o���Ȃ��J�[�h�Ȃ牽����Ȃ�
        if (!isPlayable) return;

        ToggleSelect();
    }
}