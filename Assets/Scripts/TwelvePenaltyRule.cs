using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TwelvePenaltyRule : IRule
{
    public bool CanApply(List<Card> playedCards, GameState state)
    {
        return playedCards.Count == 3 && playedCards.All(card => card.Rank == 12);
    }

    public void Apply(List<Card> playedCards, GameState state)
    {
        state.TriggerTwelvePenalty = true;
        Debug.Log("12ペナルティルール適用");
    }
}