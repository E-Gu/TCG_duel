using System;
using System.Collections.Generic;
using System.Linq;
using TcgEngine.Client;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Profiling;

namespace TcgEngine.Gameplay
{
    /// <summary>
    /// Executes and resolves game rules and logic
    /// </summary>
    public class GameLogic
    {
        #region Event variables
        // Game lifecycle events
        public UnityAction onGameStart;
        public UnityAction<Player> onGameEnd;

        // Turn-based events
        public UnityAction onTurnStart;
        public UnityAction onTurnPlay;
        public UnityAction onTurnEnd;

        // Card actions
        public UnityAction<Card, Slot> onCardPlayed;
        public UnityAction<Card, Slot> onCardSummoned;
        public UnityAction<Card, Slot> onCardMoved;
        public UnityAction<Card> onCardTransformed;
        public UnityAction<Card> onCardDiscarded;
        public UnityAction<int> onCardDrawn;

        // Dice roll event
        public UnityAction<int> onRollValue;

        // Ability events
        public UnityAction<AbilityData, Card> onAbilityStart;
        public UnityAction<AbilityData, Card, Card> onAbilityTargetCard;
        public UnityAction<AbilityData, Card, Player> onAbilityTargetPlayer;
        public UnityAction<AbilityData, Card, Slot> onAbilityTargetSlot;
        public UnityAction<AbilityData, Card> onAbilityEnd;

        // Combat events
        public UnityAction<Card, Card> onAttackStart;
        public UnityAction<Card, Card> onAttackEnd;
        public UnityAction<Card, Player> onAttackPlayerStart;
        public UnityAction<Card, Player> onAttackPlayerEnd;

        // Damage and healing events
        public UnityAction<Card, int> onCardDamaged;
        public UnityAction<Card, int> onCardHealed;
        public UnityAction<Player, int> onPlayerDamaged;
        public UnityAction<Player, int> onPlayerHealed;

        // Secret events
        public UnityAction<Card, Card> onSecretTrigger;
        public UnityAction<Card, Card> onSecretResolve;

        // Refresh event
        public UnityAction onRefresh;
        #endregion

        #region Fields variables
        private Game game_data;
        private ResolveQueue resolve_queue;
        private bool is_ai_predict = false;
        private System.Random random = new System.Random();

        private ListSwap<Card> card_array = new ListSwap<Card>();
        private ListSwap<Player> player_array = new ListSwap<Player>();
        private ListSwap<Slot> slot_array = new ListSwap<Slot>();
        private ListSwap<CardData> card_data_array = new ListSwap<CardData>();
        private List<Card> cards_to_clear = new List<Card>();
        #endregion

        #region Constructors
        //Конструктор — это специальный метод, который вызывается при создании объекта класса. 
        //В данном случае, класс GameLogic управляет игровой логикой (правилами игры, атаками, ходами и т. д.). 
        //У него есть два конструктора:
        public GameLogic(bool is_ai)
        //Используется, когда мы создаем логику только для ИИ, например, чтобы заранее "подумать" о возможных ходах.
        //Создает объект класса GameLogic, если не переданы конкретные данные о самой игре.
        //Принимает один аргумент is_ai, который говорит, создаем ли мы эту логику для ИИ (искусственного интеллекта) или для обычного игрока.
        //Создает объект resolve_queue (очередь обработки игровых событий).
        //Сохраняет флаг is_ai_predict, чтобы в будущем код знал, используется ли эта игровая логика для предсказаний хода ИИ.
        {
            resolve_queue = new ResolveQueue(null, is_ai);
            //resolve_queue — это объект, который отвечает за обработку и выполнение игровых действий (например, атаки, заклинания, передвижения).
            //Мы создаем новый объект ResolveQueue, передавая в него два параметра:
            //null означает, что пока нет данных о самой игре (game_data).
            //is_ai — передаем значение true или false, чтобы определить, управляется ли игра ИИ.
            is_ai_predict = is_ai;
            //Сохраняем is_ai в переменную is_ai_predict.
            //Если is_ai == true, значит, этот объект будет использоваться для прогнозирования действий ИИ.
            //Если is_ai == false, значит, это обычная игровая логика для людей.
        }

        // Конструктор `GameLogic`, который принимает объект `Game` (текущие данные игры)
        public GameLogic(Game game)
        //Используется, когда у нас уже есть объект Game (например, загруженная игра).
        //Привязывается к конкретной игре (game_data = game).
        //Создает объект ResolveQueue, который будет обрабатывать игровые события.
        {
            //Debug.Log("GameLogic(Game game) start");
            // Сохраняем переданный объект `game` в поле `game_data`.
            // Это основная структура, содержащая всю информацию о текущем состоянии игры.
            game_data = game;

            // Создаем новый объект `ResolveQueue`, связанный с переданной игрой (`game`).
            // Второй параметр `false` означает, что этот объект НЕ предназначен для предсказания действий AI.
            resolve_queue = new ResolveQueue(game, false);
            //Debug.Log("GameLogic(Game game) finished: resolve_queue is " + (resolve_queue != null ? "initialized" : "null"));
        }
        #endregion

        #region Lifecycle // 🔹 Секция кода, связанная с жизненным циклом логики игры

        // Метод `SetData` используется для установки данных игры (`game_data`).
        // Он вызывается, когда нужно привязать объект `GameLogic` к конкретной игре.
        public virtual void SetData(Game game)
        {
            // Сохраняем переданный объект `Game` в поле `game_data`
            // Теперь `game_data` содержит всю информацию о текущем состоянии игры.
            game_data = game;

            // Устанавливаем `game` для объекта `resolve_queue` (очередь обработки игровых событий).
            // Это необходимо, чтобы `resolve_queue` знала, с какой игрой она работает.
            resolve_queue.SetData(game);
        }

        // Метод `Update` вызывается каждый кадр и обновляет игровую логику.
        // `delta` — это время, прошедшее с последнего кадра (например, 0.016 секунды при 60 FPS).
        public virtual void Update(float delta)
        {
            // Обновляем очередь событий `resolve_queue`, передавая ей `delta`.
            // Это позволяет обрабатывать игровые действия постепенно (например, анимации атак, розыгрыш карт).
            resolve_queue.Update(delta);
        }

        #endregion // 🔹 Конец секции "Lifecycle"

        #region Turn phases // 🔹 Секция кода, связанная со стартом, окончанием и переходом между фазами игры

        public virtual void StartGame() //📌 Этот метод запускает игру:

        //Проверяет, не была ли игра уже завершена.
        //Определяет первого игрока (случайно или по настройкам уровня).
        //Раздает стартовые карты и определяет начальное количество маны и здоровья.
        //Запускает фазу выбора карт (если включено правило "муллигана").
        //Если муллиган не нужен, начинает первый ход.
        {
            // Проверяем, была ли игра уже завершена.
            // Если состояние игры `GameEnded`, выходим из метода.

            if (game_data.state == GameState.GameEnded)
                return;

            // Устанавливаем состояние игры в `Play`, что означает начало игры.
            game_data.state = GameState.Play;

            // Определяем, кто будет ходить первым: случайным образом выбираем 0 (игрок) или 1 (противник).
            game_data.first_player = random.NextDouble() < 0.5 ? 0 : 1;

            // Устанавливаем текущего игрока как первого игрока.
            game_data.current_player = game_data.first_player;

            // Счетчик ходов устанавливается в 1, так как это первый ход.
            game_data.turn_count = 1;

            // --- Определяем настройки уровня (например, если это режим приключения) ---
            // Проверяем, нужно ли игрокам делать "муллиган" (замена стартовых карт перед игрой).
            bool should_mulligan = GameplayData.Get().mulligan;

            // Получаем текущий уровень (если игра идет в режиме кампании).
            LevelData level = game_data.settings.GetLevel();

            if (level != null) // Если уровень существует, применяем его настройки.
            {
                // Проверяем, кто должен ходить первым согласно настройкам уровня.
                if (level.first_player == LevelFirst.Player)
                    game_data.first_player = 0; // Игрок ходит первым.

                if (level.first_player == LevelFirst.AI)
                    game_data.first_player = 1; // Искусственный интеллект (бот) ходит первым.

                // Обновляем текущего игрока в соответствии с выбранным первым игроком.
                game_data.current_player = game_data.first_player;

                // Используем настройку "муллиган" из уровня (если в нем прописано, что муллиган должен быть).
                should_mulligan = level.mulligan;
            }

            // --- Начальная настройка игроков ---
            // Проходим по списку всех игроков и задаем им начальные параметры.
            foreach (Player player in game_data.players)
            {
                // Получаем стартовую колоду игрока (если это специальная головоломка/задание).
                DeckPuzzleData pdeck = DeckPuzzleData.Get(player.deck);

                // Устанавливаем максимальное здоровье игрока.
                // Если у него есть особая стартовая колода (pdeck), берем здоровье оттуда.
                // В противном случае берем стандартное значение из `GameplayData`.
                player.hp_max = pdeck != null ? pdeck.start_hp : GameplayData.Get().hp_start;
                player.hp = player.hp_max; // Устанавливаем текущее здоровье равным максимальному.

                // Устанавливаем максимальный запас маны по такому же принципу (из pdeck или стандартное значение).
                player.mana_max = pdeck != null ? pdeck.start_mana : GameplayData.Get().mana_start;
                player.mana = player.mana_max; // Устанавливаем текущую ману.

                // Определяем, сколько карт игрок должен получить в начале игры.
                int dcards = pdeck != null ? pdeck.start_cards : GameplayData.Get().cards_start;

                // Раздаем игроку его стартовые карты.
                DrawCard(player, dcards);

                // --- Бонус второму игроку ---
                // Если порядок хода случайный (игра не установила фиксированный порядок),
                // и если данный игрок **не** ходит первым, то он получает бонусную карту.
                bool is_random = level == null || level.first_player == LevelFirst.Random;

                if (is_random && player.player_id != game_data.first_player && GameplayData.Get().second_bonus != null)
                {
                    // Создаем бонусную карту (например, дополнительную ману).
                    Card card = Card.Create(GameplayData.Get().second_bonus, VariantData.GetDefault(), player);

                    // Добавляем эту карту в руку игрока.
                    player.cards_hand.Add(card);
                }
            }

            // --- Завершаем подготовку игры ---
            RefreshData(); // Обновляем данные игры, чтобы изменения отобразились у всех игроков.

            // Вызываем событие `onGameStart`, уведомляя все подписанные элементы (например, UI или эффекты).
            onGameStart?.Invoke();

            // Если включен "муллиган" (игроки могут заменить карты перед игрой), запускаем эту фазу.
            if (should_mulligan)
                GoToMulligan();
            else
                StartTurn(); // Иначе сразу начинаем первый ход.
        }

        public virtual void StartTurn() //📌 Этот метод запускает новый ход:

        //Очищает временные данные (например, какие карты атаковали).
        //Дает игроку новую карту из колоды.
        //Добавляет ману (с учетом ограничений).
        //Применяет эффекты состояний (например, яд уменьшает здоровье).
        //Обновляет карты на поле (убирает “сон”, активирует способности).
        //Проверяет начальные способности карт.
        // 📌 Проверяем, не завершена ли игра.
        // Если игра окончена, то этот метод не должен выполняться.
        {
            Player activePlayer = game_data.GetActivePlayer();
            if (activePlayer.player_id == GameClient.Get().GetPlayerID())
            {
                Debug.Log("Ваш ход!");
            }
            else
            {
                Debug.Log("Ход противника (" + GameClient.Get().GetPlayerName(activePlayer.player_id) + ")");
            }
            if (game_data.state == GameState.GameEnded)
                return;

            // 📌 Очищаем временные данные прошлого хода (например, атакованные карты и прочие переменные).
            ClearTurnData();

            // 📌 Устанавливаем фазу "Начало хода".
            game_data.phase = GamePhase.StartTurn;

            // 📌 Обновляем данные игры (чтобы UI и другие системы получили актуальную информацию).
            RefreshData();

            // 📌 Вызываем событие "Начало хода", чтобы другие системы могли отреагировать (например, анимации, звук и т. д.).
            onTurnStart?.Invoke();

            // 📌 Получаем текущего игрока, чей ход начался.
            Player player = game_data.GetActivePlayer();
            //Debug.Log("Атакует игрок " + player);

            // --- Раздача карт ---
            // 📌 Первый игрок в первый ход НЕ получает карту, но со второго хода раздаются карты каждому.
            if (game_data.turn_count > 1 || player.player_id != game_data.first_player)
            {
                // 📌 Даем игроку 1 карту из его колоды.
                DrawCard(player, GameplayData.Get().cards_per_turn);
            }

            // --- Обновление маны ---
            // 📌 Увеличиваем максимальный запас маны на единицу (по стандартным правилам игры).
            player.mana_max += GameplayData.Get().mana_per_turn;

            // 📌 Но нельзя превысить максимальный лимит маны (например, 10).
            player.mana_max = Mathf.Min(player.mana_max, GameplayData.Get().mana_max);

            // 📌 Восстанавливаем всю ману игроку (теперь у него полный запас маны для этого хода).
            player.mana = player.mana_max;

            // --- Обновление таймера хода ---
            // 📌 Устанавливаем таймер для хода игрока (сколько времени у него есть на ход).
            //game_data.turn_timer = GameplayData.Get().turn_duration;

            // 📌 Очищаем историю действий игрока, чтобы записывать только текущий ход.
            player.history_list.Clear();

            // --- Применение негативных эффектов (например, яд) ---
            // 📌 Если у игрока есть статус "Отравление", он теряет здоровье.
            if (player.HasStatus(StatusType.Poisoned))
                player.hp -= player.GetStatusValue(StatusType.Poisoned);

            // 📌 Если у игрока есть герой (особая карта), обновляем его состояние.
            if (player.hero != null)
                player.hero.Refresh();

            // --- Обновление карт на поле (их состояний и эффектов) ---
            // 📌 Проходим по всем картам на игровом поле.
            for (int i = player.cards_board.Count - 1; i >= 0; i--)
            {
                Card card = player.cards_board[i];

                // 📌 Если карта НЕ находится в статусе "Сон" (то есть может действовать), обновляем её состояние.
                if (!card.HasStatus(StatusType.Sleep))
                    card.Refresh();

                // 📌 Если карта отравлена, она теряет здоровье.
                if (card.HasStatus(StatusType.Poisoned))
                    DamageCard(card, card.GetStatusValue(StatusType.Poisoned));
            }

            // --- Применение постоянных эффектов (например, ауры) ---
            // 📌 Обновляем способности, которые действуют постоянно (например, бонусы атаки или защиты).
            UpdateOngoing();

            // --- Активация способностей карт, которые срабатывают в начале хода ---
            // 📌 Запускаем способности карт, у которых есть эффект "При начале хода".
            TriggerPlayerCardsAbilityType(player, AbilityTrigger.StartOfTurn);

            // 📌 Запускаем скрытые карты (секреты), которые могут сработать в начале хода.
            TriggerPlayerSecrets(player, AbilityTrigger.StartOfTurn);

            // --- Завершение фазы начала хода ---
            // 📌 Добавляем в очередь вызов метода `StartMainPhase`, чтобы перейти в основную фазу хода.
            resolve_queue.AddCallback(StartMainPhase);

            // 📌 Запускаем обработку всех событий с небольшой задержкой (0.2 секунды), чтобы анимации успели отобразиться.
            resolve_queue.ResolveAll(0.2f);
        }

        public virtual void StartNextTurn() //📌 Этот метод переключает ход к следующему игроку, проверяет победителя и начинает новый ход.

