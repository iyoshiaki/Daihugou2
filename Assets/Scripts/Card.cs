using UnityEngine;

// スート（絵柄）を列挙型で定義
public enum Suit
{
    Spade,
    Heart,
    Diamond,
    Club,
    Joker
}


public class Card
{
    public Suit Suit { get; set; }
    public int Rank { get; set; }
    public string SpritePath { get; set; }

    public Card() { }

    public Card(Suit suit, int rank)
    {
        Suit = suit;
        Rank = rank;
        SpritePath = $"Cards/{suit}_{rank}";
    }

    public override string ToString()
    {
        return $"{Suit}-{Rank}";
    }
    public bool IsJoker()
    {
        return Suit == Suit.Joker || Rank == 99;
    }

    public int GetStrength()
    {
        return IsJoker() ? 100 : Rank;
    }

    public static Card CreateJoker()
    {
        return new Card
        {
            Suit = Suit.Joker,
            Rank = 99,                     // 特殊ランクとして99を設定
            SpritePath = "Cards/Joker"     // Resources/Cards/Joker 画像が必要
        };
    }
}
