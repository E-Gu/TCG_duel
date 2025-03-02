
using Unity.Netcode;
using UnityEngine.Events;

namespace TcgEngine
{
    //✔ Этот скрипт упрощает сетевое взаимодействие между клиентом и сервером.
    //✔ Коды действий гарантируют точную передачу команд без ошибок.
    //✔ GetString() помогает конвертировать коды в текст для отладки или логирования.
    //🚀 Используется для синхронизации игровых событий в карточной игре.
    public static class GameAction // 📌 Статический класс (не требует создания экземпляра)
    {
        public const ushort None = 0; // ❌ Нет действия (по умолчанию)

        // 📌 🔹 **Команды (от клиента к серверу)** 🔹
        public const ushort PlayCard = 1000; // 🎴 Игрок разыгрывает карту
        public const ushort Attack = 1010; // ⚔️ Игрок атакует карту противника
        public const ushort AttackPlayer = 1012; // 🎯 Игрок атакует другого игрока
        public const ushort Move = 1015; // 🔄 Игрок перемещает карту по полю
        public const ushort CastAbility = 1020; // ✨ Игрок использует способность карты
        public const ushort SelectCard = 1030; // 🃏 Игрок выбирает карту
        public const ushort SelectPlayer = 1032; // 🧑‍🤝‍🧑 Игрок выбирает другого игрока
        public const ushort SelectSlot = 1034; // 🔳 Игрок выбирает слот для размещения карты
        public const ushort SelectChoice = 1036; // 🏆 Игрок выбирает один из нескольких вариантов
        public const ushort SelectCost = 1037; // 💰 Игрок выбирает стоимость заклинания
        public const ushort SelectMulligan = 1038; // 🔄 Игрок выбирает карты для замены в начале игры
        public const ushort CancelSelect = 1039; // ❌ Игрок отменяет выбор
        public const ushort EndTurn = 1040; // ⏳ Игрок завершает свой ход
        public const ushort Resign = 1050; // 🚪 Игрок сдается
        public const ushort ChatMessage = 1090; // 💬 Игрок отправляет сообщение в чат

        public const ushort PlayerSettings = 1100; // ⚙️ Отправка данных игрока после подключения
        public const ushort PlayerSettingsAI = 1102; // ⚙️ Данные ИИ-игрока после подключения
        public const ushort GameSettings = 1105; // ⚙️ Отправка настроек игры после подключения

        // 📌 🔹 **Обновления (от сервера к клиенту)** 🔹
        public const ushort Connected = 2000; // 🔗 Игрок успешно подключился
        public const ushort PlayerReady = 2001; // ✅ Игрок готов к игре

        public const ushort GameStart = 2010; // 🏁 Начало игры
        public const ushort GameEnd = 2012; // 🏆 Конец игры
        public const ushort NewTurn = 2015; // 🔄 Начался новый ход

        public const ushort CardPlayed = 2020; // 🎴 Карта была разыграна
        public const ushort CardSummoned = 2022; // 🃏 Карта была призвана на поле
        public const ushort CardTransformed = 2023; // 🔄 Карта была преобразована
        public const ushort CardDiscarded = 2025; // 🗑️ Карта была сброшена
        public const ushort CardDrawn = 2026; // 🎴 Игрок взял карту из колоды
        public const ushort CardMoved = 2027; // 🔄 Карта была перемещена по полю

        public const ushort AttackStart = 2030; // ⚔️ Начало атаки карты
        public const ushort AttackEnd = 2031; // ⚔️ Завершение атаки карты
        public const ushort AttackPlayerStart = 2032; // 🎯 Начало атаки на игрока
        public const ushort AttackPlayerEnd = 2033; // 🎯 Завершение атаки на игрока
        public const ushort CardDamaged = 2036; // 💥 Карта получила урон
        public const ushort PlayerDamaged = 2037; // 💔 Игрок получил урон
        public const ushort CardHealed = 2038; // ❤️ Карта была исцелена
        public const ushort PlayerHealed = 2039; // 💖 Игрок был исцелен

        public const ushort AbilityTrigger = 2040; // ✨ Способность активирована
        public const ushort AbilityTargetCard = 2042; // 🎯 Способность применена к карте
        public const ushort AbilityTargetPlayer = 2043; // 🎯 Способность применена к игроку
        public const ushort AbilityTargetSlot = 2044; // 🎯 Способность применена к слоту
        public const ushort AbilityEnd = 2048; // ⏳ Завершение применения способности

        public const ushort SecretTriggered = 2060; // 🕵️ Тайная карта активирована
        public const ushort SecretResolved = 2061; // 🕵️ Тайная карта разыграна
        public const ushort ValueRolled = 2070; // 🎲 Выпал случайный результат (например, бросок кубика)

        public const ushort ServerMessage = 2190; // ⚠️ Сообщение сервера (например, ошибка)
        public const ushort RefreshAll = 2100; // 🔄 Полное обновление данных игры

        /// <summary>
        /// 📌 Метод `GetString(ushort type)` возвращает строковое представление действия по его коду.
        /// </summary>
        public static string GetString(ushort type)
        {
            if (type == GameAction.PlayCard)
                return "play"; // 🎴 Разыграть карту
            if (type == GameAction.Move)
                return "move"; // 🔄 Переместить карту
            if (type == GameAction.Attack)
                return "attack"; // ⚔️ Атаковать карту
            if (type == GameAction.AttackPlayer)
                return "attack_player"; // 🎯 Атаковать игрока
            if (type == GameAction.CastAbility)
                return "cast_ability"; // ✨ Использовать способность
            if (type == GameAction.EndTurn)
                return "end_turn"; // ⏳ Завершить ход
            if (type == GameAction.SelectCard)
                return "select_card"; // 🃏 Выбрать карту
            if (type == GameAction.SelectPlayer)
                return "select_player"; // 🧑‍🤝‍🧑 Выбрать игрока
            if (type == GameAction.SelectChoice)
                return "select_choice"; // 🏆 Выбрать вариант
            if (type == GameAction.SelectCost)
                return "select_cost"; // 💰 Выбрать стоимость заклинания
            if (type == GameAction.SelectSlot)
                return "select_slot"; // 🔳 Выбрать слот
            if (type == GameAction.CancelSelect)
                return "cancel_select"; // ❌ Отменить выбор
            if (type == GameAction.Resign)
                return "resign"; // 🚪 Сдаться
            if (type == GameAction.ChatMessage)
                return "chat"; // 💬 Чат-сообщение
            return type.ToString(); // 📌 Если код не найден, просто вернуть число в виде строки
        }
    }
}