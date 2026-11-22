using NUnit.Framework;
using Project.Contexts.Application.Economy;

namespace Project.Tests
{
    public sealed class LevelChainTests
    {
        [Test]
        public void ChainEndsAtLevelTen()
        {
            Assert.AreEqual(10, CampaignRules.ChainLevels);
        }

        [TestCase(1, true)]
        [TestCase(5, true)]
        [TestCase(9, true)]
        [TestCase(10, false)]
        [TestCase(11, false)]
        [TestCase(0, false)]
        [TestCase(-1, false)]
        public void WinChainsIntoNextLevelBeforeLevelTen(int level, bool chains)
        {
            Assert.AreEqual(chains, CampaignRules.ChainsAfterWin(level));
        }

        [TestCase(1, true)]
        [TestCase(10, true)]
        [TestCase(11, false)]
        [TestCase(0, false)]
        public void AppStartsInTheLevelInsideTheChain(int level, bool inChain)
        {
            Assert.AreEqual(inChain, CampaignRules.IsChainLevel(level));
        }

        // Nothing in the chain stops between two levels: no meta period change or interstitial.
        [Test]
        public void ChainWinsHaveNoInterruptions()
        {
            for (var level = 1; level < CampaignRules.ChainLevels; level++)
            {
                Assert.AreEqual(CampaignRules.MetaPeriod(level - 1), CampaignRules.MetaPeriod(level), $"meta L{level}");
                foreach (var group in CampaignRules.AdGroups)
                    Assert.IsFalse(CampaignRules.InterstitialEligible(level, group, false), $"ad L{level} {group.Name}");
            }
        }
    }
}
