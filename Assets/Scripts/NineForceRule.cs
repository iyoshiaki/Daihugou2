using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NineForceRule : IRule
{
    public bool CanApply(List<Card> playedCards, GameState state)
    {
        return playedCards.Count == 3 && playedCards.All(card => card.Rank == 9);
    }

    public void Apply(List<Card> playedCards, GameState state)
    {
        Debug.Log("9フォースが発動しました。");
        state.TriggerNineForce = true;
    }
}