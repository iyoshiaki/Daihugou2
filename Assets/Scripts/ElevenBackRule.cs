using System.Collections.Generic;
using System.Linq;

public class ElevenBackRule : IRule
{
    public bool CanApply(List<Card> played, GameState state)
    {
        if (played == null || played.Count == 0) return false;
        return played.Any(c => c.Rank == 11);
    }

    public void Apply(List<Card> played, GameState state)
    {
        state.IsElevenBack = true;
    }

    /// <summary>
    /// 11バックを実行（一時的な革命状態を設定）
    /// この一時的な革命状態は、場が流れるまで継続する
    /// 通常の革命状態とXOR関係にあり、両方発動すると相殺される
    /// </summary>
    public void ExecuteElevenBack(GameManager gm, ref bool isTempRevolution)
    {
        gm.EnqueueMessage("11バック!");
        isTempRevolution = true;
    }
}