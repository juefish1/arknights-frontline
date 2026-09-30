using System.Collections.Generic;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class ProjectileTests
    {
        private const int GroundLayer = 8;

        private readonly List<GameObject> gameObjects = new List<GameObject>();
        private readonly List<Material> materials = new List<Material>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in gameObjects)
            {
                if (gameObject != null)
                {
                    Object.DestroyImmediate(gameObject);
                }
            }

            foreach (Material material in materials)
            {
                Object.DestroyImmediate(material);
            }

            foreach (GameObject candidate in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (candidate.name == "ProjectileLethalTarget_Corpse")
                {
                    Object.DestroyImmediate(candidate);
                }
            }

            gameObjects.Clear();
            materials.Clear();
        }

        [Test]
        public void ProjectileDamagesLegalTargetAtArrival()
        {
            CombatUnit attacker = CreateUnit("Attacker", TeamId.Blue, Vector3.zero, 100f, 12f, 0f, true);
            CombatUnit target = CreateUnit("Target", TeamId.Red, new Vector3(4f, 0f, 0f), 40f, 0f, 2f, false);
            Projectile projectile = CreateProjectile();
            CombatUnit reportedAttacker = null;
            float reportedDamage = 0f;
            target.DamageTaken += (source, amount) =>
            {
                reportedAttacker = source;
                reportedDamage = amount;
            };

            projectile.Initialize(attacker, target, 12f, 16f);
            projectile.Tick(10f);

            Assert.That(target.CurrentHealth, Is.EqualTo(target.MaxHealth - 10f));
            Assert.That(reportedAttacker, Is.SameAs(attacker));
            Assert.That(reportedDamage, Is.EqualTo(10f));
            Assert.That(projectile.IsFinished, Is.True);
            Assert.That(projectile.GetComponent<Renderer>(), Is.Not.Null);
        }

        [Test]
        public void LethalProjectileDamageNotifiesBeforeCorpsePresenterDestroysUnit()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "ProjectileTestGround";
            ground.layer = GroundLayer;
            gameObjects.Add(ground);

            CombatUnit attacker = CreateUnit("ProjectileAttacker", TeamId.Blue, Vector3.zero, 100f, 12f, 0f, true);
            CombatUnit target = CreateUnit("ProjectileLethalTarget", TeamId.Red, new Vector3(4f, 0f, 0f), 40f, 0f, 2f, false);
            float maxHealth = target.MaxHealth;
            var notifications = new List<string>();
            float reportedDamage = 0f;
            bool deadWhenDamageWasReported = false;
            target.DamageTaken += (_, amount) =>
            {
                notifications.Add("DamageTaken");
                reportedDamage = amount;
                deadWhenDamageWasReported = target.IsDead;
            };
            target.Died += _ => notifications.Add("Died");

            Material corpseMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            corpseMaterial.color = Color.red;
            materials.Add(corpseMaterial);
            target.gameObject.AddComponent<DeathCorpsePresenter>().Configure(target, corpseMaterial, GroundLayer);

            Projectile projectile = CreateProjectile();
            projectile.Initialize(
                attacker,
                target,
                new PhysicalDamagePayload(100f, 1f, 0f, 1f, 0f),
                16f);

            Assert.DoesNotThrow(() => projectile.Tick(10f));

            Assert.That(projectile.IsFinished, Is.True);
            Assert.That(target == null, Is.True);
            Assert.That(reportedDamage, Is.EqualTo(maxHealth));
            Assert.That(deadWhenDamageWasReported, Is.True);
            Assert.That(notifications, Is.EqualTo(new[] { "DamageTaken", "Died" }));

            GameObject corpse = null;
            foreach (GameObject candidate in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (candidate.name == "ProjectileLethalTarget_Corpse")
                {
                    corpse = candidate;
                    break;
                }
            }

            Assert.That(corpse, Is.Not.Null);
            gameObjects.Add(corpse);
            Assert.That(corpse.GetComponent<CorpseLifetimeController>().OwnerKey, Is.EqualTo("ProjectileLethalTarget"));
        }

        [Test]
        public void ProjectileDoesNotDamageTargetThatDiedBeforeArrival()
        {
            CombatUnit attacker = CreateUnit("Attacker", TeamId.Blue, Vector3.zero, 100f, 12f, 0f, true);
            CombatUnit target = CreateUnit("Target", TeamId.Red, new Vector3(4f, 0f, 0f), 40f, 0f, 2f, false);
            Projectile projectile = CreateProjectile();
            projectile.Initialize(attacker, target, 12f, 16f);
            target.TakePhysicalDamage(100f);

            projectile.Tick(10f);

            Assert.That(target.CurrentHealth, Is.EqualTo(0f));
            Assert.That(projectile.IsFinished, Is.True);
        }

        [Test]
        public void ProjectileDoesNotDamageStillAliveTargetWhenAttackerDiesBeforeArrival()
        {
            CombatUnit attacker = CreateUnit("Attacker", TeamId.Blue, Vector3.zero, 100f, 12f, 0f, true);
            CombatUnit target = CreateUnit("Target", TeamId.Red, new Vector3(4f, 0f, 0f), 40f, 0f, 2f, false);
            Projectile projectile = CreateProjectile();
            projectile.Initialize(attacker, target, 12f, 16f);
            attacker.TakePhysicalDamage(100f);

            projectile.Tick(10f);

            Assert.That(target.CurrentHealth, Is.EqualTo(target.MaxHealth));
            Assert.That(projectile.IsFinished, Is.True);
        }

        [Test]
        public void CancelFinishesProjectileWithoutDamagingTarget()
        {
            CombatUnit attacker = CreateUnit("Attacker", TeamId.Blue, Vector3.zero, 100f, 12f, 0f, true);
            CombatUnit target = CreateUnit("Target", TeamId.Red, new Vector3(0.1f, 0f, 0f), 40f, 0f, 2f, false);
            Projectile projectile = CreateProjectile();
            projectile.Initialize(attacker, target, 12f, 16f);

            projectile.Cancel();
            projectile.Tick(10f);

            Assert.That(projectile.IsFinished, Is.True);
            Assert.That(target.CurrentHealth, Is.EqualTo(target.MaxHealth));
        }

        [Test]
        public void ProjectilesShareOneCachedYellowMaterial()
        {
            Projectile firstProjectile = CreateProjectile();
            Projectile secondProjectile = CreateProjectile();
            Renderer firstRenderer = firstProjectile.GetComponent<Renderer>();
            Renderer secondRenderer = secondProjectile.GetComponent<Renderer>();

            Assert.That(firstRenderer.sharedMaterial, Is.Not.Null);
            Assert.That(secondRenderer.sharedMaterial, Is.SameAs(firstRenderer.sharedMaterial));
            Assert.That(firstRenderer.sharedMaterial.color, Is.EqualTo(Color.yellow));
        }

        [Test]
        public void PayloadUsesPanelAttackThenSkillMultiplierAndDefenseOnce()
        {
            CombatUnit attacker = CreateUnit("Attacker", TeamId.Blue, Vector3.zero, 100f, 12f, 0f, true);
            CombatUnit target = CreateUnit("Target", TeamId.Red, new Vector3(4f, 0f, 0f), 1000f, 0f, 2f, false);
            Projectile projectile = CreateProjectile();
            PhysicalDamagePayload payload = new PhysicalDamagePayload(55f, 1.45f, 0f, 1f, 0f);

            projectile.Initialize(attacker, target, payload, 16f);
            projectile.Tick(10f);

            Assert.That(target.CurrentHealth, Is.EqualTo(target.MaxHealth - 77.75f).Within(0.001f));
        }

        [Test]
        public void LastShotReadsActualTargetsMissingHealthAtImpact()
        {
            CombatUnit attacker = CreateUnit("Attacker", TeamId.Blue, Vector3.zero, 100f, 12f, 0f, true);
            CombatUnit target = CreateUnit("Target", TeamId.Red, new Vector3(4f, 0f, 0f), 1000f, 0f, 2f, false);
            Projectile projectile = CreateProjectile();
            PhysicalDamagePayload payload = new PhysicalDamagePayload(50f, 1.45f, 0.08f, 1f, 0f);

            projectile.Initialize(attacker, target, payload, 16f);
            target.TakePhysicalDamage(200f);
            projectile.Tick(10f);

            // Corrected from the brief's arithmetic typo: 800 - (72.5 + 16 - 2) = 713.5.
            Assert.That(target.CurrentHealth, Is.EqualTo(713.5f).Within(0.001f));
        }

        [Test]
        public void PayloadDoesNotApplySlowWhenImpactKillsTarget()
        {
            CombatUnit attacker = CreateUnit("Attacker", TeamId.Blue, Vector3.zero, 100f, 12f, 0f, true);
            CombatUnit target = CreateUnit("Target", TeamId.Red, new Vector3(4f, 0f, 0f), 10f, 0f, 2f, false);
            Projectile projectile = CreateProjectile();
            PhysicalDamagePayload payload = new PhysicalDamagePayload(20f, 1f, 0f, 0.70f, 2f);

            projectile.Initialize(attacker, target, payload, 16f);
            projectile.Tick(10f);

            Assert.That(target.IsDead, Is.True);
            Assert.That(target.GetComponent<TimedStatModifierController>(), Is.Null);
        }

        [Test]
        public void PayloadAppliesSlowToSurvivingTarget()
        {
            CombatUnit attacker = CreateUnit("Attacker", TeamId.Blue, Vector3.zero, 100f, 12f, 0f, true);
            CombatUnit target = CreateUnit("Target", TeamId.Red, new Vector3(4f, 0f, 0f), 100f, 0f, 2f, false);
            Projectile projectile = CreateProjectile();
            PhysicalDamagePayload payload = new PhysicalDamagePayload(20f, 1f, 0f, 0.70f, 2f);

            projectile.Initialize(attacker, target, payload, 16f);
            projectile.Tick(10f);

            UnitStatModifiers modifiers = target.GetComponent<UnitStatModifiers>();
            Assert.That(target.IsDead, Is.False);
            Assert.That(target.GetComponent<TimedStatModifierController>(), Is.Not.Null);
            Assert.That(modifiers.ApplyMovementSpeed(5f), Is.EqualTo(3.5f).Within(0.001f));
        }

        private CombatUnit CreateUnit(
            string name,
            TeamId team,
            Vector3 position,
            float health,
            float attackPower,
            float defense,
            bool canAttackGround)
        {
            GameObject gameObject = new GameObject(name);
            gameObjects.Add(gameObject);
            gameObject.transform.position = position;
            CombatUnit unit = gameObject.AddComponent<CombatUnit>();
            unit.Configure(team, Altitude.Ground, health, attackPower, defense, 6f, 0.5f, canAttackGround, false);
            return unit;
        }

        private Projectile CreateProjectile()
        {
            GameObject gameObject = new GameObject("Projectile");
            gameObjects.Add(gameObject);
            Projectile projectile = gameObject.AddComponent<Projectile>();
            typeof(Projectile)
                .GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(projectile, null);
            return projectile;
        }
    }
}
