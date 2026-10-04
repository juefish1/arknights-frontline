using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Camera;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using ArknightsFrontline.Movement;
using ArknightsFrontline.Skills;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArknightsFrontline.Tests.PlayMode
{
    [DefaultExecutionOrder(1000)]
    public sealed class LateTowerProjectileImpactDriver : MonoBehaviour
    {
        private Projectile projectile;
        private bool hasImpacted;

        public void Configure(Projectile configuredProjectile)
        {
            projectile = configuredProjectile;
        }

        private void Update()
        {
            if (hasImpacted || projectile == null)
            {
                return;
            }

            hasImpacted = true;
            projectile.Tick(1000f);
        }
    }

    public sealed class OperatorRosterLifecyclePlayModeTests
    {
        private const int GroundLayer = 8;

        private readonly List<GameObject> ownedObjects = new List<GameObject>();
        private readonly List<GameObject> spawnedObjects = new List<GameObject>();
        private readonly List<Material> materials = new List<Material>();
        private Material corpseMaterial;
        private float previousTimeScale;
        private int spawnNotifications;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            spawnNotifications = 0;
            ownedObjects.Clear();
            spawnedObjects.Clear();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 0f;
            foreach (GameObject gameObject in spawnedObjects.Concat(ownedObjects))
            {
                if (gameObject != null)
                {
                    Object.Destroy(gameObject);
                }
            }

            foreach (GameObject corpse in FindOperatorCorpses())
            {
                Object.Destroy(corpse);
            }

            if (corpseMaterial != null)
            {
                Object.Destroy(corpseMaterial);
            }

            foreach (Material material in materials)
            {
                if (material != null && material != corpseMaterial)
                {
                    Object.Destroy(material);
                }
            }

            yield return null;
            Time.timeScale = previousTimeScale;
            ownedObjects.Clear();
            spawnedObjects.Clear();
            materials.Clear();
        }

        [UnityTest]
        public IEnumerator PlayerTemplateAwakeAndRedeploymentRestoreInitialSkillStateAndDeathSpawnsOnce()
        {
            const string stableKey = "roster-playmode-player-exusiai";
            OperatorRosterController roster = CreateRoster(stableKey, out OperatorRosterSlot slot);
            CombatUnit firstLife = slot.CurrentOperator;
            ExusiaiSkillController firstSkills = firstLife.GetComponent<ExusiaiSkillController>();
            AssertFreshExusiaiState(firstSkills);

            CombatUnit target = CreateEnemy("roster-playmode-target", firstLife.transform.position + Vector3.right * 3f);
            BasicAttackController attacks = firstLife.GetComponent<BasicAttackController>();
            AttackSequenceExecutor sequence = firstLife.GetComponent<AttackSequenceExecutor>();
            HashSet<Projectile> existingProjectiles = new HashSet<Projectile>(
                Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None));
            attacks.SetTarget(target);
            attacks.Tick(0f);
            ownedObjects.AddRange(Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None)
                .Where(projectile => !existingProjectiles.Contains(projectile))
                .Select(projectile => projectile.gameObject));
            Assert.That(firstSkills.Snapshot.SweepProgress, Is.EqualTo(1),
                "A completed real basic attack should mutate W before the life ends.");

            Assert.That(firstSkills.BeginChargeTargeting(), Is.True);
            Assert.That(firstSkills.TryConfirmCharge(firstLife.transform.position + Vector3.right * 4f, null), Is.True);
            Assert.That(firstSkills.Snapshot.ChargePhase, Is.EqualTo(ExusiaiChargePhase.Cooldown));
            firstSkills.Tick(10f);
            Assert.That(firstSkills.TryActivateOverload(), Is.True);
            Assert.That(firstSkills.Snapshot.IsOverloadActive, Is.True);
            Assert.That(sequence, Is.Not.Null);

            firstLife.TakePhysicalDamage(firstLife.MaxHealth);
            firstLife.TakePhysicalDamage(firstLife.MaxHealth);
            Assert.That(slot.CurrentOperator, Is.Null);
            Assert.That(slot.DepartureCount, Is.EqualTo(1));
            Assert.That(spawnNotifications, Is.EqualTo(1));
            yield return null;
            Assert.That(firstLife == null, Is.True);
            Assert.That(FindOperatorCorpses(stableKey), Has.Length.EqualTo(1),
                "The death path creates one corpse before the slot redeploys.");

            roster.Tick(8f);
            CombatUnit secondLife = slot.CurrentOperator;
            Assert.That(secondLife, Is.Not.Null);
            Assert.That(secondLife, Is.Not.SameAs(firstLife));
            Assert.That(spawnNotifications, Is.EqualTo(2), "One death should produce exactly one replacement.");
            AssertFreshExusiaiState(secondLife.GetComponent<ExusiaiSkillController>());
            Assert.That(secondLife.CurrentHealth, Is.EqualTo(secondLife.MaxHealth));

            yield return null;
            Assert.That(FindActiveOperators(stableKey), Has.Count.EqualTo(1));
            Assert.That(FindOperatorCorpses(stableKey), Is.Empty,
                "The matching corpse is cleared when the new life is deployed.");
            roster.Tick(8f);
            Assert.That(slot.CurrentOperator, Is.SameAs(secondLife));
            Assert.That(spawnNotifications, Is.EqualTo(2), "A live slot cannot produce a second life from another tick.");
        }

        [UnityTest]
        public IEnumerator SuccessfulRetreatDeactivatesBeforeDeferredDestroyAndRedeploysOnlyOnceWithoutCorpse()
        {
            const string stableKey = "roster-playmode-retreat-exusiai";
            OperatorRosterController roster = CreateRoster(stableKey, out OperatorRosterSlot slot);
            CombatUnit oldLife = slot.CurrentOperator;

            Assert.That(roster.NotifySuccessfulRetreat(oldLife), Is.True);
            Assert.That(oldLife, Is.Not.Null,
                "Unity keeps a Destroy-scheduled object alive until the end of the frame.");
            Assert.That(oldLife.gameObject.activeInHierarchy, Is.False,
                "The retiring life must stop participating immediately in the Destroy frame.");
            Assert.That(slot.CurrentOperator, Is.Null);
            Assert.That(slot.DepartureCount, Is.EqualTo(1));
            Assert.That(slot.RedeployRemaining, Is.EqualTo(5.6f).Within(0.0001f));
            Assert.That(FindOperatorCorpses(stableKey), Is.Empty);
            Assert.That(roster.NotifySuccessfulRetreat(oldLife), Is.False,
                "A duplicate retreat callback must not count the same departure twice.");
            Assert.That(spawnNotifications, Is.EqualTo(1));

            yield return null;
            Assert.That(oldLife == null, Is.True);
            roster.Tick(5.59f);
            Assert.That(slot.CurrentOperator, Is.Null);
            Assert.That(spawnNotifications, Is.EqualTo(1));
            roster.Tick(0.02f);

            CombatUnit newLife = slot.CurrentOperator;
            Assert.That(newLife, Is.Not.Null);
            Assert.That(newLife, Is.Not.SameAs(oldLife));
            Assert.That(spawnNotifications, Is.EqualTo(2));
            Assert.That(FindActiveOperators(stableKey), Has.Count.EqualTo(1));
            yield return null;
            Assert.That(FindOperatorCorpses(stableKey), Is.Empty);
            roster.Tick(100f);
            Assert.That(slot.CurrentOperator, Is.SameAs(newLife));
            Assert.That(spawnNotifications, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator TowerDeathStopsPlayerRedeploymentSynchronouslyAndPreventsSettlementFrameSpawn()
        {
            MatchOutcomeController match = CreateMatchOutcomeController();
            OperatorRosterController roster = CreateRoster("roster-match-freeze-player", out OperatorRosterSlot slot);
            Assert.That(spawnNotifications, Is.EqualTo(1));
            Assert.That(roster.NotifySuccessfulRetreat(slot.CurrentOperator), Is.True);
            Assert.That(slot.RedeployRemaining, Is.EqualTo(5.6f).Within(0.0001f));

            CombatUnit redTower = GameObject.Find("match-freeze-red-tower").GetComponent<CombatUnit>();
            redTower.TakePhysicalDamage(redTower.MaxHealth);
            roster.Tick(100f);

            Assert.That(match.IsEnding, Is.True);
            Assert.That(slot.IsStopped, Is.True, "The roster must stop during the tower death callback.");
            Assert.That(slot.RedeployRemaining, Is.Zero);
            Assert.That(slot.CurrentOperator, Is.Null);
            Assert.That(spawnNotifications, Is.EqualTo(1), "No replacement may spawn in the settlement frame.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TowerProjectileImpactLaterInFramePreventsAutomaticRedeployment()
        {
            MatchOutcomeController match = CreateMatchOutcomeController();
            OperatorRosterController roster = CreateRoster("roster-late-impact-player", out OperatorRosterSlot slot);
            Assert.That(roster.NotifySuccessfulRetreat(slot.CurrentOperator), Is.True);
            roster.Tick(5.599f);
            Assert.That(slot.CurrentOperator, Is.Null);
            Assert.That(slot.RedeployRemaining, Is.GreaterThan(0f).And.LessThan(0.01f));

            CombatUnit redTower = GameObject.Find("match-freeze-red-tower").GetComponent<CombatUnit>();
            GameObject attackerObject = new GameObject("roster-late-impact-attacker");
            ownedObjects.Add(attackerObject);
            attackerObject.transform.position = redTower.transform.position + Vector3.right * 4f;
            CombatUnit attacker = attackerObject.AddComponent<CombatUnit>();
            attacker.Configure(TeamId.Blue, Altitude.Ground, 100f, 100f, 0f, 0f, 1f, true, false);

            GameObject projectileObject = new GameObject("roster-late-impact-projectile");
            ownedObjects.Add(projectileObject);
            Projectile projectile = projectileObject.AddComponent<Projectile>();
            projectile.Initialize(attacker, redTower, 100f, 0.01f);
            projectileObject.AddComponent<LateTowerProjectileImpactDriver>().Configure(projectile);

            Time.timeScale = 1f;
            yield return null;

            Assert.That(match.IsEnding, Is.True, "The real projectile impact ends the match in Update.");
            Assert.That(slot.IsStopped, Is.True);
            Assert.That(slot.CurrentOperator, Is.Null,
                "The roster must not deploy in the same frame after a later Update projectile kills a tower.");
            Assert.That(spawnNotifications, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PlayerFeedbackRebindsAcrossRetreatAndRedeploymentWithoutPullingManualCameraMovement()
        {
            const string stableKey = "roster-feedback-player";
            OperatorRosterController roster = CreateRoster(stableKey, out OperatorRosterSlot slot, true);
            CombatUnit firstLife = slot.CurrentOperator;
            GameObject canvasObject = new GameObject("feedback-canvas", typeof(Canvas));
            ownedObjects.Add(canvasObject);
            GameObject hudObject = new GameObject("feedback-hud", typeof(RectTransform));
            hudObject.transform.SetParent(canvasObject.transform, false);
            ownedObjects.Add(hudObject);
            SkillHudPresenter hud = hudObject.AddComponent<SkillHudPresenter>();
            GameObject cameraObject = new GameObject("feedback-camera");
            ownedObjects.Add(cameraObject);
            MobaCameraController camera = cameraObject.AddComponent<MobaCameraController>();

            GameObject presenterObject = new GameObject("player-deployment-presenter");
            ownedObjects.Add(presenterObject);
            PlayerDeploymentPresenter presenter = presenterObject.AddComponent<PlayerDeploymentPresenter>();
            presenter.Configure(roster, stableKey, hud, camera);

            Assert.That(presenter.CurrentOperator, Is.SameAs(firstLife));
            Assert.That(hud.transform.Find("DeploymentStatus/Label"), Is.Not.Null,
                "The HUD needs a visible line for retreat and redeployment feedback.");
            Assert.That(hud.BoundController, Is.SameAs(firstLife.GetComponent<ExusiaiSkillController>()));
            Assert.That(camera.CenteringTarget, Is.SameAs(firstLife.transform));

            OperatorRetreatController retreat = firstLife.GetComponent<OperatorRetreatController>();
            Assert.That(retreat.TryBegin(), Is.True);
            presenter.Refresh();
            Assert.That(hud.StatusText, Is.EqualTo("B RETREAT 1.5"));
            retreat.Tick(0.4f);
            presenter.Refresh();
            Assert.That(hud.StatusText, Is.EqualTo("B RETREAT 1.1"));

            retreat.Tick(1.1f);
            presenter.Refresh();
            Assert.That(presenter.CurrentOperator, Is.Null);
            Assert.That(hud.BoundController, Is.Null,
                "Departure must release the old Exusiai skill controller immediately.");
            Assert.That(hud.HasVisibleSkillControls, Is.False);
            Assert.That(hud.StatusText, Is.EqualTo("REDEPLOY 5.6"));
            Assert.That(camera.CenteringTarget, Is.Not.SameAs(firstLife.transform),
                "The camera must not retain the departing life as its target.");

            yield return null;
            Assert.That(firstLife == null, Is.True);
            roster.Tick(5.6f);
            CombatUnit secondLife = slot.CurrentOperator;
            Assert.That(secondLife, Is.Not.Null);
            presenter.Refresh();
            Assert.That(presenter.CurrentOperator, Is.SameAs(secondLife));
            Assert.That(hud.BoundController, Is.SameAs(secondLife.GetComponent<ExusiaiSkillController>()));
            Assert.That(hud.StatusText, Is.EqualTo(string.Empty));
            Assert.That(camera.CenteringTarget, Is.SameAs(secondLife.transform));
            Assert.That(hud.WLabel, Is.EqualTo("W  0/3"));
            Assert.That(hud.ELabel, Is.EqualTo("E  READY"));

            GameObject manualFocus = new GameObject("manual-camera-focus");
            manualFocus.transform.position = new Vector3(6f, 0f, 4f);
            ownedObjects.Add(manualFocus);
            camera.CenterOn(manualFocus.transform);
            Vector3 manuallyPositionedCamera = camera.transform.position;
            presenter.Refresh();
            yield return null;
            Assert.That(camera.transform.position, Is.EqualTo(manuallyPositionedCamera),
                "HUD refreshes must not re-center after a user moves the camera.");
        }

        [UnityTest]
        public IEnumerator PresenterConfiguredBeforePlayerSlotRegistrationBindsWhenTheSlotSpawns()
        {
            const string stableKey = "roster-late-registered-player";
            GameObject rosterObject = new GameObject("late-player-roster");
            ownedObjects.Add(rosterObject);
            OperatorRosterController roster = rosterObject.AddComponent<OperatorRosterController>();
            GameObject template = CreatePlayerTemplate("late-player-template", true);

            GameObject canvasObject = new GameObject("late-player-canvas", typeof(Canvas));
            ownedObjects.Add(canvasObject);
            GameObject hudObject = new GameObject("late-player-hud", typeof(RectTransform));
            hudObject.transform.SetParent(canvasObject.transform, false);
            ownedObjects.Add(hudObject);
            SkillHudPresenter hud = hudObject.AddComponent<SkillHudPresenter>();
            GameObject cameraObject = new GameObject("late-player-camera");
            ownedObjects.Add(cameraObject);
            MobaCameraController camera = cameraObject.AddComponent<MobaCameraController>();
            GameObject presenterObject = new GameObject("late-player-presenter");
            ownedObjects.Add(presenterObject);
            PlayerDeploymentPresenter presenter = presenterObject.AddComponent<PlayerDeploymentPresenter>();

            presenter.Configure(roster, stableKey, hud, camera);
            Assert.That(presenter.CurrentOperator, Is.Null);

            roster.OperatorSpawned += (_, liveOperator) => spawnedObjects.Add(liveOperator.gameObject);
            OperatorRosterSlot slot = roster.RegisterSlot(
                stableKey,
                TeamId.Blue,
                OperatorType.Exusiai,
                template,
                Vector3.zero,
                true);
            roster.StartMatch();

            Assert.That(presenter.CurrentOperator, Is.SameAs(slot.CurrentOperator));
            Assert.That(hud.BoundController, Is.SameAs(slot.CurrentOperator.GetComponent<ExusiaiSkillController>()));
            Assert.That(camera.CenteringTarget, Is.SameAs(slot.CurrentOperator.transform));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerDeathUnbindsDestroyedLifeAndRebindsHudAndCameraOnRedeployment()
        {
            const string stableKey = "roster-death-feedback-player";
            OperatorRosterController roster = CreateRoster(stableKey, out OperatorRosterSlot slot, true);
            GameObject canvasObject = new GameObject("death-feedback-canvas", typeof(Canvas));
            ownedObjects.Add(canvasObject);
            GameObject hudObject = new GameObject("death-feedback-hud", typeof(RectTransform));
            hudObject.transform.SetParent(canvasObject.transform, false);
            ownedObjects.Add(hudObject);
            SkillHudPresenter hud = hudObject.AddComponent<SkillHudPresenter>();
            GameObject cameraObject = new GameObject("death-feedback-camera");
            ownedObjects.Add(cameraObject);
            MobaCameraController camera = cameraObject.AddComponent<MobaCameraController>();
            GameObject presenterObject = new GameObject("death-feedback-presenter");
            ownedObjects.Add(presenterObject);
            PlayerDeploymentPresenter presenter = presenterObject.AddComponent<PlayerDeploymentPresenter>();
            presenter.Configure(roster, stableKey, hud, camera);

            CombatUnit firstLife = slot.CurrentOperator;
            ExusiaiSkillController firstSkills = firstLife.GetComponent<ExusiaiSkillController>();
            Assert.That(hud.BoundController, Is.SameAs(firstSkills));
            Assert.That(camera.CenteringTarget, Is.SameAs(firstLife.transform));

            firstLife.TakePhysicalDamage(firstLife.MaxHealth);
            Assert.That(slot.CurrentOperator, Is.Null);
            Assert.That(hud.BoundController, Is.Null,
                "Death must release the skill controller before Unity destroys the old life.");
            Assert.That(hud.HasVisibleSkillControls, Is.False);
            Assert.That(hud.StatusText, Is.EqualTo("REDEPLOY 8.0"));
            Assert.That(camera.CenteringTarget, Is.Not.SameAs(firstLife.transform),
                "The camera must stop retaining the dead operator immediately.");

            yield return null;
            Assert.That(firstLife == null, Is.True);
            roster.Tick(8f);
            CombatUnit secondLife = slot.CurrentOperator;
            Assert.That(secondLife, Is.Not.Null);
            presenter.Refresh();

            Assert.That(presenter.CurrentOperator, Is.SameAs(secondLife));
            Assert.That(hud.BoundController, Is.SameAs(secondLife.GetComponent<ExusiaiSkillController>()));
            Assert.That(hud.HasVisibleSkillControls, Is.True);
            Assert.That(hud.StatusText, Is.EqualTo(string.Empty));
            Assert.That(camera.CenteringTarget, Is.SameAs(secondLife.transform));
            AssertFreshExusiaiState(secondLife.GetComponent<ExusiaiSkillController>());
            yield return null;
            Assert.That(firstLife == null, Is.True);
            Assert.That(hud.BoundController, Is.Not.SameAs(firstSkills));
        }

        [UnityTest]
        public IEnumerator ReconfiguringForTheSameLifeKeepsRetreatFeedbackSubscribed()
        {
            const string stableKey = "roster-reconfigured-player";
            OperatorRosterController roster = CreateRoster(stableKey, out OperatorRosterSlot slot, true);
            GameObject canvasObject = new GameObject("reconfigured-canvas", typeof(Canvas));
            ownedObjects.Add(canvasObject);
            GameObject hudObject = new GameObject("reconfigured-hud", typeof(RectTransform));
            hudObject.transform.SetParent(canvasObject.transform, false);
            ownedObjects.Add(hudObject);
            SkillHudPresenter hud = hudObject.AddComponent<SkillHudPresenter>();
            GameObject cameraObject = new GameObject("reconfigured-camera");
            ownedObjects.Add(cameraObject);
            MobaCameraController camera = cameraObject.AddComponent<MobaCameraController>();
            GameObject presenterObject = new GameObject("reconfigured-presenter");
            ownedObjects.Add(presenterObject);
            PlayerDeploymentPresenter presenter = presenterObject.AddComponent<PlayerDeploymentPresenter>();
            presenter.Configure(roster, stableKey, hud, camera);
            presenter.Configure(roster, stableKey, hud, camera);

            OperatorRetreatController retreat = slot.CurrentOperator.GetComponent<OperatorRetreatController>();
            Assert.That(retreat.TryBegin(), Is.True);
            presenter.Refresh();

            Assert.That(presenter.CurrentOperator, Is.SameAs(slot.CurrentOperator));
            Assert.That(hud.StatusText, Is.EqualTo("B RETREAT 1.5"));
            yield return null;
        }

        private OperatorRosterController CreateRoster(
            string stableKey,
            out OperatorRosterSlot slot,
            bool includeRetreatController = false)
        {
            GameObject rosterObject = new GameObject("roster-playmode-controller");
            ownedObjects.Add(rosterObject);
            OperatorRosterController roster = rosterObject.AddComponent<OperatorRosterController>();
            GameObject template = CreatePlayerTemplate("roster-playmode-player-template", includeRetreatController);
            roster.OperatorSpawned += (registeredSlot, liveOperator) =>
            {
                spawnNotifications++;
                spawnedObjects.Add(liveOperator.gameObject);
            };
            slot = roster.RegisterSlot(
                stableKey,
                TeamId.Blue,
                OperatorType.Exusiai,
                template,
                Vector3.zero,
                true);
            roster.StartMatch();
            return roster;
        }

        private MatchOutcomeController CreateMatchOutcomeController()
        {
            ArenaLayout layout = ArenaLayout.CreateDefault();
            CombatUnit blueTower = CreateTower("match-freeze-blue-tower", TeamId.Blue, layout.BlueTower);
            CombatUnit redTower = CreateTower("match-freeze-red-tower", TeamId.Red, layout.RedTower);
            GameObject minionParent = new GameObject("match-freeze-minions");
            ownedObjects.Add(minionParent);
            Material blueMaterial = CreateMaterial(Color.blue);
            Material redMaterial = CreateMaterial(Color.red);
            MinionWaveSpawner spawner = new GameObject("match-freeze-spawner").AddComponent<MinionWaveSpawner>();
            ownedObjects.Add(spawner.gameObject);
            spawner.Configure(minionParent.transform, layout, blueTower, redTower, blueMaterial, redMaterial, 9, 8);
            MatchOutcomeController match = new GameObject("match-freeze-outcome").AddComponent<MatchOutcomeController>();
            ownedObjects.Add(match.gameObject);
            match.Configure(blueTower, redTower, spawner);
            return match;
        }

        private CombatUnit CreateTower(string name, TeamId team, Vector3 position)
        {
            GameObject towerObject = new GameObject(name);
            towerObject.transform.position = position;
            ownedObjects.Add(towerObject);
            CombatUnit tower = towerObject.AddComponent<CombatUnit>();
            tower.Configure(team, Altitude.Ground, 100f, 20f, 0f, 8f, 1f, true, false);
            return tower;
        }

        private Material CreateMaterial(Color color)
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            materials.Add(material);
            return material;
        }

        private GameObject CreatePlayerTemplate(string name, bool includeRetreatController = false)
        {
            GameObject template = new GameObject(name);
            template.SetActive(false);
            ownedObjects.Add(template);

            CombatUnit unit = template.AddComponent<CombatUnit>();
            unit.Configure(TeamId.Blue, Altitude.Ground, 1000f, 50f, 2f, 6f, 0.5f, true, true);
            template.AddComponent<OperatorIdentity>();
            template.AddComponent<UnitMotor>();
            template.AddComponent<UnitStatModifiers>();
            template.AddComponent<ArknightsFrontline.Commands.PlayerCommandController>();
            template.AddComponent<BasicAttackController>();
            template.AddComponent<AttackSequenceExecutor>();
            template.AddComponent<SkillDashController>();
            corpseMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            template.AddComponent<MeshRenderer>().sharedMaterial = corpseMaterial;
            template.AddComponent<DeathCorpsePresenter>().Configure(unit, corpseMaterial, GroundLayer);
            template.AddComponent<ExusiaiSkillController>();
            if (includeRetreatController)
            {
                template.AddComponent<OperatorRetreatController>();
            }
            return template;
        }

        private CombatUnit CreateEnemy(string name, Vector3 position)
        {
            GameObject enemyObject = new GameObject(name);
            enemyObject.transform.position = position;
            ownedObjects.Add(enemyObject);
            CombatUnit enemy = enemyObject.AddComponent<CombatUnit>();
            enemy.Configure(TeamId.Red, Altitude.Ground, 2000f, 0f, 0f, 0f, 1f, false, false);
            return enemy;
        }

        private static void AssertFreshExusiaiState(ExusiaiSkillController skills)
        {
            Assert.That(skills, Is.Not.Null);
            Assert.That(skills.isActiveAndEnabled, Is.True);
            ExusiaiSkillSnapshot snapshot = skills.Snapshot;
            Assert.That(snapshot.SweepProgress, Is.Zero, "W starts at 0.");
            Assert.That(snapshot.IsSweepReady, Is.False);
            Assert.That(snapshot.ChargePhase, Is.EqualTo(ExusiaiChargePhase.Ready), "E starts ready.");
            Assert.That(snapshot.IsChargeReady, Is.True);
            Assert.That(snapshot.ChargeCooldown, Is.Zero);
            Assert.That(snapshot.IsOverloadActive, Is.False);
            Assert.That(snapshot.OverloadCooldown, Is.EqualTo(10f).Within(0.0001f), "R starts with its ten-second wait.");
        }

        private static List<CombatUnit> FindActiveOperators(string stableKey)
        {
            return Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.None)
                .Where(unit => unit.GetComponent<OperatorIdentity>() is OperatorIdentity identity
                    && identity.StableKey == stableKey)
                .ToList();
        }

        private static GameObject[] FindOperatorCorpses(string stableKey = null)
        {
            return Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Where(gameObject => gameObject.name.EndsWith("_Corpse")
                    && (stableKey != null || gameObject.name.StartsWith("roster-playmode-"))
                    && (stableKey == null || gameObject.name == stableKey + "_Corpse"))
                .ToArray();
        }
    }
}
