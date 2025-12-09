using System.Collections.Generic;
using System.Linq;

public class RevolutionRule : IRule
{
    public bool CanApply(List<Card> played, GameState state)
    {
        if (played == null || played.Count < 4) return false;

        var realCards = played.Where(c => !c.IsJoker()).ToList();
        if (realCards.Count == 0) return false;

        return realCards.All(c => c.Rank == realCards[0].Rank);
    }

    public void Apply(List<Card> played, GameState state)
    {
        state.TriggerRevolution = true;
    }

    /// <summary>
    /// Šv–½ó‘Ô‚ğƒgƒOƒ‹‚·‚é
    /// </summary>
    public void ExecuteRevolution(GameManager gm, ref bool isRevolution)
    {
        isRevolution = !isRevolution;
        gm.EnqueueMessage(isRevolution ? "Šv–½ŠJn!" : "Šv–½I—¹!");
    }
}