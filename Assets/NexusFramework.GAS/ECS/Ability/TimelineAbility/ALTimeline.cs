using NexusFramework;
using NexusFramework.GAS.Models;
using Unity.Entities;
using UnityEngine;

namespace NexusFramework.GAS.ECS
{
    /// <summary>
    ///     时间轴技能：按帧驱动一串预设任务。
    ///     <para>
    ///         与原版的差异：任务只能通过 <see cref="AbilityTaskBase"/> 提供的受限接口
    ///         产生副作用，时间轴因此是"预设动作的组合"而非任意代码通道。
    ///     </para>
    /// </summary>
    public class ALTimeline : AbilityLogicBase<XParamALTimelineID>
    {
        protected ALTimelinePlayer Player;

        public XParamTimeline Data { get; private set; }

        /// <summary>向性技能的作用目标。</summary>
        public Entity Target { get; private set; }

        public ALTimeline(Entity ability, IArchitecture architecture) : base(ability, architecture)
        {
            Player = new ALTimelinePlayer(this);
        }

        public override void SetParam(XParam param)
        {
            base.SetParam(param);

            if (_param == null || _param.ID <= 0)
            {
                Debug.LogError($"[NF.GAS] ALTimeline 参数无效（Ability={_abilityEntity}），时间轴不会播放");
                return;
            }

            Data = Architecture.GetModel<ConfigModel>().GetTimelineAbilityParam(_param.ID);
            if (Data == null)
            {
                Debug.LogError($"[NF.GAS] ALTimeline 找不到时间轴配置: ID={_param.ID}");
                return;
            }

            Player.InitData();
        }

        public void SetAbilityTarget(Entity mainTarget)
        {
            Target = mainTarget;
        }

        public override void ActivateAbility(GlobalTimer timer)
        {
            if (Data == null) return;

            Player.Play();
        }

        public override void CancelAbility(GlobalTimer timer)
        {
            Player.Stop();
        }

        public override void EndAbility(GlobalTimer timer)
        {
            Player.Stop();
        }

        public override void AbilityTick(GlobalTimer timer)
        {
            Player.Tick(timer);
        }
    }
}
