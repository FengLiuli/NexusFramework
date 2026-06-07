using NexusFramework;
using Unity.Collections;
using NexusFramework.GAS.ECS;
using NexusFramework.GAS.Models;
using NexusFramework.GAS.Services;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

namespace NexusFramework.GAS.Tests.ECS
{
    [TestFixture]
    public class EffectPipelineTests
    {
        private TestArchitecture _arch;
        private EffectService _effectService;
        private EntityManager _em;
        private World _world;

        [SetUp]
        public void SetUp()
        {
            _arch = new TestArchitecture();
            _arch.Initialize();
            _arch.GetCarrierManager().RegisterType("TestUnit");
            _effectService = _arch.GetService<EffectService>();
            var ws = _arch.GetService<WorldService>();
            _em = ws.EntityManager;
            _world = ws.ExWorld;
        }

        [TearDown]
        public void TearDown()
        {
            _arch?.Dispose();
            _arch = null;
        }

        // ═══════════════════════════════════════════
        // Test 1: 瞬时 GE 创建——component 验证
        // ═══════════════════════════════════════════

        [Test]
        public void InstantGE_IsCreated_With_WipTags()
        {
            var src = _arch.CreateGASCarrier("TestUnit");
            var tgt = _arch.CreateGASCarrier("TestUnit");
            _effectService.ApplyEffect(configId: 1, target: tgt, source: src);

            var q = _em.CreateEntityQuery(
                ComponentType.ReadOnly<CEffectInUsage>(),
                ComponentType.ReadOnly<WipInstantiateEffect>(),
                ComponentType.ReadOnly<MCModifiers>());
            var list = q.ToEntityArray(Allocator.Temp);
            Assert.That(list.Length, Is.EqualTo(1), "应有一 GE 实体");
            list.Dispose();
            q.Dispose();
        }

        // ═══════════════════════════════════════════
        // Test 2: 瞬时 GE 完整生命周期
        // ═══════════════════════════════════════════

        [Test]
        public void InstantGE_RunsToDestruction()
        {
            var src = _arch.CreateGASCarrier("TestUnit");
            var tgt = _arch.CreateGASCarrier("TestUnit");
            _effectService.ApplyEffect(configId: 1, target: tgt, source: src);

            var q = _em.CreateEntityQuery(ComponentType.ReadOnly<WipInstantiateEffect>());
            var list = q.ToEntityArray(Allocator.Temp);
            Assume.That(list.Length, Is.EqualTo(1));
            var ge = list[0];
            list.Dispose();
            q.Dispose();

            // 多帧推进
            for (int f = 0; f < 5; f++)
            {
                _world.Update();
                Debug.Log($"[Pipeline Frame {f}] WipInst={_em.HasComponent<WipInstantiateEffect>(ge)} WipApply={_em.HasComponent<WipApplyEffect>(ge)} Destroy={_em.HasComponent<CEffectDestroy>(ge)} Exists={_em.Exists(ge)}");
            }

            Assert.That(_em.Exists(ge), Is.False, "瞬时 GE 应最终被销毁");
        }

        // ═══════════════════════════════════════════
        // Test 3: 持续 GE 施加 → 属性 +10
        // ═══════════════════════════════════════════

        [Test]
        public void DurationalEffect_Updates_Attribute()
        {
            var src = _arch.CreateGASCarrier("TestUnit");
            var tgt = _arch.CreateGASCarrier("TestUnit");
            var tgtEntity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(tgt);
            SetupAttribute(tgtEntity, attrSetCode: 1, attrCode: 1, baseValue: 100f);

            _effectService.ApplyEffect(configId: 2, target: tgt, source: src);
            TickWorld(10);

            Assert.That(GetAttrValue(tgtEntity, 1, 1),
                Is.EqualTo(110f).Within(0.01f), "BaseValue(100) + Add(10) = 110");
        }

        // ═══════════════════════════════════════════
        // Test 4: 移除 GE → 属性重算回落
        // ═══════════════════════════════════════════

        [Test]
        public void RemoveEffect_Recalculates_Attribute()
        {
            var src = _arch.CreateGASCarrier("TestUnit");
            var tgt = _arch.CreateGASCarrier("TestUnit");
            var tgtEntity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(tgt);
            SetupAttribute(tgtEntity, attrSetCode: 1, attrCode: 1, baseValue: 100f);

            _effectService.ApplyEffect(configId: 2, target: tgt, source: src);
            TickWorld(10);
            Assert.That(GetAttrValue(tgtEntity, 1, 1), Is.EqualTo(110f).Within(0.01f));

            // 销毁所有 GE → 标记属性 Dirty → 重算
            DestroyAllEffects(tgtEntity);
            MarkAttrDirty(tgtEntity, 1, 1);
            _em.AddComponent<CAttributeIsDirty>(tgtEntity);
            TickWorld(10);

            Assert.That(GetAttrValue(tgtEntity, 1, 1),
                Is.EqualTo(100f).Within(0.01f), "GE 移除后应回落至 BaseValue=100");
        }
        // ── 辅助 ────────────────────────────────────

        private void TickWorld(int frames)
        {
            for (int i = 0; i < frames; i++)
                _world.Update();
        }

        private void SetupAttribute(Entity e, int attrSetCode, int attrCode, float baseValue)
        {
            var buf = _em.GetBuffer<BEAttrSet>(e);
            var attrs = new NativeArray<CAttributeData>(1, Allocator.Persistent);
            attrs[0] = new CAttributeData { Code = attrCode, BaseValue = baseValue, CurrentValue = baseValue };
            buf.Add(new BEAttrSet { Code = attrSetCode, Attributes = attrs });
        }

        private float GetAttrValue(Entity e, int attrSetCode, int attrCode)
        {
            var buf = _em.GetBuffer<BEAttrSet>(e);
            for (int i = 0; i < buf.Length; i++)
                if (buf[i].Code == attrSetCode)
                    for (int j = 0; j < buf[i].Attributes.Length; j++)
                        if (buf[i].Attributes[j].Code == attrCode)
                            return buf[i].Attributes[j].CurrentValue;
            return 0f;
        }

        private void DestroyAllEffects(Entity target)
        {
            if (!_em.HasBuffer<BGameplayEffect>(target)) return;
            var buf = _em.GetBuffer<BGameplayEffect>(target);
            var list = new System.Collections.Generic.List<Entity>();
            for (int i = 0; i < buf.Length; i++)
                if (_em.Exists(buf[i].GameplayEffect) && !_em.HasComponent<CEffectDestroy>(buf[i].GameplayEffect))
                    list.Add(buf[i].GameplayEffect);
            foreach (var ge in list)
                if (_em.Exists(ge))
                    _em.AddComponent<CEffectDestroy>(ge);
        }

        private void MarkAttrDirty(Entity e, int attrSetCode, int attrCode)
        {
            var buf = _em.GetBuffer<BEAttrSet>(e);
            for (int i = 0; i < buf.Length; i++)
            {
                if (buf[i].Code != attrSetCode) continue;
                var set = buf[i];
                var attrs = set.Attributes;
                for (int j = 0; j < attrs.Length; j++)
                {
                    if (attrs[j].Code == attrCode)
                    {
                        var d = attrs[j]; d.Dirty = true; attrs[j] = d;
                        set.Attributes = attrs; buf[i] = set;
                        return;
                    }
                }
            }
        }
    }
}
