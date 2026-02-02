using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ElevenBackRule : IRule
{
    public bool CanApply(List<Card> playedCards, GameState state)
    {
        // 11(J) ‚ªŠÜ‚Ü‚ê‚Ä‚¢‚ê‚Î”­“®
        return playedCards.Any(c => c.Rank == 11);
    }

    public void Apply(List<Card> playedCards, GameState state)
    {
        state.IsElevenBack = true;
        Debug.Log("11ƒoƒbƒN!‚±‚Ìê‚Ì‚İŠv–½ó‘Ô‚É‚È‚è‚Ü‚·!");
    }
}