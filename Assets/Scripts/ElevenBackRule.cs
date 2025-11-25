using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ElevenBackRule : IRule
{
    public bool CanApply(List<Card> playedCards, GameState state)
    {
        // 11(J) が含まれていれば発動
        return playedCards.Any(c => c.Rank == 11);
    }

    public void Apply(List<Card> playedCards, GameState state)
    {
        state.IsElevenBack = true;
        Debug.Log("11バック！この場のみ革命状態になります。");
    }
}