        //Если игра уже закончена, ничего не делает.
        //Передает ход следующему игроку (циклично).
        //Если ход вернулся к первому игроку, увеличивает номер раунда.
        //Проверяет, победил ли кто-то.
        //Запускает новый ход.
        {
            Debug.Log("StartNextTurn");
            // 📌 Проверяем, не завершена ли игра.
            // Если игра уже закончена, этот метод не должен выполняться.
            if (game_data.state == GameState.GameEnded)
                return; // Выход из метода, если игра окончена.

            // 📌 Переход хода к следующему игроку.
            // game_data.current_player — это ID текущего игрока (например, 0 или 1, если игра 1 на 1).
            // Увеличиваем значение на 1, но используем % (остаток от деления), 
            // чтобы после последнего игрока снова начать с первого.
            // Например, если 2 игрока: (0 + 1) % 2 = 1 (второй игрок), (1 + 1) % 2 = 0 (первый игрок).
            game_data.current_player = (game_data.current_player + 1) % game_data.settings.nb_players;

            // 📌 Проверяем, прошел ли полный круг ходов.
            // Если текущий игрок снова стал первым, значит, все игроки сделали по ходу — увеличиваем счетчик раундов.
            if (game_data.current_player == game_data.first_player)
                game_data.turn_count++; // Количество раундов растет.

            // 📌 Проверяем, не победил ли кто-то после предыдущего хода.
            // Если победитель найден, игра завершится.
            CheckForWinner();

            // 📌 Запускаем новый ход для текущего игрока.
            // Этот метод подготовит нового игрока к ходу, выдаст карту, обновит ману и проверит эффекты.
            StartTurn();
        }

        public virtual void StartMainPhase() // 📌 Этот метод переключает игру в основную фазу хода.
        {
            // 📌 Проверяем, не завершена ли игра.
            // Если игра уже окончена (например, один из игроков победил),
            // то просто выходим из метода и ничего не делаем.
            if (game_data.state == GameState.GameEnded)
                return; // 🚫 Выход, если игра закончена.

            // 📌 Переключаем игру в основную фазу.
            // Основная фаза — это момент, когда игрок может:
            // - разыгрывать карты из руки,
            // - атаковать противника,
            // - использовать способности своих карт.
            game_data.phase = GamePhase.Main;

            // 📌 Сообщаем всем остальным системам, что началась основная фаза.
            // Это важно для UI (интерфейса), анимаций и других игровых механик.
            // Например, UI (интерфейс) может включить кнопки, а анимации — запуститься.
            onTurnPlay?.Invoke(); // 💡 Если на событие кто-то подписан, оно будет вызвано.

            // 📌 Обновляем игровое состояние.
            // Это нужно, чтобы интерфейс игры (UI) сразу отобразил изменения:
            // - актуальное количество маны,
            // - доступные действия,
            // - эффекты на картах.
            RefreshData();
        }

        public virtual void EndTurn() // 📌 Этот метод завершает ход игрока.
        {
            // 📌 Проверяем, не закончилась ли игра.
            // Если игра уже завершена (например, один из игроков победил),
            // то просто выходим из метода и ничего не делаем.
            if (game_data.state == GameState.GameEnded)
                return; // 🚫 Выход, если игра завершена.

            // 📌 Проверяем, находится ли игра в "основной фазе".
            // Если нет, значит, сейчас не время завершать ход (например, идет подготовка хода),
            // и мы просто выходим.
            if (game_data.phase != GamePhase.Main)
                return; // 🚫 Выход, если игра не в основной фазе.

            // 📌 Завершаем выбор целей, если он был активен.
            // Иногда перед атакой или применением способности игроку нужно выбрать цель.
            // В конце хода такой выбор больше не нужен, поэтому мы его сбрасываем.
            game_data.selector = SelectorType.None;

            // 📌 Устанавливаем текущую фазу как "Конец хода".
            game_data.phase = GamePhase.EndTurn;

            // 📌 Уменьшаем длительность всех временных эффектов.
            // Например, если на игроке или его картах был эффект "Отравление (3 хода)", 
            // он уменьшится до "Отравление (2 хода)".
            foreach (Player aplayer in game_data.players) // 🔄 Перебираем всех игроков
            {
                aplayer.ReduceStatusDurations(); // ⏳ Уменьшаем эффекты у самого игрока

                // 🔄 Также уменьшаем эффекты у всех карт на поле
                foreach (Card card in aplayer.cards_board)
                    card.ReduceStatusDurations();

                // 🔄 И у всех экипированных предметов (например, если карта носит оружие или броню)
                foreach (Card card in aplayer.cards_equip)
                    card.ReduceStatusDurations();
            }

            // 📌 Запускаем способности карт, которые срабатывают в конце хода.
            // Некоторые карты могут активировать эффекты именно в этот момент.
            Player player = game_data.GetActivePlayer(); // 🎮 Получаем текущего игрока
            TriggerPlayerCardsAbilityType(player, AbilityTrigger.EndOfTurn); // 🔥 Запускаем эффекты

            // 📌 Сообщаем всем системам, что ход завершился.
            // Например, это может запустить анимации или изменить UI.
            onTurnEnd?.Invoke(); // 💡 Если на событие кто-то подписан, оно будет вызвано.

            // 📌 Обновляем интерфейс и данные игры.
            // Это нужно, чтобы изменения (например, исчезновение эффектов) сразу отобразились.
            RefreshData();

            // 📌 Добавляем в очередь действие "Начать следующий ход".
            // Это значит, что через небольшую задержку игра автоматически переключится на следующего игрока.
            resolve_queue.AddCallback(StartNextTurn);

            // 📌 Выполняем все запланированные действия (например, переключение хода) с задержкой 0.2 секунды.
            resolve_queue.ResolveAll(0.2f);
        }

        public virtual void EndGame(int winner) // 📌 Этот метод завершает игру и объявляет победителя.
        {
            // 📌 Проверяем, не была ли игра уже завершена.
            // Если игра уже в состоянии "GameEnded", то ничего не делаем.
            if (game_data.state != GameState.GameEnded)
            {
                // 📌 Устанавливаем состояние игры как "завершено".
                game_data.state = GameState.GameEnded;

                // 📌 Очищаем текущую фазу игры.
                // После завершения игры фазы больше не существует, 
                // поэтому устанавливаем "None" (отсутствие фазы).
                game_data.phase = GamePhase.None;

                // 📌 Отменяем любые активные выборы.
                // Например, если игрок выбирал карту или цель для способности,
                // этот процесс больше не нужен.
                game_data.selector = SelectorType.None;

                // 📌 Запоминаем, кто победил.
                // `winner` — это ID победителя (например, 0 или 1 для двух игроков).
                game_data.current_player = winner;

                // 📌 Очищаем очередь действий (resolve_queue).
                // В этой очереди могли быть запланированные события (например, атаки),
                // но раз игра завершена, их больше выполнять не нужно.
                resolve_queue.Clear();

                // 📌 Получаем объект победившего игрока.
                Player player = game_data.GetPlayer(winner);

                // 📌 Сообщаем системе, что игра закончилась и кто победил.
                // Это событие может активировать анимации победы, статистику и т.д.
                onGameEnd?.Invoke(player);

                // 📌 Обновляем данные игры, чтобы UI и другие элементы знали, что игра завершена.
                RefreshData();
            }
        }

        public virtual void NextStep() // 📌 Этот метод переходит к следующему этапу или фазе игры. Фазы игры определены в скрипте Game
        {
            // 📌 Проверяем, не завершена ли игра.
            // Если игра уже окончена, просто выходим из метода.
            if (game_data.state == GameState.GameEnded)
                return;

            // 📌 Проверяем, находимся ли мы в фазе "Муллиган" (перемешивание и выбор карт перед началом игры).
            // Если да, то начинаем первый ход игры.
            if (game_data.phase == GamePhase.Mulligan)
            {
                StartTurn(); // 🔄 Запускаем первый ход.
                return; // ⏹ Выходим из метода, так как ход уже начался.
            }

            // 📌 Отменяем текущий выбор игрока, если он что-то выбирал (например, карту или цель способности).
            CancelSelection();

            // 📌 Добавляем завершение хода в очередь выполнения (resolve_queue).
            // Это делается на случай, если в игре еще выполняются какие-то действия (например, анимации или эффекты).
            resolve_queue.AddCallback(EndTurn);

            // 📌 Запускаем выполнение всех действий в очереди (например, завершаем эффекты или ждем их окончания).
            resolve_queue.ResolveAll();
        }

        protected virtual void CheckForWinner() // 📌 Этот метод проверяет, есть ли победитель в игре.
        // Если все игроки мертвы — объявляется ничья.
        // Если остался только один живой игрок — он объявляется победителем.
        {
            // 📌 Счетчик живых игроков
            int count_alive = 0;

            // 📌 Переменная для хранения последнего живого игрока
            Player alive = null;

            // 🔄 Перебираем всех игроков в игре
            foreach (Player player in game_data.players)
            {
                // 📌 Проверяем, жив ли игрок
                if (!player.IsDead()) // Метод IsDead() проверяет, что здоровье (HP) игрока больше 0
                {
                    alive = player; // Сохраняем этого игрока как последнего живого
                    count_alive++; // Увеличиваем счетчик живых игроков
                }
            }

            // 📌 Если нет живых игроков — объявляем ничью (передаем -1, что означает "никто не выиграл").
            if (count_alive == 0)
            {
                EndGame(-1); // ☠ Все мертвы → ничья.
            }
            // 📌 Если жив остался только один игрок — объявляем его победителем.
            else if (count_alive == 1)
            {
                EndGame(alive.player_id); // 🏆 Этот игрок побеждает!
            }
        }

        protected virtual void ClearTurnData() // 📌 Этот метод очищает временные данные хода, чтобы следующий ход начинался "с чистого листа".
        {
            // ❌ Сбрасываем текущий выбор игрока (он больше ничего не выбирает)
            game_data.selector = SelectorType.None;

            // 🗑️ Очищаем очередь обработки событий, если какие-то события еще не были завершены
            resolve_queue.Clear();

            // 🃏 Очищаем временные списки карт, игроков, слотов и данных карт
            card_array.Clear();       // Очистка списка карт, которые были задействованы в текущем ходу
            player_array.Clear();     // Очистка списка игроков, которые участвовали в каких-то действиях
            slot_array.Clear();       // Очистка списка слотов (мест на игровом поле)
            card_data_array.Clear();  // Очистка списка шаблонов карт (не конкретных карт, а их типов)

            // 🗑️ Сбрасываем ссылки на последнюю сыгранную, уничтоженную, атакованную и вызванную карты
            game_data.last_played = null;      // Последняя разыгранная карта
            game_data.last_destroyed = null;   // Последняя уничтоженная карта
            game_data.last_target = null;      // Последняя цель атаки/эффекта
            game_data.last_summoned = null;    // Последняя вызванная (призванная) карта

            // ❌ Сбрасываем данные о последней способности и выбранном значении (если было)
            game_data.ability_triggerer = null; // Последний активатор способности (кто её использовал)
            game_data.selected_value = 0;       // Последнее выбранное значение (если игрок выбирал что-то)

            // 🧹 Очищаем списки использованных способностей и атакованных карт за этот ход
            game_data.ability_played.Clear(); // Список использованных способностей
            game_data.cards_attacked.Clear(); // Список карт, которые атаковали в этом ходу
        }

        #endregion

        #region Setup deck // 🔹 Секция кода, связанная c настройкой деки игрока в начале матча

        public virtual void SetPlayerDeck(Player player, DeckData deck) // 📌 Этот метод задает колоду (дек) игрока на основе данных из ресурсов игры.
        // Он используется для загрузки стандартных предустановленных колод.
        {
            // 🗑️ Очищаем старую информацию о картах игрока
            player.cards_all.Clear();   // Полный список карт (включает все карты игрока)
            player.cards_deck.Clear();  // Колода карт (только карты, которые еще не сыграны)

            // 📌 Запоминаем идентификатор колоды
            player.deck = deck.id;

            // 🏆 Устанавливаем героя (если он есть в колоде)
            player.hero = null;  // По умолчанию у игрока нет героя

            // Создаём вариант карт по умолчанию (например, базовый вариант карт)
            VariantData variant = VariantData.GetDefault();

            if (deck.hero != null)
            {
                // Если в колоде есть герой, создаем его карту
                player.hero = Card.Create(deck.hero, variant, player);
            }

            // 🃏 Добавляем карты в колоду
            foreach (CardData card in deck.cards)
            {
                if (card != null) // Проверяем, что карта существует
                {
                    // Создаем карту и добавляем ее в колоду
                    Card acard = Card.Create(card, variant, player);
                    player.cards_deck.Add(acard);
                }
            }

            // 🔍 Проверяем, является ли эта колода "пазловой" (используется в одиночных уровнях)
            DeckPuzzleData puzzle = deck as DeckPuzzleData;

            // 🏗️ Если это пазловый уровень, добавляем стартовые карты на игровое поле
            if (puzzle != null)
            {
                foreach (DeckCardSlot card in puzzle.board_cards)
                {
                    // Создаем карту для этого слота
                    Card acard = Card.Create(card.card, variant, player);

                    // Определяем, в какой слот должна попасть карта (Slot.GetP(player.player_id) выбирает слот для игрока)
                    acard.slot = new Slot(card.slot, player.player_id);

                    // Добавляем карту на игровое поле
                    player.cards_board.Add(acard);
                }
            }

            // 🔀 Перемешиваем колоду, если это не пазловый уровень или в пазловом уровне разрешено перемешивание
            if (puzzle == null || !puzzle.dont_shuffle_deck)
            {
                ShuffleDeck(player.cards_deck);
            }
        }

        public virtual void SetPlayerDeck(Player player, UserDeckData deck) // 📌 Этот метод задает пользовательскую колоду (например, созданную игроком).
        // Колода может быть загружена из сохранений или базы данных.
        {
            // 🗑️ Очищаем старые карты игрока
            player.cards_all.Clear();   // Полный список карт
            player.cards_deck.Clear();  // Карты в колоде
            player.deck = deck.tid;     // Запоминаем идентификатор пользовательской колоды
            player.hero = null;         // По умолчанию у игрока нет героя

            // 🏆 Проверяем, есть ли герой в пользовательской колоде
            if (deck.hero != null)
            {
                // Получаем данные героя (ID и его вариант оформления)
                CardData hdata = CardData.Get(deck.hero.tid);
                VariantData hvariant = VariantData.Get(deck.hero.variant);

                // Если данные героя существуют, создаем его карту и назначаем игроку
                if (hdata != null && hvariant != null)
                    player.hero = Card.Create(hdata, hvariant, player);
            }

            // 🃏 Добавляем обычные карты в колоду
            foreach (UserCardData card in deck.cards)
            {
                // Получаем данные карты и ее вариацию (например, уникальный внешний вид)
                CardData icard = CardData.Get(card.tid);
                VariantData variant = VariantData.Get(card.variant);

                // Проверяем, что карта и ее вариант существуют
                if (icard != null && variant != null)
                {
                    // Добавляем указанное количество копий карты в колоду
                    for (int i = 0; i < card.quantity; i++)
                    {
                        Card acard = Card.Create(icard, variant, player);
                        player.cards_deck.Add(acard);
                    }
                }
            }

            // 🔀 Перемешиваем колоду перед началом игры
            ShuffleDeck(player.cards_deck);
        }

        #endregion

        #region Actions

