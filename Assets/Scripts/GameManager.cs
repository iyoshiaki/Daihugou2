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
    private List<PlayerBase> remainingPlayers; // まだあがっていないプレイヤー
    private Dictionary<PlayerBase, int> gameRanks = new(); // 順位 (Key:Player, Value:順位)
    private Dictionary<PlayerBase, int> previousRoundRanks = new(); // 前回順位 (Key:Player, Value:順位)
    private Dictionary<PlayerBase, string> previousRoundTitles = new(); // 前回ランク名 (Key:Player, Value:ランク名)
    private int currentRank = 1; // 現在の順位（1位からスタート）
    private bool isGameOver = false; // ゲーム（1ラウンド）終了フラグ

    [Header("Rule Settings")]
    [Tooltip("2/革命時の3、ジョーカー、8切り、スペード3、7渡し/10捨ての結果としてあがることを禁止する")]
    private bool forbidSpecialWin = false; // 初期値はOFF（許可）
    [SerializeField]
    [Tooltip("第2戦以降に都落ち（前回順位に応じたカード交換）を実施する")]
    private bool enableMiyakoOchi = true;
    [SerializeField]
    [Tooltip("第2戦以降、前回大富豪が1位を取れないと大貧民へ降格する")]
    private bool enableMiyakoOchiDemotion = true;
    private bool enableBind = true;
    private bool enableStair = true;
    private bool enableSpade3Return = true;
    private bool enableSuitLock = true;
    private bool enableJokerStop = true;
    private bool enableFourStop = true;
    private bool enableSixStop = true;
    private bool enableEightCut = true;



    // 4回戦の設定
    private const int TotalGames = 4;
    private int currentGameCount = 1;
    private Dictionary<PlayerBase, int> totalPoints = new(); // 累計スコア

    private HumanPlayer human;
    private List<CpuPlayer> cpuPlayers = new();

    public List<Card> lastPlayedCards = new();

    private int passCount = 0;
    private int lastPlayedPlayerIndex = -1;

    private int lastSkippedCount = 0;

    [SerializeField] private Button passButton;
    [SerializeField] private Button playButton;
    [SerializeField] private Button kirikaeButton;

    private List<PlayerBase> players;

    [SerializeField] private TextMeshProUGUI passMessageText;
    [SerializeField] private TextMeshProUGUI SibariMessageText;
    [SerializeField] private TextMeshProUGUI cpu1NameText;
    [SerializeField] private TextMeshProUGUI cpu2NameText;
    [SerializeField] private TextMeshProUGUI cpu3NameText;
    [SerializeField] private TextMeshProUGUI cpu1PreviousRankText;
    [SerializeField] private TextMeshProUGUI cpu2PreviousRankText;
    [SerializeField] private TextMeshProUGUI cpu3PreviousRankText;
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI playerPreviousRankText;
    [SerializeField] private TextMeshProUGUI bindStatusText;
    [SerializeField] private TextMeshProUGUI ruleEffectText;

    private Queue<string> messageQueue = new();
    private bool isShowingMessage = false;
    private string lastBindStatusText = "";
    private string lastRuleEffectText = "";
    private bool lastBindStatusVisible = false;
    private bool lastRuleEffectVisible = false;

    [SerializeField] private GameObject cardSlotPrefab;

    private List<CardSlot> playerCardSlots = new();

    // ターン管理用変数
    private int currentTurnIndex = 0;
    private bool isPlayerTurn = true;

    private List<IRule> rules = new List<IRule>();
    private bool skipTurnAdvance = false;

    private bool isFourStopWindowActive = false;
    private bool isSixStopWindowActive = false;
    private int pendingEightCutCount = 0;
    private int pendingTwoCount = 0;
    private int jokerStopTurnsRemaining = 0;

    private bool forceSingleNextTurn = false;
    private bool isSingleOnlyTurn = false;

    // ★ 革命状態フラグ
    private bool isRevolution = false;
    // ★ 一時的な11バック状態フラグ
    private bool isTempRevolution = false;
    private int elevenSilenceFieldsRemaining = 0;
    private bool isNineForceActive = false;

    private bool isCpuTurnInProgress = false;
    private bool isPlayerActionLocked = false;
    private bool isPlayerActionInProgress = false;
    private Coroutine actionLockCoroutine = null;
    private const float ActionLockSeconds = 0.2f;
    private bool suppressPassAfterPlay = false;
    // 現在の「強さ」計算プロパティ
    // 革命中または11バック中なら、強さが逆になる
    private bool IsRevolutionActive => isRevolution ^ isTempRevolution; // XOR: どっちか片方なら革命、両方なら通常

    // --- 縛り状態 ---
    private bool isNumberBindActive = false;
    private bool isSuitBindActive = false;
    private int expectedNextRank = -1;
    private HashSet<Suit> boundSuits = new();
    private bool isSuitLockTurnActive = false;
    private int suitLockTurnsRemaining = 0;
    private HashSet<Suit> suitLockSuits = new();
    private bool isSelectingSuitLock = false;
    private int suitLockSelectionIndex = 0;
    private readonly Suit[] suitLockSelectionOptions = { Suit.Spade, Suit.Heart, Suit.Diamond, Suit.Club };
    private List<Suit> suitLockSelectableSuits = new();
    private bool pendingSuitLockSelection = false;
    private PlayerBase pendingSuitLockPlayer = null;


    private bool IsJokerStopActive => jokerStopTurnsRemaining > 0;
    public bool IsJokerStopActiveForPlay => IsJokerStopActive;
    private bool IsElevenSilenceActive => elevenSilenceFieldsRemaining > 0;
    private enum CpuDifficulty
    {
        Normal,
        Strong,
        Ultimate
    }

    private CpuDifficulty cpuDifficulty = CpuDifficulty.Normal;

    private CpuDifficulty GetCpuDifficulty()
    {
        if (!SoloRuleSettings.IsSoloModeActive)
        {
            return CpuDifficulty.Normal;
        }

        if (SoloRuleSettings.IsCpuUltimate)
        {
            return CpuDifficulty.Ultimate;
        }

        if (SoloRuleSettings.IsCpuStrong)
        {
            return CpuDifficulty.Strong;
        }

        return CpuDifficulty.Normal;
    }



    public void SetForbidSpecialWin(bool value)
    {
        forbidSpecialWin = value;
        Debug.Log($"禁止あがりルールが {(value ? "ON" : "OFF")} に設定されました。");
    }

    // ★カードの強さを数値化するメソッド (修正: Jokerを最強に設定)
    public int GetCardStrength(int rank)
    {
        // 通常時: 3 < 4 ... < 13(K) < 1(A) < 2 < 16(Joker)
        // 内部データ: 3=3 ... 13=13, 14=A, 15=2, 16=Joker

        int power = 0;
        if (rank == 16) power = 14; // Joker (最強)
        else if (rank == 15) power = 13; // 2
        else if (rank == 14) power = 12; // A
        else power = rank - 3; // 3 => 0, 4 => 1 ... 13(K) => 10

        // 革命中なら強さを反転 (大きい値ほど弱いことにする)
        if (IsRevolutionActive)
        {
            return -power;
        }
        return power;
    }

    // 現在のカード強さ比較用変数 (5飛ばし等用)
    private int pendingSkipCount = 0;

    private bool isSevenPassMode = false;   // 7渡しモード中か
    private bool isTenDiscardMode = false;  // 10捨てモード中か

    private bool isSixTradeMode = false;    // 6トレードモード中か
    private bool isSelectingTradeTarget = false;
    private bool isSelectingTradeCards = false;
    private bool isSelectingTradeSourceCards = false;
    private bool isSelectingMiyakoOchiCards = false;

    private int pendingActionCardCount = 0; // 渡す/捨てる枚数

    private int pendingTradeCardCount = 0;  // 6トレードの枚数
    private PlayerBase tradeSourcePlayer;
    private PlayerBase tradeTargetPlayer;
    private List<PlayerBase> tradeTargetCandidates = new();
    private int tradeTargetIndex = 0;
    private List<Card> pendingTradeSourceCards = new();
    private bool isFreezeTwelveMode = false;
    private int pendingFreezeTwelveCount = 0;
    private List<PlayerBase> freezeTargetCandidates = new();
    private int freezeTargetIndex = 0;
    private Dictionary<PlayerBase, int> freezePassCounts = new();
    private Dictionary<PlayerBase, int> barrierCounts = new();
    private int miyakoTradeCount = 0;
    private bool miyakoSelectionDone = false;
    private List<Card> pendingMiyakoOchiCards = new();
    private PlayerBase clubThreeHolderBeforeTrade = null;



    // ================================================
    // --- ターン制管理メソッド ---
    // ================================================

    private void StartTurn()
    {
        PlayerBase currentPlayer = players[currentTurnIndex];
        isPlayerActionLocked = false;
        isPlayerActionInProgress = false;
        suppressPassAfterPlay = false;
        if (IsFreezePassActive(currentPlayer))
        {
            StartCoroutine(HandleFreezePassTurn(currentPlayer));
            return;
        }

        isSuitLockTurnActive = suitLockTurnsRemaining > 0;
        if (isSuitLockTurnActive && suitLockSuits.Count > 0)
        {
            var suitMessage = string.Join("・", suitLockSuits.Select(GetSuitLabel));
            EnqueueMessage($"スートロック中: {suitMessage} のみ");
        }

        isSingleOnlyTurn = forceSingleNextTurn;
        forceSingleNextTurn = false;

        passButton.interactable = currentTurnIndex == 0;

        if (IsJokerStopActive)
        {
            EnqueueMessage("ジョーカーストップ中!");
        }

        if (currentTurnIndex == 0)
        {
            isCpuTurnInProgress = false;
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
            SetActionButtonsActive(false);
            if (isCpuTurnInProgress)
            {
                return;
            }
            isCpuTurnInProgress = true;
            StartCoroutine(CpuPlayTurn(currentTurnIndex - 1));
        }
    }

    private void EndTurn()
    {
        // ゲーム終了時は何もしない
        if (isGameOver) return;

        ConsumeJokerStopTurn();

        // --- 1. 8切りなどで「もう一度自分のターン」の場合 ---
        if (skipTurnAdvance)
        {
            skipTurnAdvance = false;
            pendingSkipCount = 0;

            // もし「俺のターン」と言った自分が、まだあがっていなければ（手札があれば）
            if (remainingPlayers.Contains(players[currentTurnIndex]))
            {
                // インデックスを変えずに、もう一度自分のターンへ
                StartCoroutine(NextTurnDelay());
                return;
            }
        }

        // --- 2. 通常のターン進行 ---

        int nextTurnIndex = currentTurnIndex;
        int skipCount = pendingSkipCount;

        pendingSkipCount = 0; // スキップ数をリセット

        // --- 3. あがったプレイヤー（remainingPlayersにいない人）をスキップする ---
        int loopSafety = 0;

        while (true)
        {
            nextTurnIndex = (nextTurnIndex + 1) % players.Count;

            // 無限ループ防止（全員あがってしまった場合など）
            loopSafety++;
            if (loopSafety > players.Count + 2)
            {
                isGameOver = true;
                StartCoroutine(EndGameRoutine());
                return;
            }
            if (!remainingPlayers.Contains(players[nextTurnIndex]))
            {
                continue;
            }

            if (skipCount > 0)
            {
                var candidate = players[nextTurnIndex];
                if (TryConsumeBarrierForSkipCandidate(candidate))
                {
                    skipCount--;
                    lastSkippedCount = Mathf.Max(0, lastSkippedCount - 1);
                    break;
                }

                skipCount--;
                if (skipCount > 0)
                {
                    continue;
                }

                continue;
            }

            break;
        }

        // --- 4. プレイヤー確定 ---
        currentTurnIndex = nextTurnIndex;

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
        try
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
            if (!IsValidPlay(cpu.Hand, playableCards, lastPlayedCards))
            {
                Debug.LogWarning($"{cpu.Name} の出し手が無効と判断されたためパスします。");
                EnqueueMessage($"{cpu.Name} はパスしました");
                yield return new WaitForSeconds(0.8f);
                HandlePass();
                yield break;
            }

            foreach (var c in playableCards) cpu.Hand.Remove(c);

            yield return StartCoroutine(DisplayPlayedCardsOnTable(cpu, playableCards));

            // ★修正チェック: 7渡し/10捨てシーケンスが始まっていれば EndTurn をスキップ
            if (isSevenPassMode || isTenDiscardMode || isSixTradeMode || isFreezeTwelveMode)
            {
                yield break;
            }

            if (isSelectingSuitLock)
            {
                yield break;
            }

            // ★追加修正: スペード3返しなどで場が流れた場合（lastPlayedCards.Count == 0）、
            // ClearTableAndRestart() の中で既に StartTurn() が呼ばれているため、
            // ここで EndTurn() を呼ぶとターンが二重に進んでしまい、次のプレイヤーがスキップされます。

            // 8切りは lastPlayedCards.Count == 0 かつ skipTurnAdvance が true に設定され、EndTurn() に到達させる必要があります。
            // それ以外の場で流れたケース（スペード3返し、全員パス後のClearTableAndRestart）は StartTurn() が呼ばれているため、ここで終了させます。
            bool clearedBySpade3OrPass = lastPlayedCards.Count == 0 && !skipTurnAdvance;

            if (clearedBySpade3OrPass)
            {
                // ClearTableAndRestart() が StartTurn() を呼び、次のプレイヤーにターンが移っているため、
                // ここで EndTurn() を呼ぶと二重にターンが進んでしまうのを防ぐため break します。
                yield break;
            }

            yield return new WaitForSeconds(0.8f);
            EndTurn();
        }
        finally
        {
            isCpuTurnInProgress = false;
        }
    }

    // ================================================
    // --- CPUの出せるカード判定ロジック ---
    // ================================================
    private List<Card> GetPlayableCardsForCpu(CpuPlayer cpu, List<Card> field)
    {
        var hand = cpu.Hand.OrderBy(c => c.Rank).ThenBy(c => c.Suit).ToList();

        if (IsJokerStopActive)
        {
            hand = hand.Where(c => !c.IsJoker()).ToList();
        }
        if (hand.Count == 0)
        {
            return new List<Card>();
        }

        if (isFourStopWindowActive)
        {
            return GetFourStopCards(hand, GetRequiredFourStopCount());
        }
        if (isSixStopWindowActive)
        {
            return GetSixStopCards(hand, GetRequiredSixStopCount());
        }

        if (isSingleOnlyTurn)
        {
            if (field != null && field.Count > 0 && field.Count != 1)
            {
                return new List<Card>();
            }

            if (field == null || field.Count == 0)
            {
                var single = new List<Card> { hand.First() };
                return IsBindSatisfied(single) ? single : new List<Card>();
            }
        }


        if (field == null || field.Count == 0)
        {
            return cpuDifficulty == CpuDifficulty.Ultimate
                ? SelectUltimateOpeningPlay(hand)
                : SelectOpeningPlay(cpu, hand);
        }

        var (fieldRealCards, fieldJokers) = GetRealCardsAndJokers(field);
        bool isFieldStair = IsStairWithJoker(fieldRealCards, fieldJokers);
        int fieldCount = field.Count;
        int fieldStrongestRank;
        if (isFieldStair)
        {
            fieldStrongestRank = GetStairMaxRank(fieldRealCards, fieldJokers);
        }
        else if (fieldRealCards.Count == 0 && fieldJokers > 0)
        {
            fieldStrongestRank = 16;
        }
        else
        {
            fieldStrongestRank = fieldRealCards.Count > 0 ? fieldRealCards[0].Rank : 3;
        }

        var fieldStrength = GetCardStrength(fieldStrongestRank);

        if (!isFieldStair)
        {
            // ★追加: 相手がジョーカー単体の場合、スペードの3を持っていれば出す
            // (通常はJokerが最強なので、これ以外のカードは出せない)
            if (fieldCount == 1 && field[0].IsJoker())
            {
                var spade3 = hand.FirstOrDefault(c => c.Suit == Suit.Spade && c.Rank == 3);
                if (spade3 != null)
                {
                    var candidate = new List<Card> { spade3 };
                    return IsBindSatisfied(candidate) ? candidate : new List<Card>();
                }
                // スペード3がなければ、ジョーカーには勝てないのでパス(空リスト返却)
                return new List<Card>();
            }

            // 通常処理

            // ジョーカー単体の場合、fieldRankはどうなっているか？
            // 実際のロジックでは IsValidPlay 側で Joker = 16 と判定するが、
            // ここでは field[0].Rank を見ている。Cardクラスの定義によるが、
            // JokerのRankが適切に設定されていないと fieldStrength がおかしくなる可能性がある。
            // ただし上の if (IsJoker) ブロックで処理しているので、ここはJoker以外が流れてくる想定。

            return cpuDifficulty == CpuDifficulty.Ultimate
                ? SelectUltimateResponse(hand, field, fieldRealCards, fieldCount, fieldStrength)
                : SelectRankGroupResponse(hand, fieldCount, fieldStrength);
        }
        else
        {
            return cpuDifficulty == CpuDifficulty.Ultimate
                ? SelectUltimateResponse(hand, field, fieldRealCards, fieldCount, fieldStrength)
                : SelectStairResponse(hand, field, fieldRealCards, fieldCount, fieldStrength);
        }
    }

    private List<Card> SelectOpeningPlay(CpuPlayer cpu, List<Card> hand)
    {
        var stairs = FindStairSequences(hand);
        if (stairs.Count > 0)
        {
            var chosen = cpuDifficulty >= CpuDifficulty.Strong
                ? ChooseStrongOpeningStair(stairs)
                : stairs[Random.Range(0, stairs.Count)];
            if (IsBindSatisfied(chosen))
            {
                return chosen;
            }
        }

        if (cpuDifficulty >= CpuDifficulty.Strong)
        {
            var groupPlay = ChooseStrongOpeningGroup(hand);
            if (groupPlay.Count > 0)
            {
                return groupPlay;
            }
        }

        var single = new List<Card> { hand.First() };
        return IsBindSatisfied(single) ? single : new List<Card>();
    }

    private List<Card> ChooseStrongOpeningGroup(List<Card> hand)
    {
        var joker = hand.FirstOrDefault(c => c.IsJoker());
        var groups = hand
            .Where(c => !c.IsJoker())
            .GroupBy(c => c.Rank)
            .Where(g => g.Count() >= 2)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => GetCardStrength(g.Key));

        foreach (var group in groups)
        {
            var candidate = group.ToList();
            if (joker != null && candidate.Count < 4)
            {
                candidate.Add(joker);
            }
            for (int count = Mathf.Min(4, candidate.Count); count >= 2; count--)
            {
                var combo = FindBindSatisfiedCombo(candidate, count);
                if (combo.Count > 0)
                {
                    return combo;
                }
            }
        }

        return new List<Card>();
    }

    private List<Card> ChooseStrongOpeningStair(List<List<Card>> stairs)
    {
        return stairs
            .OrderByDescending(seq => seq.Count)
            .ThenByDescending(seq => GetCardStrength(seq.Max(c => c.Rank)))
            .First();
    }

    private List<Card> SelectUltimateOpeningPlay(List<Card> hand)
    {
        var candidates = GeneratePlayableCombos(hand, null);
        if (candidates.Count == 0)
        {
            return new List<Card>();
        }

        return PickBestUltimatePlay(hand, null, candidates);
    }

    private List<Card> SelectUltimateResponse(List<Card> hand, List<Card> field, List<Card> fieldRealCards, int fieldCount, int fieldStrength)
    {
        var candidates = GeneratePlayableCombos(hand, field);
        if (candidates.Count == 0)
        {
            return new List<Card>();
        }

        return PickBestUltimatePlay(hand, field, candidates);
    }

    private List<List<Card>> GeneratePlayableCombos(List<Card> hand, List<Card> field)
    {
        var playable = new List<List<Card>>();
        if (hand == null || hand.Count == 0)
        {
            return playable;
        }

        if (IsJokerStopActive)
        {
            hand = hand.Where(c => !c.IsJoker()).ToList();
        }
        if (hand.Count == 0)
        {
            return playable;
        }

        if (isFourStopWindowActive)
        {
            var forced = GetFourStopCards(hand, GetRequiredFourStopCount());
            if (forced.Count > 0)
            {
                playable.Add(forced);
            }
            return playable;
        }

        if (isSixStopWindowActive)
        {
            var forced = GetSixStopCards(hand, GetRequiredSixStopCount());
            if (forced.Count > 0)
            {
                playable.Add(forced);
            }
            return playable;
        }

        if (isSingleOnlyTurn && field != null && field.Count > 1)
        {
            return playable;
        }

        var uniqueHand = hand.Distinct().ToList();

        foreach (var card in uniqueHand)
        {
            var single = new List<Card> { card };
            if (IsValidPlay(hand, single, field))
            {
                playable.Add(single);
            }
        }

        var rankGroups = uniqueHand
            .Where(c => !c.IsJoker())
            .GroupBy(c => c.Rank)
            .ToList();

        foreach (var group in rankGroups)
        {
            var cards = group.ToList();
            var joker = uniqueHand.FirstOrDefault(c => c.IsJoker());
            if (joker != null)
            {
                cards.Add(joker);
            }

            for (int count = 2; count <= Mathf.Min(4, cards.Count); count++)
            {
                foreach (var combo in EnumerateCombinations(cards, count))
                {
                    if (IsValidPlay(hand, combo, field))
                    {
                        playable.Add(combo);
                    }
                }
            }
        }

        var stairs = FindStairSequences(uniqueHand.Where(c => !c.IsJoker()).ToList());
        foreach (var seq in stairs)
        {
            if (IsValidPlay(hand, seq, field))
            {
                playable.Add(seq);
            }
        }

        return playable;
    }

    private IEnumerable<List<Card>> EnumerateCombinations(List<Card> cards, int count)
    {
        var results = new List<List<Card>>();
        var buffer = new List<Card>(count);

        void Build(int start)
        {
            if (buffer.Count == count)
            {
                results.Add(new List<Card>(buffer));
                return;
            }

            for (int i = start; i < cards.Count; i++)
            {
                buffer.Add(cards[i]);
                Build(i + 1);
                buffer.RemoveAt(buffer.Count - 1);
            }
        }

        Build(0);
        return results;
    }

    private List<Card> PickBestUltimatePlay(List<Card> hand, List<Card> field, List<List<Card>> candidates)
    {
        List<Card> best = null;
        float bestScore = float.NegativeInfinity;

        foreach (var candidate in candidates)
        {
            var score = EvaluateUltimatePlay(hand, field, candidate);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best ?? new List<Card>();
    }

    private float EvaluateUltimatePlay(List<Card> hand, List<Card> field, List<Card> play)
    {
        var remaining = new List<Card>(hand);
        foreach (var card in play)
        {
            remaining.Remove(card);
        }

        float score = 0f;

        score -= remaining.Count * 100f;
        score -= remaining.Count(c => c.IsJoker()) * 15f;
        score -= remaining.Count(c => c.Rank == 15) * 12f;
        score -= remaining.Count(c => c.Rank == 14) * 6f;

        score += play.Count * 10f;
        score += play.Count(c => c.IsJoker()) * 4f;

        if (enableEightCut && IsEightCut(play))
        {
            score += 30f;
        }

        if (field != null && field.Count > 0)
        {
            score += GetPlayStrengthDelta(field, play) * 2f;
        }
        else
        {
            score += EvaluateOpeningLead(play) * 1.5f;
        }

        score += EvaluateFutureFlexibility(remaining) * 0.8f;
        return score;
    }

    private float GetPlayStrengthDelta(List<Card> field, List<Card> play)
    {
        int fieldRank = GetFieldStrongestRank(field);
        int playRank = GetFieldStrongestRank(play);
        return GetCardStrength(playRank) - GetCardStrength(fieldRank);
    }

    private int GetFieldStrongestRank(List<Card> cards)
    {
        if (cards == null || cards.Count == 0)
        {
            return 3;
        }

        var (realCards, jokerCount) = GetRealCardsAndJokers(cards);
        if (IsStairWithJoker(realCards, jokerCount))
        {
            return GetStairMaxRank(realCards, jokerCount);
        }

        if (realCards.Count == 0 && jokerCount > 0)
        {
            return 16;
        }

        return realCards.Count > 0 ? realCards[0].Rank : 3;
    }

    private float EvaluateOpeningLead(List<Card> play)
    {
        if (play == null || play.Count == 0)
        {
            return 0f;
        }

        float score = 0f;
        if (IsStairWithJoker(play.Where(c => !c.IsJoker()).ToList(), play.Count(c => c.IsJoker())))
        {
            score += play.Count * 8f;
        }
        else if (play.All(c => c.Rank == play[0].Rank))
        {
            score += play.Count * 6f;
        }

        return score;
    }

    private float EvaluateFutureFlexibility(List<Card> remaining)
    {
        if (remaining == null || remaining.Count == 0)
        {
            return 100f;
        }

        var rankGroups = remaining
            .Where(c => !c.IsJoker())
            .GroupBy(c => c.Rank)
            .Select(g => g.Count())
            .ToList();

        float score = rankGroups.Sum(g => g >= 2 ? 4f : 0f);

        var suits = remaining.Where(c => !c.IsJoker()).GroupBy(c => c.Suit).ToList();
        foreach (var suit in suits)
        {
            if (suit.Count() >= 3)
            {
                score += 5f;
            }
        }

        return score;
    }

    private List<Card> SelectRankGroupResponse(List<Card> hand, int fieldCount, int fieldStrength)
    {
        var joker = hand.FirstOrDefault(c => c.IsJoker());
        var candidates = hand
            .Where(c => !c.IsJoker())
            .GroupBy(c => c.Rank)
            .Where(g => GetCardStrength(g.Key) > fieldStrength)
            .Select(g => new { Rank = g.Key, Cards = g.ToList() })
            .ToList();

        if (cpuDifficulty >= CpuDifficulty.Strong)
        {
            candidates = candidates
                .OrderByDescending(c => GetCardStrength(c.Rank))
                .ToList();
        }
        else
        {
            candidates = candidates
                .OrderBy(c => GetCardStrength(c.Rank))
                .ToList();
        }

        foreach (var candidateGroup in candidates)
        {
            var candidateCards = candidateGroup.Cards.ToList();
            if (joker != null)
            {
                candidateCards.Add(joker);
            }

            if (candidateCards.Count < fieldCount)
            {
                continue;
            }

            var combo = FindBindSatisfiedCombo(candidateCards, fieldCount);
            if (combo.Count > 0)
            {
                return combo;
            }
        }

        if (joker != null && fieldCount == 1 && GetCardStrength(16) > fieldStrength)
        {
            var singleJoker = new List<Card> { joker };
            return IsBindSatisfied(singleJoker) ? singleJoker : new List<Card>();
        }

        return new List<Card>();
    }

    private List<Card> SelectStairResponse(List<Card> hand, List<Card> field, List<Card> fieldRealCards, int fieldCount, int fieldStrength)
    {
        Suit? fieldSuit = fieldRealCards.Count > 0 ? fieldRealCards[0].Suit : (Suit?)null;
        var stairs = FindStairSequences(hand)
            .Where(seq => seq.Count == fieldCount)
            .Where(seq => fieldSuit == null || seq[0].Suit == fieldSuit.Value)
            .Where(seq => GetCardStrength(seq.Max(c => c.Rank)) > fieldStrength)
            .Where(IsBindSatisfied)
            .ToList();

        if (stairs.Count == 0)
        {
            return new List<Card>();
        }

        var ordered = cpuDifficulty >= CpuDifficulty.Strong
            ? stairs.OrderByDescending(seq => GetCardStrength(seq.Max(c => c.Rank)))
            : stairs.OrderBy(seq => GetCardStrength(seq.Max(c => c.Rank)));

        return ordered.First();
    }

    private List<List<Card>> FindStairSequences(List<Card> hand)
    {
        if (!enableStair) return new List<List<Card>>();
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
        if (!enableStair) return false;
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

    private bool IsJokerStopTrigger(List<Card> played)
    {
        if (!enableJokerStop) return false;
        if (played == null || played.Count != 3) return false;
        if (played.Any(c => c.IsJoker())) return false;
        if (played.Select(c => c.Suit).Distinct().Count() != 1) return false;

        var ranks = played.Select(c => c.Rank).OrderBy(r => r).ToList();
        return ranks[0] == 3 && ranks[1] == 4 && ranks[2] == 5;
    }

    private void ConsumeJokerStopTurn()
    {
        if (jokerStopTurnsRemaining > 0)
        {
            jokerStopTurnsRemaining--;
        }
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

        passButton.onClick.AddListener(OnPassButton);
        if (kirikaeButton == null)
        {
            var kirikaeObj = GameObject.Find("KirikaeButton");
            if (kirikaeObj != null)
            {
                kirikaeButton = kirikaeObj.GetComponent<Button>();
            }
        }
        if (kirikaeButton != null)
        {
            kirikaeButton.onClick.AddListener(OnKirikaeButton);
            kirikaeButton.gameObject.SetActive(false);
        }

        players = new List<PlayerBase> { humanPlayer };
        players.AddRange(cpuPlayers);

        remainingPlayers = new List<PlayerBase>(players);
        currentGameCount = 1;
        currentTurnIndex = GetStartIndexFromPlayer(FindClubThreeHolder());
        AssignRankTextReferences();
        InitializeDebugPreviousRanks();
        UpdateCpuHeaderText();
        UpdatePreviousRankText();

        ApplySoloRuleSettings();

        StartTurn();
    }

    private void ApplySoloRuleSettings()
    {
        if (!SoloRuleSettings.IsSoloModeActive)
        {
            ApplyDefaultRuleSettings();
            return;
        }

        rules.Clear();

        cpuDifficulty = GetCpuDifficulty();

        enableBind = SoloRuleSettings.GetRuleEnabled("Bind");
        enableStair = SoloRuleSettings.GetRuleEnabled("Stair");
        enableSpade3Return = SoloRuleSettings.GetRuleEnabled("Spade3Return");
        enableSuitLock = SoloRuleSettings.GetRuleEnabled("SuitLock");
        enableJokerStop = SoloRuleSettings.GetRuleEnabled("JokerStop");
        enableFourStop = SoloRuleSettings.GetRuleEnabled("FourStop");
        enableSixStop = SoloRuleSettings.GetRuleEnabled("SixStop");
        enableEightCut = SoloRuleSettings.GetRuleEnabled("EightCut");

        forbidSpecialWin = SoloRuleSettings.GetRuleEnabled("ForbidSpecialWin");
        enableMiyakoOchi = SoloRuleSettings.GetRuleEnabled("MiyakoOchi");
        enableMiyakoOchiDemotion = enableMiyakoOchi;

        if (SoloRuleSettings.GetRuleEnabled("ElevenSilence")) rules.Add(new ElevenSilenceRule());
        if (SoloRuleSettings.GetRuleEnabled("EightCut")) rules.Add(new EightCutRule());
        if (SoloRuleSettings.GetRuleEnabled("Revolution")) rules.Add(new RevolutionRule());
        if (SoloRuleSettings.GetRuleEnabled("ElevenBack")) rules.Add(new ElevenBackRule());
        if (SoloRuleSettings.GetRuleEnabled("FiveSkip")) rules.Add(new FiveSkipRule());
        if (SoloRuleSettings.GetRuleEnabled("FourSingle")) rules.Add(new FourSingleRule());
        if (SoloRuleSettings.GetRuleEnabled("SixTrade")) rules.Add(new SixTradeRule());
        if (SoloRuleSettings.GetRuleEnabled("SevenPass")) rules.Add(new SevenPassRule());
        if (SoloRuleSettings.GetRuleEnabled("TenDiscard")) rules.Add(new TenDiscardRule());
        if (SoloRuleSettings.GetRuleEnabled("GreatChaos")) rules.Add(new GreatChaosRule());
        if (SoloRuleSettings.GetRuleEnabled("NineForce")) rules.Add(new NineForceRule());
        if (SoloRuleSettings.GetRuleEnabled("Barrier")) rules.Add(new BarrierRule());
        if (SoloRuleSettings.GetRuleEnabled("FreezeTwelve")) rules.Add(new FreezeTwelveRule());
        if (SoloRuleSettings.GetRuleEnabled("TwelvePenalty")) rules.Add(new TwelvePenaltyRule());

        if (!enableJokerStop)
        {
            jokerStopTurnsRemaining = 0;
        }
    }

    private void ApplyDefaultRuleSettings()
    {
        rules.Clear();

        cpuDifficulty = CpuDifficulty.Normal;

        enableBind = true;
        enableStair = true;
        enableSpade3Return = true;
        enableSuitLock = true;
        enableJokerStop = true;
        enableFourStop = true;
        enableSixStop = true;
        enableEightCut = true;
        forbidSpecialWin = false;
        enableMiyakoOchi = true;
        enableMiyakoOchiDemotion = true;

        rules.Add(new ElevenSilenceRule());
        rules.Add(new EightCutRule());
        rules.Add(new RevolutionRule());
        rules.Add(new ElevenBackRule());
        rules.Add(new FiveSkipRule());
        rules.Add(new FourSingleRule());
        rules.Add(new SixTradeRule());
        rules.Add(new SevenPassRule());
        rules.Add(new TenDiscardRule());
        rules.Add(new GreatChaosRule());
        rules.Add(new NineForceRule());
        rules.Add(new BarrierRule());
        rules.Add(new FreezeTwelveRule());
        rules.Add(new TwelvePenaltyRule());

    }

    private void UpdateCpuHeaderText()
    {
        if (cpu1NameText != null) cpu1NameText.text = cpuPlayers.Count >= 1 ? "CPU1" : "";
        if (cpu2NameText != null) cpu2NameText.text = cpuPlayers.Count >= 2 ? "CPU2" : "";
        if (cpu3NameText != null) cpu3NameText.text = cpuPlayers.Count >= 3 ? "CPU3" : "";
        if (playerNameText != null) playerNameText.text = "プレイヤー";
    }

    private void AssignRankTextReferences()
    {
        AssignTextReference(ref cpu1NameText, "CPU1NameText");
        AssignTextReference(ref cpu2NameText, "CPU2NameText");
        AssignTextReference(ref cpu3NameText, "CPU3NameText");
        AssignTextReference(ref cpu1PreviousRankText, "CPU1PreviousRankText");
        AssignTextReference(ref cpu2PreviousRankText, "CPU2PreviousRankText");
        AssignTextReference(ref cpu3PreviousRankText, "CPU3PreviousRankText");
        AssignTextReference(ref playerNameText, "PlayerNameText");
        AssignTextReference(ref playerPreviousRankText, "PlayerPreviousRankText");
        AssignTextReference(ref bindStatusText, "BindStatusText");
        AssignTextReference(ref ruleEffectText, "RuleEffectText");
    }

    private void AssignTextReference(ref TextMeshProUGUI target, string objectName)
    {
        if (target != null) return;
        target = FindTextByName(objectName);
    }

    private TextMeshProUGUI FindTextByName(string objectName)
    {
        var obj = GameObject.Find(objectName);
        if (obj != null)
        {
            return obj.GetComponent<TextMeshProUGUI>();
        }

        foreach (var text in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
        {
            if (!text.gameObject.scene.IsValid())
            {
                continue;
            }
            if (text.name == objectName)
            {
                return text;
            }
        }

        return null;
    }

    private void InitializeDebugPreviousRanks()
    {
        if (previousRoundRanks.Count > 0) return;

        previousRoundRanks.Clear();
        previousRoundRanks[humanPlayer] = 1;
        if (cpuPlayers.Count >= 1) previousRoundRanks[cpuPlayers[0]] = 2;
        if (cpuPlayers.Count >= 2) previousRoundRanks[cpuPlayers[1]] = 3;
        if (cpuPlayers.Count >= 3) previousRoundRanks[cpuPlayers[2]] = 4;
        SetPreviousRoundTitles();
    }

    private void UpdatePreviousRankText()
    {
        bool showRanks = previousRoundRanks.Count > 0;
        if (cpu1PreviousRankText != null)
        {
            cpu1PreviousRankText.gameObject.SetActive(showRanks);
            cpu1PreviousRankText.text = showRanks && cpuPlayers.Count >= 1 && previousRoundRanks.TryGetValue(cpuPlayers[0], out var rank1)
                ? GetRankDisplayText(rank1)
                : "";
        }
        if (cpu2PreviousRankText != null)
        {
            cpu2PreviousRankText.gameObject.SetActive(showRanks);
            cpu2PreviousRankText.text = showRanks && cpuPlayers.Count >= 2 && previousRoundRanks.TryGetValue(cpuPlayers[1], out var rank2)
                ? GetRankDisplayText(rank2)
                : "";
        }
        if (cpu3PreviousRankText != null)
        {
            cpu3PreviousRankText.gameObject.SetActive(showRanks);
            cpu3PreviousRankText.text = showRanks && cpuPlayers.Count >= 3 && previousRoundRanks.TryGetValue(cpuPlayers[2], out var rank3)
                ? GetRankDisplayText(rank3)
                : "";
        }
        if (playerPreviousRankText != null)
        {
            playerPreviousRankText.gameObject.SetActive(showRanks);
            playerPreviousRankText.text = showRanks && previousRoundRanks.TryGetValue(humanPlayer, out var playerRank)
                 ? GetRankDisplayText(playerRank)
                : "";
        }
    }


    void Update()
    {
        if (playButton != null && passButton != null)
        {
            UpdateButtonVisibility();
        }
        UpdateBindStatusText();
        UpdateRuleEffectText();
    }

    private void UpdateBindStatusText()
    {
        if (bindStatusText == null)
        {
            return;
        }

        var bindStatus = GetBindStatusText();
        var shouldShow = !string.IsNullOrEmpty(bindStatus);
        var nextText = shouldShow ? $"縛り: {bindStatus}" : "";

        UpdateStatusText(
            bindStatusText,
            nextText,
            shouldShow,
            ref lastBindStatusText,
            ref lastBindStatusVisible
        );
    }

    private void UpdateRuleEffectText()
    {
        if (ruleEffectText == null)
        {
            return;
        }

        var activeRules = GetActiveRuleLabels();
        var hasRules = activeRules.Count > 0;
        var nextText = hasRules ? $"発動中: {string.Join(" / ", activeRules)}" : "";

        UpdateStatusText(
            ruleEffectText,
            nextText,
            hasRules,
            ref lastRuleEffectText,
            ref lastRuleEffectVisible
        );
    }

    private void UpdateStatusText(
        TextMeshProUGUI target,
        string nextText,
        bool shouldShow,
        ref string cachedText,
        ref bool cachedVisibility
    )
    {
        if (target == null)
        {
            return;
        }

        if (shouldShow != cachedVisibility)
        {
            target.gameObject.SetActive(shouldShow);
            cachedVisibility = shouldShow;
        }

        if (nextText != cachedText)
        {
            target.text = nextText;
            cachedText = nextText;
        }
    }

    private string GetBindStatusText()
    {
        if (!isNumberBindActive && !isSuitBindActive)
        {
            return "";
        }

        string suitMessage = "";
        if (isSuitBindActive && boundSuits.Count > 0)
        {
            suitMessage = string.Join("・", boundSuits.Select(GetSuitLabel));
        }

        string numberMessage = "";
        if (isNumberBindActive && expectedNextRank > 0)
        {
            numberMessage = GetRankLabel(expectedNextRank);
        }

        if (isNumberBindActive && isSuitBindActive)
        {
            return $"{numberMessage} & {suitMessage}";
        }
        if (isNumberBindActive)
        {
            return $"{numberMessage} のみ";
        }

        if (string.IsNullOrEmpty(suitMessage))
        {
            return "";
        }

        return $"{suitMessage} のみ";
    }

    private List<string> GetActiveRuleLabels()
    {
        var labels = new List<string>();

        if (IsRevolutionActive)
        {
            labels.Add("革命");
        }
        if (isTempRevolution)
        {
            labels.Add("11バック");
        }
        if (IsElevenSilenceActive)
        {
            labels.Add("11静寂");
        }
        if (IsJokerStopActive)
        {
            labels.Add("ジョーカーストップ");
        }
        if (isNineForceActive)
        {
            labels.Add("9強制");
        }
        if (pendingEightCutCount > 0)
        {
            labels.Add("8切り");
        }
        if (pendingTwoCount > 0)
        {
            labels.Add("2流し");
        }
        if (isFourStopWindowActive)
        {
            labels.Add("4止め受付");
        }
        if (isSixStopWindowActive)
        {
            labels.Add("6止め受付");
        }
        if (isSevenPassMode)
        {
            labels.Add("7渡し");
        }
        if (isTenDiscardMode)
        {
            labels.Add("10捨て");
        }
        if (isSixTradeMode)
        {
            labels.Add("6交換");
        }
        if (isFreezeTwelveMode)
        {
            labels.Add("12凍結");
        }
        if (isSuitLockTurnActive && suitLockSuits.Count > 0)
        {
            var suitMessage = string.Join("・", suitLockSuits.Select(GetSuitLabel));
            labels.Add($"スートロック({suitMessage})");
        }
        if (isSingleOnlyTurn)
        {
            labels.Add("単発のみ");
        }

        return labels;
    }

    private void UpdateButtonVisibility()
    {
        if (isCpuTurnInProgress && !IsTradeSelectionActive() && !IsFreezeSelectionActive() && !IsSuitLockSelectionActive() && !IsMiyakoOchiSelectionActive())
        {
            SetActionButtonsActive(false);
            return;
        }
        if (!isPlayerTurn && !IsTradeSelectionActive() && !IsFreezeSelectionActive() && !IsSuitLockSelectionActive() && !IsMiyakoOchiSelectionActive())
        {
            SetActionButtonsActive(false);
            return;
        }

        if (playButton != null)
        {
            playButton.gameObject.SetActive(true);

            if (isSelectingTradeTarget)
            {
                playButton.interactable = true;
                if (passButton != null)
                {
                    passButton.gameObject.SetActive(true);
                    passButton.interactable = true;
                }
                if (kirikaeButton != null) kirikaeButton.gameObject.SetActive(false);
                return;
            }

            if (isSelectingSuitLock)
            {
                playButton.interactable = true;
                if (passButton != null)
                {
                    passButton.gameObject.SetActive(true);
                    passButton.interactable = true;
                }
                if (kirikaeButton != null) kirikaeButton.gameObject.SetActive(false);
                return;
            }

            if (isFreezeTwelveMode)
            {
                playButton.interactable = true;
                if (passButton != null)
                {
                    passButton.gameObject.SetActive(true);
                    passButton.interactable = true;
                }
                if (kirikaeButton != null) kirikaeButton.gameObject.SetActive(false);
                return;
            }

            if (isSevenPassMode || isTenDiscardMode || isSelectingTradeCards || isSelectingMiyakoOchiCards)
            {
                if (playButton != null)
                {
                    playButton.gameObject.SetActive(true);

                    int selectedCount = human.SelectCards(human.Hand).Count;

                    if (isSelectingTradeCards)
                    {
                        int required = Mathf.Min(pendingTradeCardCount, human.Hand.Count);
                        playButton.interactable = (selectedCount == required);
                    }
                    else if (isSelectingMiyakoOchiCards)
                    {
                        int required = Mathf.Min(miyakoTradeCount, human.Hand.Count);
                        playButton.interactable = (selectedCount == required);
                    }
                    else
                    {
                        int maxAllowed = Mathf.Min(pendingActionCardCount, human.Hand.Count);
                        playButton.interactable = (selectedCount <= maxAllowed);
                    }
                }
                if (passButton != null) passButton.gameObject.SetActive(false);
                if (kirikaeButton != null) kirikaeButton.gameObject.SetActive(false);
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
                passButton.gameObject.SetActive(!isFieldEmpty && !suppressPassAfterPlay);
                if (!isFieldEmpty)
                {
                    bool canPass = true;
                    if (isNineForceActive)
                    {
                        canPass = !HasValidPlayForNineForce(human.Hand, lastPlayedCards);
                    }
                    passButton.interactable = canPass;
                }
            }
            if (kirikaeButton != null) kirikaeButton.gameObject.SetActive(false);
        }
    }

    private void SetActionButtonsActive(bool isActive)
    {
        if (playButton != null) playButton.gameObject.SetActive(isActive);
        if (passButton != null) passButton.gameObject.SetActive(isActive);
        if (kirikaeButton != null) kirikaeButton.gameObject.SetActive(isActive);
    }

    private bool TryLockPlayerAction()
    {
        if (isPlayerActionLocked)
        {
            return false;
        }

        isPlayerActionLocked = true;
        if (actionLockCoroutine != null)
        {
            StopCoroutine(actionLockCoroutine);
        }
        actionLockCoroutine = StartCoroutine(ReleaseActionLockAfterDelay(ActionLockSeconds));
        return true;
    }

    private IEnumerator ReleaseActionLockAfterDelay(float delaySeconds)
    {
        yield return new WaitForSecondsRealtime(delaySeconds);
        isPlayerActionLocked = false;
        actionLockCoroutine = null;
    }

    private bool IsAnyCardSelected()
    {
        foreach (Transform child in handAreaPlayer)
        {
            var cv = child.GetComponent<CardView>();
            if (cv != null && cv.IsSelected)
            {
                return true;
            }
        }
        return false;
    }

    private bool IsTradeSelectionActive()
    {
        return isSelectingTradeTarget || isSelectingTradeCards;
    }
    private bool IsMiyakoOchiSelectionActive()
    {
        return isSelectingMiyakoOchiCards;
    }

    private bool IsFreezeSelectionActive()
    {
        return isFreezeTwelveMode;
    }
    private bool IsSuitLockSelectionActive()
    {
        return isSelectingSuitLock;
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

        if (isSevenPassMode || isTenDiscardMode || isSelectingTradeCards || isSelectingMiyakoOchiCards)
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

            bool canPlay = isSelectingMiyakoOchiCards || playableCards.Contains(card);
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



    private void ApplyGreatChaos()
    {
        var activePlayers = remainingPlayers.Where(player => player.Hand.Count > 0).ToList();
        if (activePlayers.Count <= 1)
        {
            return;
        }

        var hands = activePlayers.Select(player => new List<Card>(player.Hand)).ToList();

        for (int i = 0; i < hands.Count; i++)
        {
            int rand = Random.Range(i, hands.Count);
            (hands[i], hands[rand]) = (hands[rand], hands[i]);
        }

        for (int i = 0; i < activePlayers.Count; i++)
        {
            activePlayers[i].Hand = hands[i];
            activePlayers[i].SelectedCards.Clear();
        }

        if (human != null)
        {
            CreatePlayerCardSlots(human.Hand.Count);
            PopulatePlayerHand(human);
        }

        if (cpuPlayers.Count > 0) PopulateCpuHandAsBack(handAreaCPU1, cpuPlayers[0].Hand.Count);
        if (cpuPlayers.Count > 1) PopulateCpuHandAsBack(handAreaCPU2, cpuPlayers[1].Hand.Count);
        if (cpuPlayers.Count > 2) PopulateCpuHandAsBack(handAreaCPU3, cpuPlayers[2].Hand.Count);
    }

    private IEnumerator PlayerPlayRoutine(List<Card> played)
    {
        yield return StartCoroutine(DisplayPlayedCardsOnTable(human, played));

        // 7渡しや10捨てが開始された場合、DisplayPlayedCardsOnTableの中で処理が分岐するため、
        // ここには到達しないはずだが、念のためガード。
        if (isSevenPassMode || isTenDiscardMode || isSixTradeMode || isFreezeTwelveMode)
        {
            yield break;
        }
        if (isSelectingSuitLock)
        {
            yield break;
        }

        // ★修正ポイント: スペード3返しなどにより DisplayPlayedCardsOnTable の中で場が流れた場合、
        // ClearTableAndRestart() が呼ばれ、既に StartTurn() によってターンが再開されている。
        // そのため、ここで EndTurn() を実行するとターンが二重に進んでしまうため、スキップする。
        //
        // 場が空で (lastPlayedCards.Count == 0)、かつ8切りによるターン継続ではない (!skipTurnAdvance) 
        // 場合は、場が流れたと判断できる。
        if (lastPlayedCards.Count == 0 && !skipTurnAdvance)
        {
            yield break;
        }

        EndTurn();
    }

    public void OnPlayButton()
    {
        if (isPlayerActionInProgress) return;
        if (!TryLockPlayerAction()) return;
        if (isSelectingSuitLock)
        {
            ConfirmSuitLockSelection();
            return;
        }

        if (isSelectingTradeTarget)
        {
            ConfirmTradeTargetSelection();
            return;
        }

        if (isSelectingMiyakoOchiCards)
        {
            HandleMiyakoOchiSelection();
            return;
        }

        if (isSelectingTradeCards)
        {
            HandleTradeCardSelection();
            return;
        }
        if (isFreezeTwelveMode)
        {
            ConfirmFreezeTargetSelection();
            return;
        }
        if (!isPlayerTurn) return;

        if (passButton != null) passButton.gameObject.SetActive(false);
        suppressPassAfterPlay = true;

        if (isSevenPassMode)
        {
            var selected = human.SelectCards(human.Hand);
            int maxAllowed = Mathf.Min(pendingActionCardCount, human.Hand.Count);

            if (selected.Count > maxAllowed)
            {
                EnqueueMessage($"{maxAllowed}枚まで選べます");
                ClearPassMessage();
                return;
            }

            playButton.interactable = false;
            isPlayerActionInProgress = true;
            StartCoroutine(ExecuteSevenPassTransfer(human, selected));
        }
        else if (isTenDiscardMode)
        {
            var selected = human.SelectCards(human.Hand);
            int maxAllowed = Mathf.Min(pendingActionCardCount, human.Hand.Count);

            if (selected.Count > maxAllowed)
            {
                EnqueueMessage($"{maxAllowed}枚まで選べます");
                ClearPassMessage();
                return;
            }

            playButton.interactable = false;
            isPlayerActionInProgress = true;
            StartCoroutine(ExecuteTenDiscardAction(human, selected));
        }
        else
        {
            if (playButton != null && !playButton.interactable) return;
            if (playButton != null) playButton.interactable = false;
            isPlayerActionInProgress = true;

            var played = human.SelectCards(human.Hand); ;

            if (played == null || played.Count == 0)
            {
                Debug.Log("カードが選択されていません。");
                if (playButton != null) playButton.interactable = true;
                isPlayerActionInProgress = false;
                return;
            }

            if (!IsValidPlay(human.Hand, played, lastPlayedCards))
            {
                Debug.Log("そのカードは出せません。");
                if (playButton != null) playButton.interactable = true;
                isPlayerActionInProgress = false;
                return;
            }

            played = played.OrderBy(c => c.Rank).ThenBy(c => c.Suit).ToList();

            StartCoroutine(PlayerPlayRoutine(played));
        }
    }

    private bool IsValidPlay(List<Card> hand, List<Card> selected, List<Card> field)
    {
        if (selected == null || selected.Count == 0) return false;

        if (IsJokerStopActive && selected.Any(c => c.IsJoker()))
        {
            return false;
        }

        if (isFourStopWindowActive)
        {
            return IsFourStopCandidate(selected, GetRequiredFourStopCount());
        }
        if (isSixStopWindowActive)
        {
            return IsSixStopCandidate(selected, GetRequiredSixStopCount());
        }

        if (isSingleOnlyTurn && selected.Count != 1)
        {
            return false;
        }

        var (realSelected, jokerCount) = GetRealCardsAndJokers(selected);

        if (!IsSuitLockSatisfied(selected))
        {
            return false;
        }

        // --- 単体・役のチェック ---

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

        // --- 場に出ているカードとの比較 ---
        if (field != null && field.Count > 0)
        {
            bool isSpade3Counter = false;
            // ★追加: スペードの3はジョーカー単体出しに勝てるルール
            // 場がジョーカー1枚の場合のみ
            if (enableSpade3Return && field.Count == 1 && field[0].IsJoker())
            {
                // 自分が出のが「スペード」かつ「Rank3」かつ「1枚」ならOK
                if (selected.Count == 1 && selected[0].Suit == Suit.Spade && selected[0].Rank == 3)
                {
                    isSpade3Counter = true;
                }
            }

            // 枚数チェック
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
                // 場がジョーカーのみの場合は最強(Rank 16相当)とする
                if (realField.Count == 0 && fieldJokerCount > 0)
                {
                    fieldStrongestRank = 16;
                }
                else
                {
                    fieldStrongestRank = realField.Count > 0 ? realField[0].Rank : 3;
                }

                // 自分が出すカードがジョーカーのみの場合は最強(Rank 16相当)とする
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

            // 同じ強さ以下なら出せない
            if (!isSpade3Counter && selectedStrength <= fieldStrength) return false;
        }

        if (field != null && field.Count > 0 && !IsBindSatisfied(selected))
        {
            return false;
        }

        return true;
    }

    private bool HasValidPlayForNineForce(List<Card> hand, List<Card> field)
    {
        if (hand == null || hand.Count == 0) return false;
        if (field == null || field.Count == 0) return false;

        int requiredCount = isFourStopWindowActive
            ? GetRequiredFourStopCount()
            : isSixStopWindowActive
                ? GetRequiredSixStopCount()
                : field.Count;

        if (requiredCount <= 0 || hand.Count < requiredCount) return false;

        var combo = new List<Card>(requiredCount);
        bool found = false;

        void Search(int startIndex)
        {
            if (found) return;
            if (combo.Count == requiredCount)
            {
                if (IsValidPlay(hand, combo, field))
                {
                    found = true;
                }
                return;
            }

            for (int i = startIndex; i <= hand.Count - (requiredCount - combo.Count); i++)
            {
                combo.Add(hand[i]);
                Search(i + 1);
                combo.RemoveAt(combo.Count - 1);
                if (found) return;
            }
        }

        Search(0);
        return found;
    }

    private bool IsStairWithJoker(List<Card> realCards, int jokerCount)
    {
        if (!enableStair) return false;
        int totalCards = realCards.Count + jokerCount;
        // ★修正: 合計枚数を3枚または4枚に制限
        if (totalCards < 3 || totalCards > 4) return false;

        if (realCards.Count == 0)
        {
            // ジョーカーのみで階段を構成する場合 (例: J-J-J => 3枚階段)
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

        // ★修正: 既に totalCards でチェックしているが、ロジックの確認のため再度チェック
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
        if (isSelectingSuitLock)
        {
            CycleSuitLockSelection();
            return;
        }
        if (isSelectingTradeTarget)
        {
            CycleTradeTargetSelection();
            return;
        }
        if (isFreezeTwelveMode)
        {
            CycleFreezeTargetSelection();
            return;
        }
        if (isPlayerActionInProgress) return;
        if (players[currentTurnIndex] != humanPlayer) return;
        if (!TryLockPlayerAction()) return;
        isPlayerActionInProgress = true;
        if (passButton != null) passButton.interactable = false;
        HandlePass();
    }

    private void OnKirikaeButton()
    {
        if (isFreezeTwelveMode)
        {
            CycleFreezeTargetSelection();
        }
    }

    private IEnumerator DisplayPlayedCardsOnTable(PlayerBase currentPlayer, List<Card> played)
    {
        float spacing = 50f;
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

        // --- プレイ前の場のカードを一時保存 (スペード3返しの判定に使う) ---
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
            RemovePlayedCardsFromUI(played);
        }

        lastPlayedCards = new List<Card>(played);
        lastPlayedPlayerIndex = players.IndexOf(currentPlayer);
        passCount = 0;

        if (IsJokerStopTrigger(played))
        {
            jokerStopTurnsRemaining = 2;
            EnqueueMessage("ジョーカーストップ発動!");
        }

        // ★★★ スペード3返しのチェックと強制流し処理 ★★★
        bool isSpade3Counter = false;

        // 場のカードがジョーカー1枚 (fieldBeforePlayを使う)
        if (fieldBeforePlay.Count == 1 && fieldBeforePlay[0].IsJoker())
        {
            // 出したカードがスペード3の1枚出し
            if (played.Count == 1 && played[0].Suit == Suit.Spade && played[0].Rank == 3)
            {
                isSpade3Counter = true;
            }
        }

        if (enableSpade3Return && isSpade3Counter)
        {
            var winContext = new WinContext
            {
                HasPlayContext = true,
                PlayedCards = new List<Card>(played),
                IsEightCut = enableEightCut && (IsEightCut(played)),
                IsSevenPass = false,
                IsTenDiscard = false
            };
            CheckForWin(currentPlayer, winContext);
            if (isGameOver) yield break;

            EnqueueMessage("スペード3返し!場が流れます。");
            // ★修正: 強制流しの前に一時的な革命状態をリセット
            isTempRevolution = false;

            // 強制的に場を流す (lastPlayedPlayerIndexに基づいて次のターン開始プレイヤーが決定される)
            yield return new WaitForSeconds(1.0f);
            yield return StartCoroutine(ClearTableAndRestart());
            yield break; // 強制流し後は通常のターン終了処理（EndTurn）をスキップ
        }


        List<Card> effectivePlayedCards = GetEffectivePlayedCards(played);
        var state = new GameState(new List<Card>(lastPlayedCards), currentTurnIndex);

        bool isElevenSilenceActive = IsElevenSilenceActive;

        if (isElevenSilenceActive)
        {
            state.IsElevenSilence = true;
        }
        else
        {
            foreach (var rule in rules)
            {
                if (rule.CanApply(effectivePlayedCards, state))
                {
                    rule.Apply(effectivePlayedCards, state);
                    if (state.IsElevenSilence) break;
                }
            }
        }

        bool shouldDeferWinCheck = state.SevenPassCount > 0 || state.TenDiscardCount > 0;
        if (!shouldDeferWinCheck)
        {
            var winContext = new WinContext
            {
                HasPlayContext = true,
                PlayedCards = new List<Card>(played),
                IsEightCut = enableEightCut && (state.IsEightCut || IsEightCut(played)),
                IsSevenPass = false,
                IsTenDiscard = false
            };
            CheckForWin(currentPlayer, winContext);
            if (isGameOver) yield break;
        }

        bool fourStopTriggered = false;
        bool sixStopTriggered = false;
        if (!state.IsElevenSilence)
        {
            fourStopTriggered = isFourStopWindowActive && IsFourStopCandidate(played, GetRequiredFourStopCount());
            if (fourStopTriggered)
            {
                state.TriggerRevolution = false;
            }
            sixStopTriggered = isSixStopWindowActive && IsSixStopCandidate(played, GetRequiredSixStopCount());
            if (sixStopTriggered)
            {
                state.TriggerRevolution = false;
                state.SixTradeCount = 0;
            }
        }

        if (state.IsElevenSilence)
        {
            bool triggeredElevenSilenceThisPlay = !isElevenSilenceActive;
            if (triggeredElevenSilenceThisPlay)
            {
                elevenSilenceFieldsRemaining = 2;
            }
            EnqueueMessage(triggeredElevenSilenceThisPlay ? "11サイレンス!" : "11サイレンス中!");
        }

        if (state.ForceSingleNextTurn)
        {
            forceSingleNextTurn = true;
            EnqueueMessage("4シングル! 次のターンは1枚出しのみ。");
        }

        if (state.TriggerNineForce)
        {
            isNineForceActive = true;
            EnqueueMessage("9フォース発動!");
        }

        if (state.TriggerBarrier)
        {
            ActivateBarrier(currentPlayer);
        }

        if (state.TriggerRevolution)
        {
            isRevolution = !isRevolution;
            EnqueueMessage(isRevolution ? "革命開始!" : "革命終了!");
        }

        if (state.IsElevenBack)
        {
            EnqueueMessage("11バック!");
            isTempRevolution = true;
        }

        UpdateBindState(fieldBeforePlay, effectivePlayedCards);

        if (IsSuitLockTrigger(effectivePlayedCards))
        {
            pendingSuitLockSelection = true;
            pendingSuitLockPlayer = currentPlayer;
        }

        if (state.TriggerGreatChaos)
        {
            EnqueueMessage("大混乱! 手札がランダムに入れ替わります。");
            yield return new WaitForSeconds(1.0f);
            ApplyGreatChaos();
        }
        if (state.TriggerTwelvePenalty)
        {
            ApplyTwelvePenalty();
            if (isGameOver) yield break;
        }
        if (state.FreezeTwelveCount > 0 && remainingPlayers.Contains(currentPlayer))
        {
            if (state.FreezeTwelveCount >= 4)
            {
                ApplyFreezeTwelveToAll(currentPlayer);
            }
            else if (currentPlayer is HumanPlayer)
            {
                BeginFreezeTargetSelection(currentPlayer, state.FreezeTwelveCount);
                yield break;
            }
            else
            {
                ApplyFreezeTwelveForCpu(currentPlayer, state.FreezeTwelveCount);
            }
        }

        pendingSkipCount = state.SkipCount;
        lastSkippedCount = state.SkipCount;
        if (pendingSkipCount > 0)
        {
            EnqueueMessage($"{pendingSkipCount}人飛ばし!");
        }

        bool eightCutTriggered = state.IsEightCut;

        if (fourStopTriggered)
        {
            isFourStopWindowActive = false;
            pendingEightCutCount = 0;
            EnqueueMessage("4止め!");
            yield return new WaitForSeconds(1.0f);
            yield return StartCoroutine(ClearTableAndRestart());
            yield break;
        }
        if (sixStopTriggered)
        {
            isSixStopWindowActive = false;
            pendingTwoCount = 0;
            EnqueueMessage("6止め!");
            yield return new WaitForSeconds(1.0f);
            yield return StartCoroutine(ClearTableAndRestart());
            yield break;
        }

        if (eightCutTriggered)
        {
            EnqueueMessage("8切り!");

            if (IsFourStopWindowEligible(effectivePlayedCards))
            {
                isFourStopWindowActive = true;
                pendingEightCutCount = effectivePlayedCards.Count(c => c.Rank == 8);
                pendingSkipCount = 0;
                lastSkippedCount = 0;
            }
            else
            {
                yield return new WaitForSeconds(1.0f);
                foreach (Transform child in tableArea) Destroy(child.gameObject);
                lastPlayedCards.Clear();
                passCount = 0;

                pendingSkipCount = 0;
                // 8切りでも一時的な革命状態をリセット
                isTempRevolution = false;
                isNineForceActive = false;
                ResetBindState();
                ConsumeElevenSilenceField();

                if (state.KeepTurn && remainingPlayers.Contains(currentPlayer))
                {
                    skipTurnAdvance = true;
                }
            }
        }

        if (!state.IsElevenSilence)
        {
            if (IsSixStopWindowEligible(effectivePlayedCards))
            {
                isSixStopWindowActive = true;
                pendingTwoCount = effectivePlayedCards.Count(c => c.Rank == 15);
            }
            else
            {
                isSixStopWindowActive = false;
                pendingTwoCount = 0;
            }
        }


        if (state.SixTradeCount > 0 && remainingPlayers.Contains(currentPlayer))
        {
            Debug.Log($"6トレードシーケンス開始: {state.SixTradeCount}枚");
            isSixTradeMode = true;
            pendingTradeCardCount = state.SixTradeCount;

            StartCoroutine(HandleSixTradeSequence(currentPlayer));
            yield break;
        }

        // ★修正: 7渡し処理
        if (state.SevenPassCount > 0 && remainingPlayers.Contains(currentPlayer))
        {
            // まず発動メッセージを表示
            EnqueueMessage("7渡し発動!");

            // メッセージを読ませるために少し待機 (モード切替前)
            yield return new WaitForSeconds(1.5f);

            Debug.Log($"7渡しシーケンス開始: {state.SevenPassCount}枚");
            isSevenPassMode = true; // ここでモードON
            pendingActionCardCount = state.SevenPassCount;

            // シーケンス開始
            StartCoroutine(HandleSevenPassSequence(currentPlayer));
            yield break;
        }

        // ★修正: 10捨て処理
        if (state.TenDiscardCount > 0 && remainingPlayers.Contains(currentPlayer))
        {
            // まず発動メッセージを表示
            EnqueueMessage("10捨て発動!");

            // メッセージを読ませるために少し待機
            yield return new WaitForSeconds(1.5f);

            Debug.Log($"10捨てシーケンス開始: {state.TenDiscardCount}枚");
            isTenDiscardMode = true; // ここでモードON
            pendingActionCardCount = state.TenDiscardCount;

            // シーケンス開始
            StartCoroutine(HandleTenDiscardSequence(currentPlayer));
            yield break;
        }

        if (eightCutTriggered)
        {
            // skipTurnAdvance true なら EndTurn でループ
        }

        if (pendingSkipCount > 0)
        {
        }
        if (pendingSuitLockSelection && !isSixTradeMode && !isSevenPassMode && !isTenDiscardMode)
        {
            if (TryResolvePendingSuitLockSelection())
            {
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
        if (isNineForceActive && lastPlayedCards != null && lastPlayedCards.Count > 0)
        {
            var currentPlayer = players[currentTurnIndex];
            if (HasValidPlayForNineForce(currentPlayer.Hand, lastPlayedCards))
            {
                EnqueueMessage("9フォース中は出せるカードがある限りパスできません。");
                isPlayerActionInProgress = false;
                if (passButton != null) passButton.interactable = true;
                return;
            }
        }
        passCount++;

        // ★修正: 場を流す条件の計算
        // 「まだあがっていない人数 - 1」から「スキップされた人数」を引く
        // 例: 4人プレイで1人スキップされた場合、残り2人がパスすれば流れる (4 - 1 - 1 = 2回)
        int requiredPasses = remainingPlayers.Count - 1 - lastSkippedCount;

        // 安全策: マイナスにならないように補正
        if (requiredPasses < 0) requiredPasses = 0;

        Debug.Log($"Pass! passCount: {passCount}, required: {requiredPasses} (Skipped: {lastSkippedCount})");

        if (passCount >= requiredPasses)
        {
            StartCoroutine(ClearTableAndRestart());
        }
        else
        {
            EndTurn();
        }
    }

    private bool IsFreezePassActive(PlayerBase player)
    {
        return freezePassCounts.TryGetValue(player, out int count) && count > 0;
    }

    private void ConsumeFreezePass(PlayerBase player)
    {
        if (!freezePassCounts.TryGetValue(player, out int count)) return;
        count--;
        if (count <= 0)
        {
            freezePassCounts.Remove(player);
        }
        else
        {
            freezePassCounts[player] = count;
        }
    }

    private void ActivateBarrier(PlayerBase player)
    {
        barrierCounts[player] = 1;
        EnqueueMessage($"{player.Name} はバリアを獲得!");
    }

    private bool TryConsumeBarrier(PlayerBase player, string effectName)
    {
        if (!barrierCounts.TryGetValue(player, out int count) || count <= 0)
        {
            return false;
        }

        count--;
        if (count <= 0)
        {
            barrierCounts.Remove(player);
        }
        else
        {
            barrierCounts[player] = count;
        }

        EnqueueMessage($"{player.Name} のバリアで{effectName}を無効化!");
        return true;
    }
    private bool TryConsumeBarrierForSkipCandidate(PlayerBase player)
    {
        return TryConsumeBarrier(player, "5飛ばし");
    }
    private IEnumerator HandleFreezePassTurn(PlayerBase player)
    {
        isPlayerTurn = false;
        ConsumeFreezePass(player);

        if (playButton != null) playButton.gameObject.SetActive(false);
        if (passButton != null) passButton.gameObject.SetActive(false);
        if (kirikaeButton != null) kirikaeButton.gameObject.SetActive(false);

        EnqueueMessage($"フリーズ THE 12: {player.Name} はパス!");
        yield return new WaitForSeconds(0.8f);

        HandleForcedPass();
    }

    private void HandleForcedPass()
    {
        if (lastPlayedCards == null || lastPlayedCards.Count == 0)
        {
            EndTurn();
            return;
        }
        passCount++;

        int requiredPasses = remainingPlayers.Count - 1 - lastSkippedCount;
        if (requiredPasses < 0) requiredPasses = 0;

        if (passCount >= requiredPasses)
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
        lastSkippedCount = 0; // ★追加: リセット

        isTempRevolution = false;
        isNineForceActive = false;
        pendingSkipCount = 0;
        skipTurnAdvance = false;
        isFourStopWindowActive = false;
        pendingEightCutCount = 0;
        isSixStopWindowActive = false;
        pendingTwoCount = 0;
        ResetBindState();
        ConsumeElevenSilenceField();

        if (lastPlayedPlayerIndex < 0) lastPlayedPlayerIndex = 0;

        PlayerBase lastPlayer = players[lastPlayedPlayerIndex];

        if (remainingPlayers.Contains(lastPlayer))
        {
            currentTurnIndex = lastPlayedPlayerIndex; // 最後にカードを出した人から
        }
        else
        {
            // 最後にカードを出した人が既に上がっている場合、その次の人から
            int nextIdx = (lastPlayedPlayerIndex + 1) % players.Count;
            while (!remainingPlayers.Contains(players[nextIdx]))
            {
                nextIdx = (nextIdx + 1) % players.Count;
            }
            currentTurnIndex = nextIdx;
        }

        yield return new WaitForSeconds(0.6f);
        isCpuTurnInProgress = false;
        StartTurn();
    }
    private void ConsumeElevenSilenceField()
    {
        if (elevenSilenceFieldsRemaining > 0)
        {
            elevenSilenceFieldsRemaining--;
        }
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
            if (isSevenPassMode || isTenDiscardMode || isSixTradeMode || isFreezeTwelveMode)
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

    private bool IsEightCut(List<Card> played)
    {
        if (played == null || played.Count == 0) return false;
        return played.Any(c => c.Rank == 8);
    }
    private bool IsFourStopWindowEligible(List<Card> effectivePlayed)
    {
        if (!enableFourStop) return false;
        if (effectivePlayed == null || effectivePlayed.Count == 0) return false;
        if (!effectivePlayed.All(c => c.Rank == 8)) return false;
        return effectivePlayed.Count <= 2;
    }

    private bool IsSixStopWindowEligible(List<Card> effectivePlayed)
    {
        if (!enableSixStop) return false;
        if (effectivePlayed == null || effectivePlayed.Count == 0) return false;
        if (!effectivePlayed.All(c => c.Rank == 15)) return false;
        return effectivePlayed.Count <= 2;
    }

    private int GetRequiredSixStopCount()
    {
        if (!isSixStopWindowActive) return 0;
        return pendingTwoCount switch
        {
            1 => 2,
            2 => 4,
            _ => 0
        };
    }

    private int GetRequiredFourStopCount()
    {
        if (!isFourStopWindowActive) return 0;
        return pendingEightCutCount switch
        {
            1 => 2,
            2 => 4,
            _ => 0
        };
    }

    private bool IsSixStopCandidate(List<Card> selected, int requiredCount)
    {
        if (requiredCount <= 0 || selected == null) return false;
        if (selected.Count != requiredCount) return false;

        var (realSelected, jokerCount) = GetRealCardsAndJokers(selected);

        if (realSelected.Any(c => c.Rank != 6)) return false;

        return realSelected.Count + jokerCount == requiredCount;
    }

    private bool IsFourStopCandidate(List<Card> selected, int requiredCount)
    {
        if (requiredCount <= 0 || selected == null) return false;
        if (selected.Count != requiredCount) return false;

        var (realSelected, jokerCount) = GetRealCardsAndJokers(selected);

        if (realSelected.Any(c => c.Rank != 4)) return false;

        return realSelected.Count + jokerCount == requiredCount;
    }



    private List<Card> GetFourStopCards(List<Card> hand, int requiredCount)
    {
        if (requiredCount <= 0) return new List<Card>();
        if (hand == null || hand.Count == 0) return new List<Card>();

        var fours = hand.Where(c => !c.IsJoker() && c.Rank == 4).ToList();

        var candidates = new List<Card>();
        candidates.AddRange(fours);
        if (!IsJokerStopActive)
        {
            var joker = hand.FirstOrDefault(c => c.IsJoker());
            if (joker != null) candidates.Add(joker);
        }

        if (candidates.Count < requiredCount) return new List<Card>();
        return candidates.Take(requiredCount).ToList();
    }
    private List<Card> GetSixStopCards(List<Card> hand, int requiredCount)
    {
        if (requiredCount <= 0) return new List<Card>();
        if (hand == null || hand.Count == 0) return new List<Card>();

        var sixes = hand.Where(c => !c.IsJoker() && c.Rank == 6).ToList();

        var candidates = new List<Card>();
        candidates.AddRange(sixes);
        if (!IsJokerStopActive)
        {
            var joker = hand.FirstOrDefault(c => c.IsJoker());
            if (joker != null) candidates.Add(joker);
        }

        if (candidates.Count < requiredCount) return new List<Card>();
        return candidates.Take(requiredCount).ToList();
    }
    private List<Card> GetLegalCardsForUI(List<Card> hand, List<Card> field)
    {
        if (IsJokerStopActive)
        {
            hand = hand.Where(c => !c.IsJoker()).ToList();
        }
        if (isFourStopWindowActive)
        {
            return GetFourStopCards(hand, GetRequiredFourStopCount());
        }
        if (isSixStopWindowActive)
        {
            return GetSixStopCards(hand, GetRequiredSixStopCount());
        }
        if (field == null || field.Count == 0)
        {
            return new List<Card>(hand);
        }

        List<Card> playable = new List<Card>();
        int fieldCount = field.Count;

        var (fieldRealCards, fieldJokers) = GetRealCardsAndJokers(field);
        bool isFieldStair = IsStairWithJoker(fieldRealCards, fieldJokers);

        // ★UI用: ジョーカー込みの強さ計算を共通化（階段/同ランク）
        int fieldStrongestRank;
        if (isFieldStair)
        {
            fieldStrongestRank = GetStairMaxRank(fieldRealCards, fieldJokers);
        }
        else if (fieldRealCards.Count == 0 && fieldJokers > 0)
        {
            fieldStrongestRank = 16;
        }
        else
        {
            fieldStrongestRank = fieldRealCards.Count > 0 ? fieldRealCards[0].Rank : 3;
        }

        int fieldStrength = GetCardStrength(fieldStrongestRank);


        if (!isFieldStair)
        {
            // ★スペード3のチェック
            // 場がジョーカー単体なら、手札にスペード3があれば出す候補に入れる
            if (field.Count == 1 && field[0].IsJoker())
            {
                var spade3 = hand.FirstOrDefault(c => c.Suit == Suit.Spade && c.Rank == 3);
                if (spade3 != null && IsBindSatisfied(new List<Card> { spade3 }))
                {
                    playable.Add(spade3);
                }
            }

            var joker = hand.FirstOrDefault(c => c.IsJoker());
            var groups = hand.Where(c => !c.IsJoker()).GroupBy(c => c.Rank);

            foreach (var g in groups)
            {
                // ★重要: スペード3が普通の3として出せるのは、場がジョーカーでない場合のみ。
                // 既にジョーカー単体の場合は上のifブロックでスペード3のみがチェックされている。
                // ここでは普通の3としての処理を継続。
                if (g.Count() >= fieldCount)
                {
                    if (GetCardStrength(g.Key) > fieldStrength)
                    {
                        var candidate = g.ToList();
                        if (joker != null)
                        {
                            candidate.Add(joker);
                        }
                        foreach (var playableCard in GetPlayableCardsFromGroup(candidate, fieldCount))
                        {
                            if (!playable.Contains(playableCard))
                            {
                                playable.Add(playableCard);
                            }
                        }
                    }
                }
            }
            if (joker != null && fieldCount == 1 && GetCardStrength(16) > fieldStrength)
            {
                var singleJoker = new List<Card> { joker };
                if (IsBindSatisfied(singleJoker))
                {
                    playable.Add(joker);
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
                    if (IsBindSatisfied(seq))
                    {
                        playable.AddRange(seq);
                    }
                }
            }
        }

        return playable;
    }

    private IEnumerable<Card> GetPlayableCardsFromGroup(List<Card> groupCards, int requiredCount)
    {
        var playableCards = new HashSet<Card>();
        if (groupCards == null || groupCards.Count < requiredCount) return playableCards;

        var combo = new List<Card>(requiredCount);

        void BuildCombination(int startIndex)
        {
            if (combo.Count == requiredCount)
            {
                if (IsBindSatisfied(combo))
                {
                    foreach (var card in combo)
                    {
                        playableCards.Add(card);
                    }
                }
                return;
            }

            for (int i = startIndex; i < groupCards.Count; i++)
            {
                combo.Add(groupCards[i]);
                BuildCombination(i + 1);
                combo.RemoveAt(combo.Count - 1);
            }
        }

        BuildCombination(0);
        return playableCards;
    }

    private List<Card> FindBindSatisfiedCombo(List<Card> groupCards, int requiredCount)
    {
        if (groupCards == null || groupCards.Count < requiredCount)
        {
            return new List<Card>();
        }

        var combo = new List<Card>(requiredCount);
        List<Card> found = null;

        void Search(int startIndex)
        {
            if (found != null) return;
            if (combo.Count == requiredCount)
            {
                if (IsBindSatisfied(combo))
                {
                    found = new List<Card>(combo);
                }
                return;
            }

            for (int i = startIndex; i <= groupCards.Count - (requiredCount - combo.Count); i++)
            {
                combo.Add(groupCards[i]);
                Search(i + 1);
                combo.RemoveAt(combo.Count - 1);
                if (found != null) return;
            }
        }

        Search(0);
        return found ?? new List<Card>();
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

    private void UpdateBindState(List<Card> previousField, List<Card> currentPlayed)
    {
        if (!enableBind)
        {
            ResetBindState();
            return;
        }
        if (IsElevenSilenceActive)
        {
            ResetBindState();
            return;
        }
        if (previousField == null || previousField.Count == 0) return;

        var (prevReal, prevJokers) = GetRealCardsAndJokers(previousField);
        var (currReal, currJokers) = GetRealCardsAndJokers(currentPlayed);

        bool prevIsStair = IsStairWithJoker(prevReal, prevJokers);
        bool currIsStair = IsStairWithJoker(currReal, currJokers);

        var prevSuitSet = GetBindSuitSet(previousField, prevIsStair);
        var currSuitSet = GetBindSuitSet(currentPlayed, currIsStair);

        var intersection = new HashSet<Suit>(prevSuitSet);
        intersection.IntersectWith(currSuitSet);

        isSuitBindActive = intersection.Count > 0;
        boundSuits = intersection;

        if (prevIsStair || currIsStair)
        {
            isNumberBindActive = false;
            expectedNextRank = -1;
            UpdateSibariMessage();
            return;
        }

        int prevRank = GetBindRank(previousField, prevIsStair);
        int currRank = GetBindRank(currentPlayed, currIsStair);

        int expectedRankFromPrev = GetNextSequentialRank(prevRank);
        if (prevRank > 0 && currRank == expectedRankFromPrev)
        {
            isNumberBindActive = true;
            expectedNextRank = GetNextSequentialRank(currRank);
        }
        else
        {
            isNumberBindActive = false;
            expectedNextRank = -1;
        }

        UpdateSibariMessage();
    }

    private bool IsBindSatisfied(List<Card> selected)
    {
        if (!enableBind) return true;
        if (selected == null || selected.Count == 0) return false;
        if (IsElevenSilenceActive) return IsSuitLockSatisfied(selected);
        if (IsSingleJokerSelection(selected)) return true;
        if (!IsSuitLockSatisfied(selected)) return false;
        if (!isNumberBindActive && !isSuitBindActive) return true;

        var (realSelected, jokerCount) = GetRealCardsAndJokers(selected);
        bool isStair = IsStairWithJoker(realSelected, jokerCount);

        if (isNumberBindActive)
        {
            if (isStair) return false;
            int selectedRank = GetBindRank(selected, isStair);
            if (selectedRank <= 0 || expectedNextRank <= 0) return false;
            if (selectedRank != expectedNextRank) return false;
        }

        if (isSuitBindActive)
        {
            var suitSet = GetBindSuitSet(selected, isStair);
            if (suitSet.Count == 0) return false;
            if (!suitSet.All(s => boundSuits.Contains(s))) return false;
        }

        return true;
    }
    private bool IsSuitLockSatisfied(List<Card> selected)
    {
        if (!isSuitLockTurnActive) return true;
        if (selected == null || selected.Count == 0) return false;
        if (IsSingleJokerSelection(selected)) return true;

        var (realSelected, jokerCount) = GetRealCardsAndJokers(selected);
        bool isStair = IsStairWithJoker(realSelected, jokerCount);
        var suitSet = GetBindSuitSet(selected, isStair);
        if (suitSet.Count == 0) return false;
        return suitSet.All(s => suitLockSuits.Contains(s));
    }
    private bool IsSingleJokerSelection(List<Card> selected)
    {
        return selected != null
            && selected.Count == 1
            && selected[0].IsJoker();
    }

    private int GetBindRank(List<Card> cards, bool isStair)
    {
        var realCards = cards.Where(c => !c.IsJoker()).ToList();
        if (realCards.Count == 0) return -1;

        if (isStair)
        {
            var effective = GetEffectivePlayedCards(cards);
            return effective.Max(c => c.Rank);
        }

        return realCards[0].Rank;
    }

    private HashSet<Suit> GetBindSuitSet(List<Card> cards, bool isStair)
    {
        var suits = new HashSet<Suit>();

        if (isStair)
        {
            var real = cards.FirstOrDefault(c => !c.IsJoker());
            if (real != null) suits.Add(real.Suit);
            return suits;
        }

        foreach (var card in cards)
        {
            if (!card.IsJoker())
            {
                suits.Add(card.Suit);
            }
        }
        return suits;
    }

    private int GetNextSequentialRank(int rank)
    {
        if (rank <= 0) return -1;

        if (IsRevolutionActive)
        {
            return rank > 3 ? rank - 1 : -1;
        }

        return rank < 15 ? rank + 1 : -1;
    }

    private bool IsSuitLockTrigger(List<Card> effectivePlayed)
    {
        if (!enableSuitLock) return false;
        if (effectivePlayed == null || effectivePlayed.Count != 3) return false;
        if (!IsStair(effectivePlayed)) return false;
        var ranks = effectivePlayed.Select(c => c.Rank).OrderBy(r => r).ToList();
        return ranks[0] == 6 && ranks[1] == 7 && ranks[2] == 8;
    }

    private void BeginSuitLockSelection(PlayerBase player)
    {
        if (player is not HumanPlayer)
        {
            return;
        }

        isSelectingSuitLock = true;
        suitLockSelectableSuits = GetSuitLockSelectableSuits(player);
        suitLockSelectionIndex = 0;
        UpdateSuitLockMessage();

        if (playButton != null)
        {
            playButton.interactable = true;
            playButton.GetComponentInChildren<TextMeshProUGUI>().text = "決定";
        }
        if (passButton != null)
        {
            passButton.gameObject.SetActive(true);
            passButton.interactable = true;
        }
        SetPassButtonLabel("切替");
        if (kirikaeButton != null) kirikaeButton.gameObject.SetActive(false);
    }

    private void CycleSuitLockSelection()
    {
        if (!isSelectingSuitLock) return;
        if (suitLockSelectableSuits.Count == 0) return;
        suitLockSelectionIndex = (suitLockSelectionIndex + 1) % suitLockSelectableSuits.Count;
        UpdateSuitLockMessage();
    }

    private void ConfirmSuitLockSelection()
    {
        if (!isSelectingSuitLock) return;
        if (suitLockSelectableSuits.Count == 0) return;
        var suit = suitLockSelectableSuits[suitLockSelectionIndex];
        ActivateSuitLock(suit);
        EndSuitLockSelection();
        EndTurn();
    }

    private void UpdateSuitLockMessage()
    {
        if (!isSelectingSuitLock) return;
        if (suitLockSelectableSuits.Count == 0)
        {
            ShowMessageText(passMessageText, "スートロック: 選択可能なスートがありません");
            return;
        }
        var suit = suitLockSelectableSuits[suitLockSelectionIndex];
        string message = $"スートロック: 次のターンのスートを選択\n<size=120%>{GetSuitLabel(suit)}</size>\nパスで切替 / 出すで決定";
        ShowMessageText(passMessageText, message);
    }

    private void EndSuitLockSelection()
    {
        isSelectingSuitLock = false;
        suitLockSelectableSuits.Clear();
        if (passMessageText != null)
        {
            passMessageText.gameObject.SetActive(false);
            passMessageText.text = "";
        }
        ResetPlayButtonUI();
    }

    private List<Suit> GetSuitLockSelectableSuits(PlayerBase player)
    {
        var nonJokerSuits = player.Hand
            .Where(card => !card.IsJoker())
            .Select(card => card.Suit)
            .Distinct()
            .ToList();

        if (nonJokerSuits.Count > 0)
        {
            return nonJokerSuits;
        }

        if (player.Hand.Any(card => card.IsJoker()))
        {
            return suitLockSelectionOptions.ToList();
        }

        return new List<Suit>();
    }

    private void ActivateSuitLock(Suit suit)
    {
        suitLockTurnsRemaining = 1;
        suitLockSuits.Clear();
        suitLockSuits.Add(suit);
        EnqueueMessage($"スートロック発動! 次のターンは{GetSuitLabel(suit)}のみ");
    }

    private bool TryResolvePendingSuitLockSelection()
    {
        if (!pendingSuitLockSelection || pendingSuitLockPlayer == null)
        {
            return false;
        }

        var targetPlayer = pendingSuitLockPlayer;
        pendingSuitLockSelection = false;
        pendingSuitLockPlayer = null;

        if (targetPlayer is HumanPlayer)
        {
            BeginSuitLockSelection(targetPlayer);
            return true;
        }

        ActivateSuitLock(ChooseSuitLockForCpu(targetPlayer, null));
        return false;
    }

    private Suit ChooseSuitLockForCpu(PlayerBase player, List<Card> effectivePlayed)
    {
        var suitCounts = player.Hand
            .Where(c => !c.IsJoker())
            .GroupBy(c => c.Suit)
            .Select(g => new { Suit = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .FirstOrDefault();

        if (suitCounts != null)
        {
            return suitCounts.Suit;
        }

        if (effectivePlayed != null && effectivePlayed.Count > 0)
        {
            return effectivePlayed[0].Suit;
        }
        return Suit.Spade;
    }

    private void ConsumeSuitLockTurn()
    {
        if (!isSuitLockTurnActive) return;
        suitLockTurnsRemaining = Mathf.Max(0, suitLockTurnsRemaining - 1);
        if (suitLockTurnsRemaining == 0)
        {
            suitLockSuits.Clear();
        }
        isSuitLockTurnActive = false;
    }

    private void ResetBindState()
    {
        isNumberBindActive = false;
        isSuitBindActive = false;
        expectedNextRank = -1;
        boundSuits.Clear();
        ClearSibariMessage();
    }

    private void ShowMessageText(TextMeshProUGUI target, string message)
    {
        if (target == null) return;
        target.text = message;
        target.gameObject.SetActive(true);
        var cg = target.GetComponent<CanvasGroup>();
        if (cg == null) cg = target.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
    }

    private void HideMessageText(TextMeshProUGUI target)
    {
        if (target == null) return;
        var cg = target.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 0f;
        target.gameObject.SetActive(false);
        target.text = "";
    }


    private void UpdateSibariMessage()
    {
        var targetText = passMessageText != null ? passMessageText : SibariMessageText;
        if (targetText == null) return;

        if (!isNumberBindActive && !isSuitBindActive)
        {
            ClearSibariMessage();
            return;
        }

        string suitMessage = "";
        if (isSuitBindActive && boundSuits.Count > 0)
        {
            suitMessage = string.Join("・", boundSuits.Select(GetSuitLabel));
        }

        string numberMessage = "";
        if (isNumberBindActive && expectedNextRank > 0)
        {
            numberMessage = GetRankLabel(expectedNextRank);
        }

        if (isNumberBindActive && isSuitBindActive)
        {
            targetText.text = $"激縛り発動次は {numberMessage} & {suitMessage}";
        }
        else if (isNumberBindActive)
        {
            targetText.text = $"数縛り発動次は {numberMessage} のみ";
        }
        else
        {
            targetText.text = $"スート縛り発動{suitMessage} のみ";
        }

        targetText.gameObject.SetActive(true);
        var cg = targetText.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 1f;
    }

    private void ClearSibariMessage()
    {
        var targetText = passMessageText != null ? passMessageText : SibariMessageText;
        HideMessageText(targetText);
    }

    private string GetSuitLabel(Suit suit)
    {
        return suit switch
        {
            Suit.Spade => "♠",
            Suit.Heart => "♥",
            Suit.Diamond => "♦",
            Suit.Club => "♣",
            _ => suit.ToString()
        };
    }

    private string GetRankLabel(int rank)
    {
        return rank switch
        {
            14 => "A",
            15 => "2",
            16 => "Joker",
            _ => rank.ToString()
        };
    }


    /// <summary>
    /// 7渡し、10捨ての選択中に表示される永続メッセージを非表示にします。
    /// このメソッドは、プレイヤーがカードの選択を完了し、「あげる」または「捨てる」ボタンを押した際に呼び出す必要があります。
    /// </summary>
    private void ClearPassMessage()
    {
        // メッセージのGameObjectを非アクティブにし、テキストをクリア
        HideMessageText(passMessageText);

        // Playボタンのテキストを「プレイ」に戻す（操作完了時にリセット）
        if (playButton != null)
        {
            playButton.GetComponentInChildren<TextMeshProUGUI>().text = "プレイ";
        }
    }

    private IEnumerator HandleSixTradeSequence(PlayerBase player)
    {
        tradeSourcePlayer = player;
        tradeTargetPlayer = null;
        pendingTradeCardCount = Mathf.Min(pendingTradeCardCount, tradeSourcePlayer.Hand.Count);

        if (tradeSourcePlayer is HumanPlayer)
        {
            BeginTradeTargetSelection(tradeSourcePlayer);
            yield break;
        }

        tradeTargetPlayer = ChooseTradeTargetForCpu(tradeSourcePlayer);
        if (tradeTargetPlayer == null)
        {
            EndSixTradeMode();
            EndTurn();
            yield break;
        }

        if (TryConsumeBarrierForTradeTarget(tradeTargetPlayer))
        {
            yield break;
        }

        pendingTradeCardCount = Mathf.Min(pendingTradeCardCount, tradeSourcePlayer.Hand.Count, tradeTargetPlayer.Hand.Count);
        if (pendingTradeCardCount <= 0)
        {
            EndSixTradeMode();
            EndTurn();
            yield break;
        }

        var sourceCards = SelectTradeCardsForCpu(tradeSourcePlayer, pendingTradeCardCount);
        if (tradeTargetPlayer is HumanPlayer)
        {
            pendingTradeSourceCards = sourceCards;
            BeginTradeCardSelection(tradeSourcePlayer, tradeTargetPlayer, false);
            yield break;
        }

        var targetCards = SelectTradeCardsForCpu(tradeTargetPlayer, pendingTradeCardCount);
        yield return StartCoroutine(ExecuteSixTrade(tradeSourcePlayer, tradeTargetPlayer, sourceCards, targetCards));
    }

    private void BeginTradeTargetSelection(PlayerBase sourcePlayer)
    {
        tradeTargetCandidates = GetTradeTargetCandidates(sourcePlayer);
        if (tradeTargetCandidates.Count == 0)
        {
            EndSixTradeMode();
            EndTurn();
            return;
        }

        isPlayerActionInProgress = false;
        tradeTargetIndex = 0;
        isSelectingTradeTarget = true;

        UpdateTradeTargetMessage();

        if (playButton != null)
        {
            playButton.interactable = true;
            playButton.GetComponentInChildren<TextMeshProUGUI>().text = "決定";
        }
        if (passButton != null)
        {
            passButton.gameObject.SetActive(true);
            passButton.interactable = true;
        }
        SetPassButtonLabel("切替");
        if (kirikaeButton != null) kirikaeButton.gameObject.SetActive(false);
    }

    private void CycleTradeTargetSelection()
    {
        if (!isSelectingTradeTarget || tradeTargetCandidates.Count == 0) return;

        tradeTargetIndex = (tradeTargetIndex + 1) % tradeTargetCandidates.Count;
        UpdateTradeTargetMessage();
    }

    private void ConfirmTradeTargetSelection()
    {
        if (!isSelectingTradeTarget || tradeTargetCandidates.Count == 0) return;

        tradeTargetPlayer = tradeTargetCandidates[tradeTargetIndex];
        isSelectingTradeTarget = false;

        if (TryConsumeBarrierForTradeTarget(tradeTargetPlayer))
        {
            return;
        }

        pendingTradeCardCount = Mathf.Min(pendingTradeCardCount, tradeSourcePlayer.Hand.Count, tradeTargetPlayer.Hand.Count);
        if (pendingTradeCardCount <= 0)
        {
            EndSixTradeMode();
            EndTurn();
            return;
        }

        BeginTradeCardSelection(tradeSourcePlayer, tradeTargetPlayer, true);
    }

    private void BeginTradeCardSelection(PlayerBase sourcePlayer, PlayerBase targetPlayer, bool selectingSourceCards)
    {
        tradeSourcePlayer = sourcePlayer;
        tradeTargetPlayer = targetPlayer;
        isSelectingTradeCards = true;
        isSelectingTradeSourceCards = selectingSourceCards;
        isPlayerActionInProgress = false;

        string message = selectingSourceCards
            ? $"トレードに出すカードを\n<size=120%>{pendingTradeCardCount}枚</size>\n選んでください"
            : $"トレードで渡すカードを\n<size=120%>{pendingTradeCardCount}枚</size>\n選んでください";

        ShowMessageText(passMessageText, message);

        ResetPlayerSelection();
        CreatePlayerCardSlots(human.Hand.Count);
        PopulatePlayerHand(human);

        if (passButton != null) passButton.interactable = false;
        if (playButton != null)
        {
            playButton.interactable = false;
            playButton.GetComponentInChildren<TextMeshProUGUI>().text = "トレード";
        }
    }

    private void HandleTradeCardSelection()
    {
        var selected = human.SelectCards(human.Hand);
        int required = Mathf.Min(pendingTradeCardCount, human.Hand.Count);

        if (selected.Count != required)
        {
            ShowMessageText(passMessageText, $"{required}枚 選んでください");
            return;
        }

        if (playButton != null) playButton.interactable = false;

        if (isSelectingTradeSourceCards)
        {
            pendingTradeSourceCards = selected;
            var targetCards = SelectTradeCardsForCpu(tradeTargetPlayer, pendingTradeCardCount);
            StartCoroutine(ExecuteSixTrade(tradeSourcePlayer, tradeTargetPlayer, pendingTradeSourceCards, targetCards));
        }
        else
        {
            var targetCards = selected;
            StartCoroutine(ExecuteSixTrade(tradeSourcePlayer, tradeTargetPlayer, pendingTradeSourceCards, targetCards));
        }
    }

    private IEnumerator ExecuteSixTrade(PlayerBase sourcePlayer, PlayerBase targetPlayer, List<Card> sourceCards, List<Card> targetCards)
    {
        if (TryConsumeBarrier(targetPlayer, "6トレード"))
        {
            EndSixTradeMode();
            if (TryResolvePendingSuitLockSelection())
            {
                yield break;
            }
            EndTurn();
            yield break;
        }

        int tradeCount = Mathf.Min(sourceCards.Count, targetCards.Count);
        if (tradeCount <= 0)
        {
            EndSixTradeMode();
            if (TryResolvePendingSuitLockSelection())
            {
                yield break;
            }
            EndTurn();
            yield break;
        }

        Debug.Log($"{sourcePlayer.Name} と {targetPlayer.Name} が {tradeCount}枚 トレード");

        foreach (var card in sourceCards.Take(tradeCount))
        {
            sourcePlayer.Hand.Remove(card);
            targetPlayer.Hand.Add(card);

            if (sourcePlayer is HumanPlayer)
            {
                RemovePlayedCardsFromUI(new List<Card> { card });
            }
        }

        foreach (var card in targetCards.Take(tradeCount))
        {
            targetPlayer.Hand.Remove(card);
            sourcePlayer.Hand.Add(card);

            if (targetPlayer is HumanPlayer)
            {
                RemovePlayedCardsFromUI(new List<Card> { card });
            }
        }

        if (sourcePlayer is HumanPlayer || targetPlayer is HumanPlayer)
        {
            CreatePlayerCardSlots(human.Hand.Count);
            PopulatePlayerHand(human);
        }

        if (sourcePlayer is not HumanPlayer)
        {
            Transform cpuArea = sourcePlayer.handArea;
            if (cpuArea != null) PopulateCpuHandAsBack(cpuArea, sourcePlayer.Hand.Count);
        }

        if (targetPlayer is not HumanPlayer)
        {
            Transform cpuArea = targetPlayer.handArea;
            if (cpuArea != null) PopulateCpuHandAsBack(cpuArea, targetPlayer.Hand.Count);
        }

        yield return new WaitForSeconds(0.8f);

        EndSixTradeMode();
        if (TryResolvePendingSuitLockSelection())
        {
            yield break;
        }
        EndTurn();
    }

    private void EndSixTradeMode()
    {
        isSixTradeMode = false;
        isSelectingTradeTarget = false;
        isSelectingTradeCards = false;
        isSelectingTradeSourceCards = false;
        pendingTradeCardCount = 0;
        tradeTargetCandidates.Clear();
        pendingTradeSourceCards.Clear();
        tradeSourcePlayer = null;
        tradeTargetPlayer = null;

        if (passMessageText != null)
        {
            passMessageText.gameObject.SetActive(false);
            passMessageText.text = "";
        }

        ResetPlayButtonUI();
    }

    private bool TryConsumeBarrierForTradeTarget(PlayerBase targetPlayer)
    {
        if (!TryConsumeBarrier(targetPlayer, "6トレード"))
        {
            return false;
        }

        EndSixTradeMode();
        if (TryResolvePendingSuitLockSelection())
        {
            return true;
        }

        EndTurn();
        return true;
    }

    private void UpdateTradeTargetMessage()
    {
        if (tradeTargetCandidates.Count == 0) return;
        var target = tradeTargetCandidates[tradeTargetIndex];
        string message = $"トレード相手: <size=120%>{target.Name}</size>\nパスで切替 / 出すで決定";
        ShowMessageText(passMessageText, message);
    }

    private List<PlayerBase> GetTradeTargetCandidates(PlayerBase sourcePlayer)
    {
        return players.Where(p => p != sourcePlayer && remainingPlayers.Contains(p)).ToList();
    }

    private PlayerBase ChooseTradeTargetForCpu(PlayerBase sourcePlayer)
    {
        if (remainingPlayers.Contains(human) && sourcePlayer != human)
        {
            return human;
        }

        int startIndex = (players.IndexOf(sourcePlayer) + 1) % players.Count;
        for (int i = 0; i < players.Count; i++)
        {
            int index = (startIndex + i) % players.Count;
            var candidate = players[index];
            if (candidate != sourcePlayer && remainingPlayers.Contains(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private List<Card> SelectTradeCardsForCpu(PlayerBase player, int count)
    {
        int tradeCount = Mathf.Min(count, player.Hand.Count);
        return player.Hand.OrderBy(c => c.Rank).ThenBy(c => c.Suit).Take(tradeCount).ToList();
    }

    private void BeginFreezeTargetSelection(PlayerBase sourcePlayer, int count)
    {
        freezeTargetCandidates = GetFreezeTargetCandidates(sourcePlayer);
        if (freezeTargetCandidates.Count == 0)
        {
            EndFreezeTwelveMode();
            EndTurn();
            return;
        }

        isPlayerActionInProgress = false;
        pendingFreezeTwelveCount = Mathf.Min(count, freezeTargetCandidates.Count);
        freezeTargetIndex = 0;
        isFreezeTwelveMode = true;

        UpdateFreezeTargetMessage();

        if (playButton != null)
        {
            playButton.interactable = true;
            playButton.GetComponentInChildren<TextMeshProUGUI>().text = "決定";
        }
        if (passButton != null)
        {
            passButton.gameObject.SetActive(true);
            passButton.interactable = true;
        }
        SetPassButtonLabel("切替");
        if (kirikaeButton != null) kirikaeButton.gameObject.SetActive(false);
    }

    private void CycleFreezeTargetSelection()
    {
        if (!isFreezeTwelveMode || freezeTargetCandidates.Count == 0) return;
        freezeTargetIndex = (freezeTargetIndex + 1) % freezeTargetCandidates.Count;
        UpdateFreezeTargetMessage();
    }

    private void ConfirmFreezeTargetSelection()
    {
        if (!isFreezeTwelveMode || freezeTargetCandidates.Count == 0) return;

        var target = freezeTargetCandidates[freezeTargetIndex];
        if (!TryConsumeBarrierForFreezeTarget(target))
        {
            AddFreezePass(target, 1);
        }
        freezeTargetCandidates.Remove(target);
        pendingFreezeTwelveCount--;

        if (pendingFreezeTwelveCount <= 0 || freezeTargetCandidates.Count == 0)
        {
            EndFreezeTwelveMode();
            EndTurn();
            return;
        }

        if (freezeTargetIndex >= freezeTargetCandidates.Count)
        {
            freezeTargetIndex = 0;
        }

        UpdateFreezeTargetMessage();
    }

    private void EndFreezeTwelveMode()
    {
        isFreezeTwelveMode = false;
        pendingFreezeTwelveCount = 0;
        freezeTargetCandidates.Clear();
        freezeTargetIndex = 0;

        if (passMessageText != null)
        {
            passMessageText.gameObject.SetActive(false);
            passMessageText.text = "";
        }

        if (kirikaeButton != null) kirikaeButton.gameObject.SetActive(false);
        ResetPlayButtonUI();
    }

    private void UpdateFreezeTargetMessage()
    {
        if (freezeTargetCandidates.Count == 0) return;
        var target = freezeTargetCandidates[freezeTargetIndex];
        string message = $"フリーズ THE 12 対象: <size=120%>{target.Name}</size>\n残り{pendingFreezeTwelveCount}人\nパスで切替 / 出すで決定";
        ShowMessageText(passMessageText, message);
    }

    private List<PlayerBase> GetFreezeTargetCandidates(PlayerBase sourcePlayer)
    {
        return remainingPlayers.Where(p => p != sourcePlayer).ToList();
    }

    private void ApplyFreezeTwelveToAll(PlayerBase sourcePlayer)
    {
        var targets = GetFreezeTargetCandidates(sourcePlayer);
        foreach (var target in targets)
        {
            if (!TryConsumeBarrierForFreezeTarget(target))
            {
                AddFreezePass(target, 1);
            }
        }
        EnqueueMessage("フリーズ THE 12: 全員パス!");
    }

    private void ApplyFreezeTwelveForCpu(PlayerBase sourcePlayer, int count)
    {
        var targets = GetFreezeTargetCandidates(sourcePlayer);
        if (targets.Count == 0) return;

        int applyCount = Mathf.Min(count, targets.Count);
        for (int i = 0; i < applyCount; i++)
        {
            var target = targets[i];
            if (!TryConsumeBarrierForFreezeTarget(target))
            {
                AddFreezePass(target, 1);
            }
        }
        EnqueueMessage($"フリーズ THE 12: {applyCount}人をパス状態にしました。");
    }

    private void AddFreezePass(PlayerBase target, int count)
    {
        if (!freezePassCounts.ContainsKey(target))
        {
            freezePassCounts[target] = 0;
        }
        freezePassCounts[target] += count;
        EnqueueMessage($"{target.Name} はフリーズでパスになります。");
    }
    private bool TryConsumeBarrierForFreezeTarget(PlayerBase target)
    {
        return TryConsumeBarrier(target, "フリーズ12");
    }

    private IEnumerator HandleSevenPassSequence(PlayerBase player)
    {
        if (TryConsumeBarrierForSevenPassTarget(player))
        {
            yield break;
        }
        // ★修正: メッセージを具体的に
        int maxAllowed = Mathf.Min(pendingActionCardCount, player.Hand.Count);
        string message = $"渡すカードを\n<size=120%>0〜{maxAllowed}枚</size>\n選んでください";
        if (player is HumanPlayer)
        {
            // ★常時表示用にテキストエリアを直接書き換え & 表示
            ShowMessageText(passMessageText, message);

            isPlayerActionInProgress = false;
            ResetPlayerSelection();
            CreatePlayerCardSlots(human.Hand.Count);
            PopulatePlayerHand(human);

            if (passButton != null) passButton.interactable = false;
            if (playButton != null)
            {
                playButton.interactable = true;
                playButton.GetComponentInChildren<TextMeshProUGUI>().text = "あげる";
            }
            yield break;
        }
        else
        {
            // CPUの場合はエンキューで表示
            EnqueueMessage($"CPUがカードを選んでいます...");
            yield return new WaitForSeconds(1.0f);

            var hand = player.Hand.OrderBy(c => c.Rank).ToList();
            int cpuMaxAllowed = Mathf.Min(pendingActionCardCount, hand.Count);
            int count = Random.Range(0, cpuMaxAllowed + 1);
            var cardsToPass = hand.Take(count).ToList();

            yield return StartCoroutine(ExecuteSevenPassTransfer(player, cardsToPass));
        }
    }

    private IEnumerator HandleTenDiscardSequence(PlayerBase player)
    {
        // ★修正: メッセージを具体的に
        int maxAllowed = Mathf.Min(pendingActionCardCount, player.Hand.Count);
        string message = $"捨てるカードを\n<size=120%>0〜{maxAllowed}枚</size>\n選んでください";
        if (player is HumanPlayer)
        {
            // ★常時表示
            ShowMessageText(passMessageText, message);

            isPlayerActionInProgress = false;
            ResetPlayerSelection();
            CreatePlayerCardSlots(human.Hand.Count);
            PopulatePlayerHand(human);

            if (passButton != null) passButton.interactable = false;
            if (playButton != null)
            {
                playButton.interactable = true;
                playButton.GetComponentInChildren<TextMeshProUGUI>().text = "捨てる";
            }
            yield break;
        }
        else
        {
            EnqueueMessage($"CPUが捨てるカードを選んでいます...");
            yield return new WaitForSeconds(1.0f);

            var hand = player.Hand.OrderBy(c => c.Rank).ToList();
            int cpuMaxAllowed = Mathf.Min(pendingActionCardCount, hand.Count);
            int count = Random.Range(0, cpuMaxAllowed + 1);
            var cardsToDiscard = hand.Take(count).ToList();

            yield return StartCoroutine(ExecuteTenDiscardAction(player, cardsToDiscard));
        }
    }

    public IEnumerator ExecuteSevenPassTransfer(PlayerBase fromPlayer, List<Card> cards)
    {
        PlayerBase toPlayer = GetNextRemainingPlayer(fromPlayer);
        if (toPlayer == null)
        {
            Debug.LogWarning("7渡しの有効な受け取り手が見つかりません。");
            isSevenPassMode = false;
            ResetPlayButtonUI();
            EndTurn();
            yield break;
        }
        Debug.Log($"{fromPlayer.Name} から {toPlayer.Name} へ {cards.Count}枚 渡します");

        foreach (var card in cards)
        {
            fromPlayer.Hand.Remove(card);
            toPlayer.Hand.Add(card);

            if (fromPlayer is HumanPlayer)
            {
                RemovePlayedCardsFromUI(new List<Card> { card });
            }
        }

        if (toPlayer is HumanPlayer)
        {
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

        if (fromPlayer is not HumanPlayer)
        {
            Transform cpuArea = fromPlayer.handArea;
            if (cpuArea != null) PopulateCpuHandAsBack(cpuArea, fromPlayer.Hand.Count);
        }

        yield return new WaitForSeconds(0.8f);

        var winContext = new WinContext
        {
            HasPlayContext = false,
            PlayedCards = null,
            IsEightCut = false,
            IsSevenPass = cards.Count > 0,
            IsTenDiscard = false
        };
        CheckForWin(fromPlayer, winContext);
        if (isGameOver) yield break;

        isSevenPassMode = false;

        passMessageText.gameObject.SetActive(false);
        passMessageText.text = "";

        ResetPlayButtonUI();

        if (TryResolvePendingSuitLockSelection())
        {
            yield break;
        }

        EndTurn();
    }
    private PlayerBase GetNextRemainingPlayer(PlayerBase fromPlayer)
    {
        int nextIndex = (players.IndexOf(fromPlayer) + 1) % players.Count;
        PlayerBase toPlayer = players[nextIndex];

        int safetyLoop = 0;
        while (!remainingPlayers.Contains(toPlayer))
        {
            nextIndex = (nextIndex + 1) % players.Count;
            toPlayer = players[nextIndex];

            safetyLoop++;
            if (safetyLoop > players.Count)
            {
                return null;
            }
        }

        return toPlayer;
    }

    private bool TryConsumeBarrierForSevenPassTarget(PlayerBase fromPlayer)
    {
        PlayerBase toPlayer = GetNextRemainingPlayer(fromPlayer);
        if (toPlayer == null)
        {
            return false;
        }

        if (!TryConsumeBarrier(toPlayer, "7渡し"))
        {
            return false;
        }

        isSevenPassMode = false;
        if (passMessageText != null)
        {
            passMessageText.gameObject.SetActive(false);
            passMessageText.text = "";
        }
        ResetPlayButtonUI();
        if (TryResolvePendingSuitLockSelection())
        {
            return true;
        }
        var winContext = new WinContext
        {
            HasPlayContext = true,
            PlayedCards = new List<Card>(lastPlayedCards),
            IsEightCut = enableEightCut && IsEightCut(lastPlayedCards),
            IsSevenPass = false,
            IsTenDiscard = false
        };
        CheckForWin(fromPlayer, winContext);
        EndTurn();
        return true;
    }

    public IEnumerator ExecuteTenDiscardAction(PlayerBase player, List<Card> cards)
    {
        Debug.Log($"{player.Name} は {cards.Count}枚 捨てました");

        foreach (var card in cards)
        {
            player.Hand.Remove(card);
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

        var winContext = new WinContext
        {
            HasPlayContext = false,
            PlayedCards = null,
            IsEightCut = false,
            IsSevenPass = false,
            IsTenDiscard = cards.Count > 0
        };
        CheckForWin(player, winContext);
        if (isGameOver) yield break;

        isTenDiscardMode = false;

        passMessageText.gameObject.SetActive(false);
        passMessageText.text = "";

        ResetPlayButtonUI();

        if (TryResolvePendingSuitLockSelection())
        {
            yield break;
        }

        EndTurn();
    }

    private Card GetStrongestCard(PlayerBase player)
    {
        return player.Hand
            .OrderByDescending(c => GetCardStrength(c.IsJoker() ? 16 : c.Rank))
            .ThenByDescending(c => c.Rank)
            .FirstOrDefault();
    }

    private void ApplyTwelvePenalty()
    {
        if (previousRoundRanks.Count == 0)
        {
            Debug.Log("12ペナルティ: 前回順位がないためスキップします。");
            return;
        }

        var targets = previousRoundRanks
            .Where(entry => entry.Value == 1 || entry.Value == 2)
            .Select(entry => entry.Key)
            .Where(player => remainingPlayers.Contains(player))
            .ToList();

        if (targets.Count == 0)
        {
            return;
        }

        EnqueueMessage("12ペナルティ発動! 前回1位・2位は最強カードを捨てます。");

        foreach (var target in targets)
        {
            if (target.Hand.Count == 0) continue;
            if (TryConsumeBarrierForTwelvePenaltyTarget(target)) continue;
            var strongestCard = GetStrongestCard(target);
            if (strongestCard == null) continue;

            target.Hand.Remove(strongestCard);

            if (target is HumanPlayer)
            {
                CreatePlayerCardSlots(human.Hand.Count);
                PopulatePlayerHand(human);
            }
            else
            {
                Transform cpuArea = target.handArea;
                if (cpuArea != null) PopulateCpuHandAsBack(cpuArea, target.Hand.Count);
            }

            EnqueueMessage($"{target.Name} は最強カードを捨てました。");

            CheckForWin(target);
            if (isGameOver) break;
        }
    }
    private IEnumerator ApplyMiyakoOchiTrade()
    {
        if (!enableMiyakoOchi)
        {
            yield break;
        }

        if (currentGameCount <= 1)
        {
            yield break;
        }

        if (previousRoundRanks.Count == 0)
        {
            Debug.Log("都落ち: 前回順位がないためスキップします。");
            yield break;
        }

        var daifugo = GetPlayerByPreviousRank(1);
        var fugo = GetPlayerByPreviousRank(2);
        var hinmin = GetPlayerByPreviousRank(3);
        var daihinmin = GetPlayerByPreviousRank(4);

        if (daifugo == null || fugo == null || hinmin == null || daihinmin == null)
        {
            Debug.LogWarning("都落ち: 対象プレイヤーが不足しているためスキップします。");
            yield break;
        }

        int topBottomCount = Mathf.Min(2, daifugo.Hand.Count, daihinmin.Hand.Count);
        if (topBottomCount > 0)
        {
            List<Card> daifugoGive;
            if (daifugo is HumanPlayer)
            {
                yield return StartCoroutine(SelectMiyakoOchiCards(daifugo, daihinmin, topBottomCount));
                daifugoGive = new List<Card>(pendingMiyakoOchiCards);
                pendingMiyakoOchiCards.Clear();
            }
            else
            {
                daifugoGive = SelectWeakestCards(daifugo, topBottomCount);
            }
            var daihinminGive = SelectStrongestCards(daihinmin, topBottomCount);
            ExecuteCardTransfer(daifugo, daihinmin, daifugoGive);
            ExecuteCardTransfer(daihinmin, daifugo, daihinminGive);
        }

        int middleCount = Mathf.Min(1, fugo.Hand.Count, hinmin.Hand.Count);
        if (middleCount > 0)
        {
            List<Card> fugoGive;
            if (fugo is HumanPlayer)
            {
                yield return StartCoroutine(SelectMiyakoOchiCards(fugo, hinmin, middleCount));
                fugoGive = new List<Card>(pendingMiyakoOchiCards);
                pendingMiyakoOchiCards.Clear();
            }
            else
            {
                fugoGive = SelectWeakestCards(fugo, middleCount);
            }
            var hinminGive = SelectStrongestCards(hinmin, middleCount);
            ExecuteCardTransfer(fugo, hinmin, fugoGive);
            ExecuteCardTransfer(hinmin, fugo, hinminGive);
        }

        EnqueueMessage("都落ち発動! 前回順位に応じてカードを交換しました。");

        if (human != null)
        {
            CreatePlayerCardSlots(human.Hand.Count);
            PopulatePlayerHand(human);
        }

        if (cpuPlayers.Count > 0) PopulateCpuHandAsBack(handAreaCPU1, cpuPlayers[0].Hand.Count);
        if (cpuPlayers.Count > 1) PopulateCpuHandAsBack(handAreaCPU2, cpuPlayers[1].Hand.Count);
        if (cpuPlayers.Count > 2) PopulateCpuHandAsBack(handAreaCPU3, cpuPlayers[2].Hand.Count);
    }

    private IEnumerator SelectMiyakoOchiCards(PlayerBase sourcePlayer, PlayerBase targetPlayer, int count)
    {
        BeginMiyakoOchiSelection(sourcePlayer, targetPlayer, count);
        while (!miyakoSelectionDone)
        {
            yield return null;
        }
        EndMiyakoOchiSelection();
    }

    private void BeginMiyakoOchiSelection(PlayerBase sourcePlayer, PlayerBase targetPlayer, int count)
    {
        miyakoTradeCount = Mathf.Min(count, sourcePlayer.Hand.Count);
        miyakoSelectionDone = false;
        isSelectingMiyakoOchiCards = true;
        pendingMiyakoOchiCards.Clear();

        string targetName = targetPlayer != null ? targetPlayer.Name : "相手";
        string message = $"{targetName}に渡すカードを\n<size=120%>{miyakoTradeCount}枚</size>\n選んでください";
        ShowMessageText(passMessageText, message);

        ResetPlayerSelection();
        CreatePlayerCardSlots(human.Hand.Count);
        PopulatePlayerHand(human);

        if (passButton != null)
        {
            passButton.gameObject.SetActive(false);
            passButton.interactable = false;
        }
        if (playButton != null)
        {
            playButton.gameObject.SetActive(true);
            playButton.interactable = false;
            playButton.GetComponentInChildren<TextMeshProUGUI>().text = "交換";
        }
        if (kirikaeButton != null) kirikaeButton.gameObject.SetActive(false);
    }

    private void HandleMiyakoOchiSelection()
    {
        var selected = human.SelectCards(human.Hand);
        int required = Mathf.Min(miyakoTradeCount, human.Hand.Count);

        if (selected.Count != required)
        {
            ShowMessageText(passMessageText, $"{required}枚 選んでください");
            return;
        }

        pendingMiyakoOchiCards = selected;
        miyakoSelectionDone = true;
        isSelectingMiyakoOchiCards = false;

        if (playButton != null) playButton.interactable = false;
    }

    private void EndMiyakoOchiSelection()
    {
        miyakoTradeCount = 0;

        if (passMessageText != null)
        {
            passMessageText.gameObject.SetActive(false);
            passMessageText.text = "";
        }

        ResetPlayButtonUI();
    }

    private IEnumerator RunPreparationPhase()
    {
        if (!enableMiyakoOchi || currentGameCount <= 1)
        {
            yield break;
        }

        EnqueueMessage("準備フェーズ: 都落ちのカード交換を行います。");
        yield return new WaitForSeconds(1.0f);

        yield return StartCoroutine(ApplyMiyakoOchiTrade());

        yield return new WaitForSeconds(1.0f);
        EnqueueMessage("準備フェーズ終了。");
    }

    private PlayerBase GetPlayerByPreviousRank(int rank)
    {
        return previousRoundRanks.FirstOrDefault(entry => entry.Value == rank).Key;
    }

    private List<Card> SelectStrongestCards(PlayerBase player, int count)
    {
        return player.Hand
            .OrderByDescending(c => GetCardStrength(c.IsJoker() ? 16 : c.Rank))
            .ThenByDescending(c => c.Rank)
            .Take(count)
            .ToList();
    }

    private List<Card> SelectWeakestCards(PlayerBase player, int count)
    {
        return player.Hand
            .OrderBy(c => GetCardStrength(c.IsJoker() ? 16 : c.Rank))
            .ThenBy(c => c.Rank)
            .Take(count)
            .ToList();
    }

    private void ExecuteCardTransfer(PlayerBase fromPlayer, PlayerBase toPlayer, List<Card> cards)
    {
        foreach (var card in cards)
        {
            fromPlayer.Hand.Remove(card);
            toPlayer.Hand.Add(card);
        }
    }

    private bool TryConsumeBarrierForTwelvePenaltyTarget(PlayerBase target)
    {
        return TryConsumeBarrier(target, "12ペナルティ");
    }

    private void ResetPlayButtonUI()
    {
        if (playButton != null)
        {
            playButton.GetComponentInChildren<TextMeshProUGUI>().text = "出す";
            playButton.interactable = false;
        }
        if (passButton != null) passButton.interactable = true;
        SetPassButtonLabel("パス");
        if (kirikaeButton != null) kirikaeButton.gameObject.SetActive(false);
    }

    private void SetPassButtonLabel(string label)
    {
        if (passButton == null) return;
        var text = passButton.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = label;
        }
    }

    private struct WinContext
    {
        public bool HasPlayContext;
        public List<Card> PlayedCards;
        public bool IsEightCut;
        public bool IsSevenPass;
        public bool IsTenDiscard;
    }

    private bool IsForbiddenStrongestPlay(List<Card> playedCards)
    {
        if (playedCards == null || playedCards.Count == 0) return false;
        if (!IsRevolutionActive && playedCards.Any(card => card.Rank == 15)) return true;
        return IsRevolutionActive && playedCards.Any(card => card.Rank == 3);
    }

    private bool TryGetForbiddenWinReason(WinContext context, out string reason)
    {
        var reasons = new List<string>();

        if (context.IsSevenPass) reasons.Add("7渡し");
        if (context.IsTenDiscard) reasons.Add("10捨て");

        if (context.HasPlayContext && context.PlayedCards != null)
        {
            if (IsForbiddenStrongestPlay(context.PlayedCards))
            {
                reasons.Add("最強カード(2/革命時の3)");
            }
            if (context.PlayedCards.Any(card => card.IsJoker()))
            {
                reasons.Add("ジョーカー");
            }
            if (context.IsEightCut)
            {
                reasons.Add("8切り");
            }
            if (context.PlayedCards.Any(card => card.Suit == Suit.Spade && card.Rank == 3))
            {
                reasons.Add("スペード3");
            }
        }

        if (reasons.Count == 0)
        {
            reason = null;
            return false;
        }

        reason = string.Join("・", reasons);
        return true;
    }

    private bool TryApplyForbiddenWin(PlayerBase player, WinContext context)
    {
        if (!forbidSpecialWin || player.Hand.Count != 0)
        {
            return false;
        }

        if (!TryGetForbiddenWinReason(context, out var reason))
        {
            return false;
        }

        int lowestRank = GetLowestAvailableForbiddenRank();
        gameRanks[player] = lowestRank;
        EnqueueMessage($"🚫 禁止上がり: {player.Name} は{reason}であがったため{lowestRank}位になります。");

        remainingPlayers.Remove(player);
        freezePassCounts.Remove(player);

        if (remainingPlayers.Count <= 0)
        {
            isGameOver = true;
            StartCoroutine(EndGameRoutine());
            return true;
        }

        if (remainingPlayers.Count == 1)
        {
            var lastPlayer = remainingPlayers[0];
            gameRanks[lastPlayer] = currentRank;
            EnqueueMessage($"{lastPlayer.Name} が{GetRankDisplayText(currentRank)}です。");
            isGameOver = true;
            StartCoroutine(EndGameRoutine());
        }

        return true;
    }

    private int GetLowestAvailableForbiddenRank()
    {
        int startRank = currentRank;
        int endRank = currentRank + remainingPlayers.Count - 1;
        var usedRanks = new HashSet<int>(gameRanks.Values);
        for (int rank = endRank; rank >= startRank; rank--)
        {
            if (IsRankReservedForMiyakoOchi(rank)) continue;
            if (usedRanks.Contains(rank)) continue;
            return rank;
        }
        return endRank;
    }

    private bool IsRankReservedForMiyakoOchi(int rank)
    {
        if (rank != 4) return false;
        return IsMiyakoOchiDemotionPending();
    }

    private bool IsMiyakoOchiDemotionPending()
    {
        if (!enableMiyakoOchiDemotion || currentGameCount < 2)
        {
            return false;
        }

        if (previousRoundRanks.Count == 0)
        {
            return false;
        }

        if (currentRank != 1)
        {
            return false;
        }

        var previousDaifugo = GetPlayerByPreviousRank(1);
        if (previousDaifugo == null)
        {
            return false;
        }

        if (gameRanks.TryGetValue(previousDaifugo, out _))
        {
            return false;
        }

        return remainingPlayers.Contains(previousDaifugo);
    }

    private void CheckForWin(PlayerBase player, WinContext context = default)
    {
        if (player.Hand.Count == 0)
        {
            if (TryApplyForbiddenWin(player, context))
            {
                return;
            }
            gameRanks[player] = currentRank;
            EnqueueMessage($"{player.Name} があがりました! ({currentRank}位)");

            currentRank++;
            remainingPlayers.Remove(player);
            freezePassCounts.Remove(player);

            ForceMiyakoOchiDaihinmin(player);

            if (isGameOver)
            {
                return;
            }

            if (remainingPlayers.Count <= 1)
            {
                var lastPlayer = remainingPlayers[0];
                gameRanks[lastPlayer] = currentRank;
                EnqueueMessage($"{lastPlayer.Name} が{GetRankDisplayText(currentRank)}です。");

                isGameOver = true;
                StartCoroutine(EndGameRoutine());
            }
        }
    }
    private string GetRankTitle(int rank)
    {
        return rank switch
        {
            1 => "大富豪",
            2 => "富豪",
            3 => "貧民",
            4 => "大貧民",
            _ => $"{rank}位"
        };
    }
    private string GetRankDisplayText(int rank)
    {
        if (rank <= 4)
        {
            return $"{rank}位 {GetRankTitle(rank)}";
        }

        return $"{rank}位";
    }

    private void SetPreviousRoundTitles()
    {
        previousRoundTitles.Clear();
        foreach (var entry in previousRoundRanks)
        {
            previousRoundTitles[entry.Key] = GetRankTitle(entry.Value);
        }
    }



    private IEnumerator EndGameRoutine()
    {
        yield return new WaitForSeconds(2.0f);
        EnqueueMessage($"--- 第{currentGameCount}戦 終了 ---");

        previousRoundRanks = new Dictionary<PlayerBase, int>(gameRanks);
        SetPreviousRoundTitles();
        UpdatePreviousRankText();

        yield return new WaitForSeconds(3.0f);

        currentGameCount++;

        if (currentGameCount <= TotalGames)
        {
            EnqueueMessage($"第{currentGameCount}戦を開始します");
            yield return StartCoroutine(PrepareNextRound());
        }
        else
        {
            EnqueueMessage("全4戦終了!お疲れ様でした!");
        }
    }

    private void ForceMiyakoOchiDaihinmin(PlayerBase winner)
    {
        if (!enableMiyakoOchiDemotion || currentGameCount < 2)
        {
            return;
        }

        if (previousRoundRanks.Count == 0)
        {
            return;
        }

        if (currentRank != 2)
        {
            return;
        }

        var previousDaifugo = GetPlayerByPreviousRank(1);
        if (previousDaifugo == null)
        {
            return;
        }

        if (winner != null && winner == previousDaifugo)
        {
            return;
        }

        if (winner == null || !gameRanks.TryGetValue(winner, out var winnerRank) || winnerRank != 1)
        {
            return;
        }

        if (gameRanks.TryGetValue(previousDaifugo, out _))
        {
            return;
        }

        if (!remainingPlayers.Contains(previousDaifugo))
        {
            return;
        }

        gameRanks[previousDaifugo] = 4;
        previousDaifugo.Hand.Clear();
        remainingPlayers.Remove(previousDaifugo);
        freezePassCounts.Remove(previousDaifugo);
        UpdateDemotedPlayerHand(previousDaifugo);
        EnqueueMessage("都落ち発動! 前回大富豪が大貧民確定となりました。");

        if (remainingPlayers.Count == 1)
        {
            var lastPlayer = remainingPlayers[0];
            gameRanks[lastPlayer] = currentRank;
            EnqueueMessage($"{lastPlayer.Name} が{GetRankDisplayText(currentRank)}です。");

            isGameOver = true;
            StartCoroutine(EndGameRoutine());
        }
        else if (remainingPlayers.Count == 0)
        {
            isGameOver = true;
            StartCoroutine(EndGameRoutine());
        }
    }

    private void UpdateDemotedPlayerHand(PlayerBase demotedPlayer)
    {
        if (demotedPlayer == human)
        {
            PopulatePlayerHand(human);
            return;
        }

        var cpuIndex = cpuPlayers.IndexOf(demotedPlayer as CpuPlayer);
        if (cpuIndex == 0 && handAreaCPU1 != null)
        {
            PopulateCpuHandAsBack(handAreaCPU1, demotedPlayer.Hand.Count);
        }
        else if (cpuIndex == 1 && handAreaCPU2 != null)
        {
            PopulateCpuHandAsBack(handAreaCPU2, demotedPlayer.Hand.Count);
        }
        else if (cpuIndex == 2 && handAreaCPU3 != null)
        {
            PopulateCpuHandAsBack(handAreaCPU3, demotedPlayer.Hand.Count);
        }
    }

    private IEnumerator PrepareNextRound()
    {
        isGameOver = false;
        isRevolution = false;
        isTempRevolution = false;
        isCpuTurnInProgress = false;
        currentRank = 1;
        gameRanks.Clear();
        passCount = 0;
        lastPlayedCards.Clear();
        ResetBindState();
        UpdatePreviousRankText();

        foreach (Transform child in tableArea) Destroy(child.gameObject);

        remainingPlayers = new List<PlayerBase>(players);

        foreach (var p in players) p.Hand.Clear();
        DealInitialCards();
        clubThreeHolderBeforeTrade = FindClubThreeHolder();

        yield return StartCoroutine(RunPreparationPhase());

        CreatePlayerCardSlots(human.Hand.Count);
        PopulatePlayerHand(human);

        currentTurnIndex = GetStartIndexFromPlayer(clubThreeHolderBeforeTrade);

        yield return new WaitForSeconds(1.0f);
        StartTurn();
    }
    private PlayerBase FindClubThreeHolder()
    {
        if (players == null)
        {
            return null;
        }

        return players.FirstOrDefault(player =>
            player.Hand.Any(card => card.Suit == Suit.Club && card.Rank == 3));
    }

    private int GetStartIndexFromPlayer(PlayerBase starter)
    {
        if (players == null || starter == null)
        {
            return 0;
        }

        int index = players.IndexOf(starter);
        return index >= 0 ? index : 0;
    }

}
