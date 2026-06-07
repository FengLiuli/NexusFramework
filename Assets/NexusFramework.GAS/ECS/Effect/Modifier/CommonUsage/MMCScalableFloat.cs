using System;
using Unity.Entities;
using UnityEngine;

namespace NexusFramework.GAS.ECS
{
    [Serializable]
    public class MMCScalableFloat : ModMagnitudeCalculationBase<MmcParaFloatScale>
    {
        [SerializeField]
        private float k = 1f;

        [SerializeField] private float b;

        public override float CalculateMagnitude(MmcContext context, float magnitude)
        {
            return magnitude * k + b;
        }
    }
}