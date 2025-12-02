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

    // ターン管理用変数
    private int currentTurnIndex = 0;
    private bool isPlayerTurn = true;

    private List<IRule> rules = new List<IRule>();
    private bool skipTurnAdvance = false;

    // ★ 革命状態フラグ
    private bool isRevolution = false;
    // ★ 一時的な11バック状態フラグ
    private bool isTempRevolution = false;

    // 現在の「強さ」計算プロパティ
    // 革命中または11バック中なら、強さが逆になる
    private bool IsRevolutionActive => isRevolution ^ isTempRevolution; // XOR: どっちか片方なら革命、両方なら通常
    // ★ カードの強さを数値化するメソッド
    private int pendingSkipCount = 0;

    private bool isSevenPassMode = false;   // 7渡しモード中か
    private bool isTenDiscardMode = false;  // 10捨てモード中か
    private int pendingActionCardCount = 0; // 渡す/捨てる枚数
    public int GetCardStrength(int rank)
    {
        // 通常時: 3 < 4 ... < 13(K) < 1(A) < 2 < 16(Joker)
        // 内部データ: 3=3 ... 13=13, 14=A, 15=2 (と仮定)

        // まず基本の強さに変換 (3が最弱=0, 2が最強=12 とするような補正)
        int power = 0;
        if (rank == 15) power = 13; // 2
        else if (rank == 14) power = 12; // A
        else power = rank - 3; // 3 => 0, 4 => 1 ... 13(K) => 10

        // 革命中なら強さを反転 (大きい値ほど弱いことにする)
        if (IsRevolutionActive)
        {
            return -power;
        }
        return power;
    }

    // ================================================
    // --- ターン制管理メソッド ---
    // ================================================

    private void StartTurn()
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

    private void EndTurn()
    {
        int nextTurnIndex;

        if (skipTurnAdvance)
        {
            skipTurnAdvance = false;
            pendingSkipCount = 0;

            // 元のコード: nextTurnIndex = (currentTurnIndex + 1) % players.Count;

            nextTurnIndex = currentTurnIndex;


            currentTurnIndex = nextTurnIndex;

            StartCoroutine(NextTurnDelay());
            return; // 通常のターン進行ロジックへ移行しない
        }

        // --- 以下、通常のターン進行 ---
        nextTurnIndex = (currentTurnIndex + 1 + pendingSkipCount) % players.Count;

        // もし一周回って自分に戻ってきた場合（3枚出しスキップなど）
        if (pendingSkipCount > 0 && nextTurnIndex == currentTurnIndex)
        {
            EnqueueMessage("全員スキップ!場が流れ、もう一度自分の番です。");
            StartCoroutine(ClearTableAndRestart());
            return;
        }

        currentTurnIndex = nextTurnIndex;
        pendingSkipCount = 0;

        StartCoroutine(NextTurnDelay());
    }

    private IEnumerator CpuPlayCards(PlayerBase cpu)
    {
        List<Card> cardsToPlay = cpu.SelectCards(cpu.HandCards);
        if (cardsToPlay == null || cardsToPlay.Count == 0) yield break;

        foreach (Card card in cardsToPlay)
        {
            CardView cardView = FindCardViewForCard(card, cpu);
            if (cardView != null)
            {
                Vector3 targetPos = tableArea.position;
                yield return StartCoroutine(cardView.MoveTo(targetPos, 0.4f));

                cardView.transform.SetParent(tableArea);
                cardView.transform.localPosition = new Vector3(0, 0, -cpu.Hand.Count * 0.01f);
            }
        }

        foreach (Card card in cardsToPlay) cpu.HandCards.Remove(card);

        yield return new WaitForSeconds(0.5f);
    }

    private CardView FindCardViewForCard(Card card, PlayerBase player)
    {
        CardView[] allCards = FindObjectsOfType<CardView>();
        foreach (CardView cv in allCards)
            if (cv.CardData == card && cv.transform.parent == player.handArea)
                return cv;
        return null;
    }

    // プレイヤーのカード選択状態リセット
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

    // ================================================
    // --- CPUのターン処理 ---
    // ================================================
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

        // （EndTurn() はカードを選び終わった後の Execute...Action で呼ばれるため）
        if (isSevenPassMode || isTenDiscardMode)
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
            var fieldStrength = GetCardStrength(fieldRank);

            var candidates = hand
                .GroupBy(c => c.Rank)
                .Where(g => g.Count() >= fieldCount && GetCardStrength(g.Key) > fieldStrength) // ★ここ修正
                .OrderBy(g => GetCardStrength(g.Key)) // ★弱い順に出す
                .FirstOrDefault();

            return candidates?.Take(fieldCount).ToList() ?? new List<Card>();
        }
        else
        {
            // 階段の場合の革命対応は少し複雑ですが、基本は「一番強いカード」の比較
            // ここでは簡易的に Rank の大小だけで比較してしまっている既存コードだと革命時バグります。
            // 階段の革命対応まで厳密にやるならここも修正が必要です。
        }
        return new List<Card>();
    }

    // ================================================
    // --- 手札内から階段（連番）候補を探す ---
    // ================================================
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

    // ================================================
    // --- 階段（連番）判定 ---
    // ================================================
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

        //特殊ルール
        rules.Add(new EightCutRule());
        rules.Add(new RevolutionRule()); 
        rules.Add(new ElevenBackRule()); 
        rules.Add(new FiveSkipRule());
        rules.Add(new SevenPassRule());
        rules.Add(new TenDiscardRule());
    }
    void Update()
    {
        // ゲームが進行中でボタンの設定がある場合のみ実行
        if (playButton != null && passButton != null)
        {
            UpdateButtonVisibility();
        }
    }

    // ボタンの表示/非表示を管理するメソッド
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

            // --- 以下、自分のターンの処理 ---

            // ================================================================
            // ★ 追記: 特殊モードのボタン制御
            // ================================================================
            if (isSevenPassMode || isTenDiscardMode)
            {
                if (playButton != null)
                {
                    playButton.gameObject.SetActive(true);

                    int selectedCount = human.SelectCards(human.Hand).Count;
                    int required = Mathf.Min(pendingActionCardCount, human.Hand.Count);

                    // 選択枚数が指定枚数と一致した時のみボタン有効
                    playButton.interactable = (selectedCount == required);
                }
                if (passButton != null) passButton.gameObject.SetActive(false);
                return;
            }
            // ================================================================

            // 2. プレイボタンの制御 (通常時)
            if (playButton != null)
            {
                playButton.gameObject.SetActive(true);

                // 通常モードでは、役が成立している場合のみボタン有効にするロジックをここに書く
                var selected = human.SelectCards(human.Hand);
                if (selected.Count > 0)
                {
                    playButton.interactable = IsValidPlay(human.Hand, selected, lastPlayedCards);
                }
                else
                {
                    // 何も選んでいなければボタン無効
                    playButton.interactable = false;
                }
            }

            // 3. パスボタンの制御
            // 場にカードがない（null または 0枚）＝ 自分が親（最初に出す人）
            // 親ならパスできないので非表示、それ以外（場にカードがある）なら表示
            if (passButton != null)
            {
                bool isFieldEmpty = (lastPlayedCards == null || lastPlayedCards.Count == 0);
                passButton.gameObject.SetActive(!isFieldEmpty);
            }
        }
    }

    // 手札の中に「選択状態」のカードがあるかチェックする
    private bool IsAnyCardSelected()
    {
        foreach (Transform child in handAreaPlayer)
        {
            var cv = child.GetComponent<CardView>();
            // 注意: CardViewスクリプトに IsSelected プロパティ(bool)がある前提です
            if (cv != null && cv.IsSelected)
            {
                return true;
            }
        }
        return false;
    }

    // 手札スロットを自動生成
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

    // プレイヤー初期化
    void InitPlayers()
    {
        human = new HumanPlayer { Name = "You" };
        cpuPlayers.Clear();
        for (int i = 0; i < 3; i++)
        {
            cpuPlayers.Add(new CpuPlayer { Name = "CPU " + (i + 1) });
        }
    }

    // デッキ作成と配布
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

    // デッキ生成（3～15 + Joker）
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

    // シャッフル
    void Shuffle(List<Card> deck)
    {
        for (int i = 0; i < deck.Count; i++)
        {
            int rand = Random.Range(i, deck.Count);
            (deck[i], deck[rand]) = (deck[rand], deck[i]);
        }
    }

    // 手札UI生成
    public void PopulatePlayerHand(HumanPlayer player)
    {
        Debug.Log($"[PopulatePlayerHand] 呼ばれた / 手札枚数: {player.Hand.Count}");

        foreach (Transform child in handAreaPlayer)
            if (child.GetComponent<CardView>() != null) Destroy(child.gameObject);

        player.Hand.Sort((a, b) => a.Rank.CompareTo(b.Rank));

        // ================================================================
        // ★ 修正箇所: 7渡し/10捨てモードの場合は、全カードを選択可能にする
        // ================================================================
        List<Card> playableCards;

        if (isSevenPassMode || isTenDiscardMode)
        {
            // 特殊アクションモード中は、手札全てが選択可能
            playableCards = new List<Card>(player.Hand);
        }
        else
        {
            // 通常のプレイモード
            var tableCards = (lastPlayedCards == null || lastPlayedCards.Count == 0) ? null : lastPlayedCards;
            playableCards = GetLegalCardsForUI(player.Hand, tableCards);
        }
        // ================================================================

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
                // CPU2 だけカードを縦向きにする
                RectTransform rect = go.GetComponent<RectTransform>();
                rect.localRotation = Quaternion.Euler(0, 0, 180f);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(cpuArea.GetComponent<RectTransform>());
        }
    }



    private IEnumerator PlayerPlayRoutine(List<Card> played)
    {
        yield return StartCoroutine(DisplayPlayedCardsOnTable(human, played));

        // ★ 修正箇所: 特殊モードが起動したら、EndTurn()を呼ばずに入力待ちに入る
        if (isSevenPassMode || isTenDiscardMode)
        {
            // EndTurn()は特殊アクション完了時（Execute...Actionの最後）に呼ばれるので、
            // ここでは何もしない（プレイヤーの次の入力（OnPlayButton）を待つ）
            yield break;
        }

        EndTurn();
    }

    public void OnPlayButton()
    {
        if (!isPlayerTurn) return;

        // --- ★ 追加: 特殊モードの処理 ---
        if (isSevenPassMode)
        {
            var selected = human.SelectCards(human.Hand);
            // 選択枚数が足りているかチェック
            int required = Mathf.Min(pendingActionCardCount, human.Hand.Count);

            if (selected.Count != required)
            {
                EnqueueMessage($"{required}枚 選んでください");
                return;
            }

            playButton.interactable = false;
            StartCoroutine(ExecuteSevenPassTransfer(human, selected));
            return;
        }

        if (isTenDiscardMode)
        {
            var selected = human.SelectCards(human.Hand);
            int required = Mathf.Min(pendingActionCardCount, human.Hand.Count);

            if (selected.Count != required)
            {
                EnqueueMessage($"{required}枚 選んでください");
                return;
            }

            playButton.interactable = false;
            StartCoroutine(ExecuteTenDiscardAction(human, selected));
            return;
        }
        // ----------------------------------

        // 以下、既存の通常プレイ処理
        if (playButton != null && !playButton.interactable) return;
        if (playButton != null) playButton.interactable = false;

        var played = human.SelectCards(human.Hand); ;

        if (played == null || played.Count == 0)
        {
            // カードが選択されていません
            Debug.Log("カードが選択されていません。");
            if (playButton != null) playButton.interactable = true;
            return;
        }

        if (!IsValidPlay(human.Hand, played, lastPlayedCards))
        {
            Debug.Log("そのカードは出せません。");
            // 出せないカードだった場合は、選び直せるようにボタンを再度有効化する
            if (playButton != null) playButton.interactable = true;
            return;
        }

        // 場に出す前に、数字の小さい順（昇順）に並び替える
        played = played.OrderBy(c => c.Rank).ThenBy(c => c.Suit).ToList();

        // 成功した場合、ボタンは無効のまま処理を進める
        StartCoroutine(PlayerPlayRoutine(played));
    }

    private bool IsValidPlay(List<Card> hand, List<Card> selected, List<Card> field)
    {
        if (selected == null || selected.Count == 0) return false;

        // 1. 選択されたカードをリアルカードとジョーカーに分ける
        var (realSelected, jokerCount) = GetRealCardsAndJokers(selected);

        // --- 1. 自分の出したカード単体でのチェック（役になっているか？） ---

        bool isRankGroup = false;
        bool isStair = false;

        if (realSelected.Count == 0)
        {
            // ジョーカー単独出し（ワイルドカードとして有効）
            // 場が空でなければ、ワイルドカードとして場の枚数と同じ枚数の組み合わせとみなす。
            isRankGroup = (jokerCount > 0);
        }
        else
        {
            // A. 同じ数字の組み合わせか？（ジョーカーが補完できるか？）
            // リアルカードのランクがすべて同じであれば、ジョーカーで補完可能。
            isRankGroup = realSelected.All(c => c.Rank == realSelected[0].Rank);

            // B. 階段（ジョーカーが補完できるか？）
            // ジョーカー込みの階段判定が必要
            isStair = IsStairWithJoker(realSelected, jokerCount);
        }


        // ★ 修正点: ペアでも階段でもないバラバラなカードなら、場が空でも出せないようにする
        if (!isRankGroup && !isStair)
        {
            return false;
        }

        // --- 2. 場に出ているカードとの比較（場にカードがある場合のみ） ---
        if (field != null && field.Count > 0)
        {
            // 枚数チェック
            if (selected.Count != field.Count) return false;

            // 場のリアルカードとジョーカーに分ける
            var (realField, fieldJokerCount) = GetRealCardsAndJokers(field);

            // 場のタイプ判定
            bool fieldIsStair = IsStairWithJoker(realField, fieldJokerCount);

            // 場のタイプと出したカードのタイプが合っているか
            if (fieldIsStair != isStair) return false;


            // 強さチェック
            int selectedStrongestRank;
            int fieldStrongestRank;

            if (isStair) // 階段の強さ比較
            {
                // 階段の強さ比較は、一番強いカードのランクで比較します。
                // ジョーカーは「代わりになったカード」として評価する必要がありますが、
                // 簡易的にリアルカードの最大ランク+ジョーカー枚数で強さを推定します。
                selectedStrongestRank = GetStairMaxRank(realSelected, jokerCount);
                fieldStrongestRank = GetStairMaxRank(realField, fieldJokerCount);
            }
            else // ペア・単体の強さ比較
            {
                // 場のランク（ジョーカーはリアルカードと同じランクとみなす）
                fieldStrongestRank = realField.Count > 0 ? realField[0].Rank : 3; // 場がジョーカーのみなら3とみなす

                // 出したカードのランク
                selectedStrongestRank = realSelected.Count > 0 ? realSelected[0].Rank : 3; // 出したのがジョーカーのみなら3とみなす
            }

            // 最終的な強さ比較
            int fieldStrength = GetCardStrength(fieldStrongestRank);
            int selectedStrength = GetCardStrength(selectedStrongestRank);

            // 同じ強さ以下なら出せない
            if (selectedStrength <= fieldStrength) return false;
        }

        return true;
    }

    private bool IsStairWithJoker(List<Card> realCards, int jokerCount)
    {
        int totalCards = realCards.Count + jokerCount;
        // 階段は最低3枚必要
        if (totalCards < 3) return false;

        // 1. ジョーカーのみで3枚以上なら階段成立（特殊なケース）
        if (realCards.Count == 0)
        {
            return jokerCount >= 3;
        }

        // 2. リアルカードのスートがすべて同じかチェック
        if (realCards.Select(c => c.Suit).Distinct().Count() > 1)
        {
            return false;
        }

        // 3. リアルカードのランクを昇順かつ重複なしで取得 (※int型のリストとして取得できているか重要)
        var sortedRanks = realCards.OrderBy(c => c.Rank).Select(c => c.Rank).Distinct().ToList();

        // 4. リアルカードの間隔と、連番の合計長をチェック
        int requiredJokers = 0;

        // リアルカードの間隔に必要なジョーカー数を計算
        for (int i = 0; i < sortedRanks.Count - 1; i++)
        {
            int gap = sortedRanks[i + 1] - sortedRanks[i] - 1;

            if (gap < 0) return false;

            requiredJokers += gap;
        }

        // 5. ジョーカーの枚数が、リアルカード間のギャップを埋めるのに十分か
        if (jokerCount < requiredJokers)
        {
            return false; // ジョーカーが足りない
        }

        // 6. ギャップを埋めた後、残ったジョーカーで連番を伸ばす
        int remainingJokers = jokerCount - requiredJokers;

        // リアルカードだけでできている連番の長さ
        int realStairLength = sortedRanks.Count;

        // 合計の階段の長さ = (リアルカード数) + (ギャップを埋めたジョーカー数) + (残りで伸ばせるジョーカー数)
        int finalStairLength = realStairLength + requiredJokers + remainingJokers;

        // 7. 最終的な長さが3枚以上か
        return finalStairLength >= 3;
    }

    private int GetStairMaxRank(List<Card> realCards, int jokerCount)
    {
        if (realCards.Count == 0)
        {
            // ジョーカーのみの場合、とりあえず最強の2の代わりになったと仮定
            // (この処理は厳密ではありませんが、2出しより強い階段はないため)
            return 15;
        }

        // リアルカードの最大ランク
        int maxRealRank = realCards.Max(c => c.Rank);

        // ジョーカーの数が、リアルカードの最大ランクより上に連番を作れるだけあるか
        // 例: (3, 4) + Joker2枚 の場合、(3, 4, 5, 6)となり、最大ランクは6

        // リアルカードが連番の場合、ジョーカーの数だけ上に伸ばせる
        var sortedRanks = realCards.OrderBy(c => c.Rank).ToList();

        // 簡易的に、ジョーカーはすべて連番の「上」に繋がると仮定します
        // （複雑な階段ロジックを避け、強さ比較の目的を果たすため）
        return maxRealRank + jokerCount;
    }


    //パスボタン処理
    private void OnPassButton()
    {
        if (players[currentTurnIndex] != humanPlayer) return;
        HandlePass();
    }

    // 場にカードを出す
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
            RemovePlayedCardsFromUI(played);
        }

        lastPlayedCards = new List<Card>(played);
        lastPlayedPlayerIndex = players.IndexOf(currentPlayer);
        passCount = 0;

        // 1. GameState作成とルールの適用
        List<Card> effectivePlayedCards = GetEffectivePlayedCards(played);
        var state = new GameState(new List<Card>(lastPlayedCards), currentTurnIndex);

        foreach (var rule in rules)
        {
            if (rule.CanApply(effectivePlayedCards, state))
            {
                rule.Apply(effectivePlayedCards, state);
            }
        }

        // 2. ルール適用結果に基づいて演出とゲーム進行を制御

        // --- 革命 ---
        if (state.TriggerRevolution)
        {
            isRevolution = !isRevolution;
            EnqueueMessage(isRevolution ? "革命開始!" : "革命終了!");
        }

        // --- 11バック ---
        if (state.IsElevenBack)
        {
            EnqueueMessage("11バック!");
            isTempRevolution = true;
        }

        // --- 5飛ばし ---
        pendingSkipCount = state.SkipCount;
        if (pendingSkipCount > 0)
        {
            EnqueueMessage($"{pendingSkipCount}人飛ばし!");
        }

        // --- 8切り ---
        // さっき修正した state.IsEightCut フラグを見る
        if (state.IsEightCut)
        {
            EnqueueMessage("8切り!");

            // 場を流す処理
            yield return new WaitForSeconds(1.0f);
            foreach (Transform child in tableArea) Destroy(child.gameObject);
            lastPlayedCards.Clear();
            passCount = 0;

            // スキップなどを無効化
            pendingSkipCount = 0;
            isTempRevolution = false;

            if (state.KeepTurn)
            {
                skipTurnAdvance = true; // ターンを進めない
                yield break; // ここで抜けて EndTurn() へ
            }
        }

        // スキップ処理 (8切りじゃなかった場合)
        if (pendingSkipCount > 0)
        {
            passCount += pendingSkipCount;
            if (passCount >= players.Count - 1)
            {
                StartCoroutine(ClearTableAndRestart());
                yield break;
            }
        }

        // --- 7渡し ---
        // 手動計算をやめて state.SevenPassCount を見る
        if (state.SevenPassCount > 0)
        {
            Debug.Log($"7渡しシーケンス開始: {state.SevenPassCount}枚");
            skipTurnAdvance = true;
            isSevenPassMode = true;
            pendingActionCardCount = state.SevenPassCount;

            yield return new WaitForSeconds(1.0f);
            StartCoroutine(HandleSevenPassSequence(currentPlayer));
            yield break;
        }

        // --- 10捨て ---
        // 手動計算をやめて state.TenDiscardCount を見る
        if (state.TenDiscardCount > 0)
        {
            Debug.Log($"10捨てシーケンス開始: {state.TenDiscardCount}枚");
            skipTurnAdvance = true;
            isTenDiscardMode = true;
            pendingActionCardCount = state.TenDiscardCount;

            yield return new WaitForSeconds(1.0f);
            StartCoroutine(HandleTenDiscardSequence(currentPlayer));
            yield break;
        }
    }


    private void RemovePlayedCardsFromUI(List<Card> played)
    {
        var cardViews = handAreaPlayer.GetComponentsInChildren<CardView>().ToList();

        foreach (var cv in cardViews)
            if (cv != null && cv.CardData != null && played.Contains(cv.CardData))
                Destroy(cv.gameObject);
    }

    private void HandlePass()
    {
        passCount++;

        if (passCount >= players.Count - 1)
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

        isTempRevolution = false; // 場が流れたら11バック終了

        if (lastPlayedPlayerIndex < 0) lastPlayedPlayerIndex = 0;

        currentTurnIndex = lastPlayedPlayerIndex;
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

    // 8切り判定
    private bool IsEightCut(List<Card> played)
    {
        if (played == null || played.Count == 0) return false;
        return played.Any(c => c.Rank == 8);
    }
    private List<Card> GetLegalCardsForUI(List<Card> hand, List<Card> field)
    {
        // 1. 場が空なら、全て出せる（ことにしておく）
        // ※本来は役成立チェックが必要ですが、UIハイライトとしては全点灯が一般的です
        if (field == null || field.Count == 0)
        {
            return new List<Card>(hand);
        }

        List<Card> playable = new List<Card>();
        int fieldCount = field.Count;

        // 場の最強ランク（階段の場合は一番強いカード、ペア等は数字）
        // ※階段の場合、簡易的に「一番強いランク」で比較します
        int fieldStrongestRank = field.Max(c => c.Rank);
        int fieldStrength = GetCardStrength(fieldStrongestRank);

        bool isFieldStair = IsStair(field);

        if (isFieldStair)
        {
            // --- 階段の場合 ---
            // 手札から階段を探す
            var stairs = FindStairSequences(hand);
            foreach (var seq in stairs)
            {
                // 枚数が同じで、かつ マークも同じ必要がある（ローカルルールによるが一般的に）
                if (seq.Count != fieldCount) continue;
                if (seq[0].Suit != field[0].Suit) continue;

                // 強さ比較
                int seqStrongestRank = seq.Max(c => c.Rank);
                if (GetCardStrength(seqStrongestRank) > fieldStrength)
                {
                    playable.AddRange(seq);
                }
            }
        }
        else
        {
            // --- 単体 または ペア/トリプルの場合 ---
            // 手札をランクごとにグループ化
            var groups = hand.GroupBy(c => c.Rank);

            foreach (var g in groups)
            {
                // 枚数が足りているか
                if (g.Count() >= fieldCount)
                {
                    // 強さが場より上か (革命・11バックを考慮した GetCardStrength を使用)
                    if (GetCardStrength(g.Key) > fieldStrength)
                    {
                        // 条件を満たすランクのカードはすべて候補とする
                        playable.AddRange(g);
                    }
                }
            }
        }

        return playable;
    }

    /// <summary>
    /// ルール判定用に、ジョーカーを具体的なランクのカードに変換したリストを生成する
    /// (例: [6, 7, Joker] -> [6, 7, 8])
    /// </summary>
    private List<Card> GetEffectivePlayedCards(List<Card> original)
    {
        if (original == null || original.Count == 0) return new List<Card>();

        // リアルカード（ジョーカー以外）を抽出してソート
        var realCards = original.Where(c => !c.IsJoker()).OrderBy(c => c.Rank).ToList();
        int jokerCount = original.Count - realCards.Count;

        // ジョーカーがない場合はコピーをそのまま返す
        if (jokerCount == 0) return new List<Card>(original);

        // ジョーカーのみの場合 (とりあえず最強カード扱いで処理、例: Rank 15=2)
        if (realCards.Count == 0)
        {
            var list = new List<Card>();
            for (int i = 0; i < jokerCount; i++)
            {
                // ここでは仮に最強の2(15)として扱います
                list.Add(new Card { Rank = 15, Suit = Suit.Spade });
            }
            return list;
        }

        // --- A. ペア・トリプル等の判定 (実カードのランクが全て同じ) ---
        bool isGroup = realCards.All(c => c.Rank == realCards[0].Rank);
        if (isGroup)
        {
            var list = new List<Card>(realCards);
            int rank = realCards[0].Rank;
            // ジョーカーをそのランクのカードとして生成して追加
            for (int i = 0; i < jokerCount; i++)
            {
                list.Add(new Card { Rank = rank, Suit = realCards[0].Suit });
            }
            return list;
        }

        // --- B. 階段の判定 (実カードが連番、または飛び番) ---
        // 実カードの隙間を埋め、余ったら上に足す処理を行う
        var effectiveList = new List<Card>();

        // 最初のカード
        int currentRank = realCards[0].Rank;
        effectiveList.Add(realCards[0]);

        int realIndex = 1;
        int usedJokers = 0;

        // 実カードの間をチェックして埋める
        while (realIndex < realCards.Count)
        {
            int nextRealRank = realCards[realIndex].Rank;
            int gap = nextRealRank - currentRank - 1;

            if (gap > 0)
            {
                // ギャップをジョーカーで埋める
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

            // 次の実カードを追加
            currentRank = nextRealRank;
            effectiveList.Add(realCards[realIndex]);
            realIndex++;
        }

        // 余ったジョーカーは連番の「上」に足す (例: 6,7 + Joker -> 6,7,8)
        while (usedJokers < jokerCount)
        {
            currentRank++;
            effectiveList.Add(new Card { Rank = currentRank, Suit = realCards[0].Suit });
            usedJokers++;
        }

        return effectiveList;
    }
    // ================================================================
    // --- 7渡し / 10捨て 処理ロジック ---
    // ================================================================

    private IEnumerator HandleSevenPassSequence(PlayerBase player)
    {
        EnqueueMessage($"7渡し! {pendingActionCardCount}枚選んでください");

        if (player is HumanPlayer)
        {
            // --- ★ 修正開始 ★ ---
            // 1. カード選択状態をリセット
            ResetPlayerSelection();

            // 2. 手札の枚数に合わせてスロットを再生成
            CreatePlayerCardSlots(human.Hand.Count);

            // 3. 手札を再描画し、全てのカードを選択可能にする
            PopulatePlayerHand(human);
            // --- ★ 修正終了 ★ ---

            // プレイヤー入力待ちモードへ
            if (passButton != null) passButton.interactable = false; // パスはできない
            if (playButton != null)
            {
                playButton.interactable = false; // 選択するまで押せない
                playButton.GetComponentInChildren<TextMeshProUGUI>().text = "あげる";
            }

            // プレイヤーがカードを選んで「あげる」ボタンを押すのを待つ
            yield break;
        }
        else
        {
            // CPUの処理: 手札から不要なカード（弱い順）を選ぶ
            yield return new WaitForSeconds(1.0f);

            var hand = player.Hand.OrderBy(c => c.Rank).ToList(); // 弱い順
            // 枚数が足りない場合は全手札
            int count = Mathf.Min(pendingActionCardCount, hand.Count);
            var cardsToPass = hand.Take(count).ToList();

            yield return StartCoroutine(ExecuteSevenPassTransfer(player, cardsToPass));
        }
    }

    private IEnumerator HandleTenDiscardSequence(PlayerBase player)
    {
        EnqueueMessage($"10捨て! {pendingActionCardCount}枚選んで捨ててください");

        if (player is HumanPlayer)
        {
            // --- ★ 修正開始 ★ ---
            // 1. カード選択状態をリセット
            ResetPlayerSelection();

            // 2. 手札の枚数に合わせてスロットを再生成 (前のターンでカードを出したため枚数が減っている)
            CreatePlayerCardSlots(human.Hand.Count);

            // 3. 手札を再描画し、全てのカードを選択可能にする (PopulatePlayerHand内のロジックがisTenDiscardModeを見て全有効化する)
            PopulatePlayerHand(human);
            // --- ★ 修正終了 ★ ---

            if (passButton != null) passButton.interactable = false;
            if (playButton != null)
            {
                playButton.interactable = false;
                playButton.GetComponentInChildren<TextMeshProUGUI>().text = "捨てる";
            }
            // プレイヤー入力待ち
            yield break;
        }
        else
        {
            // CPUの処理: 弱い順に捨てる
            yield return new WaitForSeconds(1.0f);

            var hand = player.Hand.OrderBy(c => c.Rank).ToList();
            int count = Mathf.Min(pendingActionCardCount, hand.Count);
            var cardsToDiscard = hand.Take(count).ToList();

            yield return StartCoroutine(ExecuteTenDiscardAction(player, cardsToDiscard));
        }
    }

    // 実際にカードを移動させる処理（7渡し）
    public IEnumerator ExecuteSevenPassTransfer(PlayerBase fromPlayer, List<Card> cards)
    {
        // 次のプレイヤーを特定
        int nextIndex = (players.IndexOf(fromPlayer) + 1) % players.Count;
        PlayerBase toPlayer = players[nextIndex];

        Debug.Log($"{fromPlayer.Name} から {toPlayer.Name} へ {cards.Count}枚 渡します");

        // アニメーション用（簡易）: 手札から消して、相手の手札へ
        foreach (var card in cards)
        {
            fromPlayer.Hand.Remove(card);
            toPlayer.Hand.Add(card);

            // UI更新: 自分の手札なら消す
            if (fromPlayer is HumanPlayer)
            {
                RemovePlayedCardsFromUI(new List<Card> { card });
            }
        }

        // 相手がHumanなら手札再描画、CPUなら裏面再描画
        if (toPlayer is HumanPlayer)
        {
            // カードを受け取って手札枚数が変わったため、スロットを再生成する
            CreatePlayerCardSlots(human.Hand.Count);
            PopulatePlayerHand(human);
        }
        else
        {
            Transform cpuArea = null;
            if (toPlayer == cpuPlayers[0]) cpuArea = handAreaCPU1;
            else if (toPlayer == cpuPlayers[1]) cpuArea = handAreaCPU2;
            else if (toPlayer == cpuPlayers[2]) cpuArea = handAreaCPU3;

            if (cpuArea != null) PopulateCpuHandAsBack(cpuArea, toPlayer.Hand.Count);
        }

        // 自分がCPUでカードが減った場合も再描画
        if (fromPlayer is not HumanPlayer)
        {
            Transform cpuArea = fromPlayer.handArea;
            if (cpuArea != null) PopulateCpuHandAsBack(cpuArea, fromPlayer.Hand.Count);
        }

        yield return new WaitForSeconds(0.8f);

        // モード解除
        isSevenPassMode = false;

        skipTurnAdvance = false;

        ResetPlayButtonUI();

        EndTurn(); // ここで通常のターン進行ルートに入り、次の人へ進む
    }

    // 実際にカードを捨てる処理（10捨て）
    public IEnumerator ExecuteTenDiscardAction(PlayerBase player, List<Card> cards)
    {
        Debug.Log($"{player.Name} は {cards.Count}枚 捨てました");

        // 墓地（のような場所）へ移動アニメーションを入れても良いが、今回は削除のみ
        foreach (var card in cards)
        {
            player.Hand.Remove(card);
            // UIから削除
            if (player is HumanPlayer)
            {
                RemovePlayedCardsFromUI(new List<Card> { card });
            }
        }

        if (player is not HumanPlayer)
        {
            Transform cpuArea = player.handArea;
            if (cpuArea != null) PopulateCpuHandAsBack(cpuArea, player.Hand.Count);
        }

        yield return new WaitForSeconds(0.8f);

        isTenDiscardMode = false;
        skipTurnAdvance = false;

        ResetPlayButtonUI();

        EndTurn();
    }

    private void ResetPlayButtonUI()
    {
        if (playButton != null)
        {
            playButton.GetComponentInChildren<TextMeshProUGUI>().text = "Play";
            playButton.interactable = false;
        }
        if (passButton != null) passButton.interactable = true;
    }
}
