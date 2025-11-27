using System.Collections.Generic;
using UnityEngine;

public class RevolutionRule : IRule
{
    public bool CanApply(List<Card> playedCards, GameState state)
    {
        // 4ñáà»è„èoÇ≥ÇÍÇΩÇÁävñΩ
        return playedCards.Count >= 4;
    }

    public void Apply(List<Card> playedCards, GameState state)
    {
        state.TriggerRevolution = true;
        Debug.Log("ävñΩî≠ê∂!ã≠Ç≥Ç™îΩì]ÇµÇ‹Ç∑ÅB");
    }
}