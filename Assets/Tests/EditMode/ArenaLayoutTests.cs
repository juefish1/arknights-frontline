using ArknightsFrontline.Arena;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class ArenaLayoutTests
    {
        [Test]
        public void DefaultLayoutMirrorsTowersAndDeploymentZones()
        {
            ArenaLayout layout = ArenaLayout.CreateDefault();

            Assert.That(layout.BlueTower, Is.EqualTo(-layout.RedTower));
            Assert.That(layout.BlueDeployment, Is.EqualTo(-layout.RedDeployment));
            Assert.That(layout.LaneHalfLength, Is.EqualTo(50f));
            Assert.That(layout.LaneHalfWidth, Is.EqualTo(12f));
        }

        [Test]
        public void ClampToArenaKeepsPointInsideBounds()
        {
            ArenaLayout layout = ArenaLayout.CreateDefault();

            Assert.That(layout.Clamp(new Vector3(90f, 0f, 30f)), Is.EqualTo(new Vector3(50f, 0f, 12f)));
        }
    }
}
