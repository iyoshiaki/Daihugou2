using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FreezeTwelveRule : IRule
{
    public bool CanApply(List<Card> playedCards, GameState state)
    {
        return playedCards.Any(card => card.Rank == 12);
    }

    public void Apply(List<Card> playedCards, GameState state)
    {
        int count = playedCards.Count(card => card.Rank == 12);
        state.FreezeTwelveCount = count;
        Debug.Log($"フリーズ THE 12 ルール適用: {count}枚");
    }
}