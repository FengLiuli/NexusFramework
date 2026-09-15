using Unity.Entities;
using UnityEngine;

namespace NexusFramework.GAS.ECS
{
    public class MCAbilityLogic : IComponentData
    {
        public AbilityLogicBase logic;

        public MCAbilityLogic(AbilityLogicBase logic)
        {
            this.logic = logic;
        }

        public MCAbilityLogic()
        {
        }
    }

    public sealed class MCConfAbilityLogic : AbilityComponentConfig
    {
        public string AbilityLogicType;
        public XParam Param;

        public override void LoadToGameplayAbilityEntity(Entity ability)
        {
            var logic = AbilityLogicFactory.TryCreateAbilityLogic(AbilityLogicType, ability);
            if (logic == null)
            {
                // 静默跳过会让技能"能授予但永远不会激活"，从表现上极难定位
                Debug.LogError(
                    $"[NF.GAS] AbilityLogic 未注册或创建失败: '{AbilityLogicType}'（Ability={ability}）。" +
                    "请确认该类型已实现且被 AbilityService.ScanAndRegisterAll 扫描到。");
                return;
            }

            if (Param == null)
            {
                Debug.LogError(
                    $"[NF.GAS] AbilityLogic '{AbilityLogicType}' 的参数为空（Ability={ability}）。" +
                    "通常是 LubanConfigLoader 的 CreateAbilityLogicParam 未填充字段所致。");
            }

            logic.SetParam(Param);
            _entityManager.AddComponent<MCAbilityLogic>(ability);
            _entityManager.SetComponentData(ability, new MCAbilityLogic(logic));
        }
    }
}