        public virtual void PlayCard(Card card, Slot slot, bool skip_cost = false) // 📌 Этот метод разыгрывает карту на игровое поле.
        //Проверяет, можно ли сыграть карту.
        //Списывает ману (если нужно).
        //Перемещает карту на поле.
        //Активирует способности, если карта имеет их.
        //Обновляет данные игры.
        {
            // ✅ Проверяем, можно ли сыграть карту (учитываются манакост, правила игры и доступные слоты).
            if (game_data.CanPlayCard(card, slot, skip_cost))
            {
                // 🔍 Получаем игрока, которому принадлежит карта
                Player player = game_data.GetPlayer(card.player_id);

                // 💰 Если не указано, что карту можно сыграть бесплатно (skip_cost = false), списываем ману за её использование.
                if (!skip_cost)
                    player.PayMana(card);

                // 🗑️ Удаляем карту из всех текущих групп (из руки, из колоды и т. д.).
                player.RemoveCardFromAllGroups(card);

                // 🃏 Определяем, к какому типу карт относится текущая карта
                CardData icard = card.CardData;

                if (icard.IsBoardCard()) // 🏆 Если это карта, которая размещается на поле
                {
                    player.cards_board.Add(card); // Добавляем карту на игровое поле
                    card.slot = slot; // Закрепляем её за конкретным слотом
                    card.exhausted = true; // 💤 Новые карты не могут атаковать в тот же ход
                }
                else if (icard.IsEquipment()) // ⚔️ Если это экипировка
                {
                    Card bearer = game_data.GetSlotCard(slot); // Определяем карту, на которую надевается экипировка
                    EquipCard(bearer, card); // Надеваем экипировку
                    card.exhausted = true; // Экипировка тоже "устаёт" и не может быть использована в тот же ход
                }
                else if (icard.IsSecret()) // 🔮 Если это карта-секрет (ловушка)
                {
                    player.cards_secret.Add(card); // Добавляем её в список секретов игрока
                }
                else // ✨ Если это заклинание или другая карта с разовым эффектом
                {
                    player.cards_discard.Add(card); // Отправляем её в сброс после использования
                    card.slot = slot; // Сохраняем слот, если заклинание требует цели
                }

                // 📜 Запоминаем, что карта была сыграна (это важно для логов и повторных действий).
                if (!is_ai_predict && !icard.IsSecret())
                    player.AddHistory(GameAction.PlayCard, card);

                // 🔄 Обновляем все активные эффекты и состояния карт после разыгрывания
                game_data.last_played = card.uid;
                UpdateOngoing();

                // 🔥 Активируем способности карты, если они есть
                if (card.CardData.IsDynamicManaCost()) // Если у карты изменяемая стоимость маны
                {
                    GoToSelectorCost(card); // Спрашиваем игрока, сколько маны он хочет потратить
                }
                else
                {
                    // 🕵️‍♂️ Проверяем, не сработают ли секреты противника из-за разыгрывания карты
                    TriggerSecrets(AbilityTrigger.OnPlayOther, card);

                    // ⚡ Активируем способности самой карты
                    TriggerCardAbilityType(AbilityTrigger.OnPlay, card);

                    // 🔄 Проверяем, активируются ли способности других карт из-за разыгрывания этой
                    TriggerOtherCardsAbilityType(AbilityTrigger.OnPlayOther, card);
                }

                // 🔄 Обновляем интерфейс и данные игры после разыгрывания карты
                RefreshData();

                // 🎬 Вызываем событие, сообщающее, что карта была сыграна (например, для анимаций)
                onCardPlayed?.Invoke(card, slot);

                // ⏳ Запускаем очередь выполнения (анимации, эффекты или, например, если карта наносит урон сразу после розыгрыша)
                resolve_queue.ResolveAll(0.3f);
            }
        }

        public virtual void MoveCard(Card card, Slot slot, bool skip_cost = false) // 📌 Этот метод перемещает карту из одного слота в другой.
        // Он проверяет, можно ли переместить карту, изменяет её позицию и обновляет игру.
        {
            // ✅ Проверяем, можно ли переместить карту (учитываются правила игры, допустимые перемещения и ограничения).
            if (game_data.CanMoveCard(card, slot, skip_cost))
            {
                // 📍 Изменяем положение карты — теперь она находится в новом слоте.
                card.slot = slot;

                // 📌 В демонстрационной версии перемещение карт не оказывает влияния на игру.
                // Поэтому код ниже закомментирован, но его можно использовать для добавления штрафов за перемещение:
                // if (!skip_cost)  // Если не указано, что перемещение бесплатное
                // {
                //     card.exhausted = true; // ⏳ Карта не может атаковать после перемещения
                //     card.RemoveStatus(StatusEffect.Stealth); // 🕵️‍♂️ Если карта была скрыта (Stealth), то теряет этот статус
                //     player.AddHistory(GameAction.Move, card); // 📜 Записываем перемещение в историю ходов
                // }

                // 🔗 Если у карты есть экипировка, перемещаем её вместе с картой
                Card equip = game_data.GetEquipCard(card.equipped_uid);
                if (equip != null)
                    equip.slot = slot; // Экипировка теперь тоже привязана к новому слоту

                // 🔄 Обновляем все активные эффекты и состояния карт после перемещения
                UpdateOngoing();

                // 🔄 Обновляем интерфейс и данные игры после перемещения карты
                RefreshData();

                // 🎬 Вызываем событие, сообщающее, что карта была перемещена (например, для анимаций)
                onCardMoved?.Invoke(card, slot);

                // ⏳ Запускаем очередь выполнения (если есть эффекты, связанные с перемещением)
                resolve_queue.ResolveAll(0.2f);
            }
        }

        public virtual void CastAbility(Card card, AbilityData iability) //📌 Этот метод запускает способность карты:

        //Проверяет, может ли карта использовать способность.
        //Запускает очередь выполнения способности.
        //Применяет эффекты (например, лечение, урон, призыв новых карт).
        {
            // ✅ Проверяем, может ли карта применить свою способность
            if (game_data.CanCastAbility(card, iability))
            {
                // 🏆 Получаем игрока, которому принадлежит карта
                Player player = game_data.GetPlayer(card.player_id);

                // 📜 Если это не ИИ и способность не требует выбора цели вручную,
                // добавляем действие в историю ходов (для отображения в UI или анализа игры)
                if (!is_ai_predict && iability.target != AbilityTarget.SelectTarget)
                    player.AddHistory(GameAction.CastAbility, card, iability);

                // 🔥 Если у карты был статус "Stealth" (невидимость), она теряет его при применении способности
                card.RemoveStatus(StatusType.Stealth);

                // 🏹 Запускаем выполнение способности карты
                TriggerCardAbility(iability, card);

                // ⏳ Запускаем очередь выполнения, если есть дополнительные эффекты, требующие задержки
                resolve_queue.ResolveAll();
            }
        }

        public virtual void AttackTarget(Card attacker, Card target, bool skip_cost = false) //📌 Этот метод инициирует атаку карты:

        //Проверяет, может ли карта атаковать.
        //Добавляет атаку в очередь выполнения действий.
        //Запускает анимацию атаки и урона.
        //Удаляет карту, если у нее закончилось здоровье.
        {
            Debug.Log("AttackTarget called on object of type: " + this.GetType().Name);
            // ✅ Проверяем, может ли атакующая карта атаковать указанную цель.
            if (game_data.CanAttackTarget(attacker, target, skip_cost))
            {
                // 🏆 Получаем игрока, которому принадлежит атакующая карта
                Player player = game_data.GetPlayer(attacker.player_id);

                // 📜 Если это не ИИ, записываем атаку в историю ходов
                if (!is_ai_predict)
                    player.AddHistory(GameAction.Attack, attacker, target);

                // 🎯 Запоминаем, кого атакует карта (последняя цель атаки)
                game_data.last_target = target.uid;

                // ⚡ Активируем способности карт перед атакой
                TriggerCardAbilityType(AbilityTrigger.OnBeforeAttack, attacker, target); // Перед атакой
                TriggerCardAbilityType(AbilityTrigger.OnBeforeDefend, target, attacker); // Перед защитой

                // 🎭 Активируем секретные способности (если есть), срабатывающие перед атакой
                TriggerSecrets(AbilityTrigger.OnBeforeAttack, attacker);
                TriggerSecrets(AbilityTrigger.OnBeforeDefend, target);

                // ⚔️ Добавляем атаку в очередь выполнения (будет обработана позже)
                resolve_queue.AddAttack(attacker, target, ResolveAttack, skip_cost);

                // ⏳ Запускаем выполнение атаки
                resolve_queue.ResolveAll();
            }
        }

        protected virtual void ResolveAttack(Card attacker, Card target, bool skip_cost) //📌 Этот метод запускает атаку
        {
            //Debug.Log("Вызываем ResolveAttack из {this}");
            // ✅ Проверяем, находятся ли атакующий и защищающийся на игровом поле.
            // Если хотя бы одной карты нет на поле (например, она была уничтожена ранее), атака отменяется.
            if (!game_data.IsOnBoard(attacker) || !game_data.IsOnBoard(target))
                return;

            // ⚔️ Вызываем событие "Начало атаки", которое может запустить анимации или эффекты.
            onAttackStart?.Invoke(attacker, target);

            // 🔥 Если у атакующей карты была скрытность (Stealth), она теряет этот статус после атаки.
            attacker.RemoveStatus(StatusType.Stealth);

            // 🔄 Обновляем состояние игры (например, пересчитываем бонусы карт)
            UpdateOngoing();

            // ⚔️ Добавляем следующий шаг атаки (нанесение урона) в очередь выполнения.
            resolve_queue.AddAttack(attacker, target, ResolveAttackHit, skip_cost);

            // ⏳ Запускаем выполнение очереди с задержкой 0.3 секунды (можно использовать для анимаций).
            resolve_queue.ResolveAll(0.3f);
        }

        protected virtual void ResolveAttackHit(Card attacker, Card target, bool skip_cost) // 📌 Этот метод рассчитывает и применяет урон во время атаки.
        // 1. Определяет силу атаки атакующего и защитника.
        // 2. Применяет урон атакующему и защитнику.
        // 3. Учитывает броню, яд и другие эффекты.
        // 4. Проверяет способности карт, которые срабатывают после атаки.
        // 5. Завершает обработку атаки и проверяет победителя.

        {
            // 🔢 Получаем силу атаки атакующего и защитника
            int datt1 = attacker.GetAttack(); // Урон, который наносит атакующая карта
            int datt2 = target.GetAttack();   // Урон, который наносит защищающаяся карта в ответ

            // 💥 Наносим урон защитнику (цели атаки)
            DamageCard(attacker, target, datt1);

            // 💥 Если атакующая карта НЕ имеет статус "Intimidate" (устрашение), 
            // то она получает ответный урон от защитника.
            if (!attacker.HasStatus(StatusType.Intimidate))
                DamageCard(target, attacker, datt2);

            // 🏳️ Если атака не была пропущена (skip_cost == false), карта становится "уставшей" (exhausted)
            // Это значит, что она не сможет атаковать снова в этом же ходу.
            if (!skip_cost)
                ExhaustBattle(attacker);

            // 🔄 Обновляем состояние игры (например, пересчитываем бонусы карт)
            UpdateOngoing();

            // 🎭 Проверяем, находятся ли атакующая и защищающаяся карты на поле после атаки
            bool att_board = game_data.IsOnBoard(attacker);
            bool def_board = game_data.IsOnBoard(target);

            // 🛡️ Если атакующая карта жива, активируем её способности "После атаки"
            if (att_board)
                TriggerCardAbilityType(AbilityTrigger.OnAfterAttack, attacker, target);

            // 🛡️ Если защитник жив, активируем его способности "После защиты"
            if (def_board)
                TriggerCardAbilityType(AbilityTrigger.OnAfterDefend, target, attacker);

            // 🎭 Активируем секретные способности, которые могут сработать после атаки
            if (att_board)
                TriggerSecrets(AbilityTrigger.OnAfterAttack, attacker);
            if (def_board)
                TriggerSecrets(AbilityTrigger.OnAfterDefend, target);

            // 🏁 Завершаем атаку, вызываем событие "Атака завершена"
            onAttackEnd?.Invoke(attacker, target);

            // 🔄 Обновляем игровое состояние, чтобы интерфейс игры отобразил изменения
            RefreshData();

            // 🏆 Проверяем, не выиграл ли кто-то после атаки (если карта уничтожила последнего противника)
            CheckForWinner();

            // ⏳ Запускаем очередь выполнения с задержкой 0.2 секунды (для анимаций)
            resolve_queue.ResolveAll(0.2f);
        }

        public virtual void AttackPlayer(Card attacker, Player target, bool skip_cost = false) // 📌 Этот метод запускает атаку игрока (героя) картой противника
        {
            // ✅ Проверяем, переданы ли корректные значения для атакующего и цели.
            // Если атакующая карта (attacker) или игрок-цель (target) отсутствуют, атака невозможна.
            if (attacker == null || target == null || GameplayData.Get().duel)
                return;

            // ✅ Проверяем, может ли атакующая карта атаковать игрока.
            // Если атака невозможна (например, из-за нехватки маны или ограничений карты), выходим из метода.
            if (!game_data.CanAttackTarget(attacker, target, skip_cost))
                return;

            // 🏆 Получаем объект игрока, которому принадлежит атакующая карта.
            Player player = game_data.GetPlayer(attacker.player_id);

            // 📜 Если играется не AI (искусственный интеллект), записываем атаку в историю действий игрока.
            if (!is_ai_predict)
                player.AddHistory(GameAction.AttackPlayer, attacker, target);

            // 🔥 Перед атакой проверяем и активируем скрытые способности (секреты), которые могут сработать перед атакой.
            TriggerSecrets(AbilityTrigger.OnBeforeAttack, attacker);

            // 🛡️ Проверяем и активируем способности атакующей карты, которые срабатывают перед атакой игрока.
            TriggerCardAbilityType(AbilityTrigger.OnBeforeAttack, attacker, target);

            // ⚔️ Добавляем атаку в очередь выполнения, следующим шагом будет метод `ResolveAttackPlayer`.
            resolve_queue.AddAttack(attacker, target, ResolveAttackPlayer, skip_cost);

            // ⏳ Запускаем выполнение очереди действий, чтобы обработка атаки началась.
            resolve_queue.ResolveAll();
        }

        protected virtual void ResolveAttackPlayer(Card attacker, Player target, bool skip_cost) // 📌 Этот метод выполняет подготовку к атаке по игроку после того, как карта объявила атаку.
        {
            // ✅ Проверяем, находится ли атакующая карта на игровом поле.
            // Если карта уже была удалена (например, из-за эффекта противника), атака отменяется.
            if (!game_data.IsOnBoard(attacker))
                return;

            // 🔥 Вызываем событие начала атаки игрока.
            // Это может запустить анимации, звуки или другие визуальные эффекты.
            onAttackPlayerStart?.Invoke(attacker, target);

            // 🛡️ Удаляем статус "Скрытность" у атакующей карты, если он у нее был.
            // Это означает, что карта больше не может быть скрытой после атаки.
            attacker.RemoveStatus(StatusType.Stealth);

            // ♻️ Обновляем эффекты, зависящие от состояния игры.
            UpdateOngoing();

            // ⚔️ Добавляем следующий этап атаки в очередь выполнения действий.
            // Следующий метод, который выполнится — `ResolveAttackPlayerHit`, где будет рассчитан урон.
            resolve_queue.AddAttack(attacker, target, ResolveAttackPlayerHit, skip_cost);

            // ⏳ Запускаем выполнение очереди с небольшой задержкой (0.3 секунды),
            // чтобы дать возможность проиграть анимацию атаки.
            resolve_queue.ResolveAll(0.3f);
        }

        protected virtual void ResolveAttackPlayerHit(Card attacker, Player target, bool skip_cost) // 📌 Этот метод применяет урон к игроку после того, как атака была завершена.
        {
            // 💥 Применяем урон к игроку.
            // Вызываем метод `DamagePlayer`, передавая атакующую карту, цель и силу атаки карты.
            DamagePlayer(attacker, target, attacker.GetAttack());

            // ✅ Если затраты на атаку не были пропущены, помечаем карту как использованную в этом ходу.
            if (!skip_cost)
                ExhaustBattle(attacker); // Карта "устает" и не может атаковать снова.

            // ♻️ Обновляем состояние игры, чтобы учесть изменения после атаки.
            UpdateOngoing();

            // 🎯 Если атакующая карта всё еще на поле, активируем её способности после атаки.
            if (game_data.IsOnBoard(attacker))
                TriggerCardAbilityType(AbilityTrigger.OnAfterAttack, attacker, target);

            // 🔥 Проверяем и активируем секреты, которые могли сработать после атаки.
            TriggerSecrets(AbilityTrigger.OnAfterAttack, attacker);

            // 🏁 Вызываем событие завершения атаки по игроку (может использоваться для анимаций).
            onAttackPlayerEnd?.Invoke(attacker, target);

            // 🔄 Обновляем интерфейс игры, чтобы отразить изменения (например, уменьшение здоровья игрока).
            RefreshData();

            // 🏆 Проверяем, не выиграл ли кто-то после этой атаки.
            CheckForWinner();

            // ⏳ Завершаем выполнение очереди с задержкой (0.2 секунды).
            resolve_queue.ResolveAll(0.2f);
        }

