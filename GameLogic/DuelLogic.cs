using System;
using System.Collections;
using System.Collections.Generic;
using TcgEngine.Client;
using UnityEditor;
using UnityEngine;

namespace TcgEngine.Gameplay
{
    /// <summary>
    /// Расширенная логика для дуэльного режима.
    /// Наследуется от базовой GameLogic и переопределяет специфичные для дуэли методы.
    /// </summary>
    public class DuelLogic : GameLogic
    {
        // Поля для хранения атакующей и защитной карт в текущем раунде дуэли
        private Card duelAttacker = null;
        private Card duelDefender = null;

        public DuelLogic(Game game) : base(game)
        {
            //Debug.Log("DuelLogic(Game game) constructor finished");
        }

        public override void StartGame()
        {
            //Debug.Log("Запуск дуэльной игры");
            base.StartGame();

            // Подписываемся на событие розыгрыша карты для дуэли
            onCardPlayed += HandleDuelCardPlayed;
        }

        public override void StartTurn()
        {
            Debug.Log("🔄 [DuelLogic] Новый ход начинается. Сброс дуэльных переменных...");

            // ✅ Сбрасываем атакующую и защитную карту в начале нового хода
            duelAttacker = null;
            duelDefender = null;

            base.StartTurn();
        }

        /// <summary>
        /// Обработчик розыгрыша карты для дуэли.
        /// Если ещё не выбрана атакующая карта, назначаем её и завершаем ход,
        /// чтобы оппонент мог выбрать защитную карту.
        /// Если атакующая уже выбрана и карта принадлежит противнику – назначаем её защитной и запускаем дуэльный раунд.
        /// </summary>
        private void HandleDuelCardPlayed(Card card, Slot slot)
        {
            int designatedAttacker = GameData.first_player; // Определяем атакующего

            // ✅ Если атакующая карта еще не выбрана – назначаем её
            if (duelAttacker == null)
            {
                if (card.player_id == designatedAttacker)
                {
                    duelAttacker = card;
                    Debug.Log($"🛡 Атакующая карта выбрана: {card.card_id} (игрок {card.player_id})");
                    EndTurnImmediate(); // Завершаем ход, давая возможность защитнику выбрать карту
                }
                else
                {
                    Debug.Log($"❌ {GameClient.Get().GetPlayerName(card.player_id)} не может играть первым! Ждём атакующего.");
                }
            }
            // ✅ Если атакующая карта уже есть – выбираем защитную
            else if (duelDefender == null)
            {
                if (card.player_id != duelAttacker.player_id)
                {
                    duelDefender = card;
                    Debug.Log($"🛡 [DuelLogic] Защитная карта выбрана: {card.card_id} (игрок {card.player_id})");

                    // 🚀 **Принудительно запускаем дуэльный раунд**
                    RunDuelRound();
                }
                else
                {
                    Debug.Log($"❌ {GameClient.Get().GetPlayerName(card.player_id)} не может выбрать карту дважды!");
                }
            }
            else
            {
                Debug.Log("❌ Дуэль уже инициирована, дополнительное розыгрывание игнорируется.");
            }
        }
        public void PlayDefenderCard(Card card)
        {
            if (duelAttacker == null)
            {
                Debug.LogWarning("[DuelLogic] Нельзя выбрать защитную карту, пока атакующая не выбрана!");
                return;
            }

            if (duelDefender != null)
            {
                Debug.LogWarning("[DuelLogic] Защитная карта уже выбрана!");
                return;
            }

            duelDefender = card;
            Debug.Log($"🛡 [DuelLogic] Защитная карта выбрана: {card.card_id} ({GameClient.Get().GetPlayerName(card.player_id)})");

            RunDuelRound(); // ✅ Начинаем дуэль
        }

