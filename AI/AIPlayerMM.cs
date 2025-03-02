using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;
using TcgEngine.Client;

namespace TcgEngine.AI
{
    /// <summary>
    /// AI player using the MinMax AI algorithm
    /// </summary>

    public class AIPlayerMM : AIPlayer
    {
        private AILogic ai_logic;
        private bool is_playing = false;

        private DuelLogic duelLogic;

        public AIPlayerMM(GameLogic gameplay, int id, int level)
        {
            if (GameplayData.Get().duel)
            {
                if (gameplay is DuelLogic duelLogicInstance)
                {
                    duelLogic = duelLogicInstance;
                }
                else
                {
                    Debug.LogError("❌ Ошибка! AI ожидает DuelLogic, но получил другой GameLogic.");
                }
            }

            this.gameplay = gameplay;
            player_id = id;
            ai_level = Mathf.Clamp(level, 1, 10);
            ai_logic = AILogic.Create(id, ai_level);
        }
        public override void Update()
        {
            Game game_data = gameplay.GetGameData();
            Player player = game_data.GetPlayer(player_id);
            DuelLogic duelLogic = gameplay as DuelLogic;

            // ✅ Проверяем, идет ли дуэль и нужно ли AI выбирать защитную карту
            if (duelLogic != null && duelLogic.GetDuelAttacker() != null && duelLogic.GetDuelDefender() == null)
            {
                Debug.Log($"[AI] Обнаружил атакующую карту {duelLogic.GetDuelAttacker().card_id}, выбираю защитную карту...");
                ChooseDefenderCard();
                return;
            }

            if (!is_playing && game_data.IsPlayerTurn(player))
            {
                is_playing = true;
                TimeTool.StartCoroutine(AiTurn());
            }
            if (!is_playing && game_data.IsPlayerMulliganTurn(player))
            {
                SkipMulligan();
            }

            if (!game_data.IsPlayerTurn(player) && ai_logic.IsRunning())
                Stop();
        }
        private void ChooseDefenderIfNeeded()
        {
            if (duelLogic == null) return; // AI не может работать без DuelLogic

            Game game_data = gameplay.GetGameData();
            Card attacker = duelLogic.GetDuelAttacker();

            if (attacker != null && duelLogic.GetDuelDefender() == null)
            {
                Debug.Log($"[AI] Обнаружил атакующую карту {attacker.card_id}, выбираю защитную карту...");

                Player aiPlayer = game_data.GetPlayer(player_id);
                List<Card> handCards = aiPlayer.cards_hand;

                if (handCards.Count > 0)
                {
                    Card bestDefender = handCards[0];

                    Debug.Log($"[AI] Принудительно выбрал защитную карту: {bestDefender.card_id}");

                    gameplay.PlayCard(bestDefender, bestDefender.slot);

                    // 🔥 Запускаем бой через DuelLogic
                    duelLogic.StartDuelRound();
                }
                else
                {
                    Debug.Log("[AI] Нет доступных карт для защиты! Пропускаю защитную фазу.");
                }
            }
        }

        // ✅ Добавляем выбор защитной карты
        private void ChooseDefenderCard()
        {
            Game game_data = gameplay.GetGameData();
            DuelLogic duelLogic = gameplay as DuelLogic;

            if (duelLogic == null || duelLogic.GetDuelAttacker() == null || duelLogic.GetDuelDefender() != null)
                return; // ❌ Выход, если защитная карта уже выбрана или дуэльный режим отключен

            Card bestDefender = FindBestDefender(game_data);

            if (bestDefender != null)
            {
                Debug.Log($"[AI] Принудительно выбрал защитную карту: {bestDefender.card_id}");

                // ✅ Теперь AI действительно разыгрывает карту
                gameplay.PlayCard(bestDefender, bestDefender.slot);

                // ✅ Уведомляем `DuelLogic` о выборе защитной карты
                duelLogic.PlayDefenderCard(bestDefender);
            }
            else
            {
                Debug.LogWarning("[AI] Не удалось выбрать защитную карту!");
            }
        }

        // ✅ Логика выбора лучшей защитной карты (можно расширять)
        private Card FindBestDefender(Game game_data)
        {
            Player player = game_data.GetPlayer(player_id);
            if (player.cards_hand.Count > 0)
            {
                return player.cards_hand[0]; // Простая логика: берем первую доступную карту
            }
            return null;
        }

