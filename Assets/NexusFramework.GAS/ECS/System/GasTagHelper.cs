using Unity.Burst;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    /// <summary>Burst 兼容的标签查询（仅含 blittable 参数）</summary>
    [BurstCompile]
    public static class GasTagHelper
    {
        [BurstCompile]
        public static bool HasTag(in SingletonGameplayTagMap map, int tagA, int tagB)
        {
            return map.IsTagAIncludeTagB(tagA, tagB);
        }
    }

    /// <summary>含 EntityManager 操作的标签方法（不可 Burst）</summary>
    public static class GasTagHelperManaged
    {
        public static bool HasTemporaryTag(EntityManager entityManager, in SingletonGameplayTagMap map, Entity asc, Entity source, int tag)
        {
            var temporaryTags = entityManager.GetBuffer<BTemporaryTag>(asc);
            foreach (var t in temporaryTags)
                if (t.source == source && map.IsTagAIncludeTagB(t.tag, tag))
                    return true;
            return false;
        }

        public static bool AddTemporaryTagTo(EntityManager entityManager, in SingletonGameplayTagMap map, Entity ascTarget, Entity source, int tag)
        {
            var temporaryTags = entityManager.GetBuffer<BTemporaryTag>(ascTarget);
            if (HasTemporaryTag(entityManager, map, ascTarget, source, tag))
                return false;
            temporaryTags.Add(new BTemporaryTag { source = source, tag = tag });
            return true;
        }
    }
}