        public override void AttackTarget(Card attacker, Card target, bool skip_cost = true)
        {
            //Debug.Log("DuelLogic.AttackTarget called on object of type: " + this.GetType().Name);
            //Debug.Log("CanAttackTarget " + GameData.CanAttackTarget(attacker, target, skip_cost));
            if (GameData.CanAttackTarget(attacker, target, skip_cost))
            {
                Player player = GameData.GetPlayer(attacker.player_id);
                //if (!is_ai_predict)
                //player.AddHistory(GameAction.Attack, attacker, target);
                GameData.last_target = target.uid;
                TriggerCardAbilityType(AbilityTrigger.OnBeforeAttack, attacker, target);
                TriggerCardAbilityType(AbilityTrigger.OnBeforeDefend, target, attacker);
                TriggerSecrets(AbilityTrigger.OnBeforeAttack, attacker);
                TriggerSecrets(AbilityTrigger.OnBeforeDefend, target);
                ResolveQueue.AddAttack(attacker, target, ResolveAttack, skip_cost);
                ResolveQueue.ResolveAll(0.2f);
            }
        }
        public override void DamageCard(Card target, int value)
        {
            Debug.Log($"DuelLogic.DamageCard (overload 1) called: {value} damage to {target.card_id}");
            base.DamageCard(target, value);
            Debug.Log($"After damage, card {target.card_id} HP: {target.GetHP()}");
        }

        public override void DamageCard(Card attacker, Card target, int value, bool spell_damage = false)
        {
            //Debug.Log($"{value} damage from {attacker.card_id} to {target.card_id}");
            base.DamageCard(attacker, target, value, spell_damage);
            //Debug.Log($"After damage, card {target.card_id} HP: {target.GetHP()}");
        }

        public void EndTurnImmediate()
        {
            //Debug.Log("EndTurnImmediate called");
            base.EndTurn();
            // Здесь мы не используем resolve_queue.ResolveAll, чтобы не зависеть от задержек.
        }

        /// <summary>
        /// Запускает раунд дуэли между duelAttacker и duelDefender.
        /// Реализована простая итерация: атакующая карта атакует защитную, затем защитная отвечает.
        /// Процесс повторяется, пока у одной из карт не опустится HP до 0.
        /// </summary>
        /// <summary>
        /// Запускает раунд дуэли между duelAttacker и duelDefender.
        /// Теперь с учетом задержек, корректной проверки уничтожения и защиты от null.
        /// </summary>
        private void RunDuelRound()
        {
            Debug.Log("⚔️ Начинаем дуэльный раунд!");

            if (duelAttacker == null || duelDefender == null)
            {
                Debug.LogError("❌ Ошибка! duelAttacker или duelDefender = null. Раунд прерван.");
                EndDuelRound();
                return;
            }

            if (duelAttacker.GetHP() <= 0 || duelDefender.GetHP() <= 0)
            {
                Debug.Log("🏁 Завершаем дуэльный раунд, так как одна из карт уже мертва.");
                EndDuelRound();
                return;
            }

            // 🏆 Фаза 1: атакующая карта атакует защитную.
            ResolveQueue.AddCallback(() =>
            {
                if (duelAttacker == null || duelDefender == null) return;
                Debug.Log($"⚡ {duelAttacker.card_id} атакует {duelDefender.card_id}");
                this.AttackTarget(duelAttacker, duelDefender, false);
            });

            // 🛡️ Фаза 2: защитная карта отвечает, если выжила.
            ResolveQueue.AddCallback(() =>
            {
                if (duelDefender == null) return;

                if (duelDefender.GetHP() <= 0)
                {
                    Debug.Log("💀 Защитная карта уничтожена!");
                    EndDuelRound();
                }
                else
                {
                    if (duelAttacker == null) return;
                    Debug.Log($"🔄 {duelDefender.card_id} отвечает атакой на {duelAttacker.card_id}");
                    this.AttackTarget(duelDefender, duelAttacker, false);
                }
            });

            // 🔄 Фаза 3: проверка, остались ли карты на поле.
            ResolveQueue.AddCallback(() =>
            {
                Debug.Log("📌 Проверяем состояние карт после атаки...");
                RefreshData();

                if (duelAttacker == null || duelDefender == null)
                {
                    Debug.Log("❌ duelAttacker или duelDefender стали null во время проверки!");
                    EndDuelRound();
                    return;
                }

                if (!GameData.IsOnBoard(duelAttacker))
                {
                    Debug.Log("💀 Атакующая карта уничтожена (не на поле)!");
                    EndDuelRound();
                }
                else if (!GameData.IsOnBoard(duelDefender))
                {
                    Debug.Log("💀 Защитная карта уничтожена (не на поле)!");
                    EndDuelRound();
                }
                else
                {
                    Debug.Log("🔁 Обе карты живы, следующий обмен ударами.");
                    RunDuelRound();
                }
            });

            // 🚀 Запускаем выполнение очереди с задержкой 1.5 секунды
            Debug.Log("🚀 Запуск `ResolveQueue.ResolveAll(1.5f)`!");
            ResolveQueue.ResolveAll(1.5f);
        }
        /// <summary>
        /// 📢 Логирует все оставшиеся действия в ResolveQueue
        /// </summary>
        private void LogQueueActions()
        {
            Debug.Log("📜 Очередь действий в `ResolveQueue`:");

            Queue<AttackQueueElement> attackQueue = ResolveQueue.GetAttackQueue();
            Queue<AbilityQueueElement> abilityQueue = ResolveQueue.GetAbilityQueue();
            Queue<SecretQueueElement> secretQueue = ResolveQueue.GetSecretQueue();
            Queue<CallbackQueueElement> callbackQueue = ResolveQueue.GetCallbackQueue();

            if (attackQueue.Count == 0 && abilityQueue.Count == 0 && secretQueue.Count == 0 && callbackQueue.Count == 0)
            {
                Debug.Log("✅ Очередь пуста.");
                return;
            }

            foreach (var action in attackQueue)
                Debug.Log($"⚔️ Атака: {action.attacker.card_id} -> {action.target?.card_id}");

            foreach (var action in abilityQueue)
                Debug.Log($"🧙‍♂️ Способность: {action.ability.id} у {action.caster.card_id}");

            foreach (var action in secretQueue)
                Debug.Log($"🔮 Секрет: {action.secret.card_id} с триггером {action.secret_trigger}");

            foreach (var action in callbackQueue)
                Debug.Log("🔄 Callback-функция");
        }

        /// <summary>
        /// Завершает текущий раунд дуэли.
        /// Определяет победителя (кто остался с положительным HP) и начисляет очко герою победившей карты.
        /// Затем сбрасывает дуэльное состояние для начала нового раунда, где роли меняются.
        /// </summary>
        /// <summary>
        /// Завершает текущий раунд дуэли, теперь с задержками перед удалением карт.
        /// </summary>
        private void EndDuelRound()
        {
            Debug.Log("⚔️ EndDuelRound вызван!");

            bool attackerAlive = duelAttacker != null && GameData.IsOnBoard(duelAttacker);
            bool defenderAlive = duelDefender != null && GameData.IsOnBoard(duelDefender);

            if (!attackerAlive && !defenderAlive)
            {
                Debug.Log("🏁 Дуэль закончилась вничью, обе карты уничтожены.");
            }
            else if (attackerAlive && !defenderAlive)
            {
                Debug.Log($"🏆 Атакующая карта {duelAttacker.card_id} победила дуэль.");
                Player owner = GameData.GetPlayer(duelAttacker.player_id);
                owner.score++;
                Debug.Log($"{GameClient.Get().GetPlayerName(owner.player_id)} получает 1 очко, всего очков: {owner.score}");
            }
            else if (!attackerAlive && defenderAlive)
            {
                Debug.Log($"🏆 Защитная карта {duelDefender.card_id} победила дуэль.");
                Player owner = GameData.GetPlayer(duelDefender.player_id);
                owner.score++;
                Debug.Log($"{GameClient.Get().GetPlayerName(owner.player_id)} получает 1 очко, всего очков: {owner.score}");
            }

            // ⏳ Добавляем задержку перед удалением карт
            ResolveQueue.AddCallback(() =>
            {
                Debug.Log("⌛ Задержка 1 сек перед удалением карт...");
                TimeTool.WaitFor(1f, () =>
                {
                    // ✅ Удаление карт с поля, если они ещё существуют
                    if (duelAttacker != null)
                    {
                        Debug.Log($"🗑️ Удаляем атакующую карту {duelAttacker.card_id}");
                        DiscardCard(duelAttacker);
                    }

                    if (duelDefender != null)
                    {
                        Debug.Log($"🗑️ Удаляем защитную карту {duelDefender.card_id}");
                        DiscardCard(duelDefender);
                    }

                    // 🔄 Сбрасываем дуэльное состояние перед новым раундом
                    duelAttacker = null;
                    duelDefender = null;

                    // 🔄 Обновляем игровое состояние
                    RefreshData();

                    // ✅ Ждём завершения всех действий в очереди перед стартом нового раунда
                    TimeTool.StartCoroutine(WaitForResolveQueueAndStartNewRound());
                });
            });
        }

        private IEnumerator WaitForResolveQueueAndStartNewRound()
        {
            Debug.Log("🕒 Ожидание завершения очереди перед новым раундом...");

            while (ResolveQueue.IsResolving())
            {
                Debug.Log("[DuelLogic] Очередь ещё выполняется, ждём...");
                yield return new WaitForSeconds(0.5f);
            }

            Debug.Log("✅ Очередь завершена! Запускаем новый раунд через 2 секунды...");
            yield return new WaitForSeconds(2f);

            ResolveQueue.Clear(); // 🔄 Полная очистка перед новым раундом

            int nextAttackerId = (GameData.first_player == 0) ? 1 : 0;
            GameData.first_player = nextAttackerId;
            GameData.current_player = nextAttackerId;

            Debug.Log($"🔄 Следующий раунд дуэли: атакующим становится {GameClient.Get().GetPlayerName(nextAttackerId)}");

            Debug.Log($"✅ Готов к новому раунду. Ожидаем ход от {GameClient.Get().GetPlayerName(nextAttackerId)}");
        }
        /// <summary>
        /// Начинает новый раунд дуэли после удаления карт.
        /// </summary>
        private void StartNewDuelRound()
        {
            Debug.Log("⏳ Ожидание завершения очереди перед новым раундом...");
            TimeTool.StartCoroutine(WaitForResolveQueueAndStartNewRound());
        }



        private void AISelectDefender()
        {
            if (duelDefender != null) return; // ✅ Уже выбрали защитную карту

            Game game_data = GetGameData();
            Player aiPlayer = game_data.GetPlayer(GameData.first_player == 0 ? 1 : 0); // AI всегда защищается

            if (aiPlayer.cards_hand.Count > 0)
            {
                duelDefender = aiPlayer.cards_hand[0]; // AI выбирает первую карту из руки
                Debug.Log($"[AI] Принудительно выбрал защитную карту: {duelDefender.card_id}");

                // 🔥 Передаем карту в обработчик так же, как при выборе игрока!
                HandleDuelCardPlayed(duelDefender, Slot.None);
            }
            else
            {
                Debug.LogWarning("[AI] Нет доступных карт для защиты!");
                EndDuelRound(); // ✅ Если нечем защищаться, сразу завершаем раунд
            }
        }

        private IEnumerator WaitForResolveQueue()
        {
            while (ResolveQueue.IsResolving())
            {
                Debug.Log("[DuelLogic] Жду завершения очереди перед следующим действием...");
                yield return new WaitForSeconds(0.5f);
            }
        }
        public Card GetDuelAttacker()
        {
            return duelAttacker;
        }
        public Card GetDuelDefender()
        {
            return duelDefender;
        }
        public bool IsResolveQueueActive()
        {
            return ResolveQueue.IsResolving();
        }
        public void StartDuelRound()
        {
            RunDuelRound();
        }

    }





}
