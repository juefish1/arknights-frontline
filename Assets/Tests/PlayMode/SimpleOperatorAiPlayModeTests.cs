using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using ArknightsFrontline.Movement;
using ArknightsFrontline.Skills;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArknightsFrontline.Tests.PlayMode
{
    public sealed class SimpleOperatorAiPlayModeTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<Material> materials = new List<Material>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject gameObject in objects)
            {
                if (gameObject != null) UnityEngine.Object.Destroy(gameObject);
            }

            foreach (Material material in materials)
            {
                if (material != null) UnityEngine.Object.Destroy(material);
            }

            objects.Clear();
            materials.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator FiveComputerOperatorsMoveAttackRetreatAndRedeployWithoutSkills()
        {
            ArenaLayout layout = ArenaLayout.CreateDefault();
            CombatUnit blueTower = CreateTower("BlueTower", TeamId.Blue, layout.BlueTower);
            CombatUnit redTower = CreateTower("RedTower", TeamId.Red, layout.RedTower);
            OperatorRosterController roster = CreateObject("OperatorRoster").AddComponent<OperatorRosterController>();
            Dictionary<TeamId, CombatUnit> towers = new Dictionary<TeamId, CombatUnit>
            {
                [TeamId.Blue] = blueTower,
                [TeamId.Red] = redTower
            };
            List<CombatUnit> spawned = new List<CombatUnit>();
            roster.OperatorSpawned += (slot, live) =>
            {
                spawned.Add(live);
                objects.Add(live.gameObject);
                live.GetComponent<SimpleOperatorAiController>().Configure(
                    live.GetComponent<OperatorIdentity>(), slot, towers[slot.Team],
                    live.GetComponent<OperatorRetreatController>(), layout);
            };

            AddComputerSlot(roster, OperatorType.Exusiai, TeamId.Blue, new Vector3(-46f, 0f, -2f), "Blue-Exusiai");
            AddComputerSlot(roster, OperatorType.SilverAsh, TeamId.Blue, new Vector3(-46f, 0f, 2f), "Blue-SilverAsh");
            AddComputerSlot(roster, OperatorType.Exusiai, TeamId.Red, new Vector3(46f, 0f, -2f), "Red-Exusiai");
            AddComputerSlot(roster, OperatorType.Eyjafjalla, TeamId.Red, new Vector3(46f, 0f, 0f), "Red-Eyjafjalla");
            AddComputerSlot(roster, OperatorType.SilverAsh, TeamId.Red, new Vector3(46f, 0f, 2f), "Red-SilverAsh");
            roster.StartMatch();

            Assert.That(roster.Slots, Has.Count.EqualTo(5));
            CombatUnit[] firstLives = roster.Slots.Select(slot => slot.CurrentOperator).ToArray();
            Assert.That(firstLives.All(unit => unit != null), Is.True);
            Assert.That(firstLives.All(unit => unit.GetComponent<SimpleOperatorAiController>() != null), Is.True);
            Assert.That(firstLives.All(unit => unit.GetComponent<ExusiaiSkillController>() == null), Is.True,
                "Computer lives must not carry a player skill controller.");

            yield return new WaitForSeconds(0.35f);

            for (int index = 0; index < firstLives.Length; index++)
            {
                Assert.That(Vector3.Distance(firstLives[index].transform.position,
                    roster.Slots[index].DeploymentPosition), Is.GreaterThan(0.5f),
                    $"Computer {roster.Slots[index].StableKey} should move during real frames.");
            }

            CombatUnit practiceTarget = CreateUnit(
                "AI-attack-observer", TeamId.Red, new Vector3(-40f, 0f, 0f), 5000f, 0f);
            practiceTarget.gameObject.AddComponent<OperatorIdentity>().Configure(
                "AI-attack-observer", TeamId.Red, OperatorType.Exusiai);
            float healthBeforeAttacks = practiceTarget.CurrentHealth;
            yield return new WaitForSeconds(1.8f);

            Assert.That(practiceTarget.CurrentHealth, Is.LessThan(healthBeforeAttacks),
                "At least one computer should use its real BasicAttackController and projectile to damage an in-range enemy.");

            OperatorRosterSlot retreatingSlot = roster.Slots[0];
            CombatUnit oldLife = retreatingSlot.CurrentOperator;
            oldLife.TakePhysicalDamage(oldLife.CurrentHealth - 200f);
            yield return null;

            Assert.That(oldLife.GetComponent<OperatorRetreatController>().IsGuiding, Is.True,
                "A computer below 25% health should begin the shared retreat guidance.");
            yield return new WaitForSeconds(1.7f);

            Assert.That(retreatingSlot.CurrentOperator, Is.Null);
            Assert.That(retreatingSlot.DepartureCount, Is.EqualTo(1));
            Assert.That(retreatingSlot.RedeployRemaining, Is.GreaterThan(5.2f));

            yield return new WaitForSeconds(5.8f);

            CombatUnit replacement = retreatingSlot.CurrentOperator;
            Assert.That(replacement, Is.Not.Null, "The computer roster should create a fresh life after retreat.");
            Assert.That(replacement, Is.Not.SameAs(oldLife));
            Assert.That(replacement.CurrentHealth, Is.EqualTo(replacement.MaxHealth));
            Assert.That(replacement.GetComponent<SimpleOperatorAiController>(), Is.Not.Null);
            Assert.That(replacement.GetComponent<ExusiaiSkillController>(), Is.Null,
                "The redeployed computer still must not acquire player W/E/R behavior.");
            Assert.That(spawned.Count, Is.GreaterThan(5));
        }

        [UnityTest]
        public IEnumerator DestroyedAiUnsubscribesAndClearsTarget()
        {
            ArenaLayout layout = ArenaLayout.CreateDefault();
            CombatUnit friendlyTower = CreateTower("BlueTower", TeamId.Blue, layout.BlueTower);
            GameObject ownerObject = CreateObject("AI-owner");
            CombatUnit owner = ownerObject.AddComponent<CombatUnit>();
            owner.Configure(TeamId.Blue, Altitude.Ground, 100f, 20f, 0f, 8f, 0.5f, true, true);
            OperatorIdentity identity = ownerObject.AddComponent<OperatorIdentity>();
            identity.Configure("AI-owner", TeamId.Blue, OperatorType.Exusiai);
            UnitMotor motor = ownerObject.AddComponent<UnitMotor>();
            motor.Configure(5f, layout);
            BasicAttackController attacks = ownerObject.AddComponent<BasicAttackController>();
            attacks.Configure(owner);
            OperatorRetreatController retreat = ownerObject.AddComponent<OperatorRetreatController>();
            SimpleOperatorAiController controller = ownerObject.AddComponent<SimpleOperatorAiController>();
            controller.Configure(identity, null, friendlyTower, retreat, layout);
            CombatUnit enemy = CreateUnit("AI-enemy", TeamId.Red, new Vector3(3f, 0f, 0f), 1000f, 0f);
            enemy.gameObject.AddComponent<OperatorIdentity>().Configure("AI-enemy", TeamId.Red, OperatorType.Exusiai);

            controller.Tick(0f);
            Assert.That(controller.CurrentTarget, Is.EqualTo(enemy));
            FieldInfo damageEvent = typeof(CombatUnit).GetField(
                "DamageTaken", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(damageEvent, Is.Not.Null);
            Delegate[] listeners = ((Delegate)damageEvent.GetValue(owner)).GetInvocationList();
            Assert.That(listeners.Any(listener => listener.Target == (object)controller
                && listener.Method.Name == "OnOwnerDamageTaken"), Is.True);

            UnityEngine.Object.Destroy(controller);
            yield return null;

            Assert.That(controller == null, Is.True);
            Assert.That(attacks.CurrentTarget, Is.Null);
            Delegate[] remainingListeners = ((Delegate)damageEvent.GetValue(owner)).GetInvocationList();
            Assert.That(remainingListeners.Any(listener => listener.Target == (object)controller
                && listener.Method.Name == "OnOwnerDamageTaken"), Is.False);
        }

        private void AddComputerSlot(
            OperatorRosterController roster,
            OperatorType type,
            TeamId team,
            Vector3 position,
            string stableKey)
        {
            GameObject template = CreateObject(stableKey + "-Template");
            template.SetActive(false);
            CombatUnit unit = template.AddComponent<CombatUnit>();
            unit.Configure(team, Altitude.Ground, 1000f, 50f, 2f, 6f, 0.5f, true, type != OperatorType.SilverAsh);
            OperatorIdentity identity = template.AddComponent<OperatorIdentity>();
            identity.Configure(stableKey, team, type);
            template.AddComponent<UnitMotor>().Configure(5f, ArenaLayout.CreateDefault());
            template.AddComponent<BasicAttackController>().Configure(unit);
            template.AddComponent<OperatorRetreatController>();
            template.AddComponent<SimpleOperatorAiController>();
            template.AddComponent<MeshRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material corpseMaterial = new Material(shader);
            materials.Add(corpseMaterial);
            template.AddComponent<DeathCorpsePresenter>().Configure(unit, corpseMaterial, 0);
            roster.RegisterSlot(stableKey, team, type, template, position);
        }

        private CombatUnit CreateTower(string name, TeamId team, Vector3 position)
        {
            CombatUnit tower = CreateUnit(name, team, position, 10000f, 0f);
            tower.gameObject.AddComponent<TowerCombatController>();
            return tower;
        }

        private CombatUnit CreateUnit(string name, TeamId team, Vector3 position, float health, float attackPower)
        {
            GameObject gameObject = CreateObject(name);
            gameObject.transform.position = position;
            CombatUnit unit = gameObject.AddComponent<CombatUnit>();
            unit.Configure(team, Altitude.Ground, health, attackPower, 0f, 6f, 0.5f, true, true);
            return unit;
        }

        private GameObject CreateObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            objects.Add(gameObject);
            return gameObject;
        }
    }
}
