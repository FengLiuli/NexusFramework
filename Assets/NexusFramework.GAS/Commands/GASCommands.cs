using NexusFramework;
using NexusFramework.DataCarrier;
using NexusFramework.GAS.ECS;
using NexusFramework.GAS.Services;

namespace NexusFramework.GAS.Commands
{
    public class ApplyEffectCommand : AbstractCommand
    {
        public int ConfigId;
        public CarrierId Target;
        public CarrierId Source;

        protected override void OnExecute()
        {
            this.GetService<EffectService>().ApplyEffect(ConfigId, Target, Source);
        }
    }

    public class ActivateAbilityCommand : AbstractCommand
    {
        public CarrierId Carrier;
        public int AbilityCode;
        public XParam Param;

        protected override void OnExecute()
        {
            this.GetService<AbilityService>().TryActivate(Carrier, AbilityCode, Param);
        }
    }
}