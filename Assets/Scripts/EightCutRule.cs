using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EightCutRule : IRule
{
    public bool CanApply(List<Card> playedCards, GameState state)
    {
        return playedCards.Any(c => c.Rank == 8);
    }

    public void Apply(List<Card> playedCards, GameState state)
    {
        Debug.Log("8切りルール適用");
        state.IsEightCut = true; // フラグを立てる
        state.KeepTurn = true;   // ずっと俺のターン
        state.TableCards.Clear(); // 論理的な場を空にする
    }
}