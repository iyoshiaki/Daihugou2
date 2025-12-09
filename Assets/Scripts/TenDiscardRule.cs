using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

public class TenDiscardRule : IRule
{
    public bool CanApply(List<Card> played, GameState state)
    {
        if (played == null || played.Count == 0) return false;
        return played.Any(c => c.Rank == 10);
    }

    public void Apply(List<Card> played, GameState state)
    {
        int tenCount = played.Count(c => c.Rank == 10);
        state.TenDiscardCount = tenCount;
    }

    /// <summary>
    /// 10捨てシーケンスを開始
    /// </summary>
    public IEnumerator StartTenDiscardSequence(GameManager gm, PlayerBase player, int count)
    {
        gm.SetTenDiscardMode(true, count); // count は捨てられる最大枚数

        // ★メッセージをキューではなく即時表示させる（ShowTenDiscardUI内で実行される）
        string message = $"10捨て! 最大{count}枚選んで捨てることができます! ";

        if (player is HumanPlayer humanPlayer)
        {
            gm.ShowTenDiscardUI(message);

            // プレイヤーが選択している間、ここでコルーチンの実行を一時停止させる
            // プレイヤーはUI上のボタンで ExecuteTenDiscardAction を呼ぶ
            yield break;
        }
        else
        {
            // CPUの処理
            gm.EnqueueMessage(message); // CPUの場合はメッセージをキューに入れて流れを止めない
            yield return new WaitForSeconds(1.0f);

            var hand = player.Hand.OrderBy(c => c.Rank).ToList();

            // ★CPUは捨てられる最大枚数(count)以下の任意の枚数（今回は最大枚数）を選ぶロジック
            int cardCountToDiscard = Mathf.Min(count, hand.Count);

            // 最も弱いカードを選ぶ（例として最小ランクのカード）
            var cardsToDiscard = hand.Take(cardCountToDiscard).ToList();

            // CPUは捨てない選択肢も持ちうるが、ここでは最大枚数を捨てることにする
            if (cardsToDiscard.Count > 0)
            {
                yield return gm.StartCoroutine(ExecuteTenDiscardAction(gm, player, cardsToDiscard));
            }
            else
            {
                // CPUが捨てない場合
                gm.EnqueueMessage($"{player.Name} は何も捨てませんでした");
                gm.SetTenDiscardMode(false, 0);
                gm.HideActionMessage();
                gm.ResetPlayButtonUI();
                gm.EndPlayerTurn(); // ターンを終了してゲームを続行
            }
        }
    }

    /// <summary>
    /// 10捨ての実際の破棄処理
    /// </summary>
    public IEnumerator ExecuteTenDiscardAction(GameManager gm, PlayerBase player, List<Card> cards)
    {
        gm.SetTenDiscardMode(false, 0); // モードを終了
        gm.HideActionMessage(); // メッセージを非表示
        gm.ResetPlayButtonUI(); // Playボタンを通常に戻す

        if (cards == null || cards.Count == 0)
        {
            gm.EnqueueMessage($"{player.Name} はカードを渡し（パスし）ました");
            Debug.Log($"{player.Name} はカードを渡し（パスし）ました");
        }
        else
        {
            Debug.Log($"{player.Name} は {cards.Count}枚 捨てました");
            gm.EnqueueMessage($"{player.Name} は {cards.Count}枚 捨てました");

            foreach (var card in cards)
            {
                player.Hand.Remove(card);
            }

            // UIからカードを削除
            if (player is HumanPlayer)
            {
                gm.RemoveCardsFromPlayerUI(cards);
            }
        }

        // 手札表示を更新
        gm.UpdatePlayerHandDisplay(player);
        yield return new WaitForSeconds(0.8f);

        // ★あがり判定
        if (player.Hand.Count == 0)
        {
            if (gm.GetForbidSpecialWin())
            {
                gm.EnqueueMessage("🚫 ルールにより、特殊ルール（10捨て）でのあがりは禁止されています！");
            }
            else
            {
                gm.CheckForWin(player);
                if (gm.IsGameOver()) yield break;
            }
        }
        else // あがっていなければターンを次に進める
        {
            gm.EndTurn(); // ターンを終了してゲームを続行
        }

        // ターンを終了してゲームを続行
        gm.EndPlayerTurn();
    }
}