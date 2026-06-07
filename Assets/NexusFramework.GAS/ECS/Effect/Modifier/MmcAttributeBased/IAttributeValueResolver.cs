using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public interface IAttributeValueResolver
    {
        EntityManager GetEntityManager();
        float Resolve(EntityManager em, Entity ascEntity, int attrSetCode, int attrCode);
    }
}
