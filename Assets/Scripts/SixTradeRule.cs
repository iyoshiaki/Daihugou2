using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SixTradeRule : IRule
{
    public bool CanApply(List<Card> playedCards, GameState state)
    {
        return playedCards.Any(c => c.Rank == 6);
    }

    public void Apply(List<Card> playedCards, GameState state)
    {
        int count = playedCards.Count(c => c.Rank == 6);
        state.SixTradeCount = count;
        Debug.Log($"6トレードルール適用: {count}枚");
    }
}