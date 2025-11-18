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
    public string SpritePath { get; set; }   // 読み取り専用ではなく、代入可能に

    public Card() { }

    // コンストラクタを使う場合用（任意）
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
        return Rank == 0;
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
            Rank = 99,                     // Joker の特殊ランク
            SpritePath = "Cards/Joker"     // Joker 専用画像
        };
    }
}
