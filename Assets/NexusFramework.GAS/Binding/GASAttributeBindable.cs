using NexusFramework;
using System;
using NexusFramework.DataCarrier;
using NexusFramework.GAS.Events;

namespace NexusFramework.GAS.Binding
{
    public class GASAttributeBindable : BindableProperty<float>
    {
        private readonly CarrierId _carrierId;
        private readonly int _attrSetCode;
        private readonly int _attrCode;
        private IUnRegister _eventRegistration;

        public GASAttributeBindable(CarrierId carrierId, int attrSetCode, int attrCode, float defaultValue = 0f)
            : base(defaultValue)
        {
            _carrierId = carrierId;
            _attrSetCode = attrSetCode;
            _attrCode = attrCode;
        }

        public void Bind(IArchitecture architecture)
        {
            _eventRegistration = architecture.RegisterEvent<GASAttributeChangedEvent>(OnAttributeChanged);
        }

        public void Unbind()
        {
            _eventRegistration?.UnRegister();
        }

        private void OnAttributeChanged(GASAttributeChangedEvent e)
        {
            if (e.CarrierId.Equals(_carrierId) && e.AttrSetCode == _attrSetCode && e.AttrCode == _attrCode)
                Value = e.NewValue;
        }
    }
}