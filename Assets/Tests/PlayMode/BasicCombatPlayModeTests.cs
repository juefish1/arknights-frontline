using System.Collections;
using System.Collections.Generic;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Commands;
using ArknightsFrontline.Common;
using ArknightsFrontline.Movement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArknightsFrontline.Tests.PlayMode
{
    public sealed class BasicCombatPlayModeTests
    {
        private readonly List<GameObject> gameObjects = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject gameObject in gameObjects)
            {
                Object.Destroy(gameObject);
            }

            gameObjects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator AttackCommandPursuesThenSpawnsProjectilesAndKillsTarget()
        {
            GameObject playerObject = new GameObject("Player");
            gameObjects.Add(playerObject);
            UnitMotor motor = playerObject.AddComponent<UnitMotor>();
            motor.Configure(5f, ArenaLayout.CreateDefault());
            PlayerCommandController commands = playerObject.AddComponent<PlayerCommandController>();
            CombatUnit player = playerObject.AddComponent<CombatUnit>();
            player.Configure(TeamId.Blue, Altitude.Ground, 100f, 12f, 2f, 6f, 0.5f, true, true);
            BasicAttackController attack = playerObject.AddComponent<BasicAttackController>();
            attack.Configure(player);
            CombatCommandResolver resolver = playerObject.AddComponent<CombatCommandResolver>();
            resolver.Configure(player, motor, commands, attack);

            GameObject targetObject = new GameObject("TrainingDummy_Red");
            gameObjects.Add(targetObject);
            targetObject.transform.position = new Vector3(12f, 0f, 0f);
            CombatUnit target = targetObject.AddComponent<CombatUnit>();
            target.Configure(TeamId.Red, Altitude.Ground, 40f, 0f, 2f, 0f, 0f, false, false);

            commands.Issue(UnitCommand.Attack(targetObject));
            bool movedBeforeAttack = false;
            bool stoppedInRange = false;
            bool spawnedVisibleProjectile = false;

            for (int step = 0; step < 500 && !target.IsDead; step++)
            {
                resolver.Tick(0.05f);
                movedBeforeAttack |= motor.IsMoving;
                motor.Tick(0.05f);
                resolver.Tick(0.05f);
                if (resolver.CurrentTarget == target)
                {
                    stoppedInRange |= !motor.IsMoving;
                }

                attack.Tick(0.05f);
                Projectile[] projectiles = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None);
                foreach (Projectile projectile in projectiles)
                {
                    spawnedVisibleProjectile |= projectile.GetComponent<Renderer>() != null;
                    projectile.Tick(0.05f);
                }
            }

            Assert.That(movedBeforeAttack, Is.True);
            Assert.That(stoppedInRange, Is.True);
            Assert.That(spawnedVisibleProjectile, Is.True);
            Assert.That(target.IsDead, Is.True);
            yield return null;
        }
    }
}
