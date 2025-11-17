using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;
using System.Collections;

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

    private HumanPlayer human;         // 自分プレイヤー
    private List<CpuPlayer> cpuPlayers = new();  // CPU3人

    public List<Card> lastPlayedCards = new List<Card>(); // 場の直前のカード

    private int passCount = 0;                // 連続パス人数カウント
    private int lastPlayedPlayerIndex = -1;   // 最後にカードを出したプレイヤー

    [SerializeField] private Button passButton;

    private List<PlayerBase> players;  // ← 全プレイヤーまとめ用

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
        Debug.Log($"=== Turn {currentTurnIndex} start ===");

        // 🟢 パスボタンの状態を初期化（どのターンでも毎回制御）
        passButton.interactable = (currentTurnIndex == 0);

        if (currentTurnIndex == 0)
        {
            // 自分のターン開始前に選択状態をリセット
            ResetPlayerSelection();

            // 出せるカードを最新の場情報で再設定
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
            Debug.Log($"{cpu.Name} はパスしました");
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

        Debug.Log("カード選択状態をリセットしました。");
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
        var handArea = cpuIndex switch
        {
            0 => handAreaCPU1,
            1 => handAreaCPU2,
            2 => handAreaCPU3,
            _ => null
        };

        yield return new WaitForSeconds(0.8f);

        if (cpu.Hand.Count == 0)
        {
            Debug.Log($"{cpu.Name} はすでに上がっています。");
            EndTurn();
            yield break;
        }

        // --- ① 場のカードの状態をチェック ---
        List<Card> field = lastPlayedCards;
        List<Card> playableCards = new();

        if (field == null || field.Count == 0)
        {
            // 場が空 → 何でも出せる
            playableCards = cpu.Hand.OrderBy(c => c.Rank).Take(1).ToList();
        }
        else
        {
            int fieldCount = field.Count;
            int fieldRank = field[0].Rank; // 同ランク前提
                                           // CPUの手札から「場と同じ枚数＆より強いランク」のカードを探す
            playableCards = cpu.Hand
                .GroupBy(c => c.Rank)
                .Where(g => g.Count() >= fieldCount && g.Key > fieldRank)
                .OrderBy(g => g.Key)
                .FirstOrDefault()?
                .Take(fieldCount)
                .ToList() ?? new List<Card>();
        }

        // --- ② 出せるカードがない場合はパス ---
        if (playableCards.Count == 0)
        {
            Debug.Log($"{cpu.Name} はパスしました。");
            yield return new WaitForSeconds(0.8f);
            HandlePass();
            yield break;
        }

        // --- ③ 出すカードをCPUの手札から削除 ---
        foreach (var c in playableCards)
            cpu.Hand.Remove(c);

        Debug.Log($"{cpu.Name} played: {string.Join(", ", playableCards.Select(c => $"{c.Suit} {c.Rank}"))}");

        // --- ④ 表示処理（共通アニメーション）---
        yield return StartCoroutine(DisplayPlayedCardsOnTable(cpu, playableCards));

        // --- ⑤ 少し待って次のターンへ ---
        yield return new WaitForSeconds(0.8f);
        EndTurn();
    }

    // 外部参照用
    public HumanPlayer humanPlayer => human;

    void Start()
    {
        InitPlayers();
        DealInitialCards();
        PopulatePlayerHand(human);
        StartTurn(); // ゲーム開始時に最初のターン（人間）を開始
        passButton.onClick.AddListener(OnPassButton);
        // ゲーム開始時にプレイヤーリストを構築
        players = new List<PlayerBase>();
        players.Add(humanPlayer);
        players.AddRange(cpuPlayers);
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
        foreach (Transform child in handAreaPlayer)
            Destroy(child.gameObject);

        player.Hand.Sort((a, b) => a.Rank.CompareTo(b.Rank));

        // 今場に出ているカード
        var tableCards = lastPlayedCards;
        var playableCards = player.GetPlayableCards(tableCards);

        foreach (var card in player.Hand)
        {
            var go = Instantiate(cardPrefab, handAreaPlayer);
            var cv = go.GetComponent<CardView>();
            cv.backSprite = cardBackSprite;
            cv.SetCard(card);

            // 出せるかどうかを設定（半透明＆クリック無効対応）
            bool canPlay = playableCards.Contains(card);
            cv.SetPlayable(canPlay);
        }
    }

    // -------------------------------
    // CPU手札を裏面で表示
    // -------------------------------
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
            Debug.Log("今はあなたの番ではありません。");
            return;
        }

        Debug.Log("Play button pressed");

        var played = human.SelectCards(human.Hand);

        // 何も選んでいない → パス扱い
        if (played == null || played.Count == 0)
        {
            Debug.Log("あなたはパスしました。");
            HandlePass();
            return;
        }

        // 出せるカードかチェック
        if (!human.CanPlaySelectedCards(lastPlayedCards))
        {
            Debug.Log("この組み合わせでは出せません。");
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

        Debug.Log("あなたはパスしました。");
        HandlePass();
    }

    // 場にカードを出すアニメーション（誰が出したか currentPlayer を受け取る）
    private IEnumerator DisplayPlayedCardsOnTable(PlayerBase currentPlayer, List<Card> played)
    {
        // 既存の場を消さない（山にして積む）
        float spacing = 20f;
        int existingCards = tableArea.childCount;
        Vector3 basePos = tableArea.position;
        float startX = basePos.x - (played.Count - 1) * spacing / 2f;

        // handArea の CardView を先に全取得（安全）
        List<CardView> allCardViews = new List<CardView>();
        // currentPlayer が human か cpu かで探索元を変える
        Transform sourceArea = (currentPlayer is HumanPlayer) ? handAreaPlayer : currentPlayer.handArea;
        if (sourceArea != null)
        {
            foreach (Transform child in sourceArea)
            {
                var cv = child.GetComponent<CardView>();
                if (cv != null)
                    allCardViews.Add(cv);
            }
        }
        else
        {
            // 万が一 handArea が null な場合は全シーン検索（保険）
            foreach (var cv in FindObjectsOfType<CardView>())
            {
                allCardViews.Add(cv);
            }
        }

        var playedViews = new List<CardView>();

        for (int i = 0; i < played.Count; i++)
        {
            Card card = played[i];
            // 手札内の CardView を探す（card equality を使って一致）
            var cv = allCardViews.FirstOrDefault(v => v.CardData == card);
            if (cv == null)
            {
                // もし手札UIに直接対象が見つからなければ、シーン全体から探す（裏面カードなど）
                cv = FindObjectsOfType<CardView>().FirstOrDefault(v => v.CardData == card || (v.CardData == null && v.IsFaceUp == false));
            }

            if (cv != null)
            {
                // 表にする
                cv.SetCard(card);

                // 一時的に Canvas直下へ移動してワールド座標を保つ
                cv.transform.SetParent(tableArea.parent, true);

                // サイズ：全員同じ最終サイズに合わせる（変更したければ分岐）
                //cv.transform.localScale = Vector3.one * 2f;

                // 目標位置（少しずらして重ねる）
                Vector3 targetPos = new Vector3(startX + spacing * i, basePos.y + existingCards * 0.5f, basePos.z);

                // アニメーション（MoveToは CardView にある IEnumerator）
                yield return StartCoroutine(cv.MoveTo(targetPos, 0.35f));

                playedViews.Add(cv);
            }
        }

        // アニメ完了短待ち（すでに各 MoveTo を待っているが余裕みせる）
        yield return new WaitForSeconds(0.05f);

        // 場の子に再設定して最終調整
        foreach (var cv in playedViews)
        {
            if (cv == null) continue;

            cv.transform.SetParent(tableArea, true);
            cv.transform.localScale = Vector3.one * 2f; // 最終サイズ
            float randomRot = Random.Range(-6f, 6f);
            cv.transform.localRotation = Quaternion.Euler(0, 0, randomRot);
            // Zをずらして重なりを表現
            cv.transform.localPosition += new Vector3(0, 0, existingCards * -2f);
        }

        // --- 手札データとUIの整理（Human と CPU によって Hand から削除） ---
        // human の場合は human.Hand にあるカードを削除
        if (currentPlayer is HumanPlayer)
        {
            foreach (var c in played)
                if (human.Hand.Contains(c)) human.Hand.Remove(c);

            // handAreaPlayer 内の played な CardView を削除
            RemovePlayedCardsFromUI(played);
        }
        else
        {
            // CPU 側は player.handArea 内の裏面カードオブジェクトを 1枚ずつ削除する方が簡単
            // ここでは currentPlayer.handArea を使って、該当するカードを探して Destroy
            foreach (var c in played)
            {
                if (currentPlayer.handArea != null)
                {
                    // 手札UIから CardView を探して削除（CardData が null の裏面カードが多い）
                    Transform found = null;
                    foreach (Transform child in currentPlayer.handArea)
                    {
                        var cv = child.GetComponent<CardView>();
                        if (cv != null)
                        {
                            // 裏面で rank/suit を持たない場合は単純に削除数で管理する
                            found = child;
                            break;
                        }
                    }
                    if (found != null) Destroy(found.gameObject);
                }
                // CPUの内部Handからは既に呼び出し元で削除している想定
            }
        }

        lastPlayedCards = new List<Card>(played);

        passCount = 0; // 出したら連続パス数リセット
        lastPlayedPlayerIndex = currentTurnIndex;

        Debug.Log($"Displayed {played.Count} cards by {(currentPlayer is HumanPlayer ? "Human" : "CPU")}. Table now has {tableArea.childCount} children.");

        yield break;
    }


    // 手札UIの中から、played に含まれるカード（CardData）を削除する。
    // ※ handAreaPlayer の子のみを対象にするため、場に移動済みのカードは削除されません。
    private void RemovePlayedCardsFromUI(List<Card> played)
    {
        // 子オブジェクトを先にリスト化してから処理（foreach 中の親変更回避）
        var cardViews = new List<CardView>();
        foreach (Transform child in handAreaPlayer)
        {
            var cv = child.GetComponent<CardView>();
            if (cv != null)
                cardViews.Add(cv);
        }

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
        Debug.Log($"{currentTurnIndex}番目のプレイヤーがパス（連続{passCount}人目）");

        if (passCount >= 3)
        {
            Debug.Log("3人連続パス！場を流します。");
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
        Debug.Log("場が流れました！");

        passCount = 0;

        // 最後に出した人から再開
        if (lastPlayedPlayerIndex < 0)
            lastPlayedPlayerIndex = 0;

        currentTurnIndex = lastPlayedPlayerIndex;
        yield return new WaitForSeconds(0.6f);
        StartTurn();
    }



}
