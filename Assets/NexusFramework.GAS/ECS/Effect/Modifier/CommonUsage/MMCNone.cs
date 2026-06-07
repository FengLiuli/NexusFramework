using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public class MMCNone : ModMagnitudeCalculationBase<XParamNone>
    {
        public override float CalculateMagnitude(MmcContext context, float magnitude)
        {
            return magnitude;
        }
    }
}