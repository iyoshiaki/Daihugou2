using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FiveSkipRule : IRule
{
    public bool CanApply(List<Card> playedCards, GameState state)
    {
        // 5が含まれていれば発動
        return playedCards.Any(c => c.Rank == 5);
    }

    public void Apply(List<Card> playedCards, GameState state)
    {
        // 出した枚数分だけ飛ばす（1枚なら1人飛ばし＝次の次の人へ）
        int count = playedCards.Count(c => c.Rank == 5);
        state.SkipCount = count;
        Debug.Log($"5飛ばし! {count} 人飛ばします。");
    }
}