        private IEnumerator AiTurnCoroutine()
        {
            Debug.Log("[AI] Жду завершения очереди перед началом своего хода...");
            yield return WaitForResolveQueue();  // ✅ Ждём завершения предыдущего раунда

            Game game_data = gameplay.GetGameData();
            Player player = game_data.GetPlayer(player_id);

            // 🔹 **Проверяем, является ли AI атакующим игроком**
            if (game_data.first_player != player_id)
            {
                Debug.Log("[AI] Я не атакующий. Жду выбора атакующей карты...");
                yield break; // ❌ AI ничего не делает, если он не атакующий
            }

            yield return new WaitForSeconds(1f); // ⏳ Дополнительная задержка для безопасности

            // ✅ **AI выполняет действие только если у него есть доступные карты**
            List<AIAction> actions = ai_logic.GetAvailableActions(game_data);
            if (actions.Count > 0)
            {
                AIAction best = actions[0]; // Просто берём первое действие
                Debug.Log("[AI] Выполняю действие: " + best.GetText(game_data));
                ExecuteAction(best, game_data);
            }
            else
            {
                Debug.Log("[AI] Нет доступных действий! Пропускаю ход.");
            }

            yield return new WaitForSeconds(0.5f);
            is_playing = false;
        }
        private Card ChooseBestCardToPlay(Game game_data, int playerId)
        {
            Player player = game_data.GetPlayer(playerId);
            Card bestCard = null;
            int bestAttack = 0;

            foreach (Card card in player.cards_hand)
            {
                // Проверяем, можно ли сыграть карту
                if (game_data.CanPlayCard(card, new Slot(1, 1, playerId)))
                {
                    int attackValue = card.attackMax; // Используем максимальную атаку карты
                    if (attackValue > bestAttack)
                    {
                        bestAttack = attackValue;
                        bestCard = card;
                    }
                }
            }

            return bestCard;
        }
        private IEnumerator AiTurn()
        {
            Debug.Log("[AI] Жду завершения очереди перед началом своего хода...");
            yield return WaitForResolveQueue();

            Game game_data = gameplay.GetGameData();
            Player player = game_data.GetPlayer(player_id);

            // Проверяем, является ли AI атакующим игроком
            if (game_data.first_player != player_id)
            {
                Debug.Log("[AI] Я не атакующий. Жду выбора атакующей карты...");
                yield break; // AI ничего не делает, если он не атакующий
            }

            yield return new WaitForSeconds(1f); // Дополнительная задержка

            // Получаем доступные действия (только PlayCard)
            List<AIAction> actions = ai_logic.GetAvailableActions(game_data);
            if (actions.Count > 0)
            {
                AIAction best = actions[0]; // Просто берём первое действие
                Debug.Log("[AI] Выполняю действие: " + best.GetText(game_data));
                ExecuteAction(best, game_data);
            }
            else
            {
                Debug.Log("[AI] Нет доступных действий! Пропускаю ход.");
            }

            yield return new WaitForSeconds(0.5f);
            is_playing = false;
        }
        private IEnumerator WaitForResolveQueue()
        {
            while (gameplay.ResolveQueue.IsResolving())
            {
                Debug.Log("[AIPlayerMM] Жду завершения очереди перед следующим действием...");
                yield return new WaitForSeconds(0.5f);
            }
        }

        private void Stop()
        {
            ai_logic.Stop();
            is_playing = false;
        }

        // ---------- Улучшенный ExecuteAction ----------

