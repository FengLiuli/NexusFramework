using NexusFramework;
using NexusFramework.GAS.Config;
using NexusFramework.GAS.ECS;
using NexusFramework.GAS.Models;
using Unity.Collections;
using Unity.Entities;
using GameplayCueConfig = NexusFramework.GAS.Config.GameplayCueConfig;
using MMCConfig = NexusFramework.GAS.Config.MMCConfig;

namespace NexusFramework.GAS.Tests
{
    /// <summary>
    /// 测试用配置加载器——LoadRaw 返回空，Parse 方法直接返回预置测试数据。
    /// 测试通过 ConfigModel.PopulateTestData 批量注册。
    /// </summary>
    public class MockConfigLoader : IConfigLoader
    {
        public bool Initialized { get; set; }

        void ICanInit.Init() => Initialized = true;
        void ICanInit.Deinit() => Initialized = false;

        public string LoadRaw(string fullPath) => null;

        public GameplayEffectComponentConfig[] ParseGameplayEffect(string json) => null;
        public AbilityComponentConfig[] ParseAbility(string json) => null;
        public GameplayCueConfig ParseGameplayCue(string json) => default;
        public MMCConfig ParseMmc(string json) => default;
        public TagHierarchyData ParseTagHierarchy(string json) => default;

        /// <summary>将测试数据注册到 ConfigModel</summary>
        public static void Populate(ConfigModel model)
        {
            // 瞬时 GE
            model.RegisterEffect(1, new GameplayEffectComponentConfig[]
            {
                new TestModConfig(attrSetCode: 1, attrCode: 1, magnitude: 10f)
            });

            // 持续 GE
            model.RegisterEffect(2, new GameplayEffectComponentConfig[]
            {
                new TestDurationConfig(duration: 50),
                new TestModConfig(attrSetCode: 1, attrCode: 1, magnitude: 10f)
            });

            // 可堆叠持续 GE
            model.RegisterEffect(3, new GameplayEffectComponentConfig[]
            {
                new TestDurationConfig(duration: 100),
                new TestStackingConfig(stackingCode: 100, limitCount: 3),
                new TestModConfig(attrSetCode: 1, attrCode: 1, magnitude: 5f)
            });

            // 需求标签 GE
            model.RegisterEffect(10, new GameplayEffectComponentConfig[]
            {
                new TestDurationConfig(duration: 50),
                new TestTagRequireConfig(allTags: new[] { 10 }, anyTags: null, noneTags: null)
            });

            // 免疫标签 GE
            model.RegisterEffect(11, new GameplayEffectComponentConfig[]
            {
                new TestDurationConfig(duration: 50),
                new TestTagImmuneConfig(allTags: new[] { 20 }, anyTags: null, noneTags: null)
            });

            // 授予标签 GE
            model.RegisterEffect(12, new GameplayEffectComponentConfig[]
            {
                new TestDurationConfig(duration: 50),
                new TestGrantedTagConfig(tags: new[] { 10 })
            });

            // Cue GE
            model.RegisterEffect(20, new GameplayEffectComponentConfig[]
            {
                new TestDurationConfig(duration: 50),
                new TestCueConfig()
            });

            // 空能力
            model.RegisterAbility(1, new AbilityComponentConfig[]
            {
                new TestAbilityLogicConfig()
            });
        }
    }

    // ── 测试用配置子类 ──

    internal class TestDurationConfig : GameplayEffectComponentConfig
    {
        private readonly int _duration;
        public TestDurationConfig(int duration) { _duration = duration; }
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            _entityManager.AddComponent<CDuration>(ge);
            _entityManager.SetComponentData(ge, new CDuration { duration = _duration, timeUnit = TimeUnit.Frame, active = false });
        }
    }

    internal class TestModConfig : GameplayEffectComponentConfig
    {
        private readonly int _attrSetCode, _attrCode;
        private readonly float _magnitude;
        public TestModConfig(int attrSetCode, int attrCode, float magnitude) { _attrSetCode = attrSetCode; _attrCode = attrCode; _magnitude = magnitude; }
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            _entityManager.AddComponent<MCModifiers>(ge);
            _entityManager.SetComponentData(ge, new MCModifiers(new EffectModifier[] { new EffectModifier { AttrSetCode = _attrSetCode, AttrCode = _attrCode, Operation = GEOperation.Add, Magnitude = _magnitude } }));
        }
    }

    internal class TestStackingConfig : GameplayEffectComponentConfig
    {
        private readonly int _stackingCode, _limitCount;
        public TestStackingConfig(int stackingCode, int limitCount) { _stackingCode = stackingCode; _limitCount = limitCount; }
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            _entityManager.AddComponent<CStacking>(ge);
            _entityManager.SetComponentData(ge, new CStacking { StackType = EffectStackType.AggregateByTarget, StackingCode = _stackingCode, LimitCount = _limitCount, overflowEffects = new NativeArray<Entity>(0, Allocator.Persistent) });
        }
    }

    internal class TestTagRequireConfig : GameplayEffectComponentConfig
    {
        private readonly int[] _all, _any, _none;
        public TestTagRequireConfig(int[] allTags, int[] anyTags, int[] noneTags) { _all = allTags ?? new int[0]; _any = anyTags ?? new int[0]; _none = noneTags ?? new int[0]; }
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            _entityManager.AddComponent<CApplicationRequiredTags>(ge);
            _entityManager.SetComponentData(ge, new CApplicationRequiredTags { requirement = new TagRequirementData { all = new NativeArray<int>(_all, Allocator.Persistent), any = new NativeArray<int>(_any, Allocator.Persistent), none = new NativeArray<int>(_none, Allocator.Persistent) } });
        }
    }

    internal class TestTagImmuneConfig : GameplayEffectComponentConfig
    {
        private readonly int[] _all, _any, _none;
        public TestTagImmuneConfig(int[] allTags, int[] anyTags, int[] noneTags) { _all = allTags ?? new int[0]; _any = anyTags ?? new int[0]; _none = noneTags ?? new int[0]; }
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            _entityManager.AddComponent<CEffectImmunityTags>(ge);
            _entityManager.SetComponentData(ge, new CEffectImmunityTags { requirement = new TagRequirementData { all = new NativeArray<int>(_all, Allocator.Persistent), any = new NativeArray<int>(_any, Allocator.Persistent), none = new NativeArray<int>(_none, Allocator.Persistent) } });
        }
    }

    internal class TestGrantedTagConfig : GameplayEffectComponentConfig
    {
        private readonly int[] _tags;
        public TestGrantedTagConfig(int[] tags) { _tags = tags ?? new int[0]; }
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            _entityManager.AddComponent<CEffectGrantedTags>(ge);
            _entityManager.SetComponentData(ge, new CEffectGrantedTags { tags = new NativeArray<int>(_tags, Allocator.Persistent) });
        }
    }

    internal class TestCueConfig : GameplayEffectComponentConfig
    {
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            var cueEntity = _entityManager.CreateEntity();
            _entityManager.AddComponent<ECCuePlayable>(cueEntity); _entityManager.SetComponentEnabled<ECCuePlayable>(cueEntity, false);
            _entityManager.AddComponent<ECCuePlaying>(cueEntity);  _entityManager.SetComponentEnabled<ECCuePlaying>(cueEntity, false);
            _entityManager.AddComponent<ECKillCue>(cueEntity);    _entityManager.SetComponentEnabled<ECKillCue>(cueEntity, false);
            _entityManager.AddComponent<MCCue>(cueEntity);
            _entityManager.SetComponentData(cueEntity, new MCCue(new NullCueForTest(_entityManager)));
            _entityManager.AddComponent<CCueOnActivate>(ge);
            _entityManager.SetComponentData(ge, new CCueOnActivate { cues = new NativeArray<Entity>(new[] { cueEntity }, Allocator.Persistent) });
        }
    }

    internal class TestAbilityLogicConfig : AbilityComponentConfig
    {
        public override void LoadToGameplayAbilityEntity(Entity ability)
        {
            _entityManager.AddComponent<MCAbilityLogic>(ability);
            _entityManager.SetComponentData(ability, new MCAbilityLogic(new NullAbilityLogic(ability, _entityManager)));
        }
    }

    public class NullCueForTest : GameplayCueBase
    {
        public NullCueForTest(EntityManager em) : base(em) { }
        public override void InitParameters(XParam xParam) { }
    }
}
