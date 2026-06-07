using NexusFramework;
using NexusFramework.DataCarrier;
using Unity.Entities;
using NexusFramework.GAS.ECS;
using NexusFramework.GAS.Models;

namespace NexusFramework.GAS.Services
{
    public class AttributeService : AbstractService
    {
        protected override void OnInit() { }
        protected override void OnDeinit() { }

        /// <summary>获取属性当前值</summary>
        public float GetCurrentValue(CarrierId carrier, int attrSetCode, int attrCode)
        {
            var entity = GetEntity(carrier);
            if (entity == Entity.Null) return 0f;
            var em = this.GetService<WorldService>().EntityManager;
            var data = FindAttribute(em, entity, attrSetCode, attrCode);
            return data.Code >= 0 ? data.CurrentValue : 0f;
        }

        /// <summary>获取属性基础值</summary>
        public float GetBaseValue(CarrierId carrier, int attrSetCode, int attrCode)
        {
            var entity = GetEntity(carrier);
            if (entity == Entity.Null) return 0f;
            var em = this.GetService<WorldService>().EntityManager;
            var data = FindAttribute(em, entity, attrSetCode, attrCode);
            return data.Code >= 0 ? data.BaseValue : 0f;
        }

        /// <summary>设置属性基础值（触发重算）</summary>
        public void SetBaseValue(CarrierId carrier, int attrSetCode, int attrCode, float value)
        {
            var entity = GetEntity(carrier);
            if (entity == Entity.Null) return;
            var em = this.GetService<WorldService>().EntityManager;
            if (!em.HasBuffer<BEAttrSet>(entity)) return;
            var buf = em.GetBuffer<BEAttrSet>(entity);
            for (int i = 0; i < buf.Length; i++)
            {
                if (buf[i].Code != attrSetCode) continue;
                var set = buf[i];
                var attrs = set.Attributes;
                for (int j = 0; j < attrs.Length; j++)
                {
                    if (attrs[j].Code != attrCode) continue;
                    var d = attrs[j];
                    d.BaseValue = value;
                    d.Dirty = true;
                    attrs[j] = d;
                    set.Attributes = attrs;
                    buf[i] = set;
                    em.AddComponent<CAttributeIsDirty>(entity);
                    return;
                }
            }
        }

        /// <summary>属性是否存在</summary>
        public bool HasAttribute(CarrierId carrier, int attrSetCode, int attrCode)
        {
            var entity = GetEntity(carrier);
            if (entity == Entity.Null) return false;
            var em = this.GetService<WorldService>().EntityManager;
            var data = FindAttribute(em, entity, attrSetCode, attrCode);
            return data.Code >= 0;
        }

        /// <summary>设置属性当前值（不触发重算，慎用）</summary>
        public void SetCurrentValue(CarrierId carrier, int attrSetCode, int attrCode, float value)
        {
            var entity = GetEntity(carrier);
            if (entity == Entity.Null) return;
            var em = this.GetService<WorldService>().EntityManager;
            if (!em.HasBuffer<BEAttrSet>(entity)) return;
            var buf = em.GetBuffer<BEAttrSet>(entity);
            for (int i = 0; i < buf.Length; i++)
            {
                if (buf[i].Code != attrSetCode) continue;
                var set = buf[i];
                var attrs = set.Attributes;
                for (int j = 0; j < attrs.Length; j++)
                {
                    if (attrs[j].Code != attrCode) continue;
                    var d = attrs[j];
                    d.CurrentValue = value;
                    attrs[j] = d;
                    set.Attributes = attrs;
                    buf[i] = set;
                    return;
                }
            }
        }

        private Entity GetEntity(CarrierId carrier)
        {
            return this.GetModel<GASEntityMapModel>().GetGASEntity(carrier);
        }

        private static CAttributeData FindAttribute(EntityManager em, Entity entity, int attrSetCode, int attrCode)
        {
            if (!em.HasBuffer<BEAttrSet>(entity)) return CAttributeData.NULL;
            var buf = em.GetBuffer<BEAttrSet>(entity);
            for (int i = 0; i < buf.Length; i++)
            {
                if (buf[i].Code != attrSetCode) continue;
                var attrs = buf[i].Attributes;
                for (int j = 0; j < attrs.Length; j++)
                    if (attrs[j].Code == attrCode)
                        return attrs[j];
            }
            return CAttributeData.NULL;
        }
    }
}
