using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace NexusFramework.GAS.ECS
{
    public static class CueHelper
    {
        private static readonly Dictionary<string, Type> CueTypeMap = new();
        private static readonly Dictionary<string, Type> CueParamTypeMap = new();
        private static readonly Dictionary<string, string> CueType2CueParamTypeMap = new();

        public static void RegisterCue(string sType, Type logicType, Type cueParamType)
        {
            CueTypeMap[sType] = logicType;
            CueParamTypeMap[cueParamType.Name] = cueParamType;
            CueType2CueParamTypeMap[sType] = cueParamType.Name;
        }

        public static void RegisterCue<T>(string sType, Type cueParam) where T : GameplayCueBase
        {
            RegisterCue(sType, typeof(T), cueParam);
        }

        public static Type GetCueType(string sType)
        {
            if (CueTypeMap.TryGetValue(sType, out var type)) return type;
#if UNITY_EDITOR
            Debug.LogError($"[NF.GAS] CueTypeMap中没有找到类型: {sType}");
#endif
            return null;
        }

        public static int GetCueTypeCode(string sType)
        {
            return 0; // TODO: [NF.GAS] Implement cue type code lookup via config
        }

        public static List<string> GetCueTypeNames()
        {
            return CueTypeMap.Keys.ToList();
        }

        public static Type GetCueLogicParamType(string cueType)
        {
            return CueType2CueParamTypeMap.TryGetValue(cueType, out var cueParam) ? CueParamTypeMap[cueParam] : null;
        }

        public static Type GetCueLogicParamType(Type cueType)
        {
            if (cueType == null) return null;
            var cueParam = CueType2CueParamTypeMap[cueType.Name];
            return CueParamTypeMap[cueParam];
        }

        public static Type GetCueLogicParamType(int cueTypeCode)
        {
            return null; // TODO: [NF.GAS] Implement via config index
        }

        public static XParam CreateCueParameter(string type, List<object> paramData = null)
        {
            var cueParamConfigType = GetCueLogicParamType(type);
            if (cueParamConfigType == null) return null;
            var cueParamEditor = (XParam)Activator.CreateInstance(cueParamConfigType);
#if UNITY_EDITOR
            if (paramData != null) cueParamEditor.DecodeExcelData(paramData);
#endif
            return cueParamEditor;
        }

        public static GameplayCueBase TryCreateCue(Type type, XParam param, IGASEntityResolver resolver = null)
        {
            try
            {
                if (Activator.CreateInstance(type) is GameplayCueBase cue)
                {
                    if (resolver != null) cue.SetEntityResolver(resolver);
                    cue.InitParameters(param);
                    return cue;
                }
            }
            catch (MissingMethodException e)
            {
                Debug.LogError($"[NF.GAS] 创建Cue失败: {type?.FullName}. Error: {e.Message}");
            }
            return null;
        }

        public static GameplayCueBase TryCreateCue(string cueType, XParam param, IGASEntityResolver resolver = null)
        {
            if (CueTypeMap.TryGetValue(cueType, out var type))
                return TryCreateCue(type, param, resolver);
#if UNITY_EDITOR
            Debug.LogError($"[NF.GAS] 创建Cue失败: Can't find Cue for cueType [{cueType}].");
#endif
            return null;
        }

        public static MCCue InitInstantCueFromEffect(MCCue cue, Entity cueEntity, Entity ge)
        {
            cue.cue.SetSourceEntity(ge, CueSourceType.GameplayEffect);
            cue.cue.SetCueEntity(cueEntity);
            return cue;
        }

        public static void StopCue(EntityManager entityManager, Entity cueEntity)
        {
            if (entityManager.IsComponentEnabled<ECCuePlaying>(cueEntity))
                entityManager.SetComponentEnabled<ECCuePlayable>(cueEntity, false);
        }

        public static void PlayCue(EntityManager entityManager, Entity cueEntity)
        {
            if (!entityManager.IsComponentEnabled<ECCuePlaying>(cueEntity))
                entityManager.SetComponentEnabled<ECCuePlayable>(cueEntity, true);
        }

        private static bool EvaluateAscTagRequirement(EntityManager entityManager, Entity asc, in TagRequirementData requirement)
        {
            bool passAll = !requirement.all.IsCreated || requirement.all.Length == 0 || HasAllTags(entityManager, asc, requirement.all);
            bool passAny = !requirement.any.IsCreated || requirement.any.Length == 0 || HasAnyTags(entityManager, asc, requirement.any);
            bool passNone = !requirement.none.IsCreated || requirement.none.Length == 0 || !HasAnyTags(entityManager, asc, requirement.none);
            return passAll && passAny && passNone;
        }

        private static bool HasAllTags(EntityManager entityManager, Entity asc, NativeArray<int> requiredTags)
        {
            if (!entityManager.HasBuffer<BFixedTag>(asc)) return false;
            var fixedTags = entityManager.GetBuffer<BFixedTag>(asc);
            foreach (var reqTag in requiredTags)
            {
                bool found = false;
                for (int i = 0; i < fixedTags.Length; i++)
                    if (fixedTags[i].tag == reqTag) { found = true; break; }
                if (!found) return false;
            }
            return true;
        }

        private static bool HasAnyTags(EntityManager entityManager, Entity asc, NativeArray<int> tags)
        {
            if (!entityManager.HasBuffer<BFixedTag>(asc)) return false;
            var fixedTags = entityManager.GetBuffer<BFixedTag>(asc);
            foreach (var tag in tags)
                for (int i = 0; i < fixedTags.Length; i++)
                    if (fixedTags[i].tag == tag)
                        return true;
            return false;
        }

        public static void TryPlayCueOnAsc(EntityManager entityManager, Entity targetAsc, Entity cueEntity, Entity sourceGE)
        {
            if (entityManager.HasComponent<CPlayRequiredTags>(cueEntity))
            {
                var requiredTags = entityManager.GetComponentData<CPlayRequiredTags>(cueEntity);
                if (!EvaluateAscTagRequirement(entityManager, targetAsc, requiredTags.requirement)) return;
            }
            if (entityManager.HasComponent<CPlayImmunitedTags>(cueEntity))
            {
                var immunityTags = entityManager.GetComponentData<CPlayImmunitedTags>(cueEntity);
                if (!EvaluateAscTagRequirement(entityManager, targetAsc, immunityTags.requirement)) return;
            }

            var cueLogic = entityManager.GetComponentData<MCCue>(cueEntity);
            cueLogic.cue.SetCueEntity(cueEntity);
            cueLogic.cue.Reset();
            cueLogic.cue.SetSourceEntity(sourceGE, CueSourceType.GameplayEffect);
            cueLogic.cue.AddToTargetAsc(targetAsc);
            cueLogic.cue.Play(true);
        }
    }
}
