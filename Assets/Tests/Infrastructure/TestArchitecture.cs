using NexusFramework.GAS.Config;
using NexusFramework.GAS.Models;

namespace NexusFramework.GAS.Tests
{
    public class TestArchitecture : GASArchitecture
    {
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
