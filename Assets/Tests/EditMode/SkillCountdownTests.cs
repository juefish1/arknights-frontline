using ArknightsFrontline.Skills;
using NUnit.Framework;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class SkillCountdownTests
    {
        [Test]
        public void CountdownClampsAtZeroAndReportsReady()
        {
            SkillCountdown timer = new SkillCountdown();
            timer.Reset(10f);
            timer.Tick(9.9f);
            Assert.That(timer.IsReady, Is.False);
            timer.Tick(0.1f);
            Assert.That(timer.Remaining, Is.EqualTo(0f));
            Assert.That(timer.IsReady, Is.True);
        }

        [Test]
        public void TickRoundsNearZeroDownWithoutDiscardingLargerRemainingTime()
        {
            SkillCountdown timer = new SkillCountdown();
            timer.Reset(1f);
            timer.Tick(0.99995f);
            Assert.That(timer.Remaining, Is.EqualTo(0f));

            timer.Reset(1f);
            timer.Tick(0.9998f);
            Assert.That(timer.Remaining, Is.EqualTo(0.0002f).Within(0.00001f));
        }
    }
}
