using System.Collections.Generic;

public class GameState
{
    // GameManager 側で渡す「場のカード」のコピーや参照
    public List<Card> TableCards;

    // 今のプレイヤーインデックス（必要なら）
    public int CurrentPlayerIndex;

    // ルールから「このプレイヤーのターンを継続せよ」と伝えるフラグ
    public bool KeepTurn = false;

    public int CurrentTurnIndex { get; private set; }
    public bool TriggerRevolution { get; set; } = false; // 革命が起きたか
    public bool IsElevenBack { get; set; } = false;      // 11バック状態か
    public int SkipCount { get; set; } = 0;              // 何人飛ばすか

    public GameState(List<Card> tableCards, int currentPlayerIndex)
    {
        TableCards = tableCards;
        CurrentPlayerIndex = currentPlayerIndex;
    }
}