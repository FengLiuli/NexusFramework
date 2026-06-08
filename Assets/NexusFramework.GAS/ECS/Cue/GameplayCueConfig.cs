using System;

namespace NexusFramework.GAS.ECS
{
    [Serializable]
    public class GameplayCueConfig
    {
        public Type CueType;
        public XParam Param;

        public int[] RequiredAllTags;
        public int[] RequiredAnyTags;
        public int[] RequiredNoneTags;
        public int[] ImmunityAllTags;
        public int[] ImmunityAnyTags;
        public int[] ImmunityNoneTags;

        public GameplayCueConfig()
        {
        }

        public GameplayCueConfig(Type cueType, XParam param,
            int[] requiredAllTags = null, int[] requiredAnyTags = null, int[] requiredNoneTags = null,
            int[] immunityAllTags = null, int[] immunityAnyTags = null, int[] immunityNoneTags = null)
        {
            CueType = cueType;
            Param = param;
            RequiredAllTags = requiredAllTags ?? Array.Empty<int>();
            RequiredAnyTags = requiredAnyTags ?? Array.Empty<int>();
            RequiredNoneTags = requiredNoneTags ?? Array.Empty<int>();
            ImmunityAllTags = immunityAllTags ?? Array.Empty<int>();
            ImmunityAnyTags = immunityAnyTags ?? Array.Empty<int>();
            ImmunityNoneTags = immunityNoneTags ?? Array.Empty<int>();
        }

        public GameplayCueBase CreateCue(IGASEntityResolver resolver = null)
        {
            return CueHelper.TryCreateCue(CueType, Param, resolver);
        }
    }
}
