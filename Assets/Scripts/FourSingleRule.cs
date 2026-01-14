using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FourSingleRule : IRule
{
    public bool CanApply(List<Card> playedCards, GameState state)
    {
        return playedCards.Count == 1 && playedCards.Any(c => c.Rank == 4);
    }

    public void Apply(List<Card> playedCards, GameState state)
    {
        state.ForceSingleNextTurn = true;
        Debug.Log("4シングル発動: 次のターンは1枚出しのみ。");
    }
}