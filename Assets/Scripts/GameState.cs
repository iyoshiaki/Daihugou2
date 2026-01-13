using System.Collections.Generic;

public class GameState
{
    public List<Card> TableCards;
    public int CurrentPlayerIndex;
    public bool KeepTurn = false;
    public int CurrentTurnIndex { get; private set; }

    // --- ƒ‹[ƒ‹”»’èŒ‹‰Êƒtƒ‰ƒO ---
    public bool TriggerRevolution { get; set; } = false;
    public bool IsElevenBack { get; set; } = false;
    public int SkipCount { get; set; } = 0;

    public bool TriggerGreatChaos { get; set; } = false;
    public bool IsEightCut { get; set; } = false;
    public int SevenPassCount { get; set; } = 0; // 0‚È‚ç”­“®‚È‚µ
    public int TenDiscardCount { get; set; } = 0; // 0‚È‚ç”­“®‚È‚µ

    public GameState(List<Card> tableCards, int currentPlayerIndex)
    {
        TableCards = tableCards;
        CurrentPlayerIndex = currentPlayerIndex;
    }
}