using System.Collections.Generic;
using System.Linq;

public class FiveSkipRule : IRule
{
    public bool CanApply(List<Card> played, GameState state)
    {
        if (played == null || played.Count == 0) return false;
        return played.Any(c => c.Rank == 5);
    }

    public void Apply(List<Card> played, GameState state)
    {
        // 5が含まれている枚数分、次のプレイヤーをスキップ
        int fiveCount = played.Count(c => c.Rank == 5);
        state.SkipCount += fiveCount;
    }

    /// <summary>
    /// 5飛ばしを実行（スキップカウントを設定し、メッセージを表示）
    /// 実際のスキップ処理はGameManagerのEndTurn()で行われる
    /// </summary>
    public void ExecuteFiveSkip(GameManager gm, int skipCount)
    {
        gm.EnqueueMessage($"{skipCount}人飛ばし!");
    }
}