        public virtual void ExhaustBattle(Card attacker) // 📌 Этот метод помечает карту как "уставшую" после атаки, если у нее нет специального эффекта "Ярость" (Fury).
        // "Уставшие" карты не могут атаковать снова в этом же ходу.
        {
            // ✅ Проверяем, атаковала ли эта карта ранее в этом ходу.
            // game_data.cards_attacked — это список карт, которые уже атаковали в текущем ходу.
            bool attacked_before = game_data.cards_attacked.Contains(attacker.uid);

            // 📝 Добавляем атакующую карту в список атаковавших в этом ходу.
            game_data.cards_attacked.Add(attacker.uid);

            // 🔥 Если у карты есть эффект "Ярость" (Fury) и она еще не атаковала ранее, то она может атаковать снова.
            bool attack_again = attacker.HasStatus(StatusType.Fury) && !attacked_before;

            // ⛔ Если карта не может атаковать снова, она становится "уставшей" и не может атаковать в этом ходу.
            attacker.exhausted = !attack_again;
        }

        public virtual void RedirectAttack(Card attacker, Card new_target) // 📌 Этот метод перенаправляет атаку с одной карты на другую.
        {
            // 🔍 Проходим по всем атакам, которые находятся в очереди выполнения.
            foreach (AttackQueueElement att in resolve_queue.GetAttackQueue())
            {
                // ✅ Если атакующая карта совпадает с той, которую мы хотим перенаправить
                if (att.attacker.uid == attacker.uid)
                {
                    // 🔄 Меняем цель атаки на новую карту.
                    att.target = new_target;

                    // ❌ Убираем возможную цель-игрока (если атака была на игрока, а теперь на карту).
                    att.ptarget = null;

                    // 🔄 Устанавливаем метод обработки атаки между картами.
                    att.callback = ResolveAttack;

                    // ❌ Убираем обработку атаки по игроку, так как теперь цель — карта.
                    att.pcallback = null;
                }
            }
        }

        public virtual void RedirectAttack(Card attacker, Player new_target) // 📌 Этот метод перенаправляет атаку с карты на игрока.
        {
            // 🔍 Проходим по всем атакам, которые находятся в очереди выполнения.
            foreach (AttackQueueElement att in resolve_queue.GetAttackQueue())
            {
                // ✅ Если атакующая карта совпадает с той, которую мы хотим перенаправить
                if (att.attacker.uid == attacker.uid)
                {
                    // 🔄 Меняем цель атаки на нового игрока.
                    att.ptarget = new_target;

                    // ❌ Убираем возможную цель-карту (если атака была на карту, а теперь на игрока).
                    att.target = null;

                    // 🔄 Устанавливаем метод обработки атаки по игроку.
                    att.pcallback = ResolveAttackPlayer;

                    // ❌ Убираем обработку атаки по карте, так как теперь цель — игрок.
                    att.callback = null;
                }
            }
        }

        public virtual void ShuffleDeck(List<Card> cards) // 📌 Этот метод случайным образом перемешивает колоду карт игрока.
        //🔥 Этот метод использует алгоритм "перемешивания Фишера-Йетса", который является эффективным способом случайного перемешивания элементов списка.
        {
            // 🔄 Проходим по каждой карте в списке
            for (int i = 0; i < cards.Count; i++)
            {
                // 📌 Запоминаем текущую карту
                Card temp = cards[i];

                // 🎲 Генерируем случайный индекс от i до конца списка
                int randomIndex = random.Next(i, cards.Count);

                // 🔄 Меняем местами текущую карту и случайную карту в колоде
                cards[i] = cards[randomIndex];
                cards[randomIndex] = temp;
            }
        }

        public virtual void DrawCard(Player player, int nb = 1) // 📌 Этот метод берет указанное количество карт из колоды игрока и добавляет их в руку.
        {
            // 🔄 Повторяем действие `nb` раз (игрок берет несколько карт)
            for (int i = 0; i < nb; i++)
            {
                // ✅ Проверяем, есть ли еще карты в колоде и есть ли место в руке
                if (player.cards_deck.Count > 0 && player.cards_hand.Count < GameplayData.Get().cards_max)
                {
                    // 📌 Берем верхнюю карту из колоды
                    Card card = player.cards_deck[0];

                    // ❌ Удаляем эту карту из колоды (она перемещается в руку)
                    player.cards_deck.RemoveAt(0);

                    // ✋ Добавляем карту в руку игрока
                    player.cards_hand.Add(card);
                }
            }

            // 🔔 Сообщаем системе, что игрок взял карту (например, для анимации)
            onCardDrawn?.Invoke(nb);
        }

        public virtual void DrawDiscardCard(Player player, int nb = 1)  // 📌 Этот метод берет указанное количество карт из колоды и перемещает их в сброс (дискард).
        // 💡 Этот метод полезен для эффектов, когда карты сгорают, теряются или используются в качестве жертвы.
        {
            // 🔄 Повторяем действие `nb` раз (перемещаем несколько карт)
            for (int i = 0; i < nb; i++)
            {
                // ✅ Проверяем, есть ли еще карты в колоде
                if (player.cards_deck.Count > 0)
                {
                    // 📌 Берем верхнюю карту из колоды
                    Card card = player.cards_deck[0];

                    // ❌ Удаляем эту карту из колоды
                    player.cards_deck.RemoveAt(0);

                    // 🗑️ Добавляем карту в зону сброса (дискард)
                    player.cards_discard.Add(card);
                }
            }
        }

        public virtual Card SummonCopy(Player player, Card copy, Slot slot) // 📌 Этот метод создает копию уже существующей карты и ставит ее на игровое поле.
        //🔥 Этот метод используется для дублирования карт на поле (например, при клонировании или использовании особых эффектов).
        {
            // 🃏 Получаем данные карты, которую копируем
            CardData icard = copy.CardData;

            // 📌 Вызываем `SummonCard`, чтобы создать новую карту с теми же характеристиками
            return SummonCard(player, icard, copy.VariantData, slot);
        }

        public virtual Card SummonCopyHand(Player player, Card copy) // 📌 Этот метод создает копию карты и добавляет ее в руку игрока.
        //🔥 Этот метод полезен, когда игрок получает дубликаты карт в руку (например, через особые способности карт или заклинания).
        {
            // 🃏 Получаем данные карты, которую копируем
            CardData icard = copy.CardData;

            // 📌 Вызываем `SummonCardHand`, чтобы создать карту и поместить ее в руку игрока
            return SummonCardHand(player, icard, copy.VariantData);
        }

        public virtual Card SummonCard(Player player, CardData card, VariantData variant, Slot slot) // 📌 Этот метод создает новую карту и помещает ее в слот на поле.
        //🔥 Используется для создания новых карт в бою, например, через заклинания или особые эффекты.
        {
            // ✅ Проверяем, является ли слот игрового поля допустимым
            if (!slot.IsValid())
                return null; // ❌ Если слот некорректный, не создаем карту

            // ❌ Если слот уже занят другой картой, отменяем призыв
            if (game_data.GetSlotCard(slot) != null)
                return null;

            // 🃏 Создаем новую карту и кладем ее в руку
            Card acard = SummonCardHand(player, card, variant);

            // 🎭 Играем эту карту сразу (без учета стоимости)
            PlayCard(acard, slot, true);

            // 🔔 Сообщаем системе, что карта была призвана (например, для анимации)
            onCardSummoned?.Invoke(acard, slot);

            // 🔙 Возвращаем ссылку на созданную карту
            return acard;
        }

        public virtual Card SummonCardHand(Player player, CardData card, VariantData variant) // 📌 Этот метод создает новую карту и добавляет ее в руку игрока.
        //🔥 Используется, когда нужно создать карту в руке (например, при доборе карт заклинаниями).
        {
            // 🃏 Создаем новую карту
            Card acard = Card.Create(card, variant, player);

            // ✋ Добавляем карту в руку игрока
            player.cards_hand.Add(acard);

            // 🔄 Запоминаем, что эта карта была последней призванной
            game_data.last_summoned = acard.uid;

            // 🔙 Возвращаем ссылку на созданную карту
            return acard;
        }

        public virtual Card TransformCard(Card card, CardData transform_to)  // 📌 Этот метод заменяет текущую карту на другую (например, при использовании магии трансформации).
        //🔥 Этот метод используется, когда карта превращается в другую (например, превращение слабого существа в более сильное).
        {
            // 🌀 Заменяем данные текущей карты на новые (сохраняя ее вариант)
            card.SetCard(transform_to, card.VariantData);

            // 🔔 Сообщаем системе, что карта трансформировалась (например, для анимации)
            onCardTransformed?.Invoke(card);

            // 🔙 Возвращаем трансформированную карту
            return card;
        }

        public virtual void EquipCard(Card card, Card equipment) // 📌 Этот метод экипирует карту снаряжением (например, мечом или щитом).
        {
            // ✅ Проверяем, что обе карты существуют и принадлежат одному игроку
            if (card != null && equipment != null && card.player_id == equipment.player_id)
            {
                // ❌ Проверяем, что карта - не снаряжение, а экипируемая карта - снаряжение
                if (!card.CardData.IsEquipment() && equipment.CardData.IsEquipment())
                {
                    // 🗑️ Снимаем предыдущее снаряжение, так как можно носить только одно
                    UnequipAll(card);

                    // 🎮 Получаем игрока, владеющего картой
                    Player player = game_data.GetPlayer(card.player_id);

                    // 📌 Убираем карту снаряжения из всех других списков (например, из руки)
                    player.RemoveCardFromAllGroups(equipment);

                    // ➕ Добавляем карту в список экипировки
                    player.cards_equip.Add(equipment);

                    // 🔗 Связываем карту снаряжения с основной картой
                    card.equipped_uid = equipment.uid;

                    // 🔄 Устанавливаем слот экипировки (на то же место, где стоит карта)
                    equipment.slot = card.slot;
                }
            }
        }

        public virtual void UnequipAll(Card card) // 📌 Этот метод снимает с карты все снаряжение.
        //🔥 Этот метод нужен, чтобы снимать броню и оружие (например, при разрушении предмета или замене экипировки).
        {
            // ✅ Проверяем, есть ли у карты экипировка
            if (card != null && card.equipped_uid != null)
            {
                // 🎮 Получаем игрока, которому принадлежит карта
                Player player = game_data.GetPlayer(card.player_id);

                // 🛠️ Получаем экипированную карту (снаряжение)
                Card equip = player.GetEquipCard(card.equipped_uid);

                // ❌ Если снаряжение найдено, удаляем его
                if (equip != null)
                {
                    card.equipped_uid = null; // 🔄 Удаляем связь с основой картой
                    DiscardCard(equip);       // 🗑️ Перемещаем снаряжение в сброс
                }
            }
        }

        public virtual void ChangeOwner(Card card, Player owner) // 📌 Этот метод передает карту другому игроку (например, при краже или обмене).
        //🔥 Этот метод полезен для эффектов кражи карт, передачи карт союзникам или при изменении владельца после боя.
        {
            // ✅ Проверяем, что карта меняет владельца
            if (card.player_id != owner.player_id)
            {
                // 🎮 Получаем старого владельца карты
                Player powner = game_data.GetPlayer(card.player_id);

                // 🗑️ Убираем карту из всех групп старого владельца
                powner.RemoveCardFromAllGroups(card);
                powner.cards_all.Remove(card.uid);

                // 🆕 Добавляем карту в список карт нового владельца
                owner.cards_all[card.uid] = card;

                // 🔄 Изменяем идентификатор владельца карты
                card.player_id = owner.player_id;
            }
        }

        public virtual void DamagePlayer(Card attacker, Player target, int value) // 📌 Этот метод наносит урон игроку (например, когда карта атакует героя игрока).
        //🔥 Этот метод используется, когда персонажи или заклинания наносят урон напрямую по игроку (герою).
        {
            // 🔥 Уменьшаем здоровье игрока на значение урона
            target.hp -= value;

            // 🛡️ Убеждаемся, что здоровье игрока не опускается ниже 0 и не превышает максимум
            target.hp = Mathf.Clamp(target.hp, 0, target.hp_max);

            // ❤️ Лечение атакующего игрока, если у него есть способность "Похищение жизни" (Lifesteal)
            Player aplayer = game_data.GetPlayer(attacker.player_id);
            if (attacker.HasStatus(StatusType.LifeSteal))
                aplayer.hp += value; // 🔄 Атакующий восстанавливает здоровье на сумму нанесенного урона

            // 🔔 Сообщаем другим системам, что игрок получил урон (например, для анимации)
            onPlayerDamaged?.Invoke(target, value);
        }

        public virtual void HealCard(Card target, int value) // 📌 Этот метод восстанавливает здоровье карты (например, заклинание исцеления).
        {
            // ✅ Проверяем, существует ли цель лечения (карта)
            if (target == null)
                return; // 🚫 Если карты нет, ничего не делаем

            // 🛡️ Если у карты есть статус "Неуязвимость" (Invincibility), лечение не сработает
            if (target.HasStatus(StatusType.Invincibility))
                return;

            // ❤️ Уменьшаем полученный картой урон (то есть фактически восстанавливаем ее здоровье)
            target.damage -= value;

            // 🔄 Убеждаемся, что урон не может быть отрицательным (здоровье не может стать выше максимального)
            target.damage = Mathf.Max(target.damage, 0);

            // 🔔 Сообщаем другим системам, что карта была вылечена (например, для анимации)
            onCardHealed?.Invoke(target, value);
        }

        public virtual void HealPlayer(Player target, int value) // 📌 Этот метод восстанавливает здоровье игроку (например, зелье или способность героя).
        //🔥 Этот метод применяется для лечения героев, использования зелий здоровья и других исцеляющих эффектов.
        {
            // ✅ Проверяем, существует ли цель лечения (игрок)
            if (target == null)
                return; // 🚫 Если игрока нет, ничего не делаем

            // ❤️ Увеличиваем здоровье игрока на переданное значение
            target.hp += value;

            // 🛡️ Убеждаемся, что здоровье не превышает максимум
            target.hp = Mathf.Clamp(target.hp, 0, target.hp_max);

            // 🔔 Сообщаем другим системам, что игрок был вылечен (например, для анимации)
            onPlayerHealed?.Invoke(target, value);
        }

        public virtual void DamageCard(Card target, int value) // 📌 Метод наносит урон карте, если он не исходит от другой карты (например, глобальные эффекты, заклинания, дебаффы).
        {
            // 🔹 Если цель отсутствует (null), просто выходим из метода.
            if (target == null)
                return;

            // 🔹 Если у карты есть статус "неуязвимость" (Invincibility), урон не наносится.
            if (target.HasStatus(StatusType.Invincibility))
                return; // 🛡️ Карта полностью защищена и не может получать урон.

            // 🔹 Если у карты есть иммунитет к заклинаниям (Spell Immunity), урон тоже не проходит.
            if (target.HasStatus(StatusType.SpellImmunity))
                return; // 🔮 Карта защищена от магического урона.

            // 📌 Увеличиваем количество полученного урона (`damage`), а не уменьшаем HP!
            target.damage += value;

            // 🔹 Вызываем событие, которое сообщает другим частям кода (например, UI или анимациям), что карта получила урон.
            onCardDamaged?.Invoke(target, value);

            // 🛑 Проверяем, не умерла ли карта из-за урона (если ее HP стало 0 или меньше).
            if (target.GetHP() <= 0)
                DiscardCard(target); // 💀 Удаляем карту с поля, если у нее не осталось здоровья.
        }

