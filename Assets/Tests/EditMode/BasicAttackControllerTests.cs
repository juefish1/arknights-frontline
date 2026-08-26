using System.Collections.Generic;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class BasicAttackControllerTests
    {
        private readonly List<GameObject> gameObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in gameObjects)
            {
                Object.DestroyImmediate(gameObject);
            }

            gameObjects.Clear();
        }

        [Test]
        public void AttackTimerRaisesOneRequestPerIntervalForInRangeLegalTarget()
        {
            CombatUnit player = CreateUnit("Player", TeamId.Blue, Vector3.zero, 5f, 0.5f, true);
            CombatUnit target = CreateUnit("Target", TeamId.Red, new Vector3(2f, 0f, 0f), 1f, 1f, false);
            BasicAttackController attack = player.gameObject.AddComponent<BasicAttackController>();
            attack.Configure(player);
            int requestCount = 0;
            attack.AttackRequested += (_, _) => requestCount++;

            attack.SetTarget(target);
            attack.Tick(0f);
            attack.Tick(0.49f);
            attack.Tick(0.01f);

            Assert.That(requestCount, Is.EqualTo(2));
        }

        [Test]
        public void InvalidTargetStopsFutureAttackRequests()
        {
            CombatUnit player = CreateUnit("Player", TeamId.Blue, Vector3.zero, 5f, 0.5f, true);
            CombatUnit target = CreateUnit("Target", TeamId.Red, new Vector3(2f, 0f, 0f), 1f, 1f, false);
            BasicAttackController attack = player.gameObject.AddComponent<BasicAttackController>();
            attack.Configure(player);
            int requestCount = 0;
            attack.AttackRequested += (_, _) => requestCount++;
            attack.SetTarget(target);
            attack.Tick(0f);

            target.TakePhysicalDamage(100f);
            attack.Tick(1f);

            Assert.That(requestCount, Is.EqualTo(1));
        }

        [Test]
        public void OwnerDeathClearsAttackStateImmediately()
        {
            CombatUnit player = CreateUnit("Player", TeamId.Blue, Vector3.zero, 5f, 0.5f, true);
            CombatUnit target = CreateUnit("Target", TeamId.Red, new Vector3(2f, 0f, 0f), 1f, 1f, false);
            BasicAttackController attack = player.gameObject.AddComponent<BasicAttackController>();
            attack.Configure(player);
            int requestCount = 0;
            attack.AttackRequested += (_, _) => requestCount++;
            attack.SetTarget(target);

            player.TakePhysicalDamage(100f);
            attack.Tick(0f);

            Assert.That(requestCount, Is.EqualTo(0));
        }

        [Test]
        public void ZeroIntervalEmitsAtMostOneRequestPerTick()
        {
            CombatUnit player = CreateUnit("Player", TeamId.Blue, Vector3.zero, 5f, 0f, true);
            CombatUnit target = CreateUnit("Target", TeamId.Red, new Vector3(2f, 0f, 0f), 1f, 1f, false);
            BasicAttackController attack = player.gameObject.AddComponent<BasicAttackController>();
            attack.Configure(player);
            int requestCount = 0;
            attack.AttackRequested += (_, _) => requestCount++;
            attack.SetTarget(target);

            attack.Tick(2f);

            Assert.That(requestCount, Is.EqualTo(1));
        }

        private CombatUnit CreateUnit(string name, TeamId team, Vector3 position, float attackRange, float attackInterval, bool canAttackGround)
        {
            GameObject gameObject = new GameObject(name);
            gameObjects.Add(gameObject);
            gameObject.transform.position = position;
            CombatUnit unit = gameObject.AddComponent<CombatUnit>();
            unit.Configure(team, Altitude.Ground, 10f, 1f, 0f, attackRange, attackInterval, canAttackGround, false);
            return unit;
        }
    }
}
