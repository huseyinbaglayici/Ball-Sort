using NUnit.Framework;
using Project.Contexts.Home;

namespace Project.Tests
{
    public sealed class ChapterArtTests
    {
        [TestCase(1, 0)]
        [TestCase(20, 0)]
        [TestCase(21, 1)]
        [TestCase(40, 1)]
        [TestCase(41, 2)]
        [TestCase(61, 3)]
        [TestCase(81, 4)]
        [TestCase(100, 4)]
        [TestCase(101, 0)]
        [TestCase(0, 0)]
        public void Five_chapters_of_twenty_levels_cycle(int level, int expected) =>
            Assert.That(ChapterArt.IndexOf(level, 20, 5), Is.EqualTo(expected));

        [Test]
        public void No_chapters_gives_no_index() => Assert.That(ChapterArt.IndexOf(5, 20, 0), Is.EqualTo(-1));
    }
}
