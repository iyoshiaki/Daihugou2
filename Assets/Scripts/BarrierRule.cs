using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BarrierRule : IRule
{
    public bool CanApply(List<Card> playedCards, GameState state)
    {
        if (playedCards == null || playedCards.Count != 3)
        {
            return false;
        }

        if (playedCards.Any(card => card.IsJoker()))
        {
            return false;
        }

        var sorted = playedCards.OrderBy(card => card.Rank).ToList();
        if (sorted[0].Rank != 9 || sorted[1].Rank != 10 || sorted[2].Rank != 11)
        {
            return false;
        }

        var suit = sorted[0].Suit;
        return sorted.All(card => card.Suit == suit);
    }

    public void Apply(List<Card> playedCards, GameState state)
    {
        state.TriggerBarrier = true;
        Debug.Log("ƒoƒŠƒA”­“®€”õ");
    }
}