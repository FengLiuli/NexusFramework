using NexusFramework;
using NexusFramework.DataCarrier;
using NexusFramework.GAS.ECS;
using NexusFramework.GAS.Services;

namespace NexusFramework.GAS.Commands
{
    public class ApplyEffectCommand : AbstractCommand
    {
        public int ConfigId;
        public int Target;
        public int Source;

        protected override void OnExecute()
        {
            this.GetService<EffectService>().ApplyEffect(ConfigId, Target, Source);
        }
    }

    public class ActivateAbilityCommand : AbstractCommand
    {
        public int Carrier;
        public int AbilityCode;
        public XParam Param;

        protected override void OnExecute()
        {
            this.GetService<AbilityService>().TryActivate(Carrier, AbilityCode, Param);
        }
    }
}