        public virtual void DamageCard(Card attacker, Card target, int value, bool spell_damage = false) // 📌 Этот метод наносит урон карте от атакующей карты или заклинания.
        // Учитывает различные статусы, такие как броня, вампиризм, пробивание защиты, смертельный удар.
        {
            // 🔹 Если атакующий или цель отсутствуют, урон не применяется.
            if (attacker == null || target == null)
                return;

            // 🛡️ Если цель имеет статус "неуязвимость" (Invincibility), урон не проходит.
            if (target.HasStatus(StatusType.Invincibility))
                return;

            // 🔮 Если цель имеет "Иммунитет к заклинаниям" (SpellImmunity), и атакующий НЕ является персонажем, урон не проходит.
            if (target.HasStatus(StatusType.SpellImmunity) && attacker.CardData.type != CardType.Character)
                return;

            // 🛡️ "Двойная жизнь" (Shell): Если у карты есть этот статус, первый урон просто снимает его, а не наносит урон.
            bool doublelife = target.HasStatus(StatusType.Shell);
            if (doublelife && value > 0)
            {
                target.RemoveStatus(StatusType.Shell); // ❌ Удаляем статус "Shell" без нанесения урона.
                return;
            }

            // 🏰 "Броня" (Armor): уменьшает входящий урон, если это не магический урон.
            if (!spell_damage && target.HasStatus(StatusType.Armor))
                value = Mathf.Max(value - target.GetStatusValue(StatusType.Armor), 0); // 🛡️ Урон уменьшается на значение брони.

            // 📌 Рассчитываем реальный урон:
            int damage_max = Mathf.Min(value, target.GetHP()); // ⚖️ Не даем урону превысить текущее здоровье.
            int extra = value - target.GetHP(); // 💥 Избыточный урон (если урон больше, чем HP цели).

            // ✅ Наносим урон карте
            target.damage += value;

            // 🏇 "Пролом" (Trample): Если урон превышает HP карты, остаток урона переходит на игрока.
            Player tplayer = game_data.GetPlayer(target.player_id);
            if (!spell_damage && extra > 0 && attacker.player_id == game_data.current_player && attacker.HasStatus(StatusType.Trample))
                tplayer.hp -= extra; // 🩸 Урон идет прямо в здоровье игрока.

            // 🧛 "Вампиризм" (LifeSteal): Лечит атакующего на нанесенный урон (если не магический урон).
            Player player = game_data.GetPlayer(attacker.player_id);
            if (!spell_damage && attacker.HasStatus(StatusType.LifeSteal))
                player.hp += damage_max; // ❤️ Лечим атакующего на величину нанесенного урона.

            // 😴 Если цель находилась в "Сне", он снимается после получения урона.
            target.RemoveStatus(StatusType.Sleep);

            // 🔄 Вызываем событие, сообщающее, что карта получила урон (например, для анимаций).
            onCardDamaged?.Invoke(target, value);

            // ☠️ "Смертельный удар" (Deathtouch): Если атакующий имеет этот статус, он сразу убивает цель.
            if (value > 0 && attacker.HasStatus(StatusType.Deathtouch) && target.CardData.type == CardType.Character)
                KillCard(attacker, target); // 💀 Цель уничтожена мгновенно.

            // 💀 Если здоровье карты опустилось до 0, карта уничтожается.
            if (target.GetHP() <= 0)
                KillCard(attacker, target);
        }

        public virtual void KillCard(Card attacker, Card target) // 📌 Этот метод "убивает" карту, то есть удаляет её с поля и отправляет в сброс (если возможно).
        // Он может быть вызван при атаке, использовании способности или специального эффекта.
        {
            // 🛑 Если атакующий или цель отсутствуют, выход из метода.
            if (attacker == null || target == null)
                return;

            // 🛑 Если цель уже не находится на игровом поле и не является экипировкой, то она уже уничтожена.
            if (!game_data.IsOnBoard(target) && !game_data.IsEquipped(target))
                return;

            // 🛡️ Если карта имеет статус "Неуязвимость" (Invincibility), она не может быть уничтожена.
            if (target.HasStatus(StatusType.Invincibility))
                return;

            // 🔥 Если атакующий убил карту противника, увеличиваем его счетчик убийств.
            Player pattacker = game_data.GetPlayer(attacker.player_id);
            if (attacker.player_id != target.player_id)
                pattacker.kill_count++; // +1 убийство в статистику.

            // 💀 Перемещаем карту в сброс.
            DiscardCard(target);

            // ⚡ Триггерим способности, которые активируются при убийстве другой карты.
            TriggerCardAbilityType(AbilityTrigger.OnKill, attacker, target);
        }

        public virtual void DiscardCard(Card card) // 📌 Этот метод перемещает карту в сброс (колоду использованных карт).
        {
            // 🛑 Проверяем, существует ли карта.
            if (card == null)
                return;

            // 🛑 Если карта уже находится в сбросе, второй раз её туда не отправляем.
            if (game_data.IsInDiscard(card))
                return;

            // Получаем данные карты и владельца.
            CardData icard = card.CardData;
            Player player = game_data.GetPlayer(card.player_id);

            // 🏳️ Определяем, была ли карта на игровом поле или являлась экипировкой.
            bool was_on_board = game_data.IsOnBoard(card) || game_data.IsEquipped(card);

            // ⛔ Если карта была экипировкой, снимаем её с владельца.
            UnequipAll(card);

            // ❌ Удаляем карту из всех активных групп игрока.
            player.RemoveCardFromAllGroups(card);

            // 📤 Добавляем карту в колоду сброса.
            player.cards_discard.Add(card);
            game_data.last_destroyed = card.uid; // Запоминаем последнюю уничтоженную карту.

            // ❌ Если карта была экипировкой, удаляем её связь с владельцем.
            Card bearer = player.GetBearerCard(card);
            if (bearer != null)
                bearer.equipped_uid = null;

            if (was_on_board)
            {
                // 💀 Если карта была на поле, активируем способности "При смерти".
                TriggerCardAbilityType(AbilityTrigger.OnDeath, card);
                TriggerOtherCardsAbilityType(AbilityTrigger.OnDeathOther, card);
                TriggerSecrets(AbilityTrigger.OnDeathOther, card);

                // 🔄 Обновляем эффекты (например, если карта давала бонусы, их нужно убрать).
                UpdateOngoingCards(); // Специальный вызов, чтобы избежать зацикливания.
            }

            // ⏳ Добавляем карту в список для удаления в следующем цикле обновления (например, если есть массовый урон).
            cards_to_clear.Add(card);

            // 🔥 Вызываем событие "Карта сброшена" (для анимаций, звуков и интерфейса).
            onCardDiscarded?.Invoke(card);
        }

        public int RollRandomValue(int dice) // 📌 Этот метод бросает "кубик" с указанным числом граней (например, 6-гранный кубик).
        //этот метод просто вызывает следующий метод, передавая ему диапазон от 1 до dice.
        {
            // 🎲 Вызывает перегруженный метод, указывая диапазон от 1 до (dice + 1),
            // так как random.Next(min, max) включает min, но исключает max.
            return RollRandomValue(1, dice + 1);
        }

        public virtual int RollRandomValue(int min, int max)  // 📌 Этот метод выбирает случайное число в заданном диапазоне и вызывает соответствующее событие.
        {
            // 🎲 Генерируем случайное число от min до max (max не включается).
            game_data.rolled_value = random.Next(min, max);

            // 🔔 Вызываем событие, уведомляющее другие части кода о новом значении броска.
            onRollValue?.Invoke(game_data.rolled_value);

            // ⏳ Устанавливаем небольшую задержку перед продолжением (например, для анимации кубика).
            resolve_queue.SetDelay(1f);

            // 📤 Возвращаем сгенерированное случайное число.
            return game_data.rolled_value;
        }

        #endregion

        #region Abilities

        public virtual void TriggerCardAbilityType(AbilityTrigger type, Card caster, Card triggerer = null) // 📌 Этот метод активирует способности конкретной карты.  При этом триггером является карта
        {
            foreach (AbilityData iability in caster.GetAbilities()) // 🔄 Перебираем все способности карты
            {
                if (iability && iability.trigger == type) // ✅ Если способность имеет нужный триггер
                {
                    TriggerCardAbility(iability, caster, triggerer); // 🔥 Активируем её
                }
            }

            // 📌 Проверяем, есть ли у карты экипировка, и, если экипировка есть, активируем её способности.
            Card equipped = game_data.GetEquipCard(caster.equipped_uid);
            if (equipped != null)
                TriggerCardAbilityType(type, equipped, triggerer);
        }

        public virtual void TriggerCardAbilityType(AbilityTrigger type, Card caster, Player triggerer) // 📌 Этот метод активирует способности конкретной карты.  При этом триггером является игрок
        {
            foreach (AbilityData iability in caster.GetAbilities()) // 🔄 Перебираем способности карты
            {
                if (iability && iability.trigger == type) // ✅ Если триггер совпадает
                {
                    TriggerCardAbility(iability, caster, triggerer); // 🔥 Активируем способность
                }
            }

            // 📌 Проверяем, есть ли у карты экипировка, и, если экипировка есть, активируем её способности.
            Card equipped = game_data.GetEquipCard(caster.equipped_uid);
            if (equipped != null)
                TriggerCardAbilityType(type, equipped, triggerer);
        }

        public virtual void TriggerOtherCardsAbilityType(AbilityTrigger type, Card triggerer) // 📌 Этот метод активирует все способности на поле, которые соответствуют переданному триггеру.  При этом триггером является карта
        //например, разыгрывается карта с эффектом "Все мои существа получают +2 здоровья".
        {
            foreach (Player oplayer in game_data.players) // 🔄 Перебираем всех игроков
            {
                if (oplayer.hero != null)
                    TriggerCardAbilityType(type, oplayer.hero, triggerer); // 🔥 Активируем способности героя

                foreach (Card card in oplayer.cards_board) // 🔄 Перебираем все карты на поле
                    TriggerCardAbilityType(type, card, triggerer); // 🔥 Активируем их способности
            }
        }

        public virtual void TriggerPlayerCardsAbilityType(Player player, AbilityTrigger type) // 📌 Этот метод активирует все способности у карт конкретного игрока
        //например, у игрока есть способность "в начале хода игрока его существа получают +1 к атаке".
        {
            if (player.hero != null)
                TriggerCardAbilityType(type, player.hero, player.hero); // 🔥 Проверяем способности героя

            foreach (Card card in player.cards_board) // 🔄 Перебираем все карты на поле у игрока
                TriggerCardAbilityType(type, card, card); // 🔥 Активируем их способности
        }


        public virtual void TriggerCardAbility(AbilityData iability, Card caster) // 📌 Этот метод - упрощенная версия следующего метода. Карта передает саму себя как триггер
        // Например, если у карты есть способность "Когда разыгрывается, восстановите 2 здоровья"
        {
            TriggerCardAbility(iability, caster, caster);
        }

        public virtual void TriggerCardAbility(AbilityData iability, Card caster, Card triggerer) // 📌  Этот метод используется для активации способности с конкретным триггером (другая карта)
        //Допустим, карта имеет способность "Когда вы играете заклинание, получите +1 к атаке".
        //Этот метод проверит, сыграно ли заклинание, и активирует эффект.
        {
            // Если triggerer (триггерившая карта) не передана, используем caster (карту, у которой срабатывает способность)
            Card trigger_card = triggerer != null ? triggerer : caster;

            // Проверяем, что карта не заглушена (Silenced) и выполнены все условия способности
            if (!caster.HasStatus(StatusType.Silenced) && iability.AreTriggerConditionsMet(game_data, caster, trigger_card))
            {
                // Добавляем способность в очередь на выполнение
                resolve_queue.AddAbility(iability, caster, trigger_card, ResolveCardAbility);
            }
        }

        public virtual void TriggerCardAbility(AbilityData iability, Card caster, Player triggerer) // 📌  Этот метод используется для активации способности с триггером в виде игрока
        //Используется для способностей, зависящих от действий игрока (например, "Если игрок взял карту, восстанови 1 здоровье").

        {
            if (!caster.HasStatus(StatusType.Silenced) && iability.AreTriggerConditionsMet(game_data, caster, triggerer))
            {
                resolve_queue.AddAbility(iability, caster, caster, ResolveCardAbility);
            }
        }

        public virtual void TriggerAbilityDelayed(AbilityData iability, Card caster) // 📌  Этот метод используется для активации способности с задержкой
        //Допустим, есть карта "После того, как разыграна другая карта, вылечи героя на 2".
        //Эта способность добавится в очередь и выполнится только после разыгрывания другой карты.
        //То есть карта сама по себе активирует способность с задержкой, без привязки к какому-либо другому объекту.
        //Используется для автоматических способностей, которые запускаются позже, но не зависят от других карт.
        //Например, "Через 1 секунду нанеси 2 урона врагу"
        {
            resolve_queue.AddAbility(iability, caster, caster, TriggerCardAbility);
        }

        public virtual void TriggerAbilityDelayed(AbilityData iability, Card caster, Card triggerer) // 📌  Этот метод используется для активации способности с задержкой (но с триггером)
        //Допустим, у карты есть эффект "После того, как существо получит урон, оно восстанавливает 1 здоровье".
        //Эта способность добавится в очередь, но выполнится только после получения урона.
        //Этот метод тоже откладывает активацию способности, но уже учитывает триггер (triggerer).
        //Триггером может быть другая карта, которая вызвала эту способность.
        //Используется, если способность карты зависит от другого объекта.
        //Например, "Когда другое существо получает урон, через 1 секунду оно восстанавливает здоровье"
        {
            Card trigger_card = triggerer != null ? triggerer : caster; //Triggerer is the caster if not set
            resolve_queue.AddAbility(iability, caster, trigger_card, TriggerCardAbility);
        }

        #region ResolveCardAbility // 🔹 Секция кода, связанная с логикой применения способности (определяет цели). Методы вызываются, когда карта запускает способность. Определяет, какие цели будут затронуты способностью (карты, игроки, слоты) и проверяет, нужно ли выбирать цель
        protected virtual void ResolveCardAbility(AbilityData iability, Card caster, Card triggerer) // 📌 Этот метод выполняет (разрешает) способность карты и проверяет, требуется ли выбор цели игроком
        {
            // ✅ Проверяем, может ли карта активировать способности (например, если карта "заглушена", способность не сработает)
            if (!caster.CanDoAbilities())
                return; // Если карта не может использовать способности, выходим из метода

            // 🔄 Вызываем событие начала способности (например, для отображения анимации или эффектов)
            onAbilityStart?.Invoke(iability, caster);

            // 💾 Запоминаем, какая карта вызвала способность
            game_data.ability_triggerer = triggerer.uid;

            // 📜 Добавляем текущую способность в список уже использованных в этом ходу
            game_data.ability_played.Add(iability.id);

            // 🎯 Проверяем, требуется ли игроку выбрать цель перед выполнением способности
            bool is_selector = ResolveCardAbilitySelector(iability, caster);
            if (is_selector)
                return; // Если нужен выбор цели, ждем игрока и прерываем выполнение метода

            // 📌 Если выбор цели не требуется, выполняем способность в зависимости от её типа
            ResolveCardAbilityPlayTarget(iability, caster);  // 🎯 Если способность действует на карту, которая была сыграна в слот
            ResolveCardAbilityPlayers(iability, caster);     // 👤 Если способность влияет на игроков
            ResolveCardAbilityCards(iability, caster);       // 🃏 Если способность влияет на другие карты
            ResolveCardAbilitySlots(iability, caster);       // 🔲 Если способность влияет на слот
            ResolveCardAbilityCardData(iability, caster);    // 📖 Если способность связана с определенной картой по её данным
            ResolveCardAbilityNoTarget(iability, caster);    // ❌ Если способность не требует цели (например, "получите 1 ману")

            // ✅ Завершаем обработку способности (например, снимаем ману, активируем цепочку других эффектов)
            AfterAbilityResolved(iability, caster);
        }

