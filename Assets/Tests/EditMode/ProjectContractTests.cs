using ArknightsFrontline.Common;
using NUnit.Framework;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class ProjectContractTests
    {
        [Test]
        public void StableEnumsHaveExpectedValues()
        {
            Assert.That((int)TeamId.Blue, Is.EqualTo(0));
            Assert.That((int)TeamId.Red, Is.EqualTo(1));
            Assert.That((int)Altitude.Ground, Is.EqualTo(0));
            Assert.That((int)Altitude.Air, Is.EqualTo(1));
            Assert.That(MatchState.Running.ToString(), Is.EqualTo("Running"));
        }
    }
}
