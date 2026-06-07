using Unity.Entities;
using NexusFramework;
using NexusFramework.GAS.ECS;

namespace NexusFramework.GAS.Services
{
    public class TimerService : AbstractService
    {
        private Entity _timerEntity;

        protected override void OnInit()
        {
            var em = this.GetService<WorldService>().EntityManager;
            _timerEntity = em.CreateSingleton<GlobalTimer>();
        }

        protected override void OnDeinit()
        {
            _timerEntity = Entity.Null;
        }

        public GlobalTimer GetGlobalTimer()
        {
            var em = this.GetService<WorldService>().EntityManager;
            return em.GetComponentData<GlobalTimer>(_timerEntity);
        }

        public int CurrentFrame => GetGlobalTimer().Frame;
        public int CurrentTurn => GetGlobalTimer().Turn;
    }
}
