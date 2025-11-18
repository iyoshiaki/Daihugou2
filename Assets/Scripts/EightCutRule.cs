using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EightCutRule : IRule
{
    public bool CanApply(List<Card> playedCards, GameState state)
    {
        // playedCards ‚Ì‚Ç‚ê‚©‚ª 8 ‚ğŠÜ‚ñ‚Å‚¢‚ê‚Î 8Ø‚è”­“®
        return playedCards.Any(c => c.Rank == 8);
    }

    public void Apply(List<Card> playedCards, GameState state)
    {
        Debug.Log("8Ø‚è”­“®I");

        // ê‚ğ—¬‚·
        state.TableCards.Clear();

        // š ©•ª‚Ì”Ô‚ğŒp‘±
        state.KeepTurn = true;
    }
}

