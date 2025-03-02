using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace TcgEngine
{
    /// <summary>
    /// Resolve abilties and actions one by one, with an optional delay in between each
    /// Это очередь обработки игровых событий (например, атаки, розыгрыша карт и т. д.).
    /// Она позволяет выполнять действия в правильном порядке и с анимациями, а не сразу.
    /// Если бы атака происходила мгновенно, это выглядело бы как скачок.
    /// Но resolve_queue разбивает атаку на несколько шагов с анимацией, делая игру плавной.


    /// </summary>

    public class ResolveQueue
    {
        private Pool<AbilityQueueElement> ability_elem_pool = new Pool<AbilityQueueElement>();
        private Pool<SecretQueueElement> secret_elem_pool = new Pool<SecretQueueElement>();
        private Pool<AttackQueueElement> attack_elem_pool = new Pool<AttackQueueElement>();
        private Pool<CallbackQueueElement> callback_elem_pool = new Pool<CallbackQueueElement>();

        private Queue<AbilityQueueElement> ability_queue = new Queue<AbilityQueueElement>();
        private Queue<SecretQueueElement> secret_queue = new Queue<SecretQueueElement>();
        private Queue<AttackQueueElement> attack_queue = new Queue<AttackQueueElement>();
        private Queue<CallbackQueueElement> callback_queue = new Queue<CallbackQueueElement>();

        private Game game_data;
        private bool is_resolving = false;
        private float resolve_delay = 0f;
        private bool skip_delay = false;

        public ResolveQueue(Game data, bool skip)
        {
            game_data = data;
            skip_delay = skip;
        }

        public void SetData(Game data)
        {
            game_data = data;
        }

        public virtual void Update(float delta)
        {
            if (resolve_delay > 0f)
            {
                resolve_delay -= delta;
                if (resolve_delay <= 0f)
                    ResolveAll();
            }
        }

        public virtual void AddAbility(AbilityData ability, Card caster, Card triggerer, Action<AbilityData, Card, Card> callback)
        {
            if (ability != null && caster != null)
            {
                AbilityQueueElement elem = ability_elem_pool.Create();
                elem.caster = caster;
                elem.triggerer = triggerer;
                elem.ability = ability;
                elem.callback = callback;
                ability_queue.Enqueue(elem);
            }
        }

        public virtual void AddAttack(Card attacker, Card target, Action<Card, Card, bool> callback, bool skip_cost = false)
        {
            //Debug.Log($"AddAttack: {attacker.uid} -> {target.uid}");
            if (attacker != null && target != null)
            {
                AttackQueueElement elem = attack_elem_pool.Create();
                elem.attacker = attacker;
                elem.target = target;
                elem.ptarget = null;
                elem.skip_cost = skip_cost;
                elem.callback = callback;
                attack_queue.Enqueue(elem);
            }
        }

        public virtual void AddAttack(Card attacker, Player target, Action<Card, Player, bool> callback, bool skip_cost = false)
        {
            if (attacker != null && target != null)
            {
                AttackQueueElement elem = attack_elem_pool.Create();
                elem.attacker = attacker;
                elem.target = null;
                elem.ptarget = target;
                elem.skip_cost = skip_cost;
                elem.pcallback = callback;
                attack_queue.Enqueue(elem);
            }
        }

        public virtual void AddSecret(AbilityTrigger secret_trigger, Card secret, Card trigger, Action<AbilityTrigger, Card, Card> callback)
        {
            if (secret != null && trigger != null)
            {
                SecretQueueElement elem = secret_elem_pool.Create();
                elem.secret_trigger = secret_trigger;
                elem.secret = secret;
                elem.triggerer = trigger;
                elem.callback = callback;
                secret_queue.Enqueue(elem);
            }
        }

        public virtual void AddCallback(Action callback)
        {
            if (callback != null)
            {
                CallbackQueueElement elem = callback_elem_pool.Create();
                elem.callback = callback;
                callback_queue.Enqueue(elem);
            }
        }

        public virtual void Resolve()
        {
            if (ability_queue.Count > 0)
            {
                //Resolve Ability
                AbilityQueueElement elem = ability_queue.Dequeue();
                ability_elem_pool.Dispose(elem);
                elem.callback?.Invoke(elem.ability, elem.caster, elem.triggerer);
            }
            else if (secret_queue.Count > 0)
            {
                //Resolve Secret
                SecretQueueElement elem = secret_queue.Dequeue();
                secret_elem_pool.Dispose(elem);
                elem.callback?.Invoke(elem.secret_trigger, elem.secret, elem.triggerer);
            }
            else if (attack_queue.Count > 0)
            {
                //Resolve Attack
                AttackQueueElement elem = attack_queue.Dequeue();
                attack_elem_pool.Dispose(elem);
                if (elem.ptarget != null)
                    elem.pcallback?.Invoke(elem.attacker, elem.ptarget, elem.skip_cost);
                else
                    elem.callback?.Invoke(elem.attacker, elem.target, elem.skip_cost);
            }
            else if (callback_queue.Count > 0)
            {
                CallbackQueueElement elem = callback_queue.Dequeue();
                callback_elem_pool.Dispose(elem);
                elem.callback.Invoke();
            }
        }

        public virtual void ResolveAll(float delay)
        {
            //Debug.Log($"🚀 Запуск очереди `ResolveQueue.ResolveAll()`, задержка: {delay} сек.");

            SetDelay(delay); // Устанавливаем задержку перед выполнением
            ResolveAll();    // Затем запускаем обработку
        }

        public virtual void ResolveAll()
        {
            if (is_resolving)
                return;

            is_resolving = true;
            while (CanResolve())
            {
                Resolve();
            }
            is_resolving = false;
        }

        public virtual void SetDelay(float delay)
        {
            if (!skip_delay)
            {
                resolve_delay = Mathf.Max(resolve_delay, delay);
            }
        }

        public virtual bool CanResolve()
        {
            if (resolve_delay > 0f)
                return false;   //Is waiting delay
            if (game_data.state == GameState.GameEnded)
                return false; //Cant execute anymore when game is ended
            if (game_data.selector != SelectorType.None)
                return false; //Waiting for player input, in the middle of resolve loop
            return attack_queue.Count > 0 || ability_queue.Count > 0 || secret_queue.Count > 0 || callback_queue.Count > 0;
        }

        public virtual bool IsResolving()
        {
            return is_resolving || resolve_delay > 0f;
        }

        public virtual void Clear()
        {
            attack_elem_pool.DisposeAll();
            ability_elem_pool.DisposeAll();
            secret_elem_pool.DisposeAll();
            callback_elem_pool.DisposeAll();
            attack_queue.Clear();
            ability_queue.Clear();
            secret_queue.Clear();
            callback_queue.Clear();
        }

        public Queue<AttackQueueElement> GetAttackQueue()
        {
            return attack_queue;
        }

        public Queue<AbilityQueueElement> GetAbilityQueue()
        {
            return ability_queue;
        }

        public Queue<SecretQueueElement> GetSecretQueue()
        {
            return secret_queue;
        }

        public Queue<CallbackQueueElement> GetCallbackQueue()
        {
            return callback_queue;
        }
    
    }

    public class AbilityQueueElement
    {
        public AbilityData ability;
        public Card caster;
        public Card triggerer;
        public Action<AbilityData, Card, Card> callback;
    }

    public class AttackQueueElement
    {
        public Card attacker;
        public Card target;
        public Player ptarget;
        public bool skip_cost;
        public Action<Card, Card, bool> callback;
        public Action<Card, Player, bool> pcallback;
    }

    public class SecretQueueElement
    {
        public AbilityTrigger secret_trigger;
        public Card secret;
        public Card triggerer;
        public Action<AbilityTrigger, Card, Card> callback;
    }

    public class CallbackQueueElement
    {
        public Action callback;
    }
}
