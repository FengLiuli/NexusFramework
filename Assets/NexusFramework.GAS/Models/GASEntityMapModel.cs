using System.Collections.Generic;
using Unity.Entities;
using NexusFramework;
using NexusFramework.DataCarrier;

namespace NexusFramework.GAS.Models
{
    public class GASEntityMapModel : AbstractModel
    {
        private readonly Dictionary<CarrierId, Entity> _forward = new();
        private readonly Dictionary<Entity, CarrierId> _reverse = new();

        public void Bind(CarrierId carrierId, Entity gasEntity)
        {
            _forward[carrierId] = gasEntity;
            _reverse[gasEntity] = carrierId;
        }

        public void Unbind(CarrierId carrierId)
        {
            if (_forward.TryGetValue(carrierId, out var entity))
            {
                _reverse.Remove(entity);
                _forward.Remove(carrierId);
            }
        }

        public Entity GetGASEntity(CarrierId carrierId)
            => _forward.GetValueOrDefault(carrierId, Entity.Null);

        public CarrierId GetCarrierId(Entity gasEntity)
            => _reverse.GetValueOrDefault(gasEntity, new CarrierId());

        public bool ContainsCarrier(CarrierId carrierId)
            => _forward.ContainsKey(carrierId);

        public bool ContainsEntity(Entity gasEntity)
            => _reverse.ContainsKey(gasEntity);

        public void Clear()
        {
            _forward.Clear();
            _reverse.Clear();
        }

        protected override void OnInit() { }

        protected override void OnDeinit()
        {
            Clear();
        }
    }
}
