using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ElevenSilenceRule : IRule
{
    public bool CanApply(List<Card> playedCards, GameState state)
    {
        return playedCards.Count(c => c.Rank == 11) >= 3;
    }

    public void Apply(List<Card> playedCards, GameState state)
    {
        state.IsElevenSilence = true;
        Debug.Log("11ƒTƒCƒŒƒ“ƒX! ‚±‚Ìê‚ÆŸ‚Ìê‚Ì“ÁêŒø‰Ê‚ğ–³Œø‚É‚µ‚Ü‚·!");
    }
}
