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
        if (skipTurnAdvance)
        {
            skipTurnAdvance = false;
            // 8切りの場合は pendingSkipCount もリセットしておく（念のため）
            pendingSkipCount = 0;
            StartCoroutine(NextTurnDelay());
            return;
        }

        // 次のターン = (現在 + 1 + スキップ数) % 人数
        int nextTurnIndex = (currentTurnIndex + 1 + pendingSkipCount) % players.Count;

        // もし一周回って自分に戻ってきた場合（3枚出しスキップなど）
        if (pendingSkipCount > 0 && nextTurnIndex == currentTurnIndex)
        {
            EnqueueMessage("全員スキップ！場が流れ、もう一度自分の番です。"); // メッセージ修正

            // 全員スキップで自分に番が戻った場合、場を流して自分からスタート
            StartCoroutine(ClearTableAndRestart());
            return; // ターンを終了し、流す処理に任せる
        }

        currentTurnIndex = nextTurnIndex;

        // 計算が終わったのでリセット
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

        if (!IsValidPlay(human.Hand, played, lastPlayedCards))
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

    private bool IsValidPlay(List<Card> hand, List<Card> selected, List<Card> field)
    {
        // 1. 役として成立しているか（枚数、階段など）
        //    -> 既存のロジックがあればそれを使う、あるいはここでチェック
        if (selected.Count == 0) return false;
        bool isRankGroup = selected.All(c => c.Rank == selected[0].Rank);

        // 2. 場に出ているカードより強いか
        if (field != null && field.Count > 0)
        {
            // 枚数チェック
            if (selected.Count != field.Count) return false;

            // 強さチェック
            int fieldStrength = GetCardStrength(field[0].Rank);
            int selectedStrength = GetCardStrength(selected[0].Rank);

            if (selectedStrength <= fieldStrength) return false;
        }

        return true;
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

        // GameState を作る
        var state = new GameState(new List<Card>(lastPlayedCards), currentTurnIndex);

        // 全ルールをチェックして適用
        // ★ 毎回リセットすべき一時フラグを初期化
        isTempRevolution = false;

        foreach (var rule in rules)
        {
            if (rule.CanApply(played, state))
            {
                rule.Apply(played, state);
            }
        }

        // --- ルール適用結果の反映 ---

        // 1. 革命反映
        if (state.TriggerRevolution)
        {
            isRevolution = !isRevolution; // 状態反転
            string status = isRevolution ? "革命開始！" : "革命終了！";
            EnqueueMessage(status);
        }

        // 2. 11バック反映（場が流れるまで有効）
        if (state.IsElevenBack)
        {
            isTempRevolution = true;
        }

        // 3. 5飛ばし反映
        // ★ 修正: ここで currentTurnIndex を直接いじらず、変数に保存するだけにする
        pendingSkipCount = state.SkipCount;

        if (pendingSkipCount > 0)
        {
            EnqueueMessage($"{pendingSkipCount}人飛ばし！");
        }

        // 4. 8切り & 場を流す処理
        if (state.TableCards == null || state.TableCards.Count == 0)
        {
            // 場が流れたらスキップ効果は無効化（あるいはリセット）するのが一般的ですが、
            // 8切りの場合はそもそも「俺のターン」になるのでスキップ関係なく自分の番です。
            pendingSkipCount = 0; // リセット
            isTempRevolution = false;

            if (state.KeepTurn)
            {
                // ... (8切り処理) ...
                skipTurnAdvance = true;
                StartTurn();
                yield break;
            }
        }
        // スキップが予約されている場合、その人数分を passCount に加算する
        // （スキップされた人数 = 出せなかった人数）と見なす
        if (pendingSkipCount > 0)
        {
            // passCount にスキップ人数を加算
            passCount += pendingSkipCount;

            // もし加算後に場が流れる条件を満たしていたら、ここで場を流す処理を呼び出す
            if (passCount >= players.Count - 1)
            {
                // 次の EndTurn() や NextTurnDelay() を呼ばずに、ここで終了する
                StartCoroutine(ClearTableAndRestart());
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