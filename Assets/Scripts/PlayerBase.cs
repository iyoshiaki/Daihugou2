using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class PlayerBase
{
    public string Name { get; set; }
    public List<Card> Hand { get; set; } = new List<Card>();
    public List<Card> SelectedCards { get; set; } = new List<Card>();
    public List<Card> HandCards => Hand;

    // 👇 UI上の手札エリア
    public Transform handArea { get; set; }

    // --- カードの組み合わせタイプ ---
    public enum CardGroupType
    {
        Single,     // 単体
        Pair,       // ペア
        Triple,     // 3カード
        FourCard,   // 4カード
        Stair,      // 階段（同スート連番）
        Invalid     // 無効
    }

    // 各プレイヤーがカードを選ぶ処理
    public abstract List<Card> SelectCards(List<Card> fieldCards);

    // ==============================
    // 🔷 カードの組み合わせタイプ判定
    // ==============================
    public CardGroupType GetCardGroupType(List<Card> cards)
    {
        if (cards == null || cards.Count == 0)
            return CardGroupType.Invalid;

        // --- 1枚 ---
        if (cards.Count == 1)
            return CardGroupType.Single;

        // --- 同じ数字 ---
        if (cards.All(c => c.Rank == cards[0].Rank))
        {
            switch (cards.Count)
            {
                case 2: return CardGroupType.Pair;
                case 3: return CardGroupType.Triple;
                case 4: return CardGroupType.FourCard;
                default: return CardGroupType.Invalid;
            }
        }

        // --- 階段（スートが同じで連番） ---
        if (IsStair(cards))
            return CardGroupType.Stair;

        return CardGroupType.Invalid;
    }

    // ==============================
    // 🔷 階段判定
    // ==============================
    private bool IsStair(List<Card> cards)
    {
        if (cards == null || cards.Count < 3)
            return false;

        var suit = cards[0].Suit;
        if (cards.Any(c => c.Suit != suit))
            return false;

        var sorted = cards.OrderBy(c => c.Rank).ToList();

        for (int i = 1; i < sorted.Count; i++)
        {
            if (sorted[i].Rank != sorted[i - 1].Rank + 1)
                return false;
        }

        return true;
    }

    // ==============================
    // 🔷 出せるか判定（場との比較）
    // ==============================
    public bool CanPlaySelectedCards(List<Card> tableCards, List<Card> selected = null)
    {
        var cardsToCheck = selected ?? SelectedCards;

        if (cardsToCheck == null || cardsToCheck.Count == 0)
            return false;

        var myType = GetCardGroupType(cardsToCheck);
        if (myType == CardGroupType.Invalid)
            return false; // 自分の出すカードが無効な組み合わせ

        // --- 場が空ならどの組み合わせでもOK ---
        if (tableCards == null || tableCards.Count == 0)
            return true;

        var tableType = GetCardGroupType(tableCards);

        // --- 場が無効ならありえないが念のため ---
        if (tableType == CardGroupType.Invalid)
            return true; // 場がバグってたら出せる？（ここは設計次第だが通常は場は有効）


        if (tableType != myType)
            return false; // 種類が違えば出せない（例：3カードに階段を出すなど）

        // --- 枚数一致（同タイプなら枚数も同じはずだが、階段の場合に念のため） ---
        if (cardsToCheck.Count != tableCards.Count)
            return false;

        // --- 比較ロジック ---
        switch (myType)
        {
            case CardGroupType.Single:
            case CardGroupType.Pair:
            case CardGroupType.Triple:
            case CardGroupType.FourCard:
                // 同ランク出しは先頭のランク（全て同じなので）で比較
                return cardsToCheck[0].Rank > tableCards[0].Rank;

            case CardGroupType.Stair:
                // 階段出しは最大ランク（末尾）で比較
                var mySorted = cardsToCheck.OrderBy(c => c.Rank).ToList();
                var tableSorted = tableCards.OrderBy(c => c.Rank).ToList();
                return mySorted.Last().Rank > tableSorted.Last().Rank;

            default:
                return false;
        }
    }
    // ==============================
    // 🔷 カード受け取り
    // ==============================
    public virtual void ReceiveCard(Card card)
    {
        Hand.Add(card);
    }
}
