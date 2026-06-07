using NexusFramework;
using NexusFramework.DataCarrier;
using Unity.Entities;
using NexusFramework.GAS.ECS;
using NexusFramework.GAS.Models;

namespace NexusFramework.GAS.Services
{
    public class TagService : AbstractService
    {
        private SingletonGameplayTagMap _tagMap;

        protected override void OnInit()
        {
            var em = this.GetService<WorldService>().EntityManager;
            var hierarchy = this.GetModel<ConfigModel>().GetTagHierarchy();

            _tagMap = new SingletonGameplayTagMap
            {
                Map = new Unity.Collections.NativeHashMap<int, ComGameplayTag>(256, Unity.Collections.Allocator.Persistent)
            };
            em.CreateSingleton(_tagMap);
        }

        protected override void OnDeinit() { }

        public bool HasTag(CarrierId carrier, int tag)
        {
            var entity = this.GetModel<GASEntityMapModel>().GetGASEntity(carrier);
            if (entity == Entity.Null) return false;

            var em = this.GetService<WorldService>().EntityManager;
            var fixedTags = em.GetBuffer<BFixedTag>(entity);
            foreach (var ft in fixedTags)
                if (GasTagHelper.HasTag(_tagMap, ft.tag, tag))
                    return true;

            var tempTags = em.GetBuffer<BTemporaryTag>(entity);
            foreach (var tt in tempTags)
                if (GasTagHelper.HasTag(_tagMap, tt.tag, tag))
                    return true;

            return false;
        }
    }
}