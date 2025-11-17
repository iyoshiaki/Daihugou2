using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HumanPlayer : PlayerBase
{
    public override List<Card> SelectCards(List<Card> tableCards)
    {
        return SelectedCards;
    }

    // =============================================================
    // 出せるカードかどうかを判定（PlayerBaseのロジックに委譲）
    // =============================================================
    public bool CanPlaySelectedCards(List<Card> tableCards)
    {
        if (SelectedCards == null || SelectedCards.Count == 0)
        {
            Debug.Log("⚠️ カードが選択されていません。");
            return false;
        }

        // 親クラス（PlayerBase）のメソッドを呼び出す
        return base.CanPlaySelectedCards(tableCards, SelectedCards);
    }

    // -------------------------------------------------------------
    // 選択中カードをリセット
    // -------------------------------------------------------------
    public void ClearSelectedCards()
    {
        if (SelectedCards != null)
        {
            SelectedCards.Clear();
            Debug.Log("HumanPlayer: 選択中カードリストをクリアしました。");
        }
    }

    // =============================================================
    // 出せるカード一覧を取得（ペア・階段含む）
    // =============================================================
    public List<Card> GetPlayableCards(List<Card> tableCards)
    {
        if (Hand == null || Hand.Count == 0) return new List<Card>();
        if (tableCards == null) tableCards = new List<Card>();

        var playable = new HashSet<Card>(); // 重複防止

        // 手札の全ての有効な組み合わせを取得し、場に出せるかを判定
        foreach (var combo in FindAllPlayableCombos(Hand))
        {
            if (CanPlaySelectedCardsWithTemp(tableCards, combo))
            {
                foreach (var c in combo)
                    playable.Add(c);
            }
        }

        // Debug.Log($"[DEBUG] 出せるカード一覧: {string.Join(",", playable.Select(c => c.Rank))}");
        return playable.ToList();
    }

    // =============================================================
    // 全ての有効な組み合わせ（単体、同ランク2～4枚、階段3～4枚）を抽出
    // =============================================================
    private List<List<Card>> FindAllPlayableCombos(List<Card> hand)
    {
        List<List<Card>> combos = new List<List<Card>>();

        // --- 1枚出し ---
        foreach (var card in Hand)
        {
            combos.Add(new List<Card> { card });
        }

        // --- 同ランク（ペア〜4カード） ---
        var groups = Hand.GroupBy(c => c.Rank).Where(g => g.Count() >= 2);
        foreach (var g in groups)
        {
            var same = g.ToList();
            for (int i = 2; i <= Mathf.Min(4, same.Count); i++) // 4カードまで
            {
                combos.Add(same.Take(i).ToList());
            }
        }

        // --- 階段（3〜4枚） ---
        var suitGroups = Hand.GroupBy(c => c.Suit);
        foreach (var sg in suitGroups)
        {
            var sorted = sg.OrderBy(c => c.Rank).ToList();
            for (int i = 0; i < sorted.Count - 2; i++)
            {
                // 3枚と4枚の階段を探す
                for (int len = 3; len <= 4 && i + len <= sorted.Count; len++)
                {
                    var seq = sorted.GetRange(i, len);
                    // PlayerBase の IsStair ロジックを継承しているため、ここでは GetCardGroupType で確認
                    if (GetCardGroupType(seq) == CardGroupType.Stair)
                    {
                        combos.Add(seq);
                    }
                }
            }
        }

        return combos;
    }


    // =============================================================
    // 一時的に SelectedCards を差し替えて判定
    // =============================================================
    private bool CanPlaySelectedCardsWithTemp(List<Card> tableCards, List<Card> temp)
    {
        // PlayerBase の CanPlaySelectedCards は selected パラメータを持つため、
        // SelectedCards の差し替えは不要
        return base.CanPlaySelectedCards(CloneCards(tableCards), temp);
    }

    // =============================================================
    // Deep Copy
    // =============================================================
    private List<Card> CloneCards(List<Card> source)
    {
        if (source == null) return new List<Card>();
        return source.ToList();
    }
}