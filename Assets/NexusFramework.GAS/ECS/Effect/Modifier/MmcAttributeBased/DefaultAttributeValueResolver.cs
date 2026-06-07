using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public class DefaultAttributeValueResolver : IAttributeValueResolver
    {
        private EntityManager _em;

        public DefaultAttributeValueResolver() { }

        public DefaultAttributeValueResolver(EntityManager em)
        {
            _em = em;
        }

        public EntityManager GetEntityManager() => _em;

        public float Resolve(EntityManager em, Entity ascEntity, int attrSetCode, int attrCode)
        {
            _em = em;
            if (!em.HasBuffer<BEAttrSet>(ascEntity)) return 0f;
            var buffer = em.GetBuffer<BEAttrSet>(ascEntity);

            for (var i = 0; i < buffer.Length; i++)
            {
                var set = buffer[i];
                if (set.Code != attrSetCode) continue;
                var attrs = set.Attributes;
                for (var j = 0; j < attrs.Length; j++)
                {
                    if (attrs[j].Code == attrCode)
                        return attrs[j].BaseValue;
                }
            }
            return 0f;
        }
    }
}
