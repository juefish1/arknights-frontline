using System;
using System.Collections.Generic;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class CorpseLifetimeControllerTests
    {
        private readonly List<GameObject> gameObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in gameObjects)
            {
                if (gameObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(gameObject);
                }
            }

            gameObjects.Clear();
        }

        [Test]
        public void MinionExistsBeforeFiveSecondsAndIsDestroyedAtFiveSeconds()
        {
            CorpseLifetimeController controller = CreateController("MinionCorpse");
            controller.Configure(UnitKind.Minion, null);

            controller.Tick(4.99f);

            Assert.That(controller, Is.Not.Null);
            controller.Tick(0.01f);

            Assert.That(controller == null, Is.True);
        }

        [Test]
        public void NegativeDeltaDoesNotAdvanceMinionLifetime()
        {
            CorpseLifetimeController controller = CreateController("NegativeDeltaMinionCorpse");
            controller.Configure(UnitKind.Minion, null);

            controller.Tick(-10f);
            controller.Tick(4.99f);

            Assert.That(controller, Is.Not.Null);
            controller.Tick(0.01f);

            Assert.That(controller == null, Is.True);
        }

        [Test]
        public void OperatorPersistsUntilMatchingOwnerKeyIsCleared()
        {
            CorpseLifetimeController controller = CreateController("OperatorCorpse");
            controller.Configure(UnitKind.Operator, "operator-owner");

            controller.Tick(60f);
            CorpseLifetimeController.ClearOperatorCorpse("different-owner");
            CorpseLifetimeController.ClearOperatorCorpse(null);
            CorpseLifetimeController.ClearOperatorCorpse(string.Empty);

            Assert.That(controller, Is.Not.Null);
            Assert.DoesNotThrow(() => CorpseLifetimeController.ClearOperatorCorpse("operator-owner"));
            Assert.That(controller == null, Is.True);
            Assert.DoesNotThrow(() => CorpseLifetimeController.ClearOperatorCorpse("operator-owner"));
        }

        [TestCase(null)]
        [TestCase("")]
        public void OperatorRejectsNullOrEmptyOwnerKey(string ownerKey)
        {
            CorpseLifetimeController controller = CreateController("InvalidOperatorCorpse");

            Assert.Throws<ArgumentException>(() => controller.Configure(UnitKind.Operator, ownerKey));
            Assert.That(controller, Is.Not.Null);
        }

        [Test]
        public void RegisteringSameOperatorKeyReplacesOnlyTheOlderCorpse()
        {
            CorpseLifetimeController first = CreateController("FirstOperatorCorpse");
            first.Configure(UnitKind.Operator, "shared-owner");
            CorpseLifetimeController second = CreateController("SecondOperatorCorpse");

            second.Configure(UnitKind.Operator, "shared-owner");

            Assert.That(first == null, Is.True);
            Assert.That(second, Is.Not.Null);
            CorpseLifetimeController.ClearOperatorCorpse("shared-owner");
            Assert.That(second == null, Is.True);
        }

        [Test]
        public void ReconfiguringOperatorUnregistersItsPreviousOwnerKey()
        {
            CorpseLifetimeController controller = CreateController("ReconfiguredOperatorCorpse");
            controller.Configure(UnitKind.Operator, "old-owner");

            controller.Configure(UnitKind.Operator, "new-owner");
            CorpseLifetimeController.ClearOperatorCorpse("old-owner");

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.OwnerKey, Is.EqualTo("new-owner"));
            CorpseLifetimeController.ClearOperatorCorpse("new-owner");
            Assert.That(controller == null, Is.True);
        }

        [Test]
        public void DestroyingOperatorRemovesItsRegistration()
        {
            CorpseLifetimeController controller = CreateController("DestroyedOperatorCorpse");
            controller.Configure(UnitKind.Operator, "destroyed-owner");

            UnityEngine.Object.DestroyImmediate(controller.gameObject);

            Assert.DoesNotThrow(() => CorpseLifetimeController.ClearOperatorCorpse("destroyed-owner"));
        }

        [Test]
        public void TowerPersistsWithoutRegistryEntryOrTimer()
        {
            CorpseLifetimeController controller = CreateController("TowerCorpse");
            controller.Configure(UnitKind.Tower, "tower-owner");

            controller.Tick(60f);
            CorpseLifetimeController.ClearOperatorCorpse("tower-owner");

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.UnitKind, Is.EqualTo(UnitKind.Tower));
        }

        private CorpseLifetimeController CreateController(string name)
        {
            GameObject gameObject = new GameObject(name);
            gameObjects.Add(gameObject);
            return gameObject.AddComponent<CorpseLifetimeController>();
        }
    }
}
