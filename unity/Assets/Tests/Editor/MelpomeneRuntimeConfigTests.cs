using Foundation.Melpomene;
using NUnit.Framework;
using UnityEngine;

namespace Foundation.Tests.Editor
{
    public class MelpomeneRuntimeConfigTests
    {
        static MelpomeneRuntimeConfig NewConfig()
        {
            return ScriptableObject.CreateInstance<MelpomeneRuntimeConfig>();
        }

        [Test]
        public void IsValid_GitHubDirect_FalseWhenFieldsMissing()
        {
            var config = NewConfig();
            config.submitMode = MelpomeneSubmitMode.GitHubDirect;
            // owner/repo/token すべて空。
            Assert.IsFalse(config.IsValid);

            config.repositoryOwner = "owner";
            config.repositoryName = "repo";
            // token がまだ空。
            Assert.IsFalse(config.IsValid);
        }

        [Test]
        public void IsValid_GitHubDirect_TrueWhenAllFieldsPresent()
        {
            var config = NewConfig();
            config.submitMode = MelpomeneSubmitMode.GitHubDirect;
            config.repositoryOwner = "owner";
            config.repositoryName = "repo";
            config.accessToken = "token";
            Assert.IsTrue(config.IsValid);
        }

        [Test]
        public void IsValid_Relay_FalseWhenRelayUrlMissing()
        {
            var config = NewConfig();
            config.submitMode = MelpomeneSubmitMode.Relay;
            // GitHub フィールドが埋まっていても Relay には無関係。
            config.repositoryOwner = "owner";
            config.repositoryName = "repo";
            config.accessToken = "token";
            Assert.IsFalse(config.IsValid);
        }

        [Test]
        public void IsValid_Relay_TrueWhenRelayUrlPresent()
        {
            var config = NewConfig();
            config.submitMode = MelpomeneSubmitMode.Relay;
            config.relayUrl = "https://example.com/api/melpomene/report";
            Assert.IsTrue(config.IsValid);
        }
    }
}
