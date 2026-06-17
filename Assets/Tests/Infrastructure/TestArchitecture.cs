using NexusFramework.GAS.Config;
using NexusFramework.GAS.Models;

namespace NexusFramework.GAS.Tests
{
    public class TestArchitecture : GASArchitecture
    {
        public TestArchitecture() : base() { ArchitectureId = 0; }
        public TestArchitecture(byte id) : base(id) => ArchitectureId = id;

        protected override IConfigLoader CreateConfigLoader()
        {
            return new MockConfigLoader();
        }

        protected override void OnInit()
        {
            base.OnInit();
            var model = (ConfigModel)GetModel<ConfigModel>();
            MockConfigLoader.Populate(model);
        }
    }
}
