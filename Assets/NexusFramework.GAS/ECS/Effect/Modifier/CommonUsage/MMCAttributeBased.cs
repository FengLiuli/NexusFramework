using System;
using Unity.Entities;
using UnityEngine;

namespace NexusFramework.GAS.ECS
{
    public sealed class MMCAttributeBased : ModMagnitudeCalculationBase<AttributeBasedMmcParam>
    {
        private bool _snapshotCaptured;
        private float _snapshotValue;

        public override float CalculateMagnitude(MmcContext mmcContext, float magnitude)
        {
            var resolver = AttributeBasedMmcParam.GetResolver();
            if (resolver == null)
            {
                Debug.LogError("[AttributeBasedMmc] IAttributeValueResolver not registered.");
                return magnitude;
            }

            float attrValue;
            if (Parameter.CaptureType == AttributeCaptureType.SnapShot)
            {
                if (!_snapshotCaptured)
                {
                    _snapshotValue = ResolveAttributeValue(mmcContext, resolver);
                    _snapshotCaptured = true;
                }
                attrValue = _snapshotValue;
            }
            else
            {
                attrValue = ResolveAttributeValue(mmcContext, resolver);
            }

            return attrValue * Parameter.K + Parameter.B;
        }

        protected override void OnAdded(MmcContext mmcContext, int targetAttrSetCode, int targetAttrCode)
        {
        }

        protected override void OnRemoved()
        {
        }

        private float ResolveAttributeValue(MmcContext mmcContext, IAttributeValueResolver resolver)
        {
            var entity = Parameter.FromType == AttributeFromType.Source
                ? mmcContext.Source
                : mmcContext.Target;

            if (entity == Entity.Null) return 0f;

            var em = resolver.GetEntityManager();
            return resolver.Resolve(em, entity, Parameter.AttrSetCode, Parameter.AttrCode);
        }
    }
}