        protected virtual bool ResolveCardAbilitySelector(AbilityData iability, Card caster) // 📌 Этот метод проверяет, требуется ли игроку выбрать цель перед выполнением способности
        //Смотрим на iability.target, который определяет тип цели способности:
        //AbilityTarget.SelectTarget → игрок должен выбрать цель.
        //AbilityTarget.CardSelector → игрок выбирает карту (из руки, колоды и т.д.).
        //AbilityTarget.ChoiceSelector → игрок выбирает одно из нескольких действий.
        {
            if (iability.target == AbilityTarget.SelectTarget)
            {
                // 🏹 Если способность требует выбора конкретной цели, переводим игру в режим выбора цели
                GoToSelectTarget(iability, caster);
                return true; // Ждем выбора, останавливаем выполнение метода
            }
            else if (iability.target == AbilityTarget.CardSelector)
            {
                // 🃏 Если способность требует выбора одной из карт (например, "выберите карту из руки"), запускаем этот режим
                GoToSelectorCard(iability, caster);
                return true;
            }
            else if (iability.target == AbilityTarget.ChoiceSelector)
            {
                // 🔘 Если способность требует выбора варианта действия (например, "получите 3 маны или доберите карту"), запускаем этот режим
                GoToSelectorChoice(iability, caster);
                return true;
            }

            return false; // Если выбор не требуется, продолжаем выполнение способности
        }

        protected virtual void ResolveCardAbilityPlayTarget(AbilityData iability, Card caster) // 📌 Метод обрабатывает способность, если её цель — это место, куда была сыграна карта.
        //Работает с одночной конкретной целью в отличие от методов ниже
        {
            // ✅ Проверяем, является ли цель способности местом разыгрывания карты (например, на игровом поле)
            if (iability.target == AbilityTarget.PlayTarget)
            {
                // Получаем слот, в который была сыграна карта (игровое поле, рука и т. д.)
                Slot slot = caster.slot;

                // Проверяем, есть ли уже карта в этом слоте (например, если разыгрывается существо)
                Card slot_card = game_data.GetSlotCard(slot);

                // 🔹 Проверяем, является ли слот слотом игрока (например, если карта должна воздействовать на игрока). Если слот принадлежит игроку → пытается применить способность к игроку.
                if (slot.IsPlayerSlot())
                {
                    // Получаем игрока, которому принадлежит слот
                    Player tplayer = game_data.GetPlayer(slot.p);

                    // Проверяем, подходит ли игрок как цель для этой способности
                    if (iability.CanTarget(game_data, caster, tplayer))
                    {
                        // Если игрок может быть целью, применяем эффект способности к нему
                        ResolveEffectTarget(iability, caster, tplayer);
                    }
                }
                // 🔹 Если в слоте уже находится карта, проверяем, можно ли её выбрать целью. Если в слоте уже находится карта → пытается применить способность к этой карте.
                else if (slot_card != null)
                {
                    // Проверяем, разрешает ли способность атаковать эту карту
                    if (iability.CanTarget(game_data, caster, slot_card))
                    {
                        // Запоминаем последнюю выбранную цель (чтобы другие способности могли её использовать)
                        game_data.last_target = slot_card.uid;

                        // Применяем эффект способности к этой карте
                        ResolveEffectTarget(iability, caster, slot_card);
                    }
                }
                // 🔹 Если слот не принадлежит игроку и в нём нет карты, проверяем, можно ли выбрать сам слот. Если слот пустой, но его можно выбрать как цель → применяет способность к самому слоту.
                else
                {
                    if (iability.CanTarget(game_data, caster, slot))
                    {
                        // Если слот может быть целью, применяем способность к нему (например, создание зоны эффекта)
                        ResolveEffectTarget(iability, caster, slot);
                    }
                }
            }
        }

        protected virtual void ResolveCardAbilityPlayers(AbilityData iability, Card caster) // 📌 Метод применяет способность к игрокам, если они могут быть целью
        //Применяет способность ко многим целям по всему игровому полю.
        {
            // 🔹 Получаем список игроков, которые соответствуют условиям цели способности
            List<Player> targets = iability.GetPlayerTargets(game_data, caster, player_array);

            // 🔹 Применяем эффект способности к каждому игроку из списка
            foreach (Player target in targets)
            {
                ResolveEffectTarget(iability, caster, target);
            }
        }

        protected virtual void ResolveCardAbilityCards(AbilityData iability, Card caster) // 📌 Метод применяет способность к картам, если они могут быть целью
        //Применяет способность ко многим целям по всему игровому полю.
        {
            // 🔹 Получаем список карт, которые могут быть целью способности
            List<Card> targets = iability.GetCardTargets(game_data, caster, card_array);

            // 🔹 Применяем эффект способности к каждой карте из списка
            foreach (Card target in targets)
            {
                ResolveEffectTarget(iability, caster, target);
            }
        }

        protected virtual void ResolveCardAbilitySlots(AbilityData iability, Card caster) // 📌 Метод применяет способность к слотам, если они могут быть целью
        //Применяет способность ко многим целям по всему игровому полю.
        {
            // 🔹 Получаем список слотов, которые могут быть целью способности
            List<Slot> targets = iability.GetSlotTargets(game_data, caster, slot_array);

            // 🔹 Применяем эффект способности к каждому слоту из списка
            foreach (Slot target in targets)
            {
                ResolveEffectTarget(iability, caster, target);
            }
        }

        protected virtual void ResolveCardAbilityCardData(AbilityData iability, Card caster) // 📌 Метод применяет способность к определённым типам карт (шаблонным данным карт)
        //Применяет способность ко многим целям по всему игровому полю.
        {
            // 🔹 Получаем список типов карт, которые могут быть целью способности
            List<CardData> targets = iability.GetCardDataTargets(game_data, caster, card_data_array);

            // 🔹 Применяем эффект способности к каждому типу карты из списка
            foreach (CardData target in targets)
            {
                ResolveEffectTarget(iability, caster, target);
            }
        }

        protected virtual void ResolveCardAbilityNoTarget(AbilityData iability, Card caster) // 📌 Этот метод применяется, если у способности НЕТ конкретной цели.
        {
            // Проверяем, что цель способности указана как "None" (то есть способность не требует цели)
            if (iability.target == AbilityTarget.None)
                // Вызываем выполнение эффекта способности, передавая текущий объект (игровую логику) и карту, которая её использует
                iability.DoEffects(this, caster);
        }

        #endregion

        #region ResolveEffectTarget // 🔹 Секция кода, связанная с исполнение эффекта, когда цель уже выбрана. Методы вызываются, когда способность уже применяется к конкретной цели. Применяет эффект к цели, выполняя действия (урон, лечение, призыв)

        protected virtual void ResolveEffectTarget(AbilityData iability, Card caster, Player target) // 📌 Этот метод применяется, когда целью способности является игрок.
        {
            // Выполняем эффекты способности, применяя их к игроку
            iability.DoEffects(this, caster, target);

            // Вызываем событие, которое может быть использовано для отображения анимации или обновления UI,
            // сообщая, что способность применена к игроку.
            onAbilityTargetPlayer?.Invoke(iability, caster, target);
        }

        protected virtual void ResolveEffectTarget(AbilityData iability, Card caster, Card target) // 📌 Этот метод применяется, когда целью способности является другая карта.
        {
            // Выполняем эффекты способности, применяя их к целевой карте
            iability.DoEffects(this, caster, target);

            // Вызываем событие, уведомляя, что способность применена к карте (например, для анимации).
            onAbilityTargetCard?.Invoke(iability, caster, target);
        }

        protected virtual void ResolveEffectTarget(AbilityData iability, Card caster, Slot target) // 📌 Этот метод применяется, когда целью способности является определённый слот (место на игровом поле).
        {
            // Выполняем эффекты способности, применяя их к выбранному слоту
            iability.DoEffects(this, caster, target);

            // Вызываем событие, уведомляя, что способность применена к слоту (может понадобиться для анимации).
            onAbilityTargetSlot?.Invoke(iability, caster, target);
        }

        protected virtual void ResolveEffectTarget(AbilityData iability, Card caster, CardData target) // 📌 Этот метод применяется, если целью способности является не конкретная карта, а её тип (например, "Все воины").
        {
            // Выполняем эффекты способности, применяя их к данным карт (например, всем картам типа "Маг")
            iability.DoEffects(this, caster, target);

            // ❌ Здесь нет вызова события, так как способность применяется не к конкретному объекту, а к данным.
        }
        #endregion

        protected virtual void AfterAbilityResolved(AbilityData iability, Card caster) // 📌  Этот метод используется после срабатывания способности
        {
            // 📌 Получаем игрока, которому принадлежит карта, использовавшая способность
            Player player = game_data.GetPlayer(caster.player_id);

            // 🔹 Оплата стоимости способности
            if (iability.trigger == AbilityTrigger.Activate || iability.trigger == AbilityTrigger.None)
            {
                player.mana -= iability.mana_cost; // Вычитаем стоимость способности из маны игрока
                caster.exhausted = caster.exhausted || iability.exhaust; // Уставляет карту, если это требуется (некоторые способности можно использовать только раз за ход)
            }

            // 🔄 Пересчёт эффектов (например, если способность изменила характеристики карт)
            UpdateOngoing();

            // 🏆 Проверяем, не закончилась ли игра (например, если способность убила последнего противника)
            CheckForWinner();

            // 🔗 Запуск связанных способностей (цепочка способностей)
            // Если это не выбор альтернативных эффектов (ChoiceSelector) и игра ещё не завершена
            if (iability.target != AbilityTarget.ChoiceSelector && game_data.state != GameState.GameEnded)
            {
                foreach (AbilityData chain_ability in iability.chain_abilities) // Перебираем все связанные способности
                {
                    if (chain_ability != null) // Если связанная способность существует
                    {
                        TriggerCardAbility(chain_ability, caster); // Активируем её
                    }
                }
            }

            // 🎬 Сообщаем всем объектам в игре, что способность завершена
            onAbilityEnd?.Invoke(iability, caster);

            // ⏳ Разрешаем все эффекты с небольшой задержкой (0.5 секунды)
            resolve_queue.ResolveAll(0.5f);

            // 🔄 Обновляем данные игры (например, обновляем интерфейс, если нужно)
            RefreshData();
        }
        #endregion

        #region Оngoing abilities
        //This function is called often to update status/stats affected by ongoing abilities
        //It basically first reset the bonus to 0 (CleanOngoing) and then recalculate it to make sure it it still present
        //Only cards in hand and on board are updated in this way
        public virtual void UpdateOngoing()
        {
            // 🔍 Начинаем замер производительности для профилирования
            Profiler.BeginSample("Update Ongoing");

            // 🔄 Обновляем все текущие эффекты у карт (например, изменение атаки, здоровья и статусов)
            UpdateOngoingCards();

            // 💀 Удаляем карты с нулевым здоровьем (если они должны быть уничтожены)
            UpdateOngoingKills();

            // ⏹️ Завершаем замер производительности
            Profiler.EndSample();
        }

        protected virtual void UpdateOngoingCards() // 🔄 Этот метод обновляет все текущие эффекты у карт (например, изменение атаки, здоровья и статусов)
        {
            // 🔄 Очистка всех временных эффектов у карт перед перерасчётом
            for (int p = 0; p < game_data.players.Length; p++) // Перебираем всех игроков
            {
                Player player = game_data.players[p];

                // Очищаем временные эффекты у игрока
                player.ClearOngoing();

                // Очищаем временные эффекты у всех карт на столе
                for (int c = 0; c < player.cards_board.Count; c++)
                    player.cards_board[c].ClearOngoing();

                // Очищаем временные эффекты у всех экипированных карт
                for (int c = 0; c < player.cards_equip.Count; c++)
                    player.cards_equip[c].ClearOngoing();

                // Очищаем временные эффекты у карт в руке
                for (int c = 0; c < player.cards_hand.Count; c++)
                    player.cards_hand[c].ClearOngoing();
            }

            // 🔄 Повторно применяем эффекты от карт и героев
            for (int p = 0; p < game_data.players.Length; p++) // Перебираем всех игроков
            {
                Player player = game_data.players[p];

                // Применяем эффекты способностей у героя игрока (если у него есть способности)
                UpdateOngoingAbilities(player, player.hero);

                // Применяем эффекты у карт на поле (боевые существа)
                for (int c = 0; c < player.cards_board.Count; c++)
                {
                    Card card = player.cards_board[c];
                    UpdateOngoingAbilities(player, card);
                }

                // Применяем эффекты у экипировки (если карты экипировки дают баффы)
                for (int c = 0; c < player.cards_equip.Count; c++)
                {
                    Card card = player.cards_equip[c];
                    UpdateOngoingAbilities(player, card);
                }
            }

            // 🔄 Применяем бонусы от статусов (например, повышение атаки, добавление брони)
            for (int p = 0; p < game_data.players.Length; p++) // Перебираем всех игроков
            {
                Player player = game_data.players[p];

                for (int c = 0; c < player.cards_board.Count; c++) // Обрабатываем карты на поле
                {
                    Card card = player.cards_board[c];

                    // 🛡️ Применение эффекта "Защита" (Taunt)
                    if (card.HasStatus(StatusType.Protection) && !card.HasStatus(StatusType.Stealth))
                    {
                        // Добавляем игроку статус "Защищён"
                        player.AddOngoingStatus(StatusType.Protected, 0);

                        // Перебираем все карты на поле и добавляем защиту тем, у кого её нет
                        for (int tc = 0; tc < player.cards_board.Count; tc++)
                        {
                            Card tcard = player.cards_board[tc];
                            if (!tcard.HasStatus(StatusType.Protection) && !tcard.HasStatus(StatusType.Protected))
                            {
                                tcard.AddOngoingStatus(StatusType.Protected, 0);
                            }
                        }
                    }

                    // 📊 Применяем бонусы от статусов карты (например, +атака, +здоровье)
                    foreach (CardStatus status in card.status)
                        AddOngoingStatusBonus(card, status); // Применяем стандартные бонусы

                    foreach (CardStatus status in card.ongoing_status)
                        AddOngoingStatusBonus(card, status); // Применяем временные бонусы
                }

                // 🔄 Применяем бонусы для карт в руке (если такие эффекты есть)
                for (int c = 0; c < player.cards_hand.Count; c++)
                {
                    Card card = player.cards_hand[c];

                    // 📊 Применяем бонусы от статусов карты в руке
                    foreach (CardStatus status in card.status)
                        AddOngoingStatusBonus(card, status);

                    foreach (CardStatus status in card.ongoing_status)
                        AddOngoingStatusBonus(card, status);
                }
            }
        }

        protected virtual void UpdateOngoingKills() // 💀 Этот метод удаляет карты с нулевым здоровьем (если они должны быть уничтожены)
        {
            // 🔄 Удаляем карты с нулевым или отрицательным здоровьем
            for (int p = 0; p < game_data.players.Length; p++) // Перебираем всех игроков
            {
                Player player = game_data.players[p];

                // 🔍 Проверяем все карты на поле
                for (int i = player.cards_board.Count - 1; i >= 0; i--) // Перебираем карты с конца списка
                {
                    if (i < player.cards_board.Count) // Проверяем, что индекс в допустимых пределах
                    {
                        Card card = player.cards_board[i];

                        // 💀 Если у карты 0 или меньше HP, отправляем её в сброс
                        if (card.GetHP() <= 0)
                            DiscardCard(card);
                    }
                }

                // 🔍 Проверяем все экипированные карты (например, оружие, броню)
                for (int i = player.cards_equip.Count - 1; i >= 0; i--) // Перебираем с конца
                //🛠 Почему важно проверять карты с конца списка?
                //При удалении элементов из списка изменяется его длина.
                //Если мы шли бы в прямом порядке (от 0 до Count), то после удаления карта могла бы сместить остальные элементы, и индекс стал бы некорректным.
                {
                    if (i < player.cards_equip.Count) // Проверяем корректность индекса
                    {
                        Card card = player.cards_equip[i];

                        // 💀 Если у экипированной карты 0 или меньше HP, удаляем её
                        if (card.GetHP() <= 0)
                            DiscardCard(card);

                        // 🔄 Проверяем, есть ли у экипировки владелец (например, оружие без героя)
                        Card bearer = player.GetBearerCard(card);
                        if (bearer == null) // Если владелец отсутствует, удаляем карту
                            DiscardCard(card);
                    }
                }
            }

            // 🗑️ Полностью очищаем список карт, которые нужно убрать (например, те, которые умерли в этом ходе). 
            //Этот список собирается при сбросе карт
            //Этот список собирает карты, которые нужно окончательно удалить после всех проверок.
            //Мы очищаем его в конце, чтобы избежать ошибок при одновременном изменении списка карт.
            for (int c = 0; c < cards_to_clear.Count; c++)
                cards_to_clear[c].Clear();

            // ⏹️ Полностью очищаем список карт на удаление
            //Это гарантирует, что все карты, которые были удалены в этом ходу, не вызовут неожиданных багов при следующем обновлении игры.
            cards_to_clear.Clear();
        }

        protected virtual void UpdateOngoingAbilities(Player player, Card card) //Этот метод обновляет постоянные способности (Ongoing) всех карт, проверяя, на кого они действуют и активируя соответствующие эффекты.
        //Например, метод автоматически обновляет пассивные эффекты (например, "Все ваши существа получают +1 к атаке").
        {
            // 🛑 Проверяем, существует ли карта и может ли она активировать способности
            if (card == null || !card.CanDoAbilities())
                return; // Если карта не может активировать способности, выходим из метода

            // 📜 Получаем список всех способностей карты
            List<AbilityData> cabilities = card.GetAbilities();

            // 🔄 Перебираем все способности карты
            for (int a = 0; a < cabilities.Count; a++)
            {
                AbilityData ability = cabilities[a];

                // 📌 Проверяем, что способность существует и является "Постоянным эффектом" (Ongoing)
                if (ability != null && ability.trigger == AbilityTrigger.Ongoing && ability.AreTriggerConditionsMet(game_data, card))
                {
                    // 🔹 Если способность действует на саму карту
                    if (ability.target == AbilityTarget.Self)
                    {
                        if (ability.AreTargetConditionsMet(game_data, card, card))
                        {
                            ability.DoOngoingEffects(this, card, card);
                        }
                    }

                    // 🔹 Если способность действует на владельца карты (игрока)
                    if (ability.target == AbilityTarget.PlayerSelf)
                    {
                        if (ability.AreTargetConditionsMet(game_data, card, player))
                        {
                            ability.DoOngoingEffects(this, card, player);
                        }
                    }

                    // 🔹 Если способность действует на всех игроков или только на противника
                    if (ability.target == AbilityTarget.AllPlayers || ability.target == AbilityTarget.PlayerOpponent)
                    {
                        for (int tp = 0; tp < game_data.players.Length; tp++)
                        {
                            // Если способность применяется ко всем игрокам или к противнику
                            if (ability.target == AbilityTarget.AllPlayers || tp != player.player_id)
                            {
                                Player oplayer = game_data.players[tp];
                                if (ability.AreTargetConditionsMet(game_data, card, oplayer))
                                {
                                    ability.DoOngoingEffects(this, card, oplayer);
                                }
                            }
                        }
                    }

                    // 🔹 Если способность действует на экипированную карту (оружие, броню и т.д.)
                    if (ability.target == AbilityTarget.EquippedCard)
                    {
                        if (card.CardData.IsEquipment())
                        {
                            // 🔍 Ищем владельца экипировки (например, герой с оружием)
                            Card target = player.GetBearerCard(card);
                            if (target != null && ability.AreTargetConditionsMet(game_data, card, target))
                            {
                                ability.DoOngoingEffects(this, card, target);
                            }
                        }
                        else if (card.equipped_uid != null)
                        {
                            // 🔍 Если карта сама является экипировкой, получаем её владельца
                            Card target = game_data.GetCard(card.equipped_uid);
                            if (target != null && ability.AreTargetConditionsMet(game_data, card, target))
                            {
                                ability.DoOngoingEffects(this, card, target);
                            }
                        }
                    }

                    // 🔹 Если способность действует на все карты в игре (рука, доска, экипировка)
                    if (ability.target == AbilityTarget.AllCardsAllPiles || ability.target == AbilityTarget.AllCardsHand || ability.target == AbilityTarget.AllCardsBoard)
                    {
                        for (int tp = 0; tp < game_data.players.Length; tp++) // Перебираем всех игроков
                        {
                            Player tplayer = game_data.players[tp];

                            // 🃏 Если способность действует на карты в руке
                            if (ability.target == AbilityTarget.AllCardsAllPiles || ability.target == AbilityTarget.AllCardsHand)
                            {
                                for (int tc = 0; tc < tplayer.cards_hand.Count; tc++)
                                {
                                    Card tcard = tplayer.cards_hand[tc];
                                    if (ability.AreTargetConditionsMet(game_data, card, tcard))
                                    {
                                        ability.DoOngoingEffects(this, card, tcard);
                                    }
                                }
                            }

                            // 🏟️ Если способность действует на карты на поле
                            if (ability.target == AbilityTarget.AllCardsAllPiles || ability.target == AbilityTarget.AllCardsBoard)
                            {
                                for (int tc = 0; tc < tplayer.cards_board.Count; tc++)
                                {
                                    Card tcard = tplayer.cards_board[tc];
                                    if (ability.AreTargetConditionsMet(game_data, card, tcard))
                                    {
                                        ability.DoOngoingEffects(this, card, tcard);
                                    }
                                }
                            }

                            // 🛡️ Если способность действует на экипированные карты
                            if (ability.target == AbilityTarget.AllCardsAllPiles)
                            {
                                for (int tc = 0; tc < tplayer.cards_equip.Count; tc++)
                                {
                                    Card tcard = tplayer.cards_equip[tc];
                                    if (ability.AreTargetConditionsMet(game_data, card, tcard))
                                    {
                                        ability.DoOngoingEffects(this, card, tcard);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        protected virtual void AddOngoingStatusBonus(Card card, CardStatus status) // Этот метод добавляет временные бонусы, пересчитываемые каждый ход.
        //Основные параметры карты (attack, hp, mana) остаются неизменными.
        {
            // 🔍 Проверяем тип статуса и добавляем соответствующий бонус

            // 📌 Если статус увеличивает атаку карты
            if (status.type == StatusType.AddAttack)
                card.attack_ongoing += status.value; // Добавляем значение статуса к текущему бонусу атаки

            // 📌 Если статус увеличивает максимальное здоровье карты
            if (status.type == StatusType.AddHP)
                card.hp_ongoing += status.value; // Добавляем значение статуса к текущему бонусу здоровья

            // 📌 Если статус увеличивает стоимость маны карты
            if (status.type == StatusType.AddManaCost)
                card.mana_ongoing += status.value; // Добавляем значение статуса к текущему бонусу стоимости маны
        }

        #endregion

        #region Secrets

        public virtual bool TriggerPlayerSecrets(Player player, AbilityTrigger secret_trigger) //Этот метод проверяет и активирует "секретные" карты у конкретного игрока.
        {
            // 🔍 Проходим по секретным картам игрока в обратном порядке (чтобы удаление не сдвигало индексы)
            for (int i = player.cards_secret.Count - 1; i >= 0; i--)
            {
                Card card = player.cards_secret[i]; // Получаем секретную карту
                CardData icard = card.CardData; // Получаем данные карты

                // 📌 Проверяем, что карта является секретом и не была использована ранее (не истощена)
                if (icard.type == CardType.Secret && !card.exhausted)
                {
                    // 🛡️ Проверяем, выполняются ли условия активации секрета
                    if (card.AreAbilityConditionsMet(secret_trigger, game_data, card, card))
                    {
                        // ➕ Добавляем секрет в очередь выполнения эффектов
                        //Он не срабатывает сразу, а ждет своей очереди для применения эффекта.
                        resolve_queue.AddSecret(secret_trigger, card, card, ResolveSecret);

                        // ⏳ Задаем небольшую задержку перед срабатыванием секрета (0.5 сек)
                        resolve_queue.SetDelay(0.5f);

                        // 🚫 Отмечаем, что секрет был активирован и больше не может сработать в этом ходу
                        card.exhausted = true;

                        // 🔥 Вызываем событие, сигнализирующее об активации секрета
                        if (onSecretTrigger != null)
                            onSecretTrigger.Invoke(card, card);

                        return true; // ❗ Срабатывает только один секрет за раз, поэтому прерываем цикл
                    }
                }
            }
            return false; // 🔄 Если ни один секрет не сработал, возвращаем false
        }

        public virtual bool TriggerSecrets(AbilityTrigger secret_trigger, Card trigger_card) //Этот метод ищет и активирует секретные карты противника, если они соответствуют условиям. Проверяет всех противников сразу
        {
            // 📌 Проверяем, не имеет ли карта, вызвавшая событие, иммунитет к заклинаниям.
            // Если у карты есть статус "Spell Immunity", то секрет не срабатывает.
            if (trigger_card != null && trigger_card.HasStatus(StatusType.SpellImmunity))
                return false; // ❌ У карты иммунитет к заклинаниям, поэтому секреты не активируются.

            // 🔄 Перебираем всех игроков в игре.
            for (int p = 0; p < game_data.players.Length; p++)
            {
                // 🎯 Проверяем, что это НЕ текущий игрок (срабатывают только секреты противника).
                if (p != game_data.current_player)
                {
                    Player other_player = game_data.players[p]; // Получаем противника.

                    // 🔄 Перебираем все секретные карты противника (в обратном порядке, чтобы избежать смещения индексов при удалении карт).
                    for (int i = other_player.cards_secret.Count - 1; i >= 0; i--)
                    {
                        Card card = other_player.cards_secret[i]; // Получаем секретную карту.
                        CardData icard = card.CardData; // Получаем данные карты.

                        // 📌 Проверяем, что карта является секретом и не была использована ранее (не истощена).
                        if (icard.type == CardType.Secret && !card.exhausted)
                        {
                            // 🎯 Определяем, кто является триггером (если trigger_card не null, то он, иначе сам секрет).
                            Card trigger = trigger_card != null ? trigger_card : card;

                            // ✅ Проверяем, выполняются ли условия активации секрета.
                            if (card.AreAbilityConditionsMet(secret_trigger, game_data, card, trigger))
                            {
                                // ➕ Добавляем секрет в очередь выполнения.
                                resolve_queue.AddSecret(secret_trigger, card, trigger, ResolveSecret);

                                // ⏳ Устанавливаем задержку перед срабатыванием секрета (0.5 сек).
                                resolve_queue.SetDelay(0.5f);

                                // 🚫 Отмечаем, что секрет был активирован и больше не может сработать в этом ходу.
                                card.exhausted = true;

                                // 🔥 Вызываем событие, сигнализирующее об активации секрета.
                                if (onSecretTrigger != null)
                                    onSecretTrigger.Invoke(card, trigger);

                                return true; // ❗ Срабатывает только один секрет за раз, поэтому прерываем цикл.
                            }
                        }
                    }
                }
            }
            return false; // 🔄 Если ни один секрет не сработал, возвращаем false.
        }

        protected virtual void ResolveSecret(AbilityTrigger secret_trigger, Card secret_card, Card trigger) //Этот метод активирует эффект секретной карты, если она сработала. В отличие от TriggerSecrets, который ставит подходящие секреты в очередь, применяет найденный секрет
        {
            // 📌 Получаем данные карты секрета.
            CardData icard = secret_card.CardData;

            // 📌 Получаем игрока, которому принадлежит секретная карта.
            Player player = game_data.GetPlayer(secret_card.player_id);

            // ✅ Проверяем, действительно ли карта является секретом.
            if (icard.type == CardType.Secret)
            {
                // 📌 Получаем игрока, который вызвал срабатывание секрета.
                Player tplayer = game_data.GetPlayer(trigger.player_id);

                // 📜 Если это не предсказание для ИИ, записываем событие в историю.
                if (!is_ai_predict)
                    tplayer.AddHistory(GameAction.SecretTriggered, secret_card, trigger);

                // 🔥 Активируем способности секретной карты.
                TriggerCardAbilityType(secret_trigger, secret_card, trigger);

                // 🗑️ После активации карта с секретом уничтожается (отправляется в сброс).
                DiscardCard(secret_card);

                // 📢 Вызываем событие, сигнализирующее о разрешении секрета.
                if (onSecretResolve != null)
                    onSecretResolve.Invoke(secret_card, trigger); //Это событие, уведомляющее другие части кода о том, что секрет сработал.
                                                                  //Оно может использоваться для анимации, звуков, логов в интерфейсе и т.д.
            }
        }

        #endregion

        #region Resolve Selector

        public virtual void SelectCard(Card target) // 📌 Метод для выбора карты в качестве цели способности.
        {
            // ✅ Если сейчас нет активного выбора, прерываем выполнение.
            if (game_data.selector == SelectorType.None)
                return;

            // 📌 Получаем карту, которая использует способность (кастер).
            Card caster = game_data.GetCard(game_data.selector_caster_uid);

            // 📌 Получаем данные способности, которая требует выбора цели.
            AbilityData ability = AbilityData.Get(game_data.selector_ability_id);

            // ✅ Если кастер, цель или способность отсутствуют — прерываем выполнение.
            if (caster == null || target == null || ability == null)
                return;

            // 🎯 Если это выбор цели для способности (обычная нацеленная способность).
            if (game_data.selector == SelectorType.SelectTarget) //SelectTarget — обычный выбор цели, например, для атак, лечения, заклинаний.
            {
                // ✅ Проверяем, может ли эта способность нацеливаться на выбранную карту.
                if (!ability.CanTarget(game_data, caster, target))
                    return; // ❌ Если цель не подходит, просто выходим.

                // 📜 Добавляем запись в историю хода (если это не предсказание ИИ).
                Player player = game_data.GetPlayer(caster.player_id);
                if (!is_ai_predict)
                    player.AddHistory(GameAction.CastAbility, caster, ability, target);

                // ✅ Завершаем выбор цели.
                game_data.selector = SelectorType.None;
                game_data.last_target = target.uid; // Сохраняем последнюю цель.

                // 🔥 Применяем эффект способности к выбранной карте.
                ResolveEffectTarget(ability, caster, target);

                // 🎯 Завершаем применение способности.
                AfterAbilityResolved(ability, caster);

                // 🕒 Запускаем очередь разрешения действий.
                resolve_queue.ResolveAll();
            }

            // 🃏 Если это особый режим выбора карты (например, из руки или из колоды).
            if (game_data.selector == SelectorType.SelectorCard) //SelectorCard — выбор карты из определенной зоны, например, из руки или сброшенных карт.
            {
                // ✅ Проверяем, удовлетворяет ли выбранная карта условиям выбора.
                if (!ability.IsCardSelectionValid(game_data, caster, target, card_array))
                    return; // ❌ Если карта не подходит, просто выходим.

                // ✅ Завершаем выбор.
                game_data.selector = SelectorType.None;
                game_data.last_target = target.uid; // Сохраняем последнюю цель.

                // 🔥 Применяем эффект способности к выбранной карте.
                ResolveEffectTarget(ability, caster, target);

                // 🎯 Завершаем применение способности.
                AfterAbilityResolved(ability, caster);

                // 🕒 Запускаем очередь разрешения действий.
                resolve_queue.ResolveAll();
            }
        }

        public virtual void SelectPlayer(Player target) // 📌 Метод для выбора игрока в качестве цели способности.
        {
            // ✅ Если нет активного выбора, прерываем выполнение.
            if (game_data.selector == SelectorType.None)
                return;

            // 📌 Получаем карту, которая использует способность (кастер).
            Card caster = game_data.GetCard(game_data.selector_caster_uid);

            // 📌 Получаем данные способности, которая требует выбора цели.
            AbilityData ability = AbilityData.Get(game_data.selector_ability_id);

            // ✅ Если кастер, цель или способность отсутствуют — прерываем выполнение.
            if (caster == null || target == null || ability == null)
                return;

            // 🎯 Если это обычный выбор цели (игрока).
            if (game_data.selector == SelectorType.SelectTarget)
            {
                // ✅ Проверяем, может ли способность нацеливаться на выбранного игрока.
                if (!ability.CanTarget(game_data, caster, target))
                    return; // ❌ Если цель не подходит, просто выходим.

                // 📜 Добавляем запись в историю хода (если это не предсказание ИИ).
                Player player = game_data.GetPlayer(caster.player_id);
                if (!is_ai_predict)
                    player.AddHistory(GameAction.CastAbility, caster, ability, target);

                // ✅ Завершаем выбор.
                game_data.selector = SelectorType.None;

                // 🔥 Применяем эффект способности к выбранному игроку.
                ResolveEffectTarget(ability, caster, target);

                // 🎯 Завершаем применение способности.
                AfterAbilityResolved(ability, caster);

                // 🕒 Запускаем очередь разрешения действий.
                resolve_queue.ResolveAll();
            }
        }

        public virtual void SelectSlot(Slot target) // 📌 Метод для выбора слота (позиции на поле) в качестве цели способности.
        {
            // ✅ Проверяем, активен ли режим выбора. Если нет, ничего не делаем.
            if (game_data.selector == SelectorType.None)
                return;

            // 📌 Получаем карту, которая использует способность (кастер).
            Card caster = game_data.GetCard(game_data.selector_caster_uid);

            // 📌 Получаем данные способности, которая требует выбора цели.
            AbilityData ability = AbilityData.Get(game_data.selector_ability_id);

            // ✅ Если кастер, способность или слот недействительны — прерываем выполнение.
            if (caster == null || ability == null || !target.IsValid())
                return;

            // 🎯 Если это обычный выбор цели для способности.
            if (game_data.selector == SelectorType.SelectTarget)
            {
                // ✅ Проверяем, можно ли нацелить способность на этот слот.
                if (!ability.CanTarget(game_data, caster, target))
                    return; // ❌ Если цель не подходит, просто выходим.

                // 📜 Добавляем запись в историю хода (если это не предсказание ИИ).
                Player player = game_data.GetPlayer(caster.player_id);
                if (!is_ai_predict)
                    player.AddHistory(GameAction.CastAbility, caster, ability, target);

                // ✅ Завершаем выбор.
                game_data.selector = SelectorType.None;

                // 🔥 Применяем эффект способности к выбранному слоту.
                ResolveEffectTarget(ability, caster, target);

                // 🎯 Завершаем применение способности.
                AfterAbilityResolved(ability, caster);

                // 🕒 Запускаем очередь разрешения действий.
                resolve_queue.ResolveAll();
            }
        }

        public virtual void SelectChoice(int choice) // 📌 Метод для выбора одного из возможных вариантов способности. Этот метод используется, когда игроку дается выбор из нескольких вариантов (например, "Выбери одно: Нанести урон или Лечить").
        {
            // ✅ Проверяем, активен ли режим выбора. Если нет, ничего не делаем.
            if (game_data.selector == SelectorType.None)
                return;

            // 📌 Получаем карту, которая использует способность (кастер).
            Card caster = game_data.GetCard(game_data.selector_caster_uid);

            // 📌 Получаем данные способности, которая требует выбора варианта.
            AbilityData ability = AbilityData.Get(game_data.selector_ability_id);

            // ✅ Если кастер, способность или выбор недействительны — прерываем выполнение.
            if (caster == null || ability == null || choice < 0)
                return;

            // 🎯 Если активен выбор одного из нескольких вариантов способности.
            if (game_data.selector == SelectorType.SelectorChoice && ability.target == AbilityTarget.ChoiceSelector)
            {
                // ✅ Проверяем, существует ли выбранный вариант способности.
                if (choice >= 0 && choice < ability.chain_abilities.Length)
                {
                    AbilityData achoice = ability.chain_abilities[choice];

                    // ✅ Проверяем, можно ли выбрать этот вариант.
                    if (achoice != null && game_data.CanSelectAbility(caster, achoice))
                    {
                        // ✅ Завершаем выбор.
                        game_data.selector = SelectorType.None;

                        // 🎯 Завершаем основную способность.
                        AfterAbilityResolved(ability, caster);

                        // 🔥 Запускаем выполнение выбранного варианта способности.
                        ResolveCardAbility(achoice, caster, caster);

                        // 🕒 Запускаем очередь разрешения действий.
                        resolve_queue.ResolveAll();
                    }
                }
            }
        }

        public virtual void SelectCost(int select_cost) //Этот метод используется, когда у игрока есть возможность выбрать, сколько маны он хочет потратить на розыгрыш карты.
        {
            // ✅ Проверяем, активен ли режим выбора. Если нет, ничего не делаем.
            if (game_data.selector == SelectorType.None)
                return;

            // 📌 Получаем игрока, который делает выбор.
            Player player = game_data.GetPlayer(game_data.selector_player_id);

            // 📌 Получаем карту, которая использует способность (кастер).
            Card caster = game_data.GetCard(game_data.selector_caster_uid);

            // ✅ Если игрок, кастер или введенное значение недействительны — прерываем выполнение.
            if (player == null || caster == null || select_cost < 0)
                return;

            // 🎯 Проверяем, активен ли выбор стоимости (мана-стоимости).
            if (game_data.selector == SelectorType.SelectorCost)
            {
                // ✅ Проверяем, что:
                // - Выбранная стоимость находится в диапазоне от 0 до 9 (максимум 10)
                // - У игрока хватает маны для оплаты
                if (select_cost >= 0 && select_cost < 10 && select_cost <= player.mana)
                {
                    // ✅ Завершаем режим выбора.
                    game_data.selector = SelectorType.None;

                    // 💰 Фиксируем выбранное количество маны.
                    game_data.selected_value = select_cost;

                    // 💳 Списываем потраченные очки маны у игрока.
                    player.mana -= select_cost;

                    // 🔄 Обновляем интерфейс и состояние игры.
                    RefreshData();

                    // 🎯 Активируем скрытые секреты и способности, которые срабатывают при розыгрыше карты.
                    TriggerSecrets(AbilityTrigger.OnPlayOther, caster);
                    TriggerCardAbilityType(AbilityTrigger.OnPlay, caster);
                    TriggerOtherCardsAbilityType(AbilityTrigger.OnPlayOther, caster);

                    // 🕒 Запускаем очередь разрешения действий.
                    resolve_queue.ResolveAll();
                }
            }
        }

        public virtual void CancelSelection() // 📌 Отмена выбора в игре
        {
            // ✅ Проверяем, активен ли режим выбора (то есть игрок сейчас что-то выбирает)
            if (game_data.selector != SelectorType.None)
            {
                // 🛑 Если игрок выбирал стоимость карты, отменяем розыгрыш карты
                if (game_data.selector == SelectorType.SelectorCost)
                    CancelPlayCard();

                // 🚫 Завершаем режим выбора (очищаем выбранный тип селектора)
                game_data.selector = SelectorType.None;

                // 🔄 Обновляем интерфейс и состояние игры
                RefreshData();
            }
        }

        public void CancelPlayCard() // 📌 Отмена розыгрыша карты (если игрок передумал играть карту)
        {
            // 🃏 Получаем карту, которую игрок пытался сыграть
            Card card = game_data.GetCard(game_data.selector_caster_uid);

            // ✅ Проверяем, существует ли эта карта
            if (card != null)
            {
                // 🎮 Получаем игрока, который пытался разыграть карту
                Player player = game_data.GetPlayer(card.player_id);

                // 💰 Возвращаем потраченную ману:
                // Если карта имеет **динамическую стоимость**, возвращаем именно ту ману, что была потрачена.
                if (card.CardData.IsDynamicManaCost())
                    player.mana += game_data.selected_value;
                else
                    // Иначе возвращаем **стандартную стоимость карты**
                    player.mana += card.CardData.cost;

                // ❌ Удаляем карту из всех игровых групп (руки, стола, сброса и т. д.)
                player.RemoveCardFromAllGroups(card);

                // 🔄 Возвращаем карту в руку игрока
                player.AddCard(player.cards_hand, card);

                // 🧹 Очищаем временные данные карты
                card.Clear();
            }
        }

        public virtual void Mulligan(Player player, string[] cards) // 📌 Метод "Mulligan" позволяет игроку заменить некоторые стартовые карты перед началом игры.

        {
            // 🛑 Проверяем, идет ли сейчас фаза "муллигана" и не подтвердил ли игрок уже выбор карт.
            if (game_data.phase == GamePhase.Mulligan && !player.ready)
            {
                // 🔢 Счетчик количества карт, которые игрок заменит.
                int count = 0;

                // 📋 Список карт, которые игрок хочет заменить.
                List<Card> remove_list = new List<Card>();

                // 🔄 Перебираем все карты в руке игрока.
                foreach (Card card in player.cards_hand)
                {
                    // ✅ Если ID карты есть в списке "cards" (то есть игрок хочет ее заменить), добавляем в список удаления.
                    if (cards.Contains(card.uid))
                    {
                        remove_list.Add(card);
                        count++; // Увеличиваем счетчик замененных карт.
                    }
                }

                // 🔄 Удаляем выбранные игроком карты.
                foreach (Card card in remove_list)
                {
                    player.RemoveCardFromAllGroups(card); // Удаляем карту из всех групп (например, из руки).
                    player.cards_discard.Add(card);       // Добавляем карту в сброс.
                }

                // ✅ Отмечаем, что игрок готов (выбрал карты для замены).
                player.ready = true;

                // 🔄 Добираем столько же карт, сколько было сброшено.
                DrawCard(player, count);

                // 🔄 Обновляем интерфейс и данные игры.
                RefreshData();

                // 🔎 Проверяем, готовы ли все игроки.
                if (game_data.AreAllPlayersReady())
                {
                    // 🚀 Если все игроки завершили муллиган, начинается первый ход.
                    StartTurn();
                }
            }
        }

        #endregion

        #region Trigger Selector // 🔹 Секция кода, которая отвечает за переход в различные режимы выбора в игре:

        protected virtual void GoToSelectTarget(AbilityData iability, Card caster) // 📌 Метод переводит игру в режим выбора цели для способности.
        {
            // 🔄 Устанавливаем тип селектора в "Выбор цели"
            game_data.selector = SelectorType.SelectTarget;

            // 🏆 Запоминаем ID игрока, который использует способность
            game_data.selector_player_id = caster.player_id;

            // 🃏 Запоминаем ID способности, которую игрок хочет использовать
            game_data.selector_ability_id = iability.id;

            // 🔢 Запоминаем UID карты, которая применяет способность
            game_data.selector_caster_uid = caster.uid;

            // 🔄 Обновляем интерфейс и игровые данные
            RefreshData();
        }

        protected virtual void GoToSelectorCard(AbilityData iability, Card caster) // 📌 Метод переводит игру в режим выбора карты (например, если карта требует выбора другой карты для эффекта).
        {
            // 🔄 Устанавливаем тип селектора в "Выбор карты"
            game_data.selector = SelectorType.SelectorCard;

            // 🏆 Запоминаем ID игрока, который использует способность
            game_data.selector_player_id = caster.player_id;

            // 🃏 Запоминаем ID способности, которую игрок хочет использовать
            game_data.selector_ability_id = iability.id;

            // 🔢 Запоминаем UID карты, которая применяет способность
            game_data.selector_caster_uid = caster.uid;

            // 🔄 Обновляем интерфейс и игровые данные
            RefreshData();
        }

        protected virtual void GoToSelectorChoice(AbilityData iability, Card caster) // 📌 Метод переводит игру в режим выбора одного из нескольких возможных эффектов способности.
        {
            // 🔄 Устанавливаем тип селектора в "Выбор эффекта"
            game_data.selector = SelectorType.SelectorChoice;

            // 🏆 Запоминаем ID игрока, который использует способность
            game_data.selector_player_id = caster.player_id;

            // 🃏 Запоминаем ID способности, которую игрок хочет использовать
            game_data.selector_ability_id = iability.id;

            // 🔢 Запоминаем UID карты, которая применяет способность
            game_data.selector_caster_uid = caster.uid;

            // 🔄 Обновляем интерфейс и игровые данные
            RefreshData();
        }

        protected virtual void GoToSelectorCost(Card caster) // 📌 Метод переводит игру в режим выбора стоимости для динамических способностей.
        {
            // 🔄 Устанавливаем тип селектора в "Выбор стоимости"
            game_data.selector = SelectorType.SelectorCost;

            // 🏆 Запоминаем ID игрока, который использует способность
            game_data.selector_player_id = caster.player_id;

            // ❌ Способность не привязана к конкретному ID, поэтому оставляем пустым
            game_data.selector_ability_id = "";

            // 🔢 Запоминаем UID карты, которая применяет способность
            game_data.selector_caster_uid = caster.uid;

            // 🔢 Обнуляем выбранное значение стоимости
            game_data.selected_value = 0;

            // 🔄 Обновляем интерфейс и игровые данные
            RefreshData();
        }

        protected virtual void GoToMulligan() // 📌 Метод переводит игру в фазу муллигана (обмена стартовых карт).

        {
            // 🔄 Устанавливаем текущую фазу игры как "Муллиган"
            game_data.phase = GamePhase.Mulligan;

            // ⏳ Устанавливаем таймер хода для муллигана (берем значение из игровых настроек)
           //game_data.turn_timer = GameplayData.Get().turn_duration;

            // 🔄 Отмечаем, что все игроки еще не подтвердили свой муллиган
            foreach (Player player in game_data.players)
                player.ready = false;

            // 🔄 Обновляем интерфейс и игровые данные
            RefreshData();
        }

        #endregion

        public virtual void RefreshData() // 📌 Обновляет данные игры и интерфейс
        {
            onRefresh?.Invoke(); // 🔄 Вызывает событие обновления данных, если есть подписчики
        }

        public virtual void ClearResolve() // 📌 Очищает очередь выполнения игровых эффектов и действий
        {
            resolve_queue.Clear(); // 🔄 Удаляет все запланированные в очереди эффекты
        }

        public virtual bool IsResolving() // 📌 Проверяет, есть ли в данный момент нерешенные игровые эффекты
        {
            return resolve_queue.IsResolving(); // 🔍 Возвращает true, если очередь действий все еще выполняется
        }

        public virtual bool IsGameStarted() // 📌 Проверяет, началась ли игра
        {
            return game_data.HasStarted(); // 🔍 Вызывает метод, который проверяет, была ли игра запущена
        }

        public virtual bool IsGameEnded() // 📌 Проверяет, закончилась ли игра
        {
            return game_data.HasEnded(); // 🔍 Вызывает метод, который проверяет, завершилась ли игра
        }

        public virtual Game GetGameData() // 📌 Возвращает объект, содержащий все данные текущей игры
        {
            return game_data; // 📜 Возвращает объект `game_data`, содержащий текущее состояние игры
        }

        public System.Random GetRandom() // 📌 Возвращает объект генератора случайных чисел
        {
            return random; // 🎲 Возвращает экземпляр `Random`, используемый для генерации случайных чисел в игре
        }

        public Game GameData // 📌 Свойство, предоставляющее доступ к данным игры
        {
            get { return game_data; } // 📜 Геттер, возвращает объект `game_data`
        }

        public ResolveQueue ResolveQueue // 📌 Свойство, предоставляющее доступ к очереди выполнения эффектов
        {
            get { return resolve_queue; } // 📜 Геттер, возвращает объект `resolve_queue`
        }

    }
}