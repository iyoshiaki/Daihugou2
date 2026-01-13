using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GreatChaosRule : IRule
{
    public bool CanApply(List<Card> playedCards, GameState state)
    {
        return playedCards.Count == 3 && playedCards.All(c => c.Rank == 3);
    }

    public void Apply(List<Card> playedCards, GameState state)
    {
        state.TriggerGreatChaos = true;
        Debug.Log("卬! D_ɓւ܂B");
    }
}