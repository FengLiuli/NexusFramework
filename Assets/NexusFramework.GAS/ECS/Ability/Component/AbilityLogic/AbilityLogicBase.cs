using Unity.Entities;
using NexusFramework;
using NexusFramework.GAS.Services;

namespace NexusFramework.GAS.ECS
{
    public abstract class AbilityLogicBase : IBelongToArchitecture
    {
        public EntityManager _entityManager;
        protected XParam _paramRaw;
        protected Entity _abilityEntity;
        protected int _code;

        public IArchitecture Architecture { get; set; }

        protected AbilityLogicBase(Entity ability, IArchitecture architecture)
        {
            Architecture = architecture;
            _entityManager = architecture.GetService<WorldService>().EntityManager;
            Init(ability);
        }

        protected AbilityLogicBase(Entity ability, EntityManager entityManager)
        {
            _entityManager = entityManager;
            Init(ability);
        }

        private void Init(Entity ability)
        {
            if (ability != Entity.Null)
            {
                _abilityEntity = ability;
                if (_entityManager.HasComponent<CAbilityBaseInfo>(_abilityEntity))
                {
                    var basicInfo = _entityManager.GetComponentData<CAbilityBaseInfo>(_abilityEntity);
                    _code = basicInfo.Code;
                }
            }
        }

        public abstract void ActivateAbility(GlobalTimer timer);
        public abstract void CancelAbility(GlobalTimer timer);
        public abstract void EndAbility(GlobalTimer timer);
        public abstract void AbilityTick(GlobalTimer timer);

        public void SetAbilityEntity(Entity abilityEntity) => _abilityEntity = abilityEntity;
        public Entity GetAbilityEntity() => _abilityEntity;

        public Entity GetOwnerAscEntity()
        {
            if (_entityManager.HasComponent<CAbilityBaseInfo>(_abilityEntity))
                return _entityManager.GetComponentData<CAbilityBaseInfo>(_abilityEntity).Owner;
            return Entity.Null;
        }

        public Entity OwnerEntity => GetOwnerAscEntity();

        public virtual void TryEndSelf()
        {
            if (_entityManager.Exists(_abilityEntity))
                _entityManager.AddComponent<CAbilityInTryEnd>(_abilityEntity);
        }

        public virtual void SetParam(XParam param) => _paramRaw = param;
        public XParam GetParam() => _paramRaw;

        protected Entity CreateGameplayEffectEntity(GameplayEffectComponentConfig[] configs)
        {
            GameplayEffectComponentConfig.SetEntityManager(_entityManager);
            var entity = _entityManager.CreateEntity();
            foreach (var config in configs)
                config.LoadToGameplayEffectEntity(entity);
            return entity;
        }

        protected void ApplyGameplayEffectTo(Entity gameplayEffect, Entity target, Entity source)
        {
            _entityManager.AddComponent<CEffectInUsage>(gameplayEffect);
            _entityManager.AddComponent<WipInstantiateEffect>(gameplayEffect);
            _entityManager.SetComponentData(gameplayEffect, new CEffectInUsage
            {
                Source = source,
                Target = target
            });
            _entityManager.AddComponent<CCreatedByAbility>(gameplayEffect);
            _entityManager.SetComponentData(gameplayEffect, new CCreatedByAbility
            {
                sourceAbility = _abilityEntity
            });
        }

        protected void RemoveGameplayEffect(Entity gameplayEffect)
        {
            if (_entityManager.Exists(gameplayEffect) && _entityManager.HasComponent<CEffectInUsage>(gameplayEffect))
            {
                _entityManager.AddComponent<CEffectDestroy>(gameplayEffect);
            }
        }
    }

    public abstract class AbilityLogicBase<T> : AbilityLogicBase where T : XParam
    {
        protected T _param;

        protected AbilityLogicBase(Entity ability, IArchitecture architecture) : base(ability, architecture) { }

        public override void SetParam(XParam param)
        {
            base.SetParam(param);
            if (param is T t) SetParam(t);
        }

        public void SetParam(T param) => _param = param;
        public new T GetParam() => _param;
    }
}