        private void ExecuteAction(AIAction action, Game game_data)
        {
            if (!CanPlay())
                return;

            Debug.Log("[AI] Выполняем действие: " + action.type);

            switch (action.type)
            {
                case GameAction.PlayCard:
                    if (game_data.first_player != player_id)
                    {
                        Debug.LogWarning("[AI] Я не атакующий! Не могу играть карту.");
                        return;
                    }
                    PlayCard(action.card_uid, action.slot);
                    break;

                case GameAction.Attack:
                    AttackCard(action.card_uid, action.target_uid);
                    break;

                case GameAction.AttackPlayer:
                    AttackPlayer(action.card_uid, action.target_player_id);
                    break;

                case GameAction.Move:
                    MoveCard(action.card_uid, action.slot);
                    break;

                case GameAction.CastAbility:
                    Card card = game_data.GetCard(action.card_uid);
                    if (!game_data.CanCastAbility(card, AbilityData.Get(action.ability_id)))
                    {
                        Debug.LogWarning("[AI] AI не может использовать способность: " + AbilityData.Get(action.ability_id).id + " у карты " + card.card_id);
                    }
                    if (card != null && game_data.CanCastAbility(card, AbilityData.Get(action.ability_id)))
                    {
                        CastAbility(action.card_uid, action.ability_id);
                    }
                    else
                    {
                        Debug.LogWarning($"[AI] Не удалось использовать способность {action.ability_id}. Пробуем другое действие...");
                        List<AIAction> availableActions = ai_logic.GetAvailableActions(game_data);
                        AIAction alternative = availableActions.Find(a => a.type != GameAction.CastAbility);
                        if (alternative != null)
                        {
                            ExecuteAction(alternative, game_data);
                        }
                    }
                    break;

                case GameAction.EndTurn:
                    EndTurn();
                    break;
            }
        }

        private void PlayCard(string card_uid, Slot slot)
        {
            Game game_data = gameplay.GetGameData();
            Card card = game_data.GetCard(card_uid);
            if (card != null)
            {
                gameplay.PlayCard(card, slot);
            }
        }

        private void MoveCard(string card_uid, Slot slot)
        {
            Game game_data = gameplay.GetGameData();
            Card card = game_data.GetCard(card_uid);
            if (card != null)
            {
                gameplay.MoveCard(card, slot);
            }
        }

        private void AttackCard(string attacker_uid, string target_uid)
        {
            Game game_data = gameplay.GetGameData();
            Card card = game_data.GetCard(attacker_uid);
            Card target = game_data.GetCard(target_uid);
            if (card != null && target != null)
            {
                gameplay.AttackTarget(card, target);
            }
        }

        private void AttackPlayer(string attacker_uid, int target_player_id)
        {
            Game game_data = gameplay.GetGameData();
            Card card = game_data.GetCard(attacker_uid);
            if (card != null)
            {
                Player oplayer = game_data.GetPlayer(target_player_id);
                gameplay.AttackPlayer(card, oplayer);
            }
        }

        private void CastAbility(string caster_uid, string ability_id)
        {
            Game game_data = gameplay.GetGameData();
            Card caster = game_data.GetCard(caster_uid);
            AbilityData iability = AbilityData.Get(ability_id);
            if (caster != null && iability != null)
            {
                gameplay.CastAbility(caster, iability);
            }
        }

        private void SelectCard(string target_uid)
        {
            Game game_data = gameplay.GetGameData();
            Card target = game_data.GetCard(target_uid);
            if (target != null)
            {
                gameplay.SelectCard(target);
            }
        }

        private void SelectPlayer(int tplayer_id)
        {
            Game game_data = gameplay.GetGameData();
            Player target = game_data.GetPlayer(tplayer_id);
            if (target != null)
            {
                gameplay.SelectPlayer(target);
            }
        }

        private void SelectSlot(Slot slot)
        {
            if (slot != Slot.None)
            {
                gameplay.SelectSlot(slot);
            }
        }

        private void SelectChoice(int choice)
        {
            gameplay.SelectChoice(choice);
        }

        private void SelectCost(int cost)
        {
            gameplay.SelectCost(cost);
        }

        private void CancelSelect()
        {
            if (CanPlay())
            {
                gameplay.CancelSelection();
            }
        }

        private void SkipMulligan()
        {
            string[] cards = new string[0]; //Don't mulligan
            SelectMulligan(cards);
        }

        private void SelectMulligan(string[] cards)
        {
            Game game_data = gameplay.GetGameData();
            Player player = game_data.GetPlayer(player_id);
            gameplay.Mulligan(player, cards);
        }

        private void EndTurn()
        {
            if (CanPlay())
            {
                gameplay.EndTurn();
            }
        }

        private void Resign()
        {
            int other = player_id == 0 ? 1 : 0;
            gameplay.EndGame(other);
        }

    }

}