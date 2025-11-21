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
        if (skipTurnAdvance)
        {
            skipTurnAdvance = false;
            StartCoroutine(NextTurnDelay());
            return;
        }

        currentTurnIndex = (currentTurnIndex + 1) % 4;
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
            var candidates = hand
                .GroupBy(c => c.Rank)
                .Where(g => g.Count() >= fieldCount && g.Key > fieldRank)
                .OrderBy(g => g.Key)
                .FirstOrDefault();

            return candidates?.Take(fieldCount).ToList() ?? new List<Card>();
        }
        else
        {
            var stairs = FindStairSequences(hand);
            foreach (var seq in stairs)
                if (seq.Count == fieldCount && seq.Last().Rank > field.Last().Rank)
                    return seq;
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
        // 1. 自分のターンでない場合、両方隠す
        if (!isPlayerTurn)
        {
            if (playButton != null) playButton.gameObject.SetActive(false);
            if (passButton != null) passButton.gameObject.SetActive(false);
            return;
        }

        // --- 以下、自分のターンの処理 ---

        // 2. プレイボタンの制御
        // 「表示させてよい」とのことなので、自分のターン中は常に表示(Active)にします
        if (playButton != null)
        {
            playButton.gameObject.SetActive(true);

            // 【オプション】もし「カードを選んでない時は押せない（グレー）」にしたいなら、
            // 下の行のコメントアウト(//)を外して有効にしてください。
            // playButton.interactable = IsAnyCardSelected();
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

    // デッキ生成（3～15 = 3〜K/A/2）
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
                    SpritePath = $"Images/{s}s_{r}"
                });
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

        var tableCards = (lastPlayedCards == null || lastPlayedCards.Count == 0) ? null : lastPlayedCards;
        var playableCards = player.GetPlayableCards(tableCards);

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
        EndTurn();
    }

    public void OnPlayButton()
    {
        // 自分のターンでなければ無視
        if (!isPlayerTurn) return;

        // ボタンが設定されており、すでに無効なら無視（連打防止）
        if (playButton != null && !playButton.interactable) return;

        // ▼ 変更: 押された瞬間にボタンを無効化（これで連打を防ぐ）
        if (playButton != null) playButton.interactable = false;

        var played = human.SelectCards(human.Hand);

        if (played == null || played.Count == 0)
        {
            // 何も選択せずにPlayボタンを押した場合の挙動
            // 元のコードではHandlePassしていましたが、一般的には「カードを選んでください」と戻すことが多いです。
            // パス扱いにするならこのままでOKですが、選び直させるなら interactable = true に戻します。

            // 今回は「選び直し」させる想定でロックを解除します
            Debug.Log("カードが選択されていません。");
            if (playButton != null) playButton.interactable = true;
            return;
        }

        if (!human.CanPlaySelectedCards(lastPlayedCards))
        {
            Debug.Log("そのカードは出せません。");
            // ▼ 追加: 出せないカードだった場合は、選び直せるようにボタンを再度有効化する
            if (playButton != null) playButton.interactable = true;

            // 選択状態を解除などの処理が必要ならここに入れる
            return;
        }

        // 成功した場合、ボタンは無効のまま処理を進める
        StartCoroutine(PlayerPlayRoutine(played));
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

        // カードが出されたので、このプレイヤーを「最後に出した人」として記録
        lastPlayedPlayerIndex = players.IndexOf(currentPlayer);
        // 新しいカードが出たので、これまでのパス回数はリセット
        passCount = 0;

        // GameState を作る（ルールに渡す情報箱）
        var state = new GameState(new List<Card>(lastPlayedCards), currentTurnIndex);

        // 全ルールをチェックして適用
        foreach (var rule in rules)
        {
            if (rule.CanApply(played, state))
            {
                rule.Apply(played, state);
            }
        }
        // （EightCutRule などは state.TableCards.Clear(); と state.KeepTurn = true; を行う想定）
        // ルールが場を流すように state.TableCards を空にした場合は、UI 側もクリアする
        if (state.TableCards == null || state.TableCards.Count == 0)
        {
            foreach (Transform t in tableArea)
                Destroy(t.gameObject);

            lastPlayedCards.Clear();
            passCount = 0;
            lastPlayedPlayerIndex = players.IndexOf(currentPlayer);

            // ★ 8切りによる継続
            if (state.KeepTurn)
            {
                EnqueueMessage($"{currentPlayer.Name} の 8切り！場をリセットします。");
                yield return new WaitForSeconds(0.6f);

                // ★ 追加：次の人へ進まないようにするフラグを立てる
                skipTurnAdvance = true;

                // 修正：ここでの StartTurn() は削除します。
                // 理由：この後呼び出される EndTurn() 内で skipTurnAdvance フラグを見て
                // 自動的に StartTurn() が呼ばれるため、ここで呼ぶと2重実行になります。
                // StartTurn(); // ← この行を削除またはコメントアウト

                yield break;
            }
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
}