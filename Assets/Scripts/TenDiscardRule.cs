using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TenDiscardRule : IRule
{
    public bool CanApply(List<Card> playedCards, GameState state)
    {
        return playedCards.Any(c => c.Rank == 10);
    }

    public void Apply(List<Card> playedCards, GameState state)
    {
        int count = playedCards.Count(c => c.Rank == 10);
        state.TenDiscardCount = count;
        Debug.Log($"10Ì‚Äƒ‹[ƒ‹“K—p: {count}–‡");
    }
}