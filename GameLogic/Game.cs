using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    //Contains all gameplay state data that is sync across network

    [System.Serializable]
    public class Game
    {
        #region Variables
        // 📌 Уникальный идентификатор игры (например, "game_12345"). 
        // Используется для идентификации конкретной игровой сессии в сети или локально.
        public string game_uid;

        // 📌 Настройки игры, хранящие такие параметры, как количество игроков, начальное количество карт, правила и т. д.
        public GameSettings settings;

        // 🏆 **Переменные состояния игры**
        // 📌 ID игрока, который ходит первым (0 или 1, если два игрока).
        public int first_player = 0;

        // 📌 ID текущего игрока, чей сейчас ход.
        public int current_player = 0;

        // 📌 Счетчик ходов — увеличивается каждый раз, когда проходит полный цикл ходов всех игроков.
        public int turn_count = 0;

        // ⏳ Таймер текущего хода (например, если есть ограничение на время хода).
        //public float turn_timer = 0f;

        // 📌 Состояние игры: `Connecting` (ожидание подключения игроков), `Play` (игра идет), `GameEnded` (игра завершена).
        public GameState state = GameState.Connecting;

        // 📌 Текущая фаза игры: `None` (не началась), `Mulligan` (пересдача карт), 
        // `StartTurn` (начало хода), `Main` (основная фаза), `EndTurn` (конец хода).
        public GamePhase phase = GamePhase.None;

        // 👥 **Список игроков**
        // 📌 Массив объектов `Player`, представляющих всех участников игры.
        public Player[] players;

        // 🎯 **Выбор цели (Selector)**
        // 📌 Тип выбора, который сейчас происходит в игре: 
        // `None` (ничего не выбирается), `SelectTarget` (выбор цели), 
        // `SelectorCard` (выбор карты), `SelectorChoice` (выбор опции), `SelectorCost` (выбор стоимости).
        public SelectorType selector = SelectorType.None;

        // 📌 ID игрока, который должен выбрать цель или действие.
        public int selector_player_id = 0;

        // 📌 ID способности, которая требует выбора цели.
        public string selector_ability_id;

        // 📌 ID карты, которая использует способность и требует выбора цели.
        public string selector_caster_uid;

        // 🔄 **Последние выполненные действия**
        // 📌 ID последней сыгранной карты.
        public string last_played;

        // 📌 ID последней цели (может быть карта или игрок).
        public string last_target;

        // 📌 ID последней уничтоженной карты.
        public string last_destroyed;

        // 📌 ID последней призванной (вызванной на поле) карты.
        public string last_summoned;

        // 📌 ID карты, активировавшей последнюю способность.
        public string ability_triggerer;

        // 🎲 Последнее выпавшее случайное значение (например, при броске кубика).
        public int rolled_value;

        // 🔢 Выбранное значение (например, при выборе стоимости заклинания).
        public int selected_value;

        // 📜 **История игровых событий**
        // 📌 Набор ID сыгранных способностей (используется для проверки, чтобы одноразовые способности не активировались повторно).
        public HashSet<string> ability_played = new HashSet<string>();

        // 📌 Набор ID карт, которые уже атаковали в этом ходу (чтобы они не могли атаковать повторно).
        public HashSet<string> cards_attacked = new HashSet<string>();

        #endregion

        // 🏗️ **Конструктор игры**
        // 📌 Пустой конструктор, который создаёт объект `Game`, но не заполняет его данными.
        public Game() { }

        public Game(string uid, int nb_players) // 📌 Конструктор класса Game
        // Этот метод создаёт новую игру, задаёт уникальный идентификатор и количество игроков.
        {
            // 🔹 Присваиваем уникальный идентификатор игры (uid), который может использоваться для сетевых игр.
            this.game_uid = uid;

            // 🔹 Создаём массив игроков с указанным количеством (nb_players).
            players = new Player[nb_players];

            // 🔹 Заполняем массив игроков, создавая новый объект Player для каждого игрока.
            for (int i = 0; i < nb_players; i++)
                players[i] = new Player(i); // ID каждого игрока совпадает с индексом в массиве.

            // 🔹 Устанавливаем настройки игры по умолчанию.
            settings = GameSettings.Default;
        }

        public virtual bool AreAllPlayersReady()  // 📌 Метод проверки, готовы ли все игроки к игре.
        {
            int ready = 0; // 🔹 Счётчик готовых игроков.

            // 🔹 Проходим по всем игрокам и проверяем, готов ли каждый из них.
            foreach (Player player in players)
            {
                if (player.IsReady()) // Если игрок готов, увеличиваем счётчик.
                    ready++;
            }

            // 🔹 Проверяем, достигло ли количество готовых игроков нужного значения.
            return ready >= settings.nb_players;
        }

        public virtual bool AreAllPlayersConnected() // 📌 Метод проверки, подключены ли все игроки к игре.
        {
            int ready = 0; // 🔹 Счётчик подключенных игроков.

            // 🔹 Проходим по всем игрокам и проверяем, подключён ли каждый из них.
            foreach (Player player in players)
            {
                if (player.IsConnected()) // Если игрок подключён, увеличиваем счётчик.
                    ready++;
            }

            // 🔹 Проверяем, подключено ли нужное количество игроков для начала игры.
            return ready >= settings.nb_players;
        }

        public virtual bool IsPlayerTurn(Player player) // 📌 Проверяет, является ли сейчас ход данного игрока.
        {
            // 🔹 Возвращает true, если сейчас либо основной ход игрока, либо игрок выбирает цель.
            return IsPlayerActionTurn(player) || IsPlayerSelectorTurn(player);
        }

        public virtual bool IsPlayerActionTurn(Player player) // 📌 Проверяет, может ли игрок выполнять действия в свой ход.
        {

            return player != null // 🔹 Проверяем, что игрок не null (существует).
                && current_player == player.player_id // 🔹 Проверяем, что ID текущего игрока совпадает с ID активного игрока.
                && state == GameState.Play // 🔹 Игра находится в активном состоянии (не завершена, не в подготовке).
                && phase == GamePhase.Main // 🔹 Сейчас основная фаза хода (когда можно играть карты, атаковать).
                && selector == SelectorType.None; // 🔹 Не активирован никакой режим выбора (например, выбор цели способности).
        }

        public virtual bool IsPlayerSelectorTurn(Player player) // 📌 Проверяет, может ли игрок выполнять выбор в рамках специального селектора (например, выбрать карту или цель).
        {
            return player != null // 🔹 Проверяем, что игрок существует.
                && selector_player_id == player.player_id // 🔹 Проверяем, что текущий игрок — это тот, кто должен выбрать цель.
                && state == GameState.Play // 🔹 Игра находится в активном состоянии.
                && phase == GamePhase.Main // 🔹 Сейчас основная фаза хода.
                && selector != SelectorType.None; // 🔹 Включён режим выбора (то есть игрок должен сделать выбор).
        }

        public virtual bool IsPlayerMulliganTurn(Player player) // 📌 Проверяет, находится ли игрок на стадии муллигана (перемешивания начальной руки).
        {
            return phase == GamePhase.Mulligan // 🔹 Проверяем, что сейчас идёт стадия муллигана.
                && !player.ready; // 🔹 Игрок ещё не подтвердил готовность.
        }

        public virtual bool CanPlayCard(Card card, Slot slot, bool skip_cost = true) // 📌 Проверяет, может ли карта быть разыграна на указанный слот.
        // 🔹 Аргументы:
        //   - `card`  — карта, которую пытаемся разыграть
        //   - `slot`  — слот, на который мы пытаемся сыграть карту
        //   - `skip_cost` (по умолчанию false) — если true, проверка маны не выполняется (например, для бесплатных карт).
        {
            // 🔹 Проверка: если карта не существует (null), нельзя её разыграть.
            if (card == null)
            {
                Debug.Log("card == null");
                return false;
            }

            // 🔹 Получаем игрока, которому принадлежит карта.
            Player player = GetPlayer(card.player_id);

            // 🔹 Проверяем, может ли игрок оплатить стоимость маны за карту.
            //    - Если `skip_cost == false` и у игрока недостаточно маны — нельзя разыграть карту.
            if (!skip_cost && !player.CanPayMana(card))
            {
                Debug.Log("🚫 Недостаточно маны.");
                return false; // 🚫 Недостаточно маны.
            }

            // 🔹 Проверяем, находится ли карта в руке игрока.
            //    - Если её нет в руке, значит, её нельзя сыграть.
            if (!player.HasCard(player.cards_hand, card))
            {
                Debug.Log(" 🚫 Карта не в руке.");
                return false; // 🚫 Карта не в руке.
            }

            // 🔹 Проверка для ИИ (искусственного интеллекта).
            //    - Если ИИ играет карту с динамической стоимостью (`X-cost`),
            //      но у него 0 маны, карта не может быть разыграна.
            //if (player.is_ai && card.CardData.IsDynamicManaCost() && player.mana == 0)
            //return false; // 🚫 ИИ не может сыграть карту с X-мана при 0 маны.

            // 📌 Если карта — это карта, размещаемая на игровом поле (существо, ловушка и т.д.).
            if (card.CardData.IsBoardCard())
            {
                // 🔹 Проверяем, что слот, на который мы пытаемся поставить карту, является допустимым.
                if (!slot.IsValid() || IsCardOnSlot(slot))
                {
                    if (IsCardOnSlot(slot))
                    {
                        //Debug.Log("🚫 Слот занят");
                    }
                    return false; // 🚫 Слот занят или невалиден.
                }

                // 🔹 Проверяем, чтобы карта не была сыграна на стороне противника.
                if (player.player_id != slot.p)
                {
                    //Debug.Log("🚫 Нельзя играть на стороне противника.");
                    return false; // 🚫 Нельзя играть на стороне противника.
                }
                return true; // ✅ Можно разыграть карту.
            }

            // 📌 Если карта — это экипировка (оружие, броня и т.д.).
            if (card.CardData.IsEquipment())
            {
                // 🔹 Проверяем, что слот корректный.
                if (!slot.IsValid())
                    return false; // 🚫 Невалидный слот.

                // 🔹 Проверяем, есть ли в этом слоте цель для экипировки.
                Card target = GetSlotCard(slot);

                // 🔹 Проверяем, что цель является персонажем (`Character`), 
                //    и что он принадлежит игроку.
                if (target == null || target.CardData.type != CardType.Character || target.player_id != card.player_id)
                    return false; // 🚫 Экипировка должна применяться только на союзных персонажей.

                return true; // ✅ Можно применить экипировку.
            }

            // 📌 Если карта — заклинание, требующее цели перед розыгрышем (например, целебное заклинание или урон по врагу).
            if (card.CardData.IsRequireTargetSpell())
            {
                return IsPlayTargetValid(card, slot); // 🔹 Проверяем, является ли указанный слот допустимой целью.
            }

            // 📌 Если карта — обычное заклинание.
            if (card.CardData.type == CardType.Spell)
            {
                return CanAnyPlayAbilityTrigger(card); // 🔹 Проверяем, есть ли у карты способности, которые активируются при розыгрыше.
            }

            // 🔹 Если ни одно из условий не подошло, значит, карту можно сыграть.
            return true;
        }

        public virtual bool CanMoveCard(Card card, Slot slot, bool skip_cost = false) // 📌 Проверяет, может ли карта переместиться на указанный слот.
        // 🔹 Аргументы:
        //   - `card`  — карта, которую пытаемся переместить
        //   - `slot`  — слот, на который мы пытаемся переместить карту
        //   - `skip_cost` (по умолчанию false) — если true, проверка стоимости передвижения не выполняется.
        {
            // 🔹 Проверяем, что карта существует (не null) и что указанный слот является допустимым.
            if (card == null || !slot.IsValid())
                return false; // 🚫 Если карта не существует или слот недопустимый, перемещение невозможно.

            // 🔹 Проверяем, находится ли карта на игровом поле (то есть, уже была разыграна).
            if (!IsOnBoard(card))
                return false; // 🚫 Перемещать можно только карты, которые уже находятся на поле.

            // 🔹 Проверяем, может ли эта карта вообще передвигаться (например, у неё нет ограничений на передвижение).
            if (!card.CanMove(skip_cost))
                return false; // 🚫 Если карта не может двигаться (например, из-за особых эффектов), перемещение невозможно.

            // 🔹 Проверяем, чтобы карта не пыталась переместиться на сторону противника.
            //if (Slot.GetP(card.player_id) != slot.p)
            //return false; // 🚫 Карта не может перемещаться на сторону соперника.

            // 🔹 Проверяем, чтобы карта не пыталась переместиться в тот же самый слот.
            if (card.slot == slot)
                return false; // 🚫 Нельзя перемещать карту в ту же самую позицию.

            // 🔹 Проверяем, что в целевом слоте нет другой карты.
            Card slot_card = GetSlotCard(slot);
            if (slot_card != null)
                return false; // 🚫 Если слот уже занят другой картой, перемещение невозможно.

            // ✅ Если все проверки пройдены, перемещение возможно.
            return true;
        }

        public virtual bool CanAttackTarget(Card attacker, Player target, bool skip_cost = false) // 📌 Проверяет, может ли указанная карта атаковать игрока.
        // 🔹 Аргументы:
        //   - `attacker` — атакующая карта (должна находиться на поле).
        //   - `target` — цель атаки (игрок).
        //   - `skip_cost` (по умолчанию `false`) — если `true`, то стоимость атаки не учитывается.
        {
            // 🔹 Проверяем, что атакующая карта и цель существуют.
            if (attacker == null || target == null)
                return false; // 🚫 Если один из аргументов `null`, атака невозможна.

            // 🔹 Проверяем, может ли атакующая карта атаковать (не истощена, не под эффектом "сон" и т. д.).
            if (!attacker.CanAttack(skip_cost))
                return false; // 🚫 Если карта не может атаковать, атака невозможна.

            // 🔹 Проверяем, чтобы карта не пыталась атаковать своего владельца.
            if (attacker.player_id == target.player_id)
                return false; // 🚫 Нельзя атаковать себя.

            // 🔹 Проверяем, что атакующая карта находится на поле и является персонажем.
            if (!IsOnBoard(attacker) || !attacker.CardData.IsCharacter())
                return false; // 🚫 Карта должна быть на поле и быть персонажем, чтобы атаковать.

            // 🔹 Проверяем, не защищён ли игрок эффектом "Таунт" (защита), если у атакующего нет статуса "Летун".
            if (target.HasStatus(StatusType.Protected) && !attacker.HasStatus(StatusType.Flying))
                return false; // 🚫 Если у игрока есть "Защита" и атакующий не имеет "Летун", атака невозможна.

            // ✅ Если все условия выполнены, атака разрешена.
            return true;
        }

        public virtual bool CanAttackTarget(Card attacker, Card target, bool skip_cost = false) // 📌 Проверяет, может ли атакующая карта атаковать другую карту.
        // 🔹 Аргументы:
        //   - `attacker` — атакующая карта.
        //   - `target` — цель атаки (другая карта).
        //   - `skip_cost` (по умолчанию `false`) — если `true`, то стоимость атаки не учитывается.
        {
            // Если игра в дуэльном режиме, разрешаем атаку
            if (GameplayData.Get().duel)
                return true;
            // 🔹 Проверяем, что атакующая карта и цель существуют.
            if (attacker == null || target == null)
                return false; // 🚫 Если один из аргументов `null`, атака невозможна.

            // 🔹 Проверяем, может ли атакующая карта атаковать.
            if (!attacker.CanAttack(skip_cost))
                return false; // 🚫 Если карта не может атаковать, атака невозможна.

            // 🔹 Проверяем, чтобы карта не пыталась атаковать свою карту.
            if (attacker.player_id == target.player_id)
                return false; // 🚫 Нельзя атаковать свою карту.

            // 🔹 Проверяем, что обе карты находятся на игровом поле.
            if (!IsOnBoard(attacker) || !IsOnBoard(target))
                return false; // 🚫 Карты должны быть на поле.

            // 🔹 Проверяем, что атакующая карта является персонажем, а цель является картой на игровом поле.
            if (!attacker.CardData.IsCharacter() || !target.CardData.IsBoardCard())
                return false; // 🚫 Атаковать могут только персонажи, а цель должна быть картой на поле.

            // 🔹 Проверяем, не имеет ли цель эффект "Скрытность" (Stealth).
            if (target.HasStatus(StatusType.Stealth))
                return false; // 🚫 Карты со "Скрытностью" нельзя атаковать.

            // 🔹 Проверяем, не защищена ли цель эффектом "Таунт", если у атакующего нет статуса "Летун".
            if (target.HasStatus(StatusType.Protected) && !attacker.HasStatus(StatusType.Flying))
                return false; // 🚫 Если у цели есть "Защита" и атакующий не имеет "Летун", атака невозможна.

            // ✅ Если все условия выполнены, атака разрешена.
            return true;
        }

        public virtual bool CanCastAbility(Card card, AbilityData ability) // 📌 Проверяет, может ли карта активировать указанную способность.
        //Удовлетворяет ли способность условиям, можно ли её оплатить, и является ли она Activate.
        {
            // 🔹 Проверяем, что карта и способность существуют.
            if (ability == null || card == null || !card.CanDoActivatedAbilities())
                return false; // 🚫 Если что-то из этого `null` или карта не может активировать способности, возвращаем `false`.

            // 🔹 Способность должна быть активируемой (то есть триггер `Activate`).
            if (ability.trigger != AbilityTrigger.Activate)
                return false; // 🚫 Если триггер у способности не `Activate`, её нельзя активировать вручную.

            // 🔹 Получаем игрока, которому принадлежит карта.
            Player player = GetPlayer(card.player_id);

            // 🔹 Проверяем, может ли игрок оплатить стоимость способности.
            if (!player.CanPayAbility(card, ability))
                return false; // 🚫 Если игрок не может оплатить, способность нельзя использовать.

            // 🔹 Проверяем, выполняются ли условия для активации способности.
            if (!ability.AreTriggerConditionsMet(this, card))
                return false; // 🚫 Если условия не выполняются, способность не может быть активирована.

            // ✅ Если все проверки пройдены, способность можно использовать.
            return true;
        }

        public virtual bool CanSelectAbility(Card card, AbilityData ability) // 📌 Для способности, выбираемой через селектор: проверяет, может ли карта использовать способность.
        //Аналогично CanCastAbility, но для способностей, выбираемых через селектор.
        {
            // 🔹 Проверяем, что карта и способность существуют.
            if (ability == null || card == null || !card.CanDoAbilities())
                return false; // 🚫 Если карта не может использовать способности или аргументы `null`, возвращаем `false`.

            // 🔹 Получаем игрока, которому принадлежит карта.
            Player player = GetPlayer(card.player_id);

            // 🔹 Проверяем, может ли игрок оплатить стоимость способности.
            if (!player.CanPayAbility(card, ability))
                return false; // 🚫 Если у игрока недостаточно ресурсов, способность нельзя выбрать.

            // 🔹 Проверяем, выполняются ли условия для срабатывания способности.
            if (!ability.AreTriggerConditionsMet(this, card))
                return false; // 🚫 Если условия не выполняются, способность нельзя выбрать.

            // ✅ Если все проверки пройдены, способность можно выбрать в селекторе.
            return true;
        }

        public virtual bool CanAnyPlayAbilityTrigger(Card card) // 📌 Проверяет, будет ли хоть одна способность активирована при розыгрыше карты.
        //Имеет ли карта способность с OnPlay, которая может сработать.
        {
            // 🔹 Проверяем, что карта существует.
            if (card == null)
                return false; // 🚫 Если карты нет, она не может активировать способности.

            // 🔹 Если карта имеет динамическую стоимость маны (`X`-мана), считаем, что способность потенциально может сработать.
            if (card.CardData.IsDynamicManaCost())
                return true; // ✅ Даже если на момент проверки условия не выполняются, стоимость может измениться.

            // 🔹 Проверяем все способности карты.
            foreach (AbilityData ability in card.GetAbilities())
            {
                // 🔹 Если способность срабатывает при розыгрыше (`OnPlay`) и её условия выполняются, возвращаем `true`.
                if (ability.trigger == AbilityTrigger.OnPlay && ability.AreTriggerConditionsMet(this, card))
                    return true;
            }

            // 🚫 Если ни одна способность не была найдена, возвращаем `false`.
            return false;
        }

        public virtual bool IsPlayTargetValid(Card caster, Player target) // 📌 Проверяет, является ли игрок допустимой целью для заклинания, если оно требует выбора цели.
        {
            // 🔹 Проверяем, что заклинатель (`caster`) и цель (`target`) существуют.
            if (caster == null || target == null)
                return false; // 🚫 Если один из аргументов `null`, цель недопустима.

            // 🔹 Перебираем все способности заклинателя.
            foreach (AbilityData ability in caster.GetAbilities())
            {
                // 🔹 Проверяем, есть ли у заклинателя способность, срабатывающая при розыгрыше (`OnPlay`),
                //    и требующая выбора цели (`PlayTarget`).
                if (ability && ability.trigger == AbilityTrigger.OnPlay && ability.target == AbilityTarget.PlayTarget)
                {
                    // 🔹 Проверяем, можно ли выбрать игрока (`target`) в качестве цели этой способности.
                    if (!ability.CanTarget(this, caster, target))
                        return false; // 🚫 Если способность не позволяет выбрать цель, возвращаем `false`.
                }
            }

            // ✅ Если все проверки пройдены, игрок является допустимой целью.
            return true;
        }

        public virtual bool IsPlayTargetValid(Card caster, Card target) // 📌 Проверяет, является ли другая карта допустимой целью для заклинания.
        {
            // 🔹 Проверяем, что заклинатель (`caster`) и цель (`target`) существуют.
            if (caster == null || target == null)
                return false; // 🚫 Если один из аргументов `null`, цель недопустима.

            // 🔹 Перебираем все способности заклинателя.
            foreach (AbilityData ability in caster.GetAbilities())
            {
                // 🔹 Проверяем, есть ли у заклинателя способность, срабатывающая при розыгрыше (`OnPlay`),
                //    и требующая выбора цели (`PlayTarget`).
                if (ability && ability.trigger == AbilityTrigger.OnPlay && ability.target == AbilityTarget.PlayTarget)
                {
                    // 🔹 Проверяем, можно ли выбрать карту (`target`) в качестве цели этой способности.
                    if (!ability.CanTarget(this, caster, target))
                        return false; // 🚫 Если способность не позволяет выбрать цель, возвращаем `false`.
                }
            }

            // ✅ Если все проверки пройдены, карта является допустимой целью.
            return true;
        }

        public virtual bool IsPlayTargetValid(Card caster, Slot target) // 📌 Проверяет, является ли слот (`Slot`) допустимой целью для заклинания.
        {
            // 🔹 Проверяем, что заклинатель (`caster`) существует.
            if (caster == null)
                return false; // 🚫 Если заклинателя нет, цель недопустима.

            // 🔹 Если слот указывает на игрока (например, `Slot(0,0)`), проверяем игрока как цель.
            if (target.IsPlayerSlot())
                return IsPlayTargetValid(caster, GetPlayer(target.p)); // 🏆 Проверяем игрока, если слот указывает на него.

            // 🔹 Проверяем, есть ли в слоте карта.
            Card slot_card = GetSlotCard(target);
            if (slot_card != null)
                return IsPlayTargetValid(caster, slot_card); // 🔄 Если в слоте есть карта, проверяем её как цель.

            // 🔹 Перебираем все способности заклинателя.
            foreach (AbilityData ability in caster.GetAbilities())
            {
                // 🔹 Проверяем, есть ли у заклинателя способность, срабатывающая при розыгрыше (`OnPlay`),
                //    и требующая выбора цели (`PlayTarget`).
                if (ability && ability.trigger == AbilityTrigger.OnPlay && ability.target == AbilityTarget.PlayTarget)
                {
                    // 🔹 Проверяем, можно ли выбрать слот (`target`) в качестве цели этой способности.
                    if (!ability.CanTarget(this, caster, target))
                        return false; // 🚫 Если способность не позволяет выбрать цель, возвращаем `false`.
                }
            }

            // ✅ Если все проверки пройдены, слот является допустимой целью.
            return true;
        }

        public Player GetPlayer(int id)   // 📌 Возвращает игрока по его идентификатору `id`.
        {
            // 🔹 Проверяем, находится ли `id` в допустимом диапазоне.
            if (id >= 0 && id < players.Length)
                return players[id]; // ✅ Если `id` корректен, возвращаем игрока.

            return null; // 🚫 Если `id` недопустимый, возвращаем `null`.
        }

        public Player GetActivePlayer() // 📌 Возвращает активного (текущего) игрока, чей ход. Текущий игрок определяется в GameLogic в методе StartGame
        {
            return GetPlayer(current_player); // 🔄 Используем `GetPlayer`, передавая `current_player`.
        }

        public Player GetOpponentPlayer(int id) // 📌 Возвращает оппонента игрока с `id`.
        {
            int oid = id == 0 ? 1 : 0; // 🔄 Определяем `oid`: если `id == 0`, противник — 1, иначе 0.
            return GetPlayer(oid); // 🔹 Возвращаем игрока с `oid`.
        }

        #region Get Card // Поиск карты по её уникальному идентификатору card_uid: пройтись по всем игрокам, попытаться найти карту в соответствующей коллекции

        public Card GetCard(string card_uid) // 📌 Ищет карту по `card_uid` среди всех карт игроков.
        {
            foreach (Player player in players) // 🔄 Проходим по всем игрокам
            {
                Card acard = player.GetCard(card_uid); // 🔍 Ищем карту в картах игрока
                if (acard != null)
                    return acard; // ✅ Если карта найдена, возвращаем её.
            }
            return null; // 🚫 Карта не найдена, возвращаем `null`.
        }

        public Card GetBoardCard(string card_uid) // 📌 Ищет карту по `card_uid` среди карт на игровом поле.
        //🔹 Отличие от GetCard — ищет только среди карт, которые уже находятся на игровом поле.
        {
            foreach (Player player in players) // 🔄 Проходим по всем игрокам
            {
                foreach (Card card in player.cards_board) // 🔍 Проверяем карты на доске
                {
                    if (card != null && card.uid == card_uid)
                        return card; // ✅ Если найдена карта с нужным `uid`, возвращаем её.
                }
            }
            return null; // 🚫 Карта не найдена.
        }

        public Card GetEquipCard(string card_uid) // 📌 Ищет экипированную карту по `card_uid`.
        //🔹 Отличие от GetBoardCard — ищет только среди экипированных карт (например, оружие).
        {
            foreach (Player player in players) // 🔄 Проходим по всем игрокам
            {
                foreach (Card card in player.cards_equip) // 🔍 Проверяем экипированные карты
                {
                    if (card != null && card.uid == card_uid)
                        return card; // ✅ Если найдена, возвращаем.
                }
            }
            return null; // 🚫 Карта не найдена.
        }

        public Card GetHandCard(string card_uid) // 📌 Ищет карту по `card_uid` среди карт в руке.
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_hand) // 🔍 Проверяем карты в руке
                {
                    if (card != null && card.uid == card_uid)
                        return card; // ✅ Найдена карта, возвращаем.
                }
            }
            return null; // 🚫 Не найдена.
        }

        public Card GetDeckCard(string card_uid) // 📌 Ищет карту по `card_uid` среди карт в колоде.
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_deck) // 🔍 Проверяем карты в колоде
                {
                    if (card != null && card.uid == card_uid)
                        return card; // ✅ Если найдена, возвращаем.
                }
            }
            return null; // 🚫 Не найдена.
        }

        public Card GetDiscardCard(string card_uid) // 📌 Ищет карту по `card_uid` среди карт в сбросе.
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_discard) // 🔍 Проверяем карты в сбросе
                {
                    if (card != null && card.uid == card_uid)
                        return card; // ✅ Найдена карта, возвращаем.
                }
            }
            return null; // 🚫 Не найдена.
        }

        public Card GetSecretCard(string card_uid) // 📌 Ищет карту по `card_uid` среди секретных карт игрока.
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_secret) // 🔍 Проверяем карты-секреты
                {
                    if (card != null && card.uid == card_uid)
                        return card; // ✅ Найдена карта, возвращаем.
                }
            }
            return null; // 🚫 Не найдена.
        }

        public Card GetTempCard(string card_uid) // 📌 Ищет временную карту (например, созданную эффектом) по `card_uid`.
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_temp) // 🔍 Проверяем временные карты
                {
                    if (card != null && card.uid == card_uid)
                        return card; // ✅ Найдена карта, возвращаем.
                }
            }
            return null; // 🚫 Не найдена.
        }

        public Card GetSlotCard(Slot slot) // 📌 Ищет карту, находящуюся в указанном слоте.
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_board) // 🔍 Проверяем карты на доске
                {
                    if (card != null && card.slot == slot)
                        return card; // ✅ Найдена карта, возвращаем.
                }
            }
            return null; // 🚫 Не найдена.
        }

        #endregion

        #region GetRandom // Случайные выборы в игре: случайный игрок, случайная карта, случайный слот
        public virtual Player GetRandomPlayer(System.Random rand) // 📌 Выбирает случайного игрока в игре.
        {
            // 🔹 Генерируем случайное число от 0 до 1 (`NextDouble()`).
            // 🔹 Если число меньше 0.5, выбираем игрока `1`, иначе `0`.
            Player player = GetPlayer(rand.NextDouble() < 0.5 ? 1 : 0);

            return player; // ✅ Возвращаем выбранного игрока.
        }

        public virtual Card GetRandomBoardCard(System.Random rand) // 📌 Выбирает случайную карту среди тех, что находятся на игровом поле.
        {
            // 🔹 Сначала выбираем случайного игрока.
            Player player = GetRandomPlayer(rand);

            // 🔹 Выбираем случайную карту из карт, находящихся на игровом поле (`cards_board`).
            return player.GetRandomCard(player.cards_board, rand);
        }

        public virtual Slot GetRandomSlot(System.Random rand) // 📌 Выбирает случайный слот (место на поле) для размещения карты.

        {
            // 🔹 Сначала выбираем случайного игрока.
            Player player = GetRandomPlayer(rand);

            // 🔹 Выбираем случайный слот из доступных у этого игрока.
            return player.GetRandomSlot(rand);
        }

        #endregion

        #region Where is the Card // Флаги, где находится карта: в руке, на поле, в колоде и т.д.

        public bool IsInHand(Card card) // 📌 Проверяет, находится ли указанная карта в руке игрока.
        {
            // 🔹 Если карта `null`, сразу возвращаем `false`.
            // 🔹 Если карта найдена в руке (`GetHandCard(card.uid) != null`), возвращаем `true`.
            return card != null && GetHandCard(card.uid) != null;
        }

        public bool IsOnBoard(Card card) // 📌 Проверяет, находится ли указанная карта на игровом поле.
        {
            // 🔹 Если карта `null`, сразу возвращаем `false`.
            // 🔹 Если карта найдена на игровом поле (`GetBoardCard(card.uid) != null`), возвращаем `true`.
            return card != null && GetBoardCard(card.uid) != null;
        }

        public bool IsEquipped(Card card) // 📌 Проверяет, является ли указанная карта экипировкой (оружие, броня и т.д.).
        {
            // 🔹 Если карта `null`, сразу возвращаем `false`.
            // 🔹 Если карта найдена в списке экипировки (`GetEquipCard(card.uid) != null`), возвращаем `true`.
            return card != null && GetEquipCard(card.uid) != null;
        }

        public bool IsInDeck(Card card) // 📌 Проверяет, находится ли указанная карта в колоде (deck).
        {
            // 🔹 Если карта `null`, сразу возвращаем `false`.
            // 🔹 Если карта найдена в колоде (`GetDeckCard(card.uid) != null`), возвращаем `true`.
            return card != null && GetDeckCard(card.uid) != null;
        }

        public bool IsInDiscard(Card card) // 📌 Проверяет, находится ли указанная карта в сбросе (discard pile).
        {
            // 🔹 Если карта `null`, сразу возвращаем `false`.
            // 🔹 Если карта найдена в сбросе (`GetDiscardCard(card.uid) != null`), возвращаем `true`.
            return card != null && GetDiscardCard(card.uid) != null;
        }

        public bool IsInSecret(Card card) // 📌 Проверяет, является ли карта "секретом" (специальной скрытой картой, которая активируется при определённых условиях).
        {
            // 🔹 Если карта `null`, сразу возвращаем `false`.
            // 🔹 Если карта найдена среди секретов (`GetSecretCard(card.uid) != null`), возвращаем `true`.
            return card != null && GetSecretCard(card.uid) != null;
        }

        public bool IsInTemp(Card card) // 📌 Проверяет, является ли карта временной (например, созданной магией).
        {
            // 🔹 Если карта `null`, сразу возвращаем `false`.
            // 🔹 Если карта найдена в списке временных карт (`GetTempCard(card.uid) != null`), возвращаем `true`.
            return card != null && GetTempCard(card.uid) != null;
        }

        public bool IsCardOnSlot(Slot slot) // 📌 Проверяет, занято ли указанное место (слот) на игровом поле.
        {
            // 🔹 Если слот содержит карту (`GetSlotCard(slot) != null`), возвращаем `true`, иначе `false`.
            return GetSlotCard(slot) != null;
        }

        #endregion

        public bool HasStarted() // 📌 Проверяет, началась ли игра (т.е. вышла ли она из состояния "Connecting").
        {
            // 🔹 Если состояние игры НЕ `GameState.Connecting`, значит, игра началась.
            return state != GameState.Connecting;
        }

        public bool HasEnded() // 📌 Проверяет, закончилась ли игра (т.е. находится ли она в состоянии "GameEnded").
        {
            // 🔹 Если состояние игры `GameState.GameEnded`, значит, игра завершена.
            return state == GameState.GameEnded;
        }

        #region Clones // Клонирование игры

        public static Game CloneNew(Game source) // 📌 Создает новый объект игры, копируя все данные из существующей игры (source).
        // 🔹 Работает медленнее, так как создает новый экземпляр Game перед клонированием.
        {
            Game game = new Game();  // 🆕 Создаем новый пустой объект игры.
            Clone(source, game);     // 🔄 Копируем все данные из `source` в `game`.
            return game;             // 🔙 Возвращаем новый клон игры.
        }

        public static void Clone(Game source, Game dest) // 📌 Копирует все переменные из `source` в `dest`.
        // 🔹 Используется, например, для создания предсказаний в AI (дублирует состояние игры)
        {
            // 🏷️ Копируем основные параметры игры
            dest.game_uid = source.game_uid;    // Уникальный идентификатор игры
            dest.settings = source.settings;    // Настройки игры

            // 🔄 Копируем текущий статус игры
            dest.first_player = source.first_player;
            dest.current_player = source.current_player;
            dest.turn_count = source.turn_count;
            //dest.turn_timer = source.turn_timer;
            dest.state = source.state;
            dest.phase = source.phase;

            // 🔹 Проверяем, есть ли уже массив `players` в `dest`
            if (dest.players == null)
            {
                // 🆕 Если `players` нет, создаем массив игроков (копируем их количество)
                dest.players = new Player[source.players.Length];

                // 🔄 Заполняем массив новыми экземплярами игроков
                for (int i = 0; i < source.players.Length; i++)
                    dest.players[i] = new Player(i);
            }

            // 🔄 Копируем данные всех игроков из `source` в `dest`
            for (int i = 0; i < source.players.Length; i++)
                Player.Clone(source.players[i], dest.players[i]); // Вызывает `Clone` у Player

            // 🎯 Копируем данные, связанные с выбором целей и способностей
            dest.selector = source.selector;
            dest.selector_player_id = source.selector_player_id;
            dest.selector_caster_uid = source.selector_caster_uid;
            dest.selector_ability_id = source.selector_ability_id;

            // 📌 Копируем последние игровые события
            dest.last_destroyed = source.last_destroyed;
            dest.last_played = source.last_played;
            dest.last_target = source.last_target;
            dest.last_summoned = source.last_summoned;
            dest.ability_triggerer = source.ability_triggerer;
            dest.rolled_value = source.rolled_value;
            dest.selected_value = source.selected_value;

            // 🔄 Клонируем множества (HashSet) атакованных карт и использованных способностей
            CloneHash(source.ability_played, dest.ability_played);
            CloneHash(source.cards_attacked, dest.cards_attacked);
        }

        public static void CloneHash(HashSet<string> source, HashSet<string> dest) // 📌 Копирует содержимое одного множества `HashSet` в другое.
        {
            dest.Clear(); // 🗑️ Очищаем текущее множество перед копированием

            // 🔄 Добавляем все элементы из `source` в `dest`
            foreach (string str in source)
                dest.Add(str);
        }

        #endregion
    }

    [System.Serializable] // ✅ Позволяет сериализовать объект (например, для сохранения или передачи по сети). Unity позволяет отображать такие enum в инспекторе.
    public enum GameState
    {
        Connecting = 0, // ⏳ Игра еще не началась, игроки подключаются.
        Play = 20,      // 🎮 Игра идет, игроки выполняют свои ходы.
        GameEnded = 99, // 🏁 Игра завершена, определен победитель.
    }

    [System.Serializable] // ✅ Позволяет отображать в Unity-инспекторе.
    public enum GamePhase //Используется для разделения логики и контроля порядка действий в игре.
    {
        None = 0,         // 🔹 Нет активной фазы (например, перед началом игры).
        Mulligan = 5,     // 🎴 Фаза выбора карт (игроки могут менять стартовые карты).
        StartTurn = 10,   // 🕛 Начало хода (выполняются эффекты начала хода).
        Main = 20,        // 🎮 Основная фаза (игроки разыгрывают карты, атакуют).
        EndTurn = 30,     // 🕙 Завершение хода (применяются эффекты конца хода).
    }

    [System.Serializable] // ✅ Дает возможность сериализовать и отображать в Unity-инспекторе.
    public enum SelectorType // 🚀 Используется для организации интерфейса и взаимодействия с игроком.
    {
        None = 0,           // 🔹 Нет выбора (обычное состояние игры).
        SelectTarget = 10,  // 🎯 Игрок должен выбрать цель (например, для атаки или заклинания).
        SelectorCard = 20,  // 🃏 Игрок должен выбрать карту (например, для сброса или перемещения).
        SelectorChoice = 30,// 🏆 Игрок должен выбрать один из нескольких вариантов.
        SelectorCost = 40,  // 💰 Игрок должен выбрать, сколько маны потратить.
    }

}