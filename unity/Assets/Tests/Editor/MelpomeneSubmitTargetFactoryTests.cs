using Foundation.Melpomene;
using NUnit.Framework;
using UnityEngine;

namespace Foundation.Tests.Editor
{
    public class MelpomeneSubmitTargetFactoryTests
    {
        static MelpomeneRuntimeConfig NewConfig()
        {
            return ScriptableObject.CreateInstance<MelpomeneRuntimeConfig>();
        }

        [Test]
        public void Create_GitHubDirect_ReturnsGitHubTarget()
        {
            var config = NewConfig();
            config.submitMode = MelpomeneSubmitMode.GitHubDirect;

            var target = MelpomeneSubmitTargetFactory.Create(config);

            Assert.That(target, Is.InstanceOf<MelpomeneGitHubTarget>());
        }

        [Test]
        public void Create_Relay_ReturnsRelayTarget()
        {
            var config = NewConfig();
            config.submitMode = MelpomeneSubmitMode.Relay;

            var target = MelpomeneSubmitTargetFactory.Create(config);

            Assert.That(target, Is.InstanceOf<MelpomeneRelayTarget>());
        }
    }
}
