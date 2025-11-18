using System.Collections.Generic;

public interface IRule
{
    // ルールが適用可能かどうかを判定する（副作用なし）
    bool CanApply(List<Card> playedCards, GameState state);

    // ルールを適用する（state を書き換える等の副作用あり）
    void Apply(List<Card> playedCards, GameState state);
}
