using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CpuPlayer : PlayerBase
{
    public override List<Card> SelectCards(List<Card> tableCards)
    {
        if (Hand.Count == 0)
            return new List<Card>();

        List<List<Card>> playableSets = new List<List<Card>>();

        // =============================
        // 🔹 1枚出し候補
        // =============================
        foreach (var card in Hand)
        {
            var single = new List<Card> { card };
            if (CanPlaySelectedCards(tableCards, single))
                playableSets.Add(single);
        }

        // =============================
        // 🔹 同ランク（ペア〜4カード）
        // =============================
        var groups = Hand.GroupBy(c => c.Rank).Where(g => g.Count() >= 2);
        foreach (var g in groups)
        {
            var sameRankCards = g.ToList();
            for (int i = 2; i <= sameRankCards.Count; i++)
            {
                var set = sameRankCards.Take(i).ToList();
                if (CanPlaySelectedCards(tableCards, set))
                    playableSets.Add(set);
            }
        }

        // =============================
        // 🔹 階段（3〜4枚）
        // =============================
        // 同じスートごとにチェック
        var suitGroups = Hand.GroupBy(c => c.Suit);
        foreach (var suitGroup in suitGroups)
        {
            var sorted = suitGroup.OrderBy(c => c.Rank).ToList();
            for (int i = 0; i < sorted.Count - 2; i++)
            {
                for (int len = 3; len <= 4 && i + len <= sorted.Count; len++)
                {
                    var seq = sorted.GetRange(i, len);

                    // 階段判定を利用
                    if (GetCardGroupType(seq) == CardGroupType.Stair &&
                        CanPlaySelectedCards(tableCards, seq))
                    {
                        playableSets.Add(seq);
                    }
                }
            }
        }

        // =============================
        // 🔹 出せるカードがない場合はパス
        // =============================
        if (playableSets.Count == 0)
        {
            Debug.Log($"{Name} はパスしました。");

            // --- GameManagerを探してUIに表示 ---
            var gm = GameObject.FindObjectOfType<GameManager>();
            if (gm != null)
            {
                gm.StartCoroutine(gm.ShowMessage($"{Name} はパスしました", 2f));
            }

            return new List<Card>();
        }

        // =============================
        // 🔹 出せる中から最も弱い組み合わせを選択
        // =============================
        var best = playableSets
            .OrderBy(set => set.Min(c => c.Rank))
            .ThenBy(set => set.Count) // 同ランクなら少ない枚数を優先
            .First();

        // =============================
        // 🔹 手札から削除
        // =============================
        foreach (var c in best)
            Hand.Remove(c);

        Debug.Log($"{Name} played {GetCardGroupType(best)}: {string.Join(",", best.Select(c => $"{c.Suit}-{c.Rank}"))}");
        return best;
    }
}
