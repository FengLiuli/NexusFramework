using NexusFramework;
using NexusFramework.GAS.ECS;

namespace NexusFramework.GAS.Services
{
    public class EventBridgeService : AbstractService
    {
        protected override void OnInit()
        {
            GASInternalBridge.OnEventEnqueued += Dispatch;
        }

        protected override void OnDeinit()
        {
            GASInternalBridge.OnEventEnqueued -= Dispatch;
        }

        private void Dispatch(object evt)
        {
            switch (evt)
            {
                case GEAppliedEvent e:          this.SendEvent(e); break;
                case GEActivatedEvent e:        this.SendEvent(e); break;
                case GERemovedEvent e:          this.SendEvent(e); break;
                case AttributeChangedEvent e:   this.SendEvent(e); break;
                case AttributeBaseChangedEvent e: this.SendEvent(e); break;
                case AbilityActivatedEvent e:   this.SendEvent(e); break;
                case AbilityEndedEvent e:       this.SendEvent(e); break;
                case AbilityCancelledEvent e:   this.SendEvent(e); break;
                case EffectStackChangedEvent e: this.SendEvent(e); break;
            }
        }
    }
}
