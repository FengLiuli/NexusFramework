using NexusFramework;
using Unity.Entities;
using NexusFramework.DataCarrier;
using NexusFramework.GAS.ECS;
using NexusFramework.GAS.Services;
using NexusFramework.GAS.Models;

namespace NexusFramework.GAS.Queries
{
    public class QueryAttributeValue : AbstractQuery<float>
    {
        public CarrierId Target;
        public int AttrSetCode;
        public int AttrCode;

        protected override float OnDo()
        {
            var entity = this.GetModel<GASEntityMapModel>().GetGASEntity(Target);
            if (entity == Unity.Entities.Entity.Null) return 0f;

            var em = this.GetService<WorldService>().EntityManager;
            var buffer = em.GetBuffer<BEAttrSet>(entity);
            for (var i = 0; i < buffer.Length; i++)
            {
                var set = buffer[i];
                if (set.Code != AttrSetCode) continue;
                var attrs = set.Attributes;
                for (var j = 0; j < attrs.Length; j++)
                    if (attrs[j].Code == AttrCode)
                        return attrs[j].CurrentValue;
            }
            return 0f;
        }
    }
}