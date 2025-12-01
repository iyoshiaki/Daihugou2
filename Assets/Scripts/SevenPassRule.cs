using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SevenPassRule : IRule
{
    public bool CanApply(List<Card> playedCards, GameState state)
    {
        return playedCards.Any(c => c.Rank == 7);
    }

    public void Apply(List<Card> playedCards, GameState state)
    {
        int count = playedCards.Count(c => c.Rank == 7);
        state.SevenPassCount = count;
        Debug.Log($"7“n‚µƒ‹[ƒ‹“K—p: {count}–‡");
    }
}