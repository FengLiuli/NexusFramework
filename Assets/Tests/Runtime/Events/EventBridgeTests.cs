using NexusFramework;
using NexusFramework.DataCarrier;
using NexusFramework.GAS.ECS;
using NexusFramework.GAS.Services;
using NUnit.Framework;

namespace NexusFramework.GAS.Tests
{
    [TestFixture]
    public class EventBridgeTests
    {
        [Test]
        public void EnqueueEvent_DoesNotThrow()
        {
            // 验证事件入队不出错
            Assert.DoesNotThrow(() =>
            {
                GASInternalBridge.Enqueue(new GEActivatedEvent { Target = default, EffectCode = 1 });
                GASInternalBridge.Enqueue(new AttributeChangedEvent { Target = default, AttrSetCode = 1, AttrCode = 1, OldValue = 0, NewValue = 10 });
                GASInternalBridge.Enqueue(new EffectStackChangedEvent { EffectEntity = default, OldStackCount = 1, NewStackCount = 2 });
                GASInternalBridge.Enqueue(new AbilityActivatedEvent { Owner = default, AbilityCode = 1 });
            });
        }

        [Test]
        public void Drain_Calls_OnEventEnqueued()
        {
            int callCount = 0;
            System.Action<object> handler = _ => callCount++;
            GASInternalBridge.OnEventEnqueued += handler;

            GASInternalBridge.Enqueue(new GEActivatedEvent { EffectCode = 1 });
            GASInternalBridge.Enqueue(new GERemovedEvent { EffectCode = 2 });
            GASInternalBridge.Drain();

            Assert.That(callCount, Is.EqualTo(2));

            GASInternalBridge.OnEventEnqueued -= handler;
            GASInternalBridge.Clear();
        }

        [Test]
        public void Drain_Calls_OnBeforeDrain()
        {
            bool called = false;
            System.Action handler = () => called = true;
            GASInternalBridge.OnBeforeDrain += handler;

            GASInternalBridge.Enqueue(new GEActivatedEvent { EffectCode = 1 });
            GASInternalBridge.Drain();

            Assert.That(called, Is.True);

            GASInternalBridge.OnBeforeDrain -= handler;
            GASInternalBridge.Clear();
        }

        [Test]
        public void EventBridgeService_HandlesDispatch_WithoutError()
        {
            var arch = new TestArchitecture();
            arch.Initialize();
            var service = arch.GetService<EventBridgeService>();

            // 注入事件 → Drain 链接触发
            GASInternalBridge.Enqueue(new GEActivatedEvent { EffectCode = 1 });
            GASInternalBridge.Enqueue(new AttributeChangedEvent { AttrSetCode = 1, AttrCode = 1, OldValue = 0, NewValue = 10 });

            Assert.DoesNotThrow(() => GASInternalBridge.Drain());

            arch.Dispose();
            GASInternalBridge.Clear();
        }
    }
}
