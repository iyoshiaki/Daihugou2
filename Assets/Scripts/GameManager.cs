using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class GameManager : MonoBehaviour
{
    [Header("UI References")]
    public Transform handAreaPlayer;   // 自分の手札表示エリア
    public Transform handAreaCPU1;
    public Transform handAreaCPU2;
    public Transform handAreaCPU3;
    public Transform tableArea;        // 場のカード表示エリア

    [Header("Prefabs & Sprites")]
    public GameObject cardPrefab;      // CardPrefab（Inspectorに割り当て）
    public Sprite cardBackSprite;      // 裏面画像

    private HumanPlayer human;          // 自分プレイヤー
    private List<CpuPlayer> cpuPlayers = new(); // CPU3人

    public List<Card> lastPlayedCards = new List<Card>(); // 場の直前のカード

    private int passCount = 0;           // 連続パス人数カウント
    private int lastPlayedPlayerIndex = -1; // 最後にカードを出したプレイヤー

    [SerializeField] private Button passButton;

    private List<PlayerBase> players; // ← 全プレイヤーまとめ用

    [SerializeField] private TextMeshProUGUI passMessageText; // ←パスのテキスト

    private Queue<string> messageQueue = new Queue<string>(); // ← メッセージキュー
    private bool isShowingMessage = false;

    [SerializeField] private GameObject cardSlotPrefab;

    private List<CardSlot> playerCardSlots = new List<CardSlot>();


    // ================================================
    // --- ターン管理用変数 ---
    // ================================================
    private int currentTurnIndex = 0;   // 0=human, 1=CPU1, 2=CPU2, 3=CPU3
    private bool isPlayerTurn = true;

    // ================================================
    // --- ターン制管理メソッド ---
    // ================================================

    // ゲーム開始時またはターン進行時に呼ばれる
    private void StartTurn()
    {
        // 🟢 パスボタンの状態を初期化（どのターンでも毎回制御）
        passButton.interactable = (currentTurnIndex == 0);

        if (currentTurnIndex == 0)
        {
            // 自分のターン開始前に選択状態をリセット
            ResetPlayerSelection();

            // 💡【修正箇所】: 手札の枚数に合わせて CardSlot を再生成する処理を追加
            // この処理が、手札の増減に合わせてUIの土台をリセットします。
            CreatePlayerCardSlots(human.Hand.Count);

            // 出せるカードを最新の場情報で再設定し、UIにカードを配置
            PopulatePlayerHand(human);

            isPlayerTurn = true;
            Debug.Log("あなたのターンです。カードを選んでPlayボタンを押してください。");
        }
        else
        {
            // CPUのターンを処理
            isPlayerTurn = false;
            StartCoroutine(CpuPlayTurn(currentTurnIndex - 1)); // CPU1=Index1 → cpuPlayers[0]
        }
    }

    // ターン終了時
    private void EndTurn()
    {
        currentTurnIndex = (currentTurnIndex + 1) % 4;
        StartCoroutine(NextTurnDelay());
    }

    // CPUがカードを出すアニメーション処理
    private IEnumerator CpuPlayCards(PlayerBase cpu)
    {
        // CPUが出すカードを決定（仮に一番上の1枚）
        List<Card> cardsToPlay = cpu.SelectCards(cpu.HandCards);
        if (cardsToPlay == null || cardsToPlay.Count == 0)
        {
            // Debug.Log($"{cpu.Name} はパスしました");
            yield break;
        }

        // CPUの手札UIから対応するCardViewを探す
        // CPUの手札UIから対応するCardViewを探す
        foreach (Card card in cardsToPlay)
        {
            CardView cardView = FindCardViewForCard(card, cpu);
            if (cardView != null)
            {
                // 手札から場中央へアニメーション移動
                Vector3 targetPos = tableArea.position; // ← playArea ではなく tableArea に変更
                yield return StartCoroutine(cardView.MoveTo(targetPos, 0.4f));

                // 少し上に積み重ねるように配置
                cardView.transform.SetParent(tableArea);
                cardView.transform.localPosition = new Vector3(0, 0, -cpu.Hand.Count * 0.01f);
            }
        }

        // 手札から削除
        foreach (Card card in cardsToPlay)
        {
            cpu.HandCards.Remove(card);
        }

        yield return new WaitForSeconds(0.5f);
    }

    private CardView FindCardViewForCard(Card card, PlayerBase player)
    {
        CardView[] allCards = FindObjectsOfType<CardView>();
        foreach (CardView cv in allCards)
        {
            if (cv.CardData == card && cv.transform.parent == player.handArea)
            {
                return cv;
            }
        }
        return null;
    }


    // --------------------------------
    // プレイヤーのカード選択状態リセット
    // --------------------------------
    private void ResetPlayerSelection()
    {
        // HumanPlayer 側の選択リストを空にする
        human.ClearSelectedCards();

        // handAreaPlayer 内の CardView を全解除
        foreach (Transform child in handAreaPlayer)
        {
            var cv = child.GetComponent<CardView>();
            if (cv != null)
            {
                cv.SetSelected(false); // CardView に選択解除メソッドがある前提
            }
        }

        // Debug.Log("カード選択状態をリセットしました。");
    }

    private IEnumerator NextTurnDelay()
    {
        yield return new WaitForSeconds(0.8f); // 少し間を空けて次のターンへ
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
            // Debug.Log($"{cpu.Name} はすでに上がっています。");
            EndTurn();
            yield break;
        }

        // --- 出せるカードを関数化 ---
        List<Card> playableCards = GetPlayableCardsForCpu(cpu, lastPlayedCards);

        if (playableCards.Count == 0)
        {
            EnqueueMessage($"{cpu.Name} はパスしました");
            Debug.Log($"{cpu.Name} はパスしました。");
            yield return new WaitForSeconds(0.8f);
            HandlePass();
            yield break;
        }

        // --- 出すカードを削除 ---
        foreach (var c in playableCards)
            cpu.Hand.Remove(c);

        // Debug.Log($"{cpu.Name} played: {string.Join(", ", playableCards.Select(c => $"{c.Suit} {c.Rank}"))}");

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

        // --- 1. 場が空ならランダムで1枚 or 階段優先なども可 ---
        if (field == null || field.Count == 0)
        {
            // 階段を優先的に狙う例
            var stairs = FindStairSequences(hand);
            if (stairs.Count > 0)
            {
                var chosen = stairs[Random.Range(0, stairs.Count)];
                // Debug.Log($"{cpu.Name} は階段を選択: {string.Join(", ", chosen.Select(c => $"{c.Rank}"))}");
                return chosen;
            }

            // それ以外は最小ランクの1枚
            return new List<Card> { hand.First() };
        }

        // --- 2. 場が同ランクセットの場合 ---
        bool isFieldStair = IsStair(field);
        int fieldCount = field.Count;
        int fieldRank = field[0].Rank;

        if (!isFieldStair)
        {
            // 同ランク出しの場合
            var candidates = hand
                .GroupBy(c => c.Rank)
                .Where(g => g.Count() >= fieldCount && g.Key > fieldRank)
                .OrderBy(g => g.Key)
                .FirstOrDefault();

            return candidates?.Take(fieldCount).ToList() ?? new List<Card>();
        }
        else
        {
            // 階段出しの場合（場の階段より強い階段を探す）
            var stairs = FindStairSequences(hand);
            foreach (var seq in stairs)
            {
                if (seq.Count == fieldCount && seq.Last().Rank > field.Last().Rank)
                    return seq;
            }
        }

        return new List<Card>(); // 出せない
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
                    // 連番チェック
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

            if (current.Count >= 3)
                stairs.Add(new List<Card>(current));
        }

        return stairs;
    }

    // ================================================
    // --- 階段（連番）判定 ---
    // ================================================
    private bool IsStair(List<Card> cards)
    {
        if (cards == null || cards.Count < 3) return false;

        // すべて同じスートか？
        var suit = cards[0].Suit;
        if (cards.Any(c => c.Suit != suit))
            return false;

        // ランク順に並べる
        var sorted = cards.OrderBy(c => c.Rank).ToList();

        // すべて同じランク（3カード等）の場合はfalse
        if (sorted.All(c => c.Rank == sorted[0].Rank))
            return false;

        // 連番チェック
        for (int i = 1; i < sorted.Count; i++)
        {
            if (sorted[i].Rank != sorted[i - 1].Rank + 1)
                return false;
        }

        return true;
    }



    // 外部参照用
    public HumanPlayer humanPlayer => human;

    void Start()
    {
        InitPlayers();

        if (cpuPlayers.Count > 0) cpuPlayers[0].handArea = handAreaCPU1;
        if (cpuPlayers.Count > 1) cpuPlayers[1].handArea = handAreaCPU2;
        if (cpuPlayers.Count > 2) cpuPlayers[2].handArea = handAreaCPU3;

        human.handArea = handAreaPlayer;

        DealInitialCards();

        // ✅ 先にスロットを生成する
        CreatePlayerCardSlots(human.Hand.Count);

        // ✅ その後に手札を配置
        PopulatePlayerHand(human);

        StartTurn();

        passButton.onClick.AddListener(OnPassButton);

        // ✅ 最後にプレイヤーリストを構築
        players = new List<PlayerBase>();
        players.Add(humanPlayer);
        players.AddRange(cpuPlayers);
    }

    // ================================
    // 手札スロットを自動生成
    // ================================

    private void CreatePlayerCardSlots(int slotCount)
    {
        // 古いスロットを削除
        foreach (Transform child in handAreaPlayer)
        {
            if (child.GetComponent<CardSlot>() != null)
                Destroy(child.gameObject);
        }

        playerCardSlots.Clear();

        // スロットを自動生成
        float spacing = 50f; // カードの間隔
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

    // -------------------------------
    // プレイヤー初期化
    // -------------------------------
    void InitPlayers()
    {
        human = new HumanPlayer { Name = "You" };
        cpuPlayers.Clear();
        for (int i = 0; i < 3; i++)
        {
            cpuPlayers.Add(new CpuPlayer { Name = "CPU " + (i + 1) });
        }
    }

    // -------------------------------
    // デッキ作成と配布
    // -------------------------------
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

    // -------------------------------
    // デッキ生成（3～15 = 3〜K/A/2）
    // -------------------------------
    List<Card> CreateDeck()
    {
        var deck = new List<Card>();
        Suit[] suits = { Suit.Spade, Suit.Heart, Suit.Diamond, Suit.Club };

        for (int r = 3; r <= 15; r++)
        {
            foreach (var s in suits)
            {
                deck.Add(new Card
                {
                    Suit = s,
                    Rank = r,
                    SpritePath = $"Images/{s}s_{r}"
                });
            }
        }
        return deck;
    }

    // -------------------------------
    // シャッフル
    // -------------------------------
    void Shuffle(List<Card> deck)
    {
        for (int i = 0; i < deck.Count; i++)
        {
            int rand = Random.Range(i, deck.Count);
            (deck[i], deck[rand]) = (deck[rand], deck[i]);
        }
    }

    // -------------------------------
    // 手札UI生成
    // -------------------------------
    public void PopulatePlayerHand(HumanPlayer player)
{
    Debug.Log($"[PopulatePlayerHand] 呼ばれた / 手札枚数: {player.Hand.Count}");

    // 既存のカードを削除
    foreach (Transform child in handAreaPlayer)
    {
        if (child.GetComponent<CardView>() != null)
            Destroy(child.gameObject);
    }

    // 手札をソート
    player.Hand.Sort((a, b) => a.Rank.CompareTo(b.Rank));

    // 今場に出ているカード
    var tableCards = (lastPlayedCards == null || lastPlayedCards.Count == 0) ? null : lastPlayedCards;
    var playableCards = player.GetPlayableCards(tableCards);

    for (int i = 0; i < player.Hand.Count; i++)
    {
        var card = player.Hand[i];

        // ✅ 親を設定する際に「false」を明示してローカル座標を維持
        var go = Instantiate(cardPrefab);
        go.transform.SetParent(playerCardSlots[i].transform, false);

        // ✅ 位置・スケール初期化（安全のため）
        var rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;

        // ✅ カード設定
        var cv = go.GetComponent<CardView>();
        cv.backSprite = cardBackSprite;
        cv.SetCard(card);

        bool canPlay = playableCards.Contains(card);
        cv.SetPlayable(canPlay);
    }
}

    public void PopulateCpuHandAsBack(Transform cpuArea, int cardCount)
    {
        foreach (Transform child in cpuArea)
            Destroy(child.gameObject);

        bool isSide = (cpuArea == handAreaCPU2 || cpuArea == handAreaCPU3);

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
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(cpuArea.GetComponent<RectTransform>());
    }

    // プレイヤーがカードを出すワークフロー（アニメ待ち→手札削除→UI更新→ターン終了）
    private IEnumerator PlayerPlayRoutine(List<Card> played)
    {
        // human を currentPlayer として渡す
        yield return StartCoroutine(DisplayPlayedCardsOnTable(human, played));

        // ターンを進める（アニメ完了後）
        EndTurn();

        yield break;
    }

    // -------------------------------
    // Playボタン処理
    // -------------------------------
    public void OnPlayButton()
    {
        if (!isPlayerTurn)
        {
            // Debug.Log("今はあなたの番ではありません。");
            return;
        }

        // Debug.Log("Play button pressed");

        var played = human.SelectCards(human.Hand);

        // 何も選んでいない → パス扱い
        if (played == null || played.Count == 0)
        {
            // Debug.Log("あなたはパスしました。");
            HandlePass();
            return;
        }

        // 出せるカードかチェック
        if (!human.CanPlaySelectedCards(lastPlayedCards))
        {
            // Debug.Log("この組み合わせでは出せません。");
            return;
        }

        // アニメーション→削除→ターン終了
        StartCoroutine(PlayerPlayRoutine(played));
    }

    //パスボタン処理
    private void OnPassButton()
    {
        // 人間のターン以外では押せない
        if (players[currentTurnIndex] != humanPlayer) return;

        // Debug.Log("あなたはパスしました。");
        HandlePass();
    }

    // 場にカードを出すアニメーション（誰が出したか currentPlayer を受け取る）
    private IEnumerator DisplayPlayedCardsOnTable(PlayerBase currentPlayer, List<Card> played)
    {
        float spacing = 20f;
        int existingCards = tableArea.childCount;
        Vector3 basePos = tableArea.position;
        float startX = basePos.x - (played.Count - 1) * spacing / 2f;

        // === 出発元（手札エリア）を取得 ===
        Transform sourceArea = null;
        if (currentPlayer is HumanPlayer)
            sourceArea = handAreaPlayer;
        else if (currentPlayer == cpuPlayers[0])
            sourceArea = handAreaCPU1;
        else if (currentPlayer == cpuPlayers[1])
            sourceArea = handAreaCPU2;
        else if (currentPlayer == cpuPlayers[2])
            sourceArea = handAreaCPU3;

        if (sourceArea == null)
        {
            Debug.LogWarning("手札エリアが見つかりません: " + currentPlayer);
            yield break;
        }

        // --- 出すカードのViewを探す ---
        List<CardView> allCardViews = sourceArea.GetComponentsInChildren<CardView>().ToList();
        var playedViews = new List<CardView>();

        for (int i = 0; i < played.Count; i++)
        {
            Card card = played[i];
            CardView cv = allCardViews.FirstOrDefault(v => v.CardData == card);

            // CPU裏カード対応（裏面 → 表に変換）
            if (cv == null && !(currentPlayer is HumanPlayer))
            {
                cv = allCardViews.FirstOrDefault(v => v.CardData == null);
            }

            if (cv == null)
            {
                Debug.LogWarning($"カードビューが見つかりません: {card}");
                continue;
            }

            // === CPUカードを表向きにする ===
            cv.SetCard(card);

            // === 右端の位置を取得 ===
            RectTransform rt = sourceArea as RectTransform;
            Vector3 startPos = sourceArea.position;
            if (rt != null && rt.childCount > 0)
            {
                var lastCard = rt.GetChild(rt.childCount - 1);
                startPos = lastCard.position;
            }

            // === アニメーション終了位置（場の配置位置） ===
            Vector3 endPos = new Vector3(startX + spacing * i, basePos.y, basePos.z);

            // === 🔹 出す瞬間に手札から削除 ===
            cv.transform.SetParent(tableArea.parent, true); // テーブル用に親を外す
            if (!(currentPlayer is HumanPlayer))
            {
                // CPUの手札から今のカードビューを削除
                if (cv.transform.parent != null && cv.transform.parent == sourceArea)
                {
                    cv.transform.SetParent(tableArea.parent, true);
                }

                // 🔸ここで手札エリアから見た目を削除
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
                    if (removeTarget != null)
                    {
                        Destroy(removeTarget.gameObject); // ← 出す瞬間に消す！
                    }
                }
            }

            // --- 移動アニメーション ---
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

        // --- 少し間を置いて整列 ---
        yield return new WaitForSeconds(0.05f);

        // --- テーブル上に整列して置く ---
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

        // --- 手札から削除（Humanのみここで実行） ---
        if (currentPlayer is HumanPlayer)
        {
            foreach (var c in played) human.Hand.Remove(c);
            RemovePlayedCardsFromUI(played);
        }

        // --- 状態更新 ---
        lastPlayedCards = new List<Card>(played);
        passCount = 0;
        lastPlayedPlayerIndex = currentTurnIndex;
    }




    // 手札UIの中から、played に含まれるカード（CardData）を削除する。
    // ※ handAreaPlayer の子のみを対象にするため、場に移動済みのカードは削除されません。
    private void RemovePlayedCardsFromUI(List<Card> played)
    {
        // handAreaPlayer 配下の全 CardView を取得（CardSlot 内も含む）
        var cardViews = handAreaPlayer.GetComponentsInChildren<CardView>().ToList();

        foreach (var cv in cardViews)
        {
            if (cv != null && cv.CardData != null && played.Contains(cv.CardData))
            {
                Destroy(cv.gameObject);
            }
        }
    }

    private void HandlePass()
    {
        passCount++;
        // Debug.Log($"{currentTurnIndex}番目のプレイヤーがパス（連続{passCount}人目）");

        // 場を流す条件：自分以外全員がパスした
        // つまり 3人が連続パス or 全員パスして自分に戻った時
        if (passCount >= players.Count - 1)
        {
            // Debug.Log("全員パス！場を流します。");
            StartCoroutine(ClearTableAndRestart());
        }
        else
        {
            EndTurn();
        }
    }

    private IEnumerator ClearTableAndRestart()
    {
        // 少し待ってから場をクリア
        yield return new WaitForSeconds(0.6f);

        foreach (Transform child in tableArea)
            Destroy(child.gameObject);

        lastPlayedCards.Clear();
        // Debug.Log("場が流れました！");

        passCount = 0;

        // 最後に出した人から再開
        if (lastPlayedPlayerIndex < 0)
            lastPlayedPlayerIndex = 0;

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

        // フェードイン
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

        // 一定時間表示
        yield return new WaitForSeconds(duration);

        // フェードアウト
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
        if (!isShowingMessage)
            StartCoroutine(ProcessMessageQueue());
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

            // CanvasGroup でフェード制御
            CanvasGroup cg = passMessageText.GetComponent<CanvasGroup>();
            if (cg == null) cg = passMessageText.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            // フェードイン
            float t = 0f;
            while (t < 0.3f)
            {
                cg.alpha = Mathf.Lerp(0, 1, t / 0.3f);
                t += Time.deltaTime;
                yield return null;
            }
            cg.alpha = 1f;

            // 表示保持（例：1.5秒）
            yield return new WaitForSeconds(1.5f);

            // フェードアウト
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

}