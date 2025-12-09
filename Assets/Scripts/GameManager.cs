using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class GameManager : MonoBehaviour
{
    // UI References
    [Header("UI References")]
    public Transform handAreaPlayer;
    public Transform handAreaCPU1;
    public Transform handAreaCPU2;
    public Transform handAreaCPU3;
    public Transform tableArea;

    // Prefabs & Sprites
    [Header("Prefabs & Sprites")]
    public GameObject cardPrefab;
    public Sprite cardBackSprite;

    // 勝敗・ゲーム進行管理用
    private List<PlayerBase> remainingPlayers;
    private Dictionary<PlayerBase, int> gameRanks = new();
    private int currentRank = 1;
    private bool isGameOver = false;

    [Header("Rule Settings")]
    [Tooltip("7渡しや10捨てのような特殊アクションの結果としてあがることを禁止する")]
    private bool forbidSpecialWin = false;

    private const int TotalGames = 4;
    private int currentGameCount = 1;
    private Dictionary<PlayerBase, int> totalPoints = new();

    private HumanPlayer human;
    private List<CpuPlayer> cpuPlayers = new();

    public List<Card> lastPlayedCards = new();

    private int passCount = 0;
    private int lastPlayedPlayerIndex = -1;

    [SerializeField] private Button passButton;
    [SerializeField] private Button playButton;

    private List<PlayerBase> players;

    [SerializeField] private TextMeshProUGUI passMessageText;

    private Queue<string> messageQueue = new();
    private bool isShowingMessage = false;

    [SerializeField] private GameObject cardSlotPrefab;

    private List<CardSlot> playerCardSlots = new();

    private int currentTurnIndex = 0;
    private bool isPlayerTurn = true;

    private List<IRule> rules = new List<IRule>();
    private bool skipTurnAdvance = false;

    private bool isRevolution = false;
    private bool isTempRevolution = false;

    private bool IsRevolutionActive => isRevolution ^ isTempRevolution;

    private int pendingSkipCount = 0;

    private bool isSevenPassMode = false;
    private bool isTenDiscardMode = false;
    private int pendingActionCardCount = 0;

    // ★各ルールへの参照を保持
    private EightCutRule eightCutRule;
    private RevolutionRule revolutionRule;
    private ElevenBackRule elevenBackRule;
    private FiveSkipRule fiveSkipRule;
    private SevenPassRule sevenPassRule;
    private TenDiscardRule tenDiscardRule;

    // ================================================
    // --- 公開メソッド（Ruleクラスから呼び出し用）---
    // ================================================

    public List<PlayerBase> GetPlayers() => players;
    public bool GetForbidSpecialWin() => forbidSpecialWin;
    public bool IsGameOver() => isGameOver;

    public void ResetPassCount()
    {
        passCount = 0;
    }

    public void ResetPendingSkipCount()
    {
        pendingSkipCount = 0;
    }

    public void ResetTempRevolution()
    {
        isTempRevolution = false;
    }

    public void SetSkipTurnAdvance(bool value)
    {
        skipTurnAdvance = value;
    }

    public bool IsPlayerRemaining(PlayerBase player)
    {
        return remainingPlayers.Contains(player);
    }

    public void SetSevenPassMode(bool active, int count)
    {
        isSevenPassMode = active;
        pendingActionCardCount = count;
    }

    public void SetTenDiscardMode(bool active, int count)
    {
        isTenDiscardMode = active;
        pendingActionCardCount = count;
    }

    public void ShowSevenPassUI(string message)
    {
        passMessageText.text = message;
        passMessageText.gameObject.SetActive(true);

        ResetPlayerSelection();
        CreatePlayerCardSlots(human.Hand.Count);
        PopulatePlayerHand(human);

        if (passButton != null) passButton.interactable = false;
        if (playButton != null)
        {
            playButton.interactable = false;
            playButton.GetComponentInChildren<TextMeshProUGUI>().text = "あげる";
        }
    }

    public void ShowTenDiscardUI(string message)
    {
        passMessageText.text = message;
        passMessageText.gameObject.SetActive(true);

        ResetPlayerSelection();
        CreatePlayerCardSlots(human.Hand.Count);
        PopulatePlayerHand(human);

        if (passButton != null) passButton.interactable = false;
        if (playButton != null)
        {
            playButton.interactable = false;
            playButton.GetComponentInChildren<TextMeshProUGUI>().text = "捨てる";
        }
    }

    public void HideActionMessage()
    {
        passMessageText.gameObject.SetActive(false);
        passMessageText.text = "";
    }

    public void RemoveCardsFromPlayerUI(List<Card> cards)
    {
        var cardViews = handAreaPlayer.GetComponentsInChildren<CardView>().ToList();
        foreach (var cv in cardViews)
            if (cv != null && cv.CardData != null && cards.Contains(cv.CardData))
                Destroy(cv.gameObject);
    }

    public void UpdatePlayerHandDisplay(PlayerBase player)
    {
        if (player is HumanPlayer)
        {
            CreatePlayerCardSlots(human.Hand.Count);
            PopulatePlayerHand(human);
        }
        else
        {
            Transform cpuArea = null;
            if (player == cpuPlayers[0]) cpuArea = handAreaCPU1;
            else if (player == cpuPlayers[1]) cpuArea = handAreaCPU2;
            else if (player == cpuPlayers[2]) cpuArea = handAreaCPU3;

            if (cpuArea != null) PopulateCpuHandAsBack(cpuArea, player.Hand.Count);
        }
    }

    public void ResetPlayButtonUI()
    {
        if (playButton != null)
        {
            playButton.GetComponentInChildren<TextMeshProUGUI>().text = "出す";
            playButton.interactable = false;
        }
        if (passButton != null) passButton.interactable = true;
    }

    public void EndPlayerTurn()
    {
        EndTurn();
    }

    public void CheckForWin(PlayerBase player)
    {
        if (player.Hand.Count == 0)
        {
            gameRanks[player] = currentRank;
            EnqueueMessage($"{player.Name} があがりました! ({currentRank}位)");

            currentRank++;
            remainingPlayers.Remove(player);

            if (remainingPlayers.Count <= 1)
            {
                var lastPlayer = remainingPlayers[0];
                gameRanks[lastPlayer] = currentRank;
                EnqueueMessage($"{lastPlayer.Name} が大貧民確定です。");

                isGameOver = true;
                StartCoroutine(EndGameRoutine());
            }
        }
    }

    public void SetForbidSpecialWin(bool value)
    {
        forbidSpecialWin = value;
        Debug.Log($"禁止あがりルールが {(value ? "ON" : "OFF")} に設定されました。");
    }

    public int GetCardStrength(int rank)
    {
        int power = 0;
        if (rank == 16) power = 14;
        else if (rank == 15) power = 13;
        else if (rank == 14) power = 12;
        else power = rank - 3;

        if (IsRevolutionActive)
        {
            return -power;
        }
        return power;
    }

    // ================================================
    // --- ターン制管理メソッド ---
    // ================================================

    public void StartTurn()
    {
        passButton.interactable = currentTurnIndex == 0;

        if (currentTurnIndex == 0)
        {
            if (playButton != null) playButton.interactable = true;

            ResetPlayerSelection();
            CreatePlayerCardSlots(human.Hand.Count);
            PopulatePlayerHand(human);
            isPlayerTurn = true;
            Debug.Log("あなたのターンです。カードを選んでPlayボタンを押してください。");
        }
        else
        {
            if (playButton != null) playButton.interactable = false;

            isPlayerTurn = false;
            StartCoroutine(CpuPlayTurn(currentTurnIndex - 1));
        }
    }

    public void EndTurn()
    {
        if (isGameOver) return;

        if (skipTurnAdvance)
        {
            skipTurnAdvance = false;
            pendingSkipCount = 0;

            if (remainingPlayers.Contains(players[currentTurnIndex]))
            {
                StartCoroutine(NextTurnDelay());
                return;
            }
        }

        int nextTurnIndex = (currentTurnIndex + 1 + pendingSkipCount) % players.Count;

        pendingSkipCount = 0;

        int loopSafety = 0;

        while (!remainingPlayers.Contains(players[nextTurnIndex]))
        {
            nextTurnIndex = (nextTurnIndex + 1) % players.Count;

            loopSafety++;
            if (loopSafety > players.Count + 2)
            {
                isGameOver = true;
                StartCoroutine(EndGameRoutine());
                return;
            }
        }

        currentTurnIndex = nextTurnIndex;

        StartCoroutine(NextTurnDelay());
    }

    private CardView FindCardViewForCard(Card card, PlayerBase player)
    {
        CardView[] allCards = FindObjectsOfType<CardView>();
        foreach (CardView cv in allCards)
            if (cv.CardData == card && cv.transform.parent == player.handArea)
                return cv;
        return null;
    }

    private void ResetPlayerSelection()
    {
        human.ClearSelectedCards();
        foreach (Transform child in handAreaPlayer)
        {
            var cv = child.GetComponent<CardView>();
            if (cv != null) cv.SetSelected(false);
        }
    }

    private IEnumerator NextTurnDelay()
    {
        yield return new WaitForSeconds(0.8f);
        StartTurn();
    }

    private IEnumerator CpuPlayTurn(int cpuIndex)
    {
        var cpu = cpuPlayers[cpuIndex];
        yield return new WaitForSeconds(0.8f);

        if (cpu.Hand.Count == 0)
        {
            EndTurn();
            yield break;
        }

        List<Card> playableCards = GetPlayableCardsForCpu(cpu, lastPlayedCards);

        if (playableCards.Count == 0)
        {
            EnqueueMessage($"{cpu.Name} はパスしました");
            Debug.Log($"{cpu.Name} はパスしました。");
            yield return new WaitForSeconds(0.8f);
            HandlePass();
            yield break;
        }

        foreach (var c in playableCards) cpu.Hand.Remove(c);

        yield return StartCoroutine(DisplayPlayedCardsOnTable(cpu, playableCards));

        if (isSevenPassMode || isTenDiscardMode)
        {
            yield break;
        }

        bool clearedBySpade3OrPass = lastPlayedCards.Count == 0 && !skipTurnAdvance;

        if (clearedBySpade3OrPass)
        {
            yield break;
        }

        yield return new WaitForSeconds(0.8f);
        EndTurn();
    }
    // ================================================
    // --- CPUの出せるカード判定ロジック ---
    // ================================================
    private List<Card> GetPlayableCardsForCpu(CpuPlayer cpu, List<Card> field)
    {
        var hand = cpu.Hand.OrderBy(c => c.Rank).ThenBy(c => c.Suit).ToList();

        if (field == null || field.Count == 0)
        {
            var stairs = FindStairSequences(hand);
            if (stairs.Count > 0)
            {
                var chosen = stairs[Random.Range(0, stairs.Count)];
                return chosen;
            }
            return new List<Card> { hand.First() };
        }

        bool isFieldStair = IsStair(field);
        int fieldCount = field.Count;
        int fieldRank = field[0].Rank;

        if (!isFieldStair)
        {
            if (fieldCount == 1 && field[0].IsJoker())
            {
                var spade3 = hand.FirstOrDefault(c => c.Suit == Suit.Spade && c.Rank == 3);
                if (spade3 != null)
                {
                    return new List<Card> { spade3 };
                }
                return new List<Card>();
            }

            var fieldStrength = GetCardStrength(fieldRank);

            var candidates = hand
                .GroupBy(c => c.Rank)
                .Where(g => g.Count() >= fieldCount && GetCardStrength(g.Key) > fieldStrength)
                .OrderBy(g => GetCardStrength(g.Key))
                .FirstOrDefault();

            return candidates?.Take(fieldCount).ToList() ?? new List<Card>();
        }
        else
        {
        }
        return new List<Card>();
    }

    private List<List<Card>> FindStairSequences(List<Card> hand)
    {
        List<List<Card>> stairs = new();
        var suits = hand.GroupBy(c => c.Suit);

        foreach (var s in suits)
        {
            var suitCards = s.OrderBy(c => c.Rank).ToList();
            List<Card> current = new();

            for (int i = 0; i < suitCards.Count; i++)
            {
                if (current.Count == 0)
                {
                    current.Add(suitCards[i]);
                }
                else
                {
                    if (suitCards[i].Rank == current.Last().Rank + 1)
                    {
                        current.Add(suitCards[i]);
                    }
                    else
                    {
                        if (current.Count >= 3) stairs.Add(new List<Card>(current));
                        current.Clear();
                        current.Add(suitCards[i]);
                    }
                }
            }
            if (current.Count >= 3) stairs.Add(new List<Card>(current));
        }
        return stairs;
    }

    private bool IsStair(List<Card> cards)
    {
        if (cards == null || cards.Count < 3) return false;

        var suit = cards[0].Suit;
        if (cards.Any(c => c.Suit != suit)) return false;

        var sorted = cards.OrderBy(c => c.Rank).ToList();

        if (sorted.All(c => c.Rank == sorted[0].Rank)) return false;

        for (int i = 1; i < sorted.Count; i++)
            if (sorted[i].Rank != sorted[i - 1].Rank + 1) return false;

        return true;
    }

    private (List<Card> realCards, int jokerCount) GetRealCardsAndJokers(List<Card> cards)
    {
        var realCards = cards.Where(c => !c.IsJoker()).ToList();
        int jokerCount = cards.Count - realCards.Count;
        return (realCards, jokerCount);
    }

    public HumanPlayer humanPlayer => human;

    void Start()
    {
        InitPlayers();

        if (cpuPlayers.Count > 0) cpuPlayers[0].handArea = handAreaCPU1;
        if (cpuPlayers.Count > 1) cpuPlayers[1].handArea = handAreaCPU2;
        if (cpuPlayers.Count > 2) cpuPlayers[2].handArea = handAreaCPU3;

        human.handArea = handAreaPlayer;

        DealInitialCards();

        CreatePlayerCardSlots(human.Hand.Count);
        PopulatePlayerHand(human);

        StartTurn();

        passButton.onClick.AddListener(OnPassButton);

        players = new List<PlayerBase> { humanPlayer };
        players.AddRange(cpuPlayers);

        remainingPlayers = new List<PlayerBase>(players);
        currentGameCount = 1;

        // ★各ルールのインスタンスを保持
        eightCutRule = new EightCutRule();
        revolutionRule = new RevolutionRule();
        elevenBackRule = new ElevenBackRule();
        fiveSkipRule = new FiveSkipRule();
        sevenPassRule = new SevenPassRule();
        tenDiscardRule = new TenDiscardRule();

        rules.Add(eightCutRule);
        rules.Add(revolutionRule);
        rules.Add(elevenBackRule);
        rules.Add(fiveSkipRule);
        rules.Add(sevenPassRule);
        rules.Add(tenDiscardRule);
    }

    void Update()
    {
        if (playButton != null && passButton != null)
        {
            UpdateButtonVisibility();
        }
    }

    private void UpdateButtonVisibility()
    {
        if (!isPlayerTurn)
        {
            if (playButton != null) playButton.gameObject.SetActive(false);
            if (passButton != null) passButton.gameObject.SetActive(false);
            return;
        }

        if (playButton != null)
        {
            playButton.gameObject.SetActive(true);

            if (isSevenPassMode || isTenDiscardMode)
            {
                if (playButton != null)
                {
                    playButton.gameObject.SetActive(true);

                    int selectedCount = human.SelectCards(human.Hand).Count;
                    int required = Mathf.Min(pendingActionCardCount, human.Hand.Count);

                    playButton.interactable = (selectedCount >= 0 && selectedCount <= required);
                }
                if (passButton != null)
                {
                    passButton.gameObject.SetActive(true);
                    passButton.interactable = true;
                }
                return;
            }

            if (playButton != null)
            {
                playButton.gameObject.SetActive(true);
                var selected = human.SelectCards(human.Hand);
                if (selected.Count > 0)
                {
                    playButton.interactable = IsValidPlay(human.Hand, selected, lastPlayedCards);
                }
                else
                {
                    playButton.interactable = false;
                }
            }

            if (passButton != null)
            {
                bool isFieldEmpty = (lastPlayedCards == null || lastPlayedCards.Count == 0);
                passButton.gameObject.SetActive(!isFieldEmpty);
            }
        }
    }

    private void CreatePlayerCardSlots(int slotCount)
    {
        foreach (Transform child in handAreaPlayer)
            if (child.GetComponent<CardSlot>() != null) Destroy(child.gameObject);

        playerCardSlots.Clear();

        float spacing = 50f;
        float startX = -(slotCount - 1) * spacing / 2f;

        for (int i = 0; i < slotCount; i++)
        {
            var slotObj = Instantiate(cardSlotPrefab, handAreaPlayer);
            var rect = slotObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(80, 120);
            rect.anchoredPosition = new Vector2(startX + i * spacing, 0);
            playerCardSlots.Add(slotObj.GetComponent<CardSlot>());
        }

        Debug.Log($"[CreatePlayerCardSlots] スロット生成: {playerCardSlots.Count}個");
    }

    void InitPlayers()
    {
        human = new HumanPlayer { Name = "You" };
        cpuPlayers.Clear();
        for (int i = 0; i < 3; i++)
        {
            cpuPlayers.Add(new CpuPlayer { Name = "CPU " + (i + 1) });
        }
    }

    void DealInitialCards()
    {
        var deck = CreateDeck();
        Shuffle(deck);

        int index = 0;
        while (deck.Count > 0)
        {
            if (index % 4 == 0) human.Hand.Add(deck[0]);
            else cpuPlayers[index % 4 - 1].Hand.Add(deck[0]);
            deck.RemoveAt(0);
            index++;
        }

        PopulateCpuHandAsBack(handAreaCPU1, cpuPlayers[0].Hand.Count);
        PopulateCpuHandAsBack(handAreaCPU2, cpuPlayers[1].Hand.Count);
        PopulateCpuHandAsBack(handAreaCPU3, cpuPlayers[2].Hand.Count);
    }

    List<Card> CreateDeck()
    {
        var deck = new List<Card>();
        Suit[] suits = { Suit.Spade, Suit.Heart, Suit.Diamond, Suit.Club };

        for (int r = 3; r <= 15; r++)
            foreach (var s in suits)
                deck.Add(new Card
                {
                    Suit = s,
                    Rank = r,
                    SpritePath = $"Images/{s}_{r}"
                });
        deck.Add(Card.CreateJoker());

        return deck;
    }

    void Shuffle(List<Card> deck)
    {
        for (int i = 0; i < deck.Count; i++)
        {
            int rand = Random.Range(i, deck.Count);
            (deck[i], deck[rand]) = (deck[rand], deck[i]);
        }
    }

    public void PopulatePlayerHand(HumanPlayer player)
    {
        Debug.Log($"[PopulatePlayerHand] 呼ばれた / 手札枚数: {player.Hand.Count}");

        foreach (Transform child in handAreaPlayer)
            if (child.GetComponent<CardView>() != null) Destroy(child.gameObject);

        player.Hand.Sort((a, b) => a.Rank.CompareTo(b.Rank));

        List<Card> playableCards;

        if (isSevenPassMode || isTenDiscardMode)
        {
            playableCards = new List<Card>(player.Hand);
        }
        else
        {
            var tableCards = (lastPlayedCards == null || lastPlayedCards.Count == 0) ? null : lastPlayedCards;
            playableCards = GetLegalCardsForUI(player.Hand, tableCards);
        }

        for (int i = 0; i < player.Hand.Count; i++)
        {
            var card = player.Hand[i];

            var go = Instantiate(cardPrefab);
            go.transform.SetParent(playerCardSlots[i].transform, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;

            var cv = go.GetComponent<CardView>();
            cv.backSprite = cardBackSprite;
            cv.SetCard(card);

            bool canPlay = playableCards.Contains(card);
            cv.SetPlayable(canPlay);
        }
    }

    public void PopulateCpuHandAsBack(Transform cpuArea, int cardCount)
    {
        foreach (Transform child in cpuArea) Destroy(child.gameObject);

        bool isSide = (cpuArea == handAreaCPU1 || cpuArea == handAreaCPU3);
        bool isCpu2 = (cpuArea == handAreaCPU2);

        for (int i = 0; i < cardCount; i++)
        {
            var go = Instantiate(cardPrefab, cpuArea);
            var cv = go.GetComponent<CardView>();
            cv.backSprite = cardBackSprite;
            cv.SetFaceDown();

            if (isSide)
            {
                var rect = go.GetComponent<RectTransform>();
                rect.localRotation = Quaternion.Euler(0, 0, 90f);
            }
            else if (isCpu2)
            {
                RectTransform rect = go.GetComponent<RectTransform>();
                rect.localRotation = Quaternion.Euler(0, 0, 180f);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(cpuArea.GetComponent<RectTransform>());
        }
    }
    private IEnumerator PlayerPlayRoutine(List<Card> played)
    {
        yield return StartCoroutine(DisplayPlayedCardsOnTable(human, played));

        if (isSevenPassMode || isTenDiscardMode)
        {
            yield break;
        }

        if (lastPlayedCards.Count == 0 && !skipTurnAdvance)
        {
            yield break;
        }

        EndTurn();
    }

    public void OnPlayButton()
    {
        if (!isPlayerTurn) return;

        // ★7渡しモード時の処理をSevenPassRuleに委譲
        if (isSevenPassMode)
        {
            var selected = human.SelectCards(human.Hand);
            int required = Mathf.Min(pendingActionCardCount, human.Hand.Count);

            if (selected.Count != required)
            {
                EnqueueMessage($"{required}枚 選んでください");
                return;
            }

            playButton.interactable = false;
            StartCoroutine(sevenPassRule.ExecuteSevenPassTransfer(this, human, selected));
            return;
        }

        // ★10捨てモード時の処理をTenDiscardRuleに委譲
        if (isTenDiscardMode)
        {
            var selected = human.SelectCards(human.Hand);
            int maxAllowed = pendingActionCardCount;

            if (selected.Count > maxAllowed)
            {
                EnqueueMessage($"最大 {maxAllowed}枚 選んでください");
                return;
            }

            // 0枚選択も許可するため、selected.Count == required のチェックは不要。
            // ここに到達した時点で、選択枚数は 0 <= selected.Count <= maxAllowed である。

            playButton.interactable = false;
            // 選択されたカードリストを渡す
            StartCoroutine(tenDiscardRule.ExecuteTenDiscardAction(this, human, selected));
            return;
        }

        if (playButton != null && !playButton.interactable) return;
        if (playButton != null) playButton.interactable = false;

        var played = human.SelectCards(human.Hand);

        if (played == null || played.Count == 0)
        {
            Debug.Log("カードが選択されていません。");
            if (playButton != null) playButton.interactable = true;
            return;
        }

        if (!IsValidPlay(human.Hand, played, lastPlayedCards))
        {
            Debug.Log("そのカードは出せません。");
            if (playButton != null) playButton.interactable = true;
            return;
        }

        played = played.OrderBy(c => c.Rank).ThenBy(c => c.Suit).ToList();

        StartCoroutine(PlayerPlayRoutine(played));
    }

    private bool IsValidPlay(List<Card> hand, List<Card> selected, List<Card> field)
    {
        if (selected == null || selected.Count == 0) return false;

        var (realSelected, jokerCount) = GetRealCardsAndJokers(selected);

        bool isRankGroup = false;
        bool isStair = false;

        if (realSelected.Count == 0)
        {
            isRankGroup = (jokerCount > 0);
        }
        else
        {
            isRankGroup = realSelected.All(c => c.Rank == realSelected[0].Rank);
            isStair = IsStairWithJoker(realSelected, jokerCount);
        }

        if (!isRankGroup && !isStair)
        {
            return false;
        }

        if (field != null && field.Count > 0)
        {
            if (field.Count == 1 && field[0].IsJoker())
            {
                if (selected.Count == 1 && selected[0].Suit == Suit.Spade && selected[0].Rank == 3)
                {
                    return true;
                }
            }

            if (selected.Count != field.Count) return false;

            var (realField, fieldJokerCount) = GetRealCardsAndJokers(field);
            bool fieldIsStair = IsStairWithJoker(realField, fieldJokerCount);

            if (fieldIsStair != isStair) return false;

            int selectedStrongestRank;
            int fieldStrongestRank;

            if (isStair)
            {
                selectedStrongestRank = GetStairMaxRank(realSelected, jokerCount);
                fieldStrongestRank = GetStairMaxRank(realField, fieldJokerCount);
            }
            else
            {
                if (realField.Count == 0 && fieldJokerCount > 0)
                {
                    fieldStrongestRank = 16;
                }
                else
                {
                    fieldStrongestRank = realField.Count > 0 ? realField[0].Rank : 3;
                }

                if (realSelected.Count == 0 && jokerCount > 0)
                {
                    selectedStrongestRank = 16;
                }
                else
                {
                    selectedStrongestRank = realSelected.Count > 0 ? realSelected[0].Rank : 3;
                }
            }

            int fieldStrength = GetCardStrength(fieldStrongestRank);
            int selectedStrength = GetCardStrength(selectedStrongestRank);

            if (selectedStrength <= fieldStrength) return false;
        }

        return true;
    }

    private bool IsStairWithJoker(List<Card> realCards, int jokerCount)
    {
        int totalCards = realCards.Count + jokerCount;
        if (totalCards < 3 || totalCards > 4) return false;

        if (realCards.Count == 0)
        {
            return jokerCount >= 3;
        }

        if (realCards.Select(c => c.Suit).Distinct().Count() > 1)
        {
            return false;
        }

        var sortedRanks = realCards.OrderBy(c => c.Rank).Select(c => c.Rank).Distinct().ToList();

        int requiredJokers = 0;

        for (int i = 0; i < sortedRanks.Count - 1; i++)
        {
            int gap = sortedRanks[i + 1] - sortedRanks[i] - 1;
            if (gap < 0) return false;
            requiredJokers += gap;
        }

        if (jokerCount < requiredJokers)
        {
            return false;
        }

        int remainingJokers = jokerCount - requiredJokers;
        int realStairLength = sortedRanks.Count;
        int finalStairLength = realStairLength + requiredJokers + remainingJokers;

        return finalStairLength >= 3 && finalStairLength <= 4;
    }

    private int GetStairMaxRank(List<Card> realCards, int jokerCount)
    {
        if (realCards.Count == 0)
        {
            return 15;
        }

        int maxRealRank = realCards.Max(c => c.Rank);
        return maxRealRank + jokerCount;
    }

    private void OnPassButton()
    {
        if (players[currentTurnIndex] != humanPlayer) return;

        // ★修正点 1: 特殊アクションモード中はすぐにパスボタンを非活性化する
        if (isSevenPassMode || isTenDiscardMode)
        {
            passButton.interactable = false;
            playButton.interactable = false;
        }

        if (isSevenPassMode)
        {
            // 7渡しモードでパス（カードをあげない）
            playButton.interactable = false;
            // 0枚のリストを渡して ExecuteSevenPassTransfer を実行
            StartCoroutine(sevenPassRule.ExecuteSevenPassTransfer(this, human, new List<Card>()));
            return;
        }

        if (isTenDiscardMode)
        {
            // 10捨てモードでパス（カードを捨てない）
            playButton.interactable = false;
            // 0枚のリストを渡して ExecuteTenDiscardAction を実行
            StartCoroutine(tenDiscardRule.ExecuteTenDiscardAction(this, human, new List<Card>()));
            return;
        }

        // 通常のターンでのパス処理
        HandlePass();
    }

    private IEnumerator DisplayPlayedCardsOnTable(PlayerBase currentPlayer, List<Card> played)
    {
        float spacing = 20f;
        int existingCards = tableArea.childCount;
        Vector3 basePos = tableArea.position;
        float startX = basePos.x - (played.Count - 1) * spacing / 2f;

        Transform sourceArea = null;
        if (currentPlayer is HumanPlayer) sourceArea = handAreaPlayer;
        else if (currentPlayer == cpuPlayers[0]) sourceArea = handAreaCPU1;
        else if (currentPlayer == cpuPlayers[1]) sourceArea = handAreaCPU2;
        else if (currentPlayer == cpuPlayers[2]) sourceArea = handAreaCPU3;

        if (sourceArea == null)
        {
            Debug.LogWarning("手札エリアが見つかりません: " + currentPlayer);
            yield break;
        }

        List<CardView> allCardViews = sourceArea.GetComponentsInChildren<CardView>().ToList();
        var playedViews = new List<CardView>();

        List<Card> fieldBeforePlay = new List<Card>(lastPlayedCards);

        for (int i = 0; i < played.Count; i++)
        {
            Card card = played[i];
            CardView cv = allCardViews.FirstOrDefault(v => v.CardData == card);

            if (cv == null && !(currentPlayer is HumanPlayer))
            {
                cv = allCardViews.FirstOrDefault(v => v.CardData == null);
            }

            if (cv == null)
            {
                Debug.LogWarning($"カードビューが見つかりません: {card}");
                continue;
            }

            cv.SetCard(card);

            RectTransform rt = sourceArea as RectTransform;
            Vector3 startPos = sourceArea.position;
            if (rt != null && rt.childCount > 0)
            {
                var lastCard = rt.GetChild(rt.childCount - 1);
                startPos = lastCard.position;
            }

            Vector3 endPos = new Vector3(startX + spacing * i, basePos.y, basePos.z);

            cv.transform.SetParent(tableArea.parent, true);

            if (!(currentPlayer is HumanPlayer))
            {
                if (sourceArea.childCount > 0)
                {
                    Transform removeTarget = null;
                    foreach (Transform t in sourceArea)
                    {
                        CardView tmp = t.GetComponent<CardView>();
                        if (tmp != null && tmp.CardData == card)
                        {
                            removeTarget = t;
                            break;
                        }
                    }
                    if (removeTarget != null) Destroy(removeTarget.gameObject);
                }
            }

            float duration = 0.4f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                cv.transform.position = Vector3.Lerp(startPos, endPos, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            cv.transform.position = endPos;

            playedViews.Add(cv);
        }

        yield return new WaitForSeconds(0.05f);

        foreach (var cv in playedViews)
        {
            if (cv == null) continue;
            cv.transform.SetParent(tableArea, true);
            cv.transform.localScale = Vector3.one * 2f;
            float randomRot = Random.Range(-6f, 6f);
            cv.transform.localRotation = Quaternion.Euler(0, 0, randomRot);
            cv.transform.localPosition += new Vector3(0, 0, existingCards * -2f);
            cv.DisableInteraction();
        }

        if (currentPlayer is HumanPlayer)
        {
            foreach (var c in played) human.Hand.Remove(c);
            RemoveCardsFromPlayerUI(played);
        }
        CheckForWin(currentPlayer);
        if (isGameOver) yield break;

        lastPlayedCards = new List<Card>(played);
        lastPlayedPlayerIndex = players.IndexOf(currentPlayer);
        passCount = 0;

        bool isSpade3Counter = false;

        if (fieldBeforePlay.Count == 1 && fieldBeforePlay[0].IsJoker())
        {
            if (played.Count == 1 && played[0].Suit == Suit.Spade && played[0].Rank == 3)
            {
                isSpade3Counter = true;
            }
        }

        if (isSpade3Counter)
        {
            EnqueueMessage("スペード3返し！場が流れます。");
            isTempRevolution = false;

            yield return new WaitForSeconds(1.0f);
            yield return StartCoroutine(ClearTableAndRestart());
            yield break;
        }

        List<Card> effectivePlayedCards = GetEffectivePlayedCards(played);
        var state = new GameState(new List<Card>(lastPlayedCards), currentTurnIndex);

        foreach (var rule in rules)
        {
            if (rule.CanApply(effectivePlayedCards, state))
            {
                rule.Apply(effectivePlayedCards, state);
            }
        }

        // ★各ルールの処理を委譲
        if (state.TriggerRevolution)
        {
            revolutionRule.ExecuteRevolution(this, ref isRevolution);
        }

        if (state.IsElevenBack)
        {
            elevenBackRule.ExecuteElevenBack(this, ref isTempRevolution);
        }

        if (state.SkipCount > 0)
        {
            pendingSkipCount = state.SkipCount;
            fiveSkipRule.ExecuteFiveSkip(this, state.SkipCount);
        }

        bool eightCutTriggered = state.IsEightCut;

        if (eightCutTriggered)
        {
            yield return StartCoroutine(eightCutRule.ExecuteEightCut(this, tableArea, currentPlayer));
            yield break;
        }

        if (state.SevenPassCount > 0 && remainingPlayers.Contains(currentPlayer))
        {
            Debug.Log($"7渡しシーケンス開始: {state.SevenPassCount}枚");
            yield return new WaitForSeconds(1.0f);
            yield return StartCoroutine(sevenPassRule.StartSevenPassSequence(this, currentPlayer, state.SevenPassCount));
            // ★修正: 7渡しアクションが完了したので、DisplayPlayedCardsOnTableの残りの処理（EndTurnなど）をスキップする
            yield break; //
        }

        if (state.TenDiscardCount > 0 && remainingPlayers.Contains(currentPlayer))
        {
            Debug.Log($"10捨てシーケンス開始: {state.TenDiscardCount}枚");
            yield return new WaitForSeconds(1.0f);
            yield return StartCoroutine(tenDiscardRule.StartTenDiscardSequence(this, currentPlayer, state.TenDiscardCount));
            // ★修正: 10捨てアクションが完了したので、DisplayPlayedCardsOnTableの残りの処理（EndTurnなど）をスキップする
            yield break; //
        }
    }

    private void HandlePass()
    {
        passCount++;

        if (passCount >= remainingPlayers.Count - 1)
        {
            StartCoroutine(ClearTableAndRestart());
        }
        else
        {
            EndTurn();
        }
    }

    private IEnumerator ClearTableAndRestart()
    {
        yield return new WaitForSeconds(0.6f);

        foreach (Transform child in tableArea) Destroy(child.gameObject);

        lastPlayedCards.Clear();
        passCount = 0;

        isTempRevolution = false;

        pendingSkipCount = 0;
        skipTurnAdvance = false;

        if (lastPlayedPlayerIndex < 0) lastPlayedPlayerIndex = 0;

        PlayerBase lastPlayer = players[lastPlayedPlayerIndex];

        if (remainingPlayers.Contains(lastPlayer))
        {
            currentTurnIndex = lastPlayedPlayerIndex;
        }
        else
        {
            int nextIdx = (lastPlayedPlayerIndex + 1) % players.Count;
            while (!remainingPlayers.Contains(players[nextIdx]))
            {
                nextIdx = (nextIdx + 1) % players.Count;
            }
            currentTurnIndex = nextIdx;
        }

        yield return new WaitForSeconds(0.6f);
        StartTurn();
    }

    public IEnumerator ShowMessage(string message, float duration = 2f)
    {
        if (passMessageText == null)
        {
            Debug.LogWarning("passMessageText が未設定です。Canvas上のテキストをアサインしてください。");
            yield break;
        }

        passMessageText.text = message;
        passMessageText.gameObject.SetActive(true);

        CanvasGroup cg = passMessageText.GetComponent<CanvasGroup>();
        if (cg == null) cg = passMessageText.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        float t = 0f;
        while (t < 0.3f)
        {
            cg.alpha = Mathf.Lerp(0, 1, t / 0.3f);
            t += Time.deltaTime;
            yield return null;
        }
        cg.alpha = 1f;

        yield return new WaitForSeconds(duration);

        t = 0f;
        while (t < 0.5f)
        {
            cg.alpha = Mathf.Lerp(1, 0, t / 0.5f);
            t += Time.deltaTime;
            yield return null;
        }

        passMessageText.gameObject.SetActive(false);
    }

    public void EnqueueMessage(string message)
    {
        messageQueue.Enqueue(message);
        if (!isShowingMessage) StartCoroutine(ProcessMessageQueue());
    }

    private IEnumerator ProcessMessageQueue()
    {
        isShowingMessage = true;

        while (messageQueue.Count > 0)
        {
            if (isSevenPassMode || isTenDiscardMode)
            {
                yield return null;
                continue;
            }

            string message = messageQueue.Dequeue();

            if (passMessageText == null)
            {
                Debug.LogWarning("passMessageText が未設定です。");
                yield break;
            }

            passMessageText.text = message;
            passMessageText.gameObject.SetActive(true);

            CanvasGroup cg = passMessageText.GetComponent<CanvasGroup>();
            if (cg == null) cg = passMessageText.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            float t = 0f;
            while (t < 0.3f)
            {
                cg.alpha = Mathf.Lerp(0, 1, t / 0.3f);
                t += Time.deltaTime;
                yield return null;
            }
            cg.alpha = 1f;

            yield return new WaitForSeconds(1.5f);

            t = 0f;
            while (t < 0.5f)
            {
                cg.alpha = Mathf.Lerp(1, 0, t / 0.5f);
                t += Time.deltaTime;
                yield return null;
            }

            passMessageText.gameObject.SetActive(false);
        }

        isShowingMessage = false;
    }

    private List<Card> GetLegalCardsForUI(List<Card> hand, List<Card> field)
    {
        if (field == null || field.Count == 0)
        {
            return new List<Card>(hand);
        }

        List<Card> playable = new List<Card>();
        int fieldCount = field.Count;

        int fieldStrongestRank;
        if (field.Count == 1 && field[0].IsJoker())
        {
            fieldStrongestRank = 16;
        }
        else
        {
            fieldStrongestRank = field.Max(c => c.Rank);
        }

        int fieldStrength = GetCardStrength(fieldStrongestRank);

        bool isFieldStair = IsStair(field);

        if (!isFieldStair)
        {
            if (field.Count == 1 && field[0].IsJoker())
            {
                var spade3 = hand.FirstOrDefault(c => c.Suit == Suit.Spade && c.Rank == 3);
                if (spade3 != null) playable.Add(spade3);
            }

            var groups = hand.GroupBy(c => c.Rank);

            foreach (var g in groups)
            {
                if (g.Count() >= fieldCount)
                {
                    if (GetCardStrength(g.Key) > fieldStrength)
                    {
                        playable.AddRange(g);
                    }
                }
            }
        }
        else
        {
            var stairs = FindStairSequences(hand);
            foreach (var seq in stairs)
            {
                if (seq.Count != fieldCount) continue;
                if (seq[0].Suit != field[0].Suit) continue;

                int seqStrongestRank = seq.Max(c => c.Rank);
                if (GetCardStrength(seqStrongestRank) > fieldStrength)
                {
                    playable.AddRange(seq);
                }
            }
        }

        return playable;
    }

    private List<Card> GetEffectivePlayedCards(List<Card> original)
    {
        if (original == null || original.Count == 0) return new List<Card>();

        var realCards = original.Where(c => !c.IsJoker()).OrderBy(c => c.Rank).ToList();
        int jokerCount = original.Count - realCards.Count;

        if (jokerCount == 0) return new List<Card>(original);

        if (realCards.Count == 0)
        {
            var list = new List<Card>();
            for (int i = 0; i < jokerCount; i++)
            {
                list.Add(new Card { Rank = 15, Suit = Suit.Spade });
            }
            return list;
        }

        bool isGroup = realCards.All(c => c.Rank == realCards[0].Rank);
        if (isGroup)
        {
            var list = new List<Card>(realCards);
            int rank = realCards[0].Rank;
            for (int i = 0; i < jokerCount; i++)
            {
                list.Add(new Card { Rank = rank, Suit = realCards[0].Suit });
            }
            return list;
        }

        var effectiveList = new List<Card>();
        int currentRank = realCards[0].Rank;
        effectiveList.Add(realCards[0]);

        int realIndex = 1;
        int usedJokers = 0;

        while (realIndex < realCards.Count)
        {
            int nextRealRank = realCards[realIndex].Rank;
            int gap = nextRealRank - currentRank - 1;

            if (gap > 0)
            {
                for (int k = 0; k < gap; k++)
                {
                    if (usedJokers < jokerCount)
                    {
                        currentRank++;
                        effectiveList.Add(new Card { Rank = currentRank, Suit = realCards[0].Suit });
                        usedJokers++;
                    }
                }
            }

            currentRank = nextRealRank;
            effectiveList.Add(realCards[realIndex]);
            realIndex++;
        }

        while (usedJokers < jokerCount)
        {
            currentRank++;
            effectiveList.Add(new Card { Rank = currentRank, Suit = realCards[0].Suit });
            usedJokers++;
        }

        return effectiveList;
    }

    private IEnumerator EndGameRoutine()
    {
        yield return new WaitForSeconds(2.0f);
        EnqueueMessage($"--- 第{currentGameCount}戦 終了 ---");

        yield return new WaitForSeconds(3.0f);

        currentGameCount++;

        if (currentGameCount <= TotalGames)
        {
            EnqueueMessage($"第{currentGameCount}戦を開始します");
            yield return StartCoroutine(PrepareNextRound());
        }
        else
        {
            EnqueueMessage("全4戦終了！お疲れ様でした！");
        }
    }

    private IEnumerator PrepareNextRound()
    {
        isGameOver = false;
        isRevolution = false;
        isTempRevolution = false;
        currentRank = 1;
        gameRanks.Clear();
        passCount = 0;
        lastPlayedCards.Clear();

        foreach (Transform child in tableArea) Destroy(child.gameObject);

        remainingPlayers = new List<PlayerBase>(players);

        foreach (var p in players) p.Hand.Clear();
        DealInitialCards();

        CreatePlayerCardSlots(human.Hand.Count);
        PopulatePlayerHand(human);

        currentTurnIndex = 0;

        yield return new WaitForSeconds(1.0f);
        StartTurn();
    }
    /// <summary>
    /// あがっていないプレイヤーのリストを取得します。
    /// </summary>
    public List<PlayerBase> GetRemainingPlayers()
    {
        return remainingPlayers;
    }

    /// <summary>
    /// 現在のターンプレイヤーのインデックスを設定します。
    /// </summary>
    public void SetCurrentTurnIndex(int index)
    {
        // インデックスが有効な範囲内かチェックを入れるとより安全です
        if (index >= 0 && index < players.Count)
        {
            currentTurnIndex = index;
        }
    }
    

}