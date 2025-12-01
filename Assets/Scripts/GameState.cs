using System.Collections.Generic;

public class GameState
{
    public List<Card> TableCards;
    public int CurrentPlayerIndex;
    public bool KeepTurn = false;
    public int CurrentTurnIndex { get; private set; }

    // --- ルール判定結果フラグ ---
    public bool TriggerRevolution { get; set; } = false;
    public bool IsElevenBack { get; set; } = false;
    public int SkipCount { get; set; } = 0;

    // ★ 追加: 8切り、7渡し、10捨ての状態を追加
    public bool IsEightCut { get; set; } = false;
    public int SevenPassCount { get; set; } = 0; // 0なら発動なし
    public int TenDiscardCount { get; set; } = 0; // 0なら発動なし

    public GameState(List<Card> tableCards, int currentPlayerIndex)
    {
        TableCards = tableCards;
        CurrentPlayerIndex = currentPlayerIndex;
    }
}