using UnityEngine;

// スート（絵柄）を列挙型で定義
public enum Suit
{
    Spade,
    Heart,
    Diamond,
    Club
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
}