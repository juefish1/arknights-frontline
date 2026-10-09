using System.Collections;
using System.Linq;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using ArknightsFrontline.Skills;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ArknightsFrontline.Tests.PlayMode
{
    public sealed class NiuLaiCharacterPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var scene = SceneManager.GetSceneByName("PrototypeArena");
            if (scene.isLoaded)
            {
                var cleanup = SceneManager.CreateScene("NiuLaiCleanup");
                SceneManager.SetActiveScene(cleanup);
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            foreach (var unit in Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.None)) Object.Destroy(unit.gameObject);
            foreach (var effect in Object.FindObjectsByType<NiuLaiMamaImpact>(FindObjectsSortMode.None)) Object.Destroy(effect.gameObject);
            Time.timeScale = 1;
            yield return null;
        }

        [UnityTest]
        public IEnumerator SelectionPausesAndDefaultPreservesExusiai()
        {
            SceneManager.LoadScene("PrototypeArena"); yield return null;
            var selection = Object.FindFirstObjectByType<PlayerCharacterSelection>();
            Assert.That(selection.IsPending, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            var roster = Object.FindFirstObjectByType<OperatorRosterController>();
            Assert.That(roster.Slots.Count, Is.Zero);
            Assert.That(selection.ConfirmSelection(false), Is.True);
            Assert.That(selection.ConfirmSelection(true), Is.False);
            Assert.That(roster.Slots.Count, Is.EqualTo(6));
            var player = roster.Slots.Single(s => s.IsPlayerControlled).CurrentOperator;
            Assert.That(player.GetComponent<ExusiaiSkillController>(), Is.Not.Null);
            Assert.That(player.GetComponent<ExusiaiCombatPresentation>(), Is.Not.Null);
            Assert.That(player.GetComponent<NiuLaiSkillController>(), Is.Null);
            Assert.That(Time.timeScale, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CowSelectionRetainsFiveOtherSeatsAndRedeploysCow()
        {
            SceneManager.LoadScene("PrototypeArena"); yield return null;
            Assert.That(Object.FindFirstObjectByType<PlayerCharacterSelection>().ConfirmSelection(true), Is.True);
            var roster = Object.FindFirstObjectByType<OperatorRosterController>();
            Assert.That(roster.Slots.Count, Is.EqualTo(6));
            var slot = roster.Slots.Single(s => s.IsPlayerControlled);
            Assert.That(slot.OperatorType, Is.EqualTo(OperatorType.NiuLai));
            Assert.That(slot.CurrentOperator.GetComponent<NiuLaiSkillController>(), Is.Not.Null);
            Assert.That(slot.CurrentOperator.transform.Find("NiuLaiVisual"), Is.Not.Null);
            Assert.That(roster.Slots.Count(s => !s.IsPlayerControlled), Is.EqualTo(5));
            slot.CurrentOperator.TakePhysicalDamage(9999);
            roster.Tick(8);
            Assert.That(slot.CurrentOperator, Is.Not.Null);
            Assert.That(slot.CurrentOperator.MaxHealth, Is.EqualTo(1300));
            Assert.That(slot.CurrentOperator.GetComponent<NiuLaiSkillController>().RCooldown, Is.EqualTo(15));
        }

        private static NiuLaiSkillController CreateCow()
        {
            var prefab = Resources.Load<GameObject>("NiuLaiPlayer");
            Assert.That(prefab, Is.Not.Null);
            var instance = Object.Instantiate(prefab, new Vector3(0, 0.75f, 0), Quaternion.identity);
            instance.SetActive(true);
            return instance.GetComponent<NiuLaiSkillController>();
        }
        private static CombatUnit CreateEnemy(Vector3 position, float defense = 10)
        {
            var enemy = new GameObject("CowTestEnemy"); enemy.transform.position = position;
            var unit = enemy.AddComponent<CombatUnit>();
            unit.Configure(TeamId.Red, Altitude.Ground, 1000, 0, defense, 1, 1, true, false);
            return unit;
        }

        [UnityTest]
        public IEnumerator EmpoweredMeleeDealsOneHitAndExpires()
        {
            var cow = CreateCow(); var target = CreateEnemy(new Vector3(1, 0.75f, 0));
            cow.ActivateEmpower();
            var attacks = cow.GetComponent<BasicAttackController>();
            attacks.SetTarget(target); attacks.Tick(0);
            Assert.That(target.CurrentHealth, Is.EqualTo(810));
            Assert.That(cow.IsEmpowered, Is.False);
            Assert.That(Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None), Is.Empty);
            attacks.SetTarget(null); cow.Tick(6); cow.ActivateEmpower(); cow.Tick(5.1f);
            Assert.That(cow.IsEmpowered, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FlightCrossesObstacleAndRejectsOccupiedDestination()
        {
            var cow = CreateCow();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.layer = ExusiaiSkillController.ReservedObstacleLayerIndex;
            wall.transform.position = new Vector3(2, 0.75f, 0);
            wall.transform.localScale = new Vector3(0.5f, 2, 3);
            Physics.SyncTransforms();
            cow.HandleSkill2(); cow.TryHandleConfirm(wall.transform.position, null);
            Assert.That(cow.IsFlying, Is.False);
            Assert.That(cow.ECooldown, Is.Zero);
            cow.TryHandleConfirm(new Vector3(5, 0, 0), null);
            Assert.That(cow.IsFlying, Is.True);
            Assert.That(cow.GetComponent<OperatorRetreatController>().TryBegin(), Is.False);
            cow.Tick(0.65f);
            Assert.That(cow.transform.position.x, Is.EqualTo(5).Within(0.01f));
            Assert.That(cow.IsFlying, Is.False);
            Object.Destroy(wall);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MamaWaitsForWarningAndDamagesEnemyOnlyOnce()
        {
            var cow = CreateCow(); var target = CreateEnemy(new Vector3(2, 0.75f, 0));
            cow.Tick(15); cow.HandleSkill3(); cow.TryHandleConfirm(new Vector3(2, 0, 0), null);
            var effect = Object.FindFirstObjectByType<NiuLaiMamaImpact>();
            Assert.That(effect, Is.Not.Null);
            effect.Tick(0.8f); Assert.That(target.CurrentHealth, Is.EqualTo(1000));
            // Already released impacts survive the caster's death.
            cow.GetComponent<CombatUnit>().TakePhysicalDamage(9999);
            effect.Tick(0.11f); effect.Tick(1);
            Assert.That(target.CurrentHealth, Is.EqualTo(650));
            yield return null;
        }
    }
}
