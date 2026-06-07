using System;
using UnityEngine;

namespace NexusFramework.GAS.ECS
{
    public class MMCConfig
    {
        public Type MmcType;
        public XParam MmcParameter;

        public ModMagnitudeCalculationBase CreateMmc()
        {
            if (MmcType == null) return null;
            try
            {
                var instance = (ModMagnitudeCalculationBase)Activator.CreateInstance(MmcType);
                if (MmcParameter != null) instance.InitParameters(MmcParameter);
                return instance;
            }
            catch (Exception e)
            {
                Debug.LogError($"[MMCConfig] Failed to create MMC: {MmcType?.Name}. Error: {e.Message}");
                return null;
            }
        }
    }
}