using Unity.Entities;
using UnityEngine;

namespace NexusFramework.GAS.ECS
{
    /// <summary>
    ///     时间轴任务基类。任务在 <see cref="TaskClipData"/> 指定的帧区间内被驱动。
    ///     <para>
    ///         <b>能力约束</b>：任务只应通过 <see cref="Logic"/> 暴露的受保护辅助方法
    ///         （<c>CreateGameplayEffectEntity</c> / <c>ApplyGameplayEffectTo</c> /
    ///         <c>RemoveGameplayEffect</c>）与 <see cref="OwnerEntity"/> 产生副作用，
    ///         不应直接操作任意 ECS API。这条约束让时间轴成为"预设动作的组合"
    ///         而非任意代码通道——是相对原版实现的关键收紧。
    ///     </para>
    /// </summary>
    public abstract class AbilityTaskBase
    {
        protected AbilityLogicBase Logic { get; }

        protected int StartTime { get; private set; }

        protected AbilityTaskBase(AbilityLogicBase logic)
        {
            Logic = logic;
        }

        /// <summary>任务作用的 ASC 实体。</summary>
        protected Entity OwnerEntity => Logic?.OwnerEntity ?? Entity.Null;

        /// <summary>所属技能实体。</summary>
        protected Entity AbilityEntity => Logic?.GetAbilityEntity() ?? Entity.Null;

        protected EntityManager EntityManager => Logic._entityManager;

        public abstract void InitParameters(XParam parameter);

        public void Begin(int startTime)
        {
            StartTime = startTime;
            OnBegin(startTime);
        }

        public void Tick(int tickTime) => OnTick(tickTime);

        public void Finish(int endTime) => OnFinish(endTime);

        /// <summary>任务被丢弃时调用（时间轴提前停止）。</summary>
        public virtual void Dispose()
        {
        }

        protected virtual void OnBegin(int startFrame)
        {
        }

        protected virtual void OnTick(int frameIndex)
        {
        }

        protected virtual void OnFinish(int endFrame)
        {
        }
    }

    /// <summary>
    ///     带强类型参数的 Task 基类。参数类型由 <c>AbilityService.ScanAndRegisterAll</c>
    ///     反射推导并登记到 <see cref="AbilityLogicFactory"/>。
    /// </summary>
    public abstract class AbilityTaskBase<T> : AbilityTaskBase where T : XParam
    {
        public T Parameter { get; private set; }

        protected AbilityTaskBase(AbilityLogicBase logic) : base(logic)
        {
        }

        public override void InitParameters(XParam parameter)
        {
            if (parameter is T t)
            {
                Parameter = t;
                return;
            }

            Debug.LogError(
                $"[NF.GAS] {GetType().Name} 参数类型不匹配：期望 {typeof(T).Name}，" +
                $"实际 {parameter?.GetType().Name ?? "null"}");
        }
    }
}
