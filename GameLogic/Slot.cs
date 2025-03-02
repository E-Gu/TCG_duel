using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace TcgEngine
{
    /// <summary>
    /// Represent a slot in gameplay (data only)
    /// </summary>

    [System.Serializable]
    public struct Slot : INetworkSerializable
    {
        public int x;
        public int y;
        public int p; // 0 - игрок, 1 - оппонент

        public static int x_min = 1;
        public static int x_max = 1;

        public static int y_min = 1;
        public static int y_max = 1;

        public static bool ignore_p = false;

        private static Dictionary<int, List<Slot>> player_slots = new Dictionary<int, List<Slot>>();
        private static List<Slot> all_slots = new List<Slot>();

        // ✅ Стандартные слоты для игрока и оппонента
        public static Slot DefaultSlotPlayer => new Slot(1, 1, 0);
        public static Slot DefaultSlotOpponent => new Slot(1, 1, 1);

        // ✅ Добавляем MaxP (максимальное значение p, т.е. количество игроков - 1)
        public static int MaxP => ignore_p ? 0 : 1;

        public Slot(int x, int y, int pid)
        {
            this.x = x;
            this.y = y;
            this.p = pid;
        }

        public Slot(SlotXY slot, int pid)
        {
            this.x = slot.x;
            this.y = slot.y;
            this.p = pid;
        }

        /// <summary>
        /// ✅ Метод, который вернёт стандартный слот для заданного игрока
        /// </summary>
        public static Slot GetDefaultSlot(int playerId)
        {
            //Debug.Log("GetDefaultSlot");
            return playerId == 0 ? DefaultSlotPlayer : DefaultSlotOpponent;
        }

        /// <summary>
        /// Проверка, входит ли слот в допустимые границы
        /// </summary>
        public bool IsValid()
        {
            bool isValid = x >= x_min && x <= x_max && y >= y_min && y <= y_max && p >= 0;

            // 🔍 Логирование проверки валидности слота
            //Debug.Log($"[IsValid] Проверка слота: x={x}, y={y}, p={p} | " +
                      //$"x_min={x_min}, x_max={x_max}, y_min={y_min}, y_max={y_max}, MaxP={MaxP} | " +
                      //$"Результат: {(isValid ? "✅ Валиден" : "❌ НЕвалиден")}");

            return isValid;
        }
        /// <summary>
        /// Проверяет, является ли слот игрока (то есть стандартным начальным положением)
        /// </summary>
        public bool IsPlayerSlot()
        {
            return this == DefaultSlotPlayer;
        }
        public static List<Slot> GetAll(int pid)
        {
            int p = GetP(pid); // Учитываем, используется ли P (player_id)

            if (player_slots.ContainsKey(p))
                return player_slots[p]; // Если кэш уже есть, возвращаем его

            List<Slot> list = new List<Slot>();
            for (int y = y_min; y <= y_max; y++)
            {
                for (int x = x_min; x <= x_max; x++)
                {
                    list.Add(new Slot(x, y, p));
                }
            }

            player_slots[p] = list; // Сохраняем в кэш
            return list;
        }
        public static int GetP(int pid)
        {
            return ignore_p ? 0 : pid;
        }
        public static Slot GetRandom(int pid, System.Random rand)
        {
            int p = GetP(pid); // Получаем правильное значение для игрока (игнорировать или использовать ID игрока)
            if (y_max > y_min)
                return new Slot(rand.Next(x_min, x_max + 1), rand.Next(y_min, y_max + 1), p);
            return new Slot(rand.Next(x_min, x_max + 1), y_min, p);
        }
        public static Slot Get(int x, int y, int p)
        {
            List<Slot> slots = GetAll();
            foreach (Slot slot in slots)
            {
                if (slot.x == x && slot.y == y && slot.p == p)
                    return slot;
            }
            return new Slot(x, y, p);
        }

        public static List<Slot> GetAll()
        {
            if (all_slots.Count > 0)
                return all_slots;

            for (int p = 0; p <= MaxP; p++)
            {
                for (int y = y_min; y <= y_max; y++)
                {
                    for (int x = x_min; x <= x_max; x++)
                    {
                        all_slots.Add(new Slot(x, y, p));
                    }
                }
            }
            return all_slots;
        }

        public static bool operator ==(Slot slot1, Slot slot2)
        {
            return slot1.x == slot2.x && slot1.y == slot2.y && slot1.p == slot2.p;
        }

        public static bool operator !=(Slot slot1, Slot slot2)
        {
            return slot1.x != slot2.x || slot1.y != slot2.y || slot1.p != slot2.p;
        }

        public override bool Equals(object o)
        {
            return base.Equals(o);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref x);
            serializer.SerializeValue(ref y);
            serializer.SerializeValue(ref p);
        }

        public static Slot None => new Slot(0, 0, 0);
    }

    [System.Serializable]
    public struct SlotXY
    {
        public int x;
        public int y;
    }

}