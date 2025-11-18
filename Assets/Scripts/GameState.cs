using System.Collections.Generic;

public class GameState
{
    // GameManager 側で渡す「場のカード」のコピーや参照
    public List<Card> TableCards;

    // 今のプレイヤーインデックス（必要なら）
    public int CurrentPlayerIndex;

    // ルールから「このプレイヤーのターンを継続せよ」と伝えるフラグ
    public bool KeepTurn = false;

    public GameState(List<Card> tableCards, int currentPlayerIndex)
    {
        TableCards = tableCards;
        CurrentPlayerIndex = currentPlayerIndex;
    }
}