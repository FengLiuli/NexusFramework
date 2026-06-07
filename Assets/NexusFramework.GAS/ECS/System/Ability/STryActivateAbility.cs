using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    [UpdateInGroup(typeof(SGAbility))]
    [DisableAutoCreation]
    public partial struct STryActivateAbility : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CAbilityInTryActivate>();
            state.RequireForUpdate<SingletonGameplayTagMap>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var tagMap = SystemAPI.GetSingleton<SingletonGameplayTagMap>();
            var globalTimer = SystemAPI.GetSingletonRW<GlobalTimer>();

            foreach (var (_, basicInfo, ability) in SystemAPI
                         .Query<RefRO<CAbilityInTryActivate>, RefRO<CAbilityBaseInfo>>().WithEntityAccess())
            {
                var result = CanActivateAbility(state.EntityManager, tagMap, ability);
                if (result == AbilityActivationResult.Success)
                {
                    var owner = basicInfo.ValueRO.Owner;
                    if (state.EntityManager.HasComponent<CAbilityActivationOwnedTags>(ability))
                    {
                        var abilityActivationOwnedTags =
                            state.EntityManager.GetComponentData<CAbilityActivationOwnedTags>(ability);
                        foreach (var tag in abilityActivationOwnedTags.tags)
                            GasTagHelperManaged.AddTemporaryTagTo(state.EntityManager, tagMap, owner, ability, tag);
                    }

                    ecb.AddComponent(ability, new CAbilityActive());
                    var abilityLogic = state.EntityManager.GetComponentData<MCAbilityLogic>(ability);
                    abilityLogic.logic.ActivateAbility(globalTimer.ValueRO);

                    CancelAbilitiesWithTags(state.EntityManager, tagMap, ecb, ability);
                    GASInternalBridge.Enqueue(new AbilityActivatedEvent { Owner = owner, AbilityCode = basicInfo.ValueRO.Code });
                }


                ecb.RemoveComponent<CAbilityInTryActivate>(ability);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        private static AbilityActivationResult CanActivateAbility(EntityManager entityManager, SingletonGameplayTagMap tagMap, Entity ability)
        {
            if (entityManager.HasComponent<CAbilityActive>(ability))
                return AbilityActivationResult.FailHasActivated;

            if (!CheckGameplayTagsValidToActivate(entityManager, tagMap, ability))
                return AbilityActivationResult.FailTagRequirement;

            if (!CheckCost(entityManager, ability))
                return AbilityActivationResult.FailCost;

            if (!CheckCooldownReady(entityManager, tagMap, ability))
                return AbilityActivationResult.FailCooldown;

            return AbilityActivationResult.Success;
        }

        private static bool CheckGameplayTagsValidToActivate(EntityManager entityManager, SingletonGameplayTagMap tagMap, Entity ability)
        {
            var owner = entityManager.GetComponentData<CAbilityBaseInfo>(ability).Owner;

            var hasAllTags = true;
            if (entityManager.HasComponent<CAbilityActivationRequiredTags>(ability))
            {
                var requirement = entityManager.GetComponentData<CAbilityActivationRequiredTags>(ability).requirement;
                hasAllTags = tagMap.AscEvaluateTagRequirement(entityManager, owner, requirement);
            }

            var notHasAnyTags = true;
            if (entityManager.HasComponent<CAbilityActivationBlockedTags>(ability))
            {
                var requirement = entityManager.GetComponentData<CAbilityActivationBlockedTags>(ability).requirement;
                notHasAnyTags = tagMap.AscEvaluateTagRequirement(entityManager, owner, requirement);
            }

            var notBlockedByOtherAbility = true;
            var ownerAbilities = entityManager.GetBuffer<BAbility>(owner);
            foreach (var ownerAbility in ownerAbilities)
            {
                var otherAbility = ownerAbility.Ability;
                if (otherAbility == ability) continue;
                if (!entityManager.HasComponent<CAbilityActive>(otherAbility)) continue;
                if (!entityManager.HasComponent<CBlockAbilityWithTags>(otherAbility)) continue;

                var blockTags = entityManager.GetComponentData<CBlockAbilityWithTags>(otherAbility);
                if (AbilityHasAnyTags(entityManager, tagMap, ability, blockTags.tags))
                {
                    notBlockedByOtherAbility = false;
                    break;
                }
            }

            return hasAllTags && notHasAnyTags && notBlockedByOtherAbility;
        }

        private static bool AbilityHasAnyTags(EntityManager entityManager, SingletonGameplayTagMap tagMap, Entity ability, NativeArray<int> tags)
        {
            if (!entityManager.HasComponent<CAbilityAssetTags>(ability)) return false;

            var assetTags = entityManager.GetComponentData<CAbilityAssetTags>(ability);
            foreach (var tag in tags)
                foreach (var assetTag in assetTags.tags)
                    if (GasTagHelper.HasTag(tagMap, assetTag, tag))
                        return true;
            return false;
        }

        private static bool CheckCost(EntityManager entityManager, Entity ability)
        {
            if (!entityManager.HasComponent<CAbilityCost>(ability)) return true;

            var costComponent = entityManager.GetComponentData<CAbilityCost>(ability);
            bool isInstantEffect = !entityManager.HasComponent<CDuration>(costComponent.ProtoGameplayEffectCost);
            if (!isInstantEffect) return true;

            var mcModifiers = entityManager.GetComponentData<MCModifiers>(costComponent.ProtoGameplayEffectCost);
            var owner = entityManager.GetComponentData<CAbilityBaseInfo>(ability).Owner;
            var attrSets = entityManager.GetBuffer<BEAttrSet>(owner);
            foreach (var modifier in mcModifiers.Modifiers)
            {
                var opt = modifier.Operation;
                if (opt != GEOperation.Add && opt != GEOperation.Minus) continue;

                var attrSetIndex = attrSets.IndexOfAttrSetCode(modifier.AttrSetCode);
                if (attrSetIndex == -1) continue;

                var attrSet = attrSets[attrSetIndex];
                var attributes = attrSet.Attributes;

                var attrIndex = attributes.IndexOfAttrCode(modifier.AttrCode);
                if (attrIndex == -1) continue;

                var attr = attributes[attrIndex];
                var resultValue = GasMmcHelper.Calculate(entityManager, costComponent.ProtoGameplayEffectCost, modifier, attr.CurrentValue, owner, owner);
                if (resultValue < 0) return false;
            }

            return true;
        }

        private static bool CheckCooldownReady(EntityManager entityManager, SingletonGameplayTagMap tagMap, Entity ability)
        {
            if (!entityManager.HasComponent<CAbilityCooldown>(ability)) return true;

            var cooldown = entityManager.GetComponentData<CAbilityCooldown>(ability);
            if (!cooldown.CooldownTags.IsCreated || cooldown.CooldownTags.Length == 0) return true;

            var owner = entityManager.GetComponentData<CAbilityBaseInfo>(ability).Owner;
            return !tagMap.AscHasAnyTags(entityManager, owner, cooldown.CooldownTags);
        }

        private static void CancelAbilitiesWithTags(EntityManager entityManager, SingletonGameplayTagMap tagMap, EntityCommandBuffer ecb, Entity ability)
        {
            if (!entityManager.HasComponent<CCancelAbilityWithTags>(ability)) return;

            var cancelTags = entityManager.GetComponentData<CCancelAbilityWithTags>(ability);
            var owner = entityManager.GetComponentData<CAbilityBaseInfo>(ability).Owner;
            var ownerAbilities = entityManager.GetBuffer<BAbility>(owner);

            foreach (var ownerAbility in ownerAbilities)
            {
                var otherAbility = ownerAbility.Ability;
                if (otherAbility == ability) continue;

                if (!entityManager.HasComponent<CAbilityActive>(otherAbility)) continue;
                if (entityManager.HasComponent<CAbilityInTryCancel>(otherAbility)) continue;

                if (AbilityHasAnyTags(entityManager, tagMap, otherAbility, cancelTags.tags))
                {
                    ecb.AddComponent<CAbilityInTryCancel>(otherAbility);
                }
            }
        }
    }

    public enum AbilityActivationResult
    {
        Success,
        FailHasActivated,
        FailTagRequirement,
        FailCost,
        FailCooldown,
        FailOtherReason
    }
}
