using System;
using System.Reflection;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class ProjectileSpawnPointTests
    {
        private GameObject root, enemy, projectileObject, clone;
        private CombatUnit attacker, target;
        private Transform muzzle;
        private Component origin;
        [SetUp] public void Setup()
        {
            root = new GameObject("OriginAttacker"); root.transform.position = new Vector3(2, 1.2f, 0);
            attacker = root.AddComponent<CombatUnit>(); attacker.Configure(TeamId.Blue, Altitude.Ground, 100, 10, 0, 6, 1, true, true);
            enemy = new GameObject("OriginTarget"); enemy.transform.position = Vector3.right * 4;
            target = enemy.AddComponent<CombatUnit>(); target.Configure(TeamId.Red, Altitude.Ground, 100, 10, 0, 6, 1, true, true);
            muzzle = new GameObject("Muzzle").transform; muzzle.SetParent(root.transform, false); muzzle.localPosition = new Vector3(0.6f, 0.3f, 0.2f);
            Type type = typeof(CombatUnit).Assembly.GetType("ArknightsFrontline.Combat.ProjectileSpawnPoint");
            Assert.That(type, Is.Not.Null, "Optional muzzle component is missing");
            origin = root.AddComponent(type); type.GetMethod("Configure").Invoke(origin, new object[] { muzzle });
            projectileObject = new GameObject("OriginProjectile"); projectileObject.AddComponent<Projectile>();
        }
        [TearDown] public void Cleanup()
        {
            if (root) Object.DestroyImmediate(root); if (enemy) Object.DestroyImmediate(enemy);
            if (clone) Object.DestroyImmediate(clone); if (projectileObject) Object.DestroyImmediate(projectileObject);
        }
        [Test] public void ProjectileStartsAtConfiguredMuzzle()
        {
            var projectile = projectileObject.GetComponent<Projectile>();
            projectile.Initialize(attacker, target, 10, 16);
            Assert.That(projectile.transform.position, Is.EqualTo(muzzle.position));
            projectile.Tick(10);
            Assert.That(target.CurrentHealth, Is.EqualTo(90));
        }
        [Test] public void MissingOrDestroyedMuzzleFallsBackToRoot()
        {
            Object.DestroyImmediate(muzzle.gameObject);
            var projectile = projectileObject.GetComponent<Projectile>();
            projectile.Initialize(attacker, target, 10, 16);
            Assert.That(projectile.transform.position, Is.EqualTo(root.transform.position));
            Object.DestroyImmediate(origin);
            projectile.Initialize(attacker, target, new PhysicalDamagePayload(10, 1, 0, 1, 0), 16);
            Assert.That(projectile.transform.position, Is.EqualTo(root.transform.position));
        }
        [Test] public void TurningAndRedeploymentResolveLiveMuzzle()
        {
            root.transform.rotation = Quaternion.Euler(0, 90, 0);
            clone = Object.Instantiate(root, Vector3.forward * 5, Quaternion.Euler(0, 180, 0));
            var clonedOrigin = clone.GetComponent(origin.GetType());
            var clonedMuzzle = (Transform)origin.GetType().GetProperty("Muzzle").GetValue(clonedOrigin);
            Assert.That(clonedMuzzle.IsChildOf(clone.transform), Is.True);
            Assert.That(clonedMuzzle, Is.Not.SameAs(muzzle));
            var projectile = projectileObject.GetComponent<Projectile>();
            projectile.Initialize(clone.GetComponent<CombatUnit>(), target, 10, 16);
            Assert.That(projectile.transform.position, Is.EqualTo(clonedMuzzle.position));
        }
    }
}
