using System.Collections.Generic;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class DeathCorpsePresenterTests
    {
        private const int GroundLayer = 8;

        private readonly List<GameObject> gameObjects = new List<GameObject>();
        private readonly List<Material> materials = new List<Material>();
        private readonly HashSet<EntityId> initialCorpseIds = new HashSet<EntityId>();

        [SetUp]
        public void SetUp()
        {
            initialCorpseIds.Clear();
            foreach (GameObject corpse in FindCorpses())
            {
                initialCorpseIds.Add(corpse.GetEntityId());
            }
        }

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

            gameObjects.Clear();
            materials.Clear();
            initialCorpseIds.Clear();
        }

        [Test]
        public void GroundUnitDeathCreatesSameMaterialCorpseAtGroundOffset()
        {
            CreateGround();
            CombatUnit unit = CreateUnit("BlueGround", Altitude.Ground, new Vector3(3f, 1f, -2f));
            Material material = CreateMaterial(Color.blue);
            unit.gameObject.AddComponent<DeathCorpsePresenter>().Configure(unit, material, GroundLayer);

            unit.TakePhysicalDamage(unit.MaxHealth);

            GameObject corpse = FindAndTrackNewCorpse("BlueGround_Corpse");
            Assert.That(corpse.transform.localScale, Is.EqualTo(new Vector3(0.15f, 1f, 0.15f)));
            Assert.That(corpse.GetComponent<Renderer>().sharedMaterial, Is.EqualTo(material));
            Assert.That(corpse.transform.position, Is.EqualTo(new Vector3(3f, 0.01f, -2f)));
            Assert.That(corpse.GetComponent<CombatUnit>(), Is.Null);
            Assert.That(LayerMask.NameToLayer("Targetable"), Is.EqualTo(9));
            Assert.That(corpse.layer, Is.EqualTo(LayerMask.NameToLayer("Default")));
            Collider collider = corpse.GetComponent<Collider>();
            Assert.That(collider == null || !collider.enabled, Is.True);
        }

        [Test]
        public void ConfiguredCorpseScaleControlsPlaneFootprint()
        {
            CreateGround();
            CombatUnit unit = CreateUnit("BlueTower", Altitude.Ground, new Vector3(-40f, 3f, 0f));
            Material material = CreateMaterial(Color.blue);
            unit.gameObject.AddComponent<DeathCorpsePresenter>().Configure(
                unit,
                material,
                GroundLayer,
                new Vector3(0.3f, 1f, 0.3f));

            unit.TakePhysicalDamage(unit.MaxHealth);

            GameObject corpse = FindAndTrackNewCorpse("BlueTower_Corpse");
            Assert.That(corpse.transform.localScale, Is.EqualTo(new Vector3(0.3f, 1f, 0.3f)));
            Assert.That(corpse.GetComponent<Renderer>().sharedMaterial, Is.SameAs(material));
            Assert.That(corpse.GetComponent<CombatUnit>(), Is.Null);
            Assert.That(corpse.GetComponent<Collider>(), Is.Null);
        }

        [Test]
        public void AirCorpseFallsToGroundOnlyAfterConfiguredDuration()
        {
            CreateGround();
            CombatUnit unit = CreateUnit("RedAir", Altitude.Air, new Vector3(-4f, 5f, 2f));
            unit.gameObject.AddComponent<DeathCorpsePresenter>().Configure(unit, CreateMaterial(Color.red), GroundLayer);

            unit.TakePhysicalDamage(unit.MaxHealth);
            CorpseFallController fall = FindAndTrackNewCorpse("RedAir_Corpse").GetComponent<CorpseFallController>();
            fall.Tick(0.29f);
            Assert.That(fall.transform.position.y, Is.GreaterThan(0.01f));
            Assert.That(fall.HasLanded, Is.False);
            fall.Tick(0.3f - 0.29f);
            Assert.That(fall.HasLanded, Is.True);
            Assert.That(fall.enabled, Is.False);
            Assert.That(fall.transform.position.y, Is.EqualTo(0.01f).Within(0.0001f));
        }

        [Test]
        public void ConfiguredUnitKindIsCopiedToCreatedCorpseLifetime()
        {
            CreateGround();
            CombatUnit unit = CreateUnit("BlueMinion", Altitude.Ground, Vector3.zero);
            DeathCorpsePresenter presenter = unit.gameObject.AddComponent<DeathCorpsePresenter>();
            presenter.Configure(unit, CreateMaterial(Color.blue), GroundLayer, UnitKind.Minion);

            unit.TakePhysicalDamage(unit.MaxHealth);

            CorpseLifetimeController lifetime = FindAndTrackNewCorpse("BlueMinion_Corpse")
                .GetComponent<CorpseLifetimeController>();
            Assert.That(lifetime, Is.Not.Null);
            Assert.That(lifetime.UnitKind, Is.EqualTo(UnitKind.Minion));
            Assert.That(lifetime.OwnerKey, Is.EqualTo("BlueMinion"));
        }

        [Test]
        public void AirMinionLifetimeStartsBeforeFallCompletes()
        {
            CreateGround();
            CombatUnit unit = CreateUnit("RedAirMinion", Altitude.Air, new Vector3(0f, 5f, 0f));
            unit.gameObject.AddComponent<DeathCorpsePresenter>().Configure(
                unit,
                CreateMaterial(Color.red),
                GroundLayer,
                UnitKind.Minion);

            unit.TakePhysicalDamage(unit.MaxHealth);

            GameObject corpse = FindAndTrackNewCorpse("RedAirMinion_Corpse");
            CorpseLifetimeController lifetime = corpse.GetComponent<CorpseLifetimeController>();
            CorpseFallController fall = corpse.GetComponent<CorpseFallController>();
            Assert.That(lifetime, Is.Not.Null);
            Assert.That(fall, Is.Not.Null);
            Assert.That(fall.HasLanded, Is.False);

            lifetime.Tick(5f);

            Assert.That(corpse == null, Is.True);
        }

        [Test]
        public void LegacyConfigureOverloadsDefaultToOperator()
        {
            CreateGround();
            CombatUnit defaultScaleUnit = CreateUnit("LegacyDefaultCorpse", Altitude.Ground, Vector3.zero);
            DeathCorpsePresenter defaultScalePresenter = defaultScaleUnit.gameObject.AddComponent<DeathCorpsePresenter>();
            defaultScalePresenter.Configure(defaultScaleUnit, CreateMaterial(Color.white), GroundLayer);
            Assert.That(defaultScalePresenter.UnitKind, Is.EqualTo(UnitKind.Operator));

            defaultScaleUnit.TakePhysicalDamage(defaultScaleUnit.MaxHealth);

            CorpseLifetimeController defaultScaleLifetime = FindAndTrackNewCorpse("LegacyDefaultCorpse_Corpse")
                .GetComponent<CorpseLifetimeController>();
            Assert.That(defaultScaleLifetime.UnitKind, Is.EqualTo(UnitKind.Operator));
            Assert.That(defaultScaleLifetime.OwnerKey, Is.EqualTo("LegacyDefaultCorpse"));

            CombatUnit customScaleUnit = CreateUnit("LegacyScaleCorpse", Altitude.Ground, new Vector3(2f, 0f, 0f));
            DeathCorpsePresenter customScalePresenter = customScaleUnit.gameObject.AddComponent<DeathCorpsePresenter>();
            customScalePresenter.Configure(
                customScaleUnit,
                CreateMaterial(Color.gray),
                GroundLayer,
                new Vector3(0.3f, 1f, 0.3f));
            Assert.That(customScalePresenter.UnitKind, Is.EqualTo(UnitKind.Operator));

            customScaleUnit.TakePhysicalDamage(customScaleUnit.MaxHealth);

            CorpseLifetimeController customScaleLifetime = FindAndTrackNewCorpse("LegacyScaleCorpse_Corpse")
                .GetComponent<CorpseLifetimeController>();
            Assert.That(customScaleLifetime.UnitKind, Is.EqualTo(UnitKind.Operator));
            Assert.That(customScaleLifetime.OwnerKey, Is.EqualTo("LegacyScaleCorpse"));
        }

        [Test]
        public void UnitDeathDestroysSourceImmediatelyInEditMode()
        {
            CreateGround();
            CombatUnit unit = CreateUnit("RenderedUnit", Altitude.Ground, Vector3.zero);
            unit.gameObject.AddComponent<MeshRenderer>();
            unit.gameObject.AddComponent<DeathCorpsePresenter>().Configure(unit, CreateMaterial(Color.white), GroundLayer);

            unit.TakePhysicalDamage(unit.MaxHealth);

            Assert.That(unit == null, Is.True);
            FindAndTrackNewCorpse("RenderedUnit_Corpse");
        }

        [Test]
        public void DeathWithoutConfiguredPresenterDoesNotCreateCorpse()
        {
            CreateGround();
            CombatUnit unit = CreateUnit("TrainingTarget", Altitude.Ground, Vector3.zero);

            unit.TakePhysicalDamage(unit.MaxHealth);

            Assert.That(GetCorpseIds(), Is.EquivalentTo(initialCorpseIds));
        }

        private void CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.layer = GroundLayer;
            gameObjects.Add(ground);
        }

        private CombatUnit CreateUnit(string name, Altitude altitude, Vector3 position)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.position = position;
            gameObjects.Add(gameObject);
            CombatUnit unit = gameObject.AddComponent<CombatUnit>();
            unit.Configure(TeamId.Blue, altitude, 10f, 1f, 0f, 1f, 1f, true, true);
            return unit;
        }

        private Material CreateMaterial(Color color)
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            materials.Add(material);
            return material;
        }

        private GameObject FindAndTrackNewCorpse(string name)
        {
            GameObject corpse = null;
            foreach (GameObject candidate in FindCorpses())
            {
                if (candidate.name == name && !initialCorpseIds.Contains(candidate.GetEntityId()))
                {
                    corpse = candidate;
                    break;
                }
            }

            Assert.That(corpse, Is.Not.Null);
            gameObjects.Add(corpse);
            return corpse;
        }

        private static GameObject[] FindCorpses()
        {
            return System.Array.FindAll(
                GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None),
                gameObject => gameObject.name.EndsWith("_Corpse"));
        }

        private static HashSet<EntityId> GetCorpseIds()
        {
            HashSet<EntityId> corpseIds = new HashSet<EntityId>();
            foreach (GameObject corpse in FindCorpses())
            {
                corpseIds.Add(corpse.GetEntityId());
            }

            return corpseIds;
        }
    }
}
