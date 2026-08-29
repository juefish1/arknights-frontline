using System.Collections;
using System.Collections.Generic;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class DeathCorpsePresenterTests
    {
        private const int GroundLayer = 8;

        private readonly List<GameObject> gameObjects = new List<GameObject>();
        private readonly List<Material> materials = new List<Material>();
        private readonly HashSet<int> initialCorpseIds = new HashSet<int>();

        [SetUp]
        public void SetUp()
        {
            initialCorpseIds.Clear();
            foreach (GameObject corpse in FindCorpses())
            {
                initialCorpseIds.Add(corpse.GetInstanceID());
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
            Assert.That(corpse.GetComponent<Renderer>().sharedMaterial, Is.EqualTo(material));
            Assert.That(corpse.transform.position, Is.EqualTo(new Vector3(3f, 0.01f, -2f)));
            Assert.That(corpse.GetComponent<CombatUnit>(), Is.Null);
            Assert.That(LayerMask.NameToLayer("Targetable"), Is.EqualTo(9));
            Assert.That(corpse.layer, Is.EqualTo(LayerMask.NameToLayer("Default")));
            Collider collider = corpse.GetComponent<Collider>();
            Assert.That(collider == null || !collider.enabled, Is.True);
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

        [UnityTest]
        public IEnumerator UnitDeathHidesSourceImmediatelyAndDestroysItOnNextFrame()
        {
            CreateGround();
            CombatUnit unit = CreateUnit("RenderedUnit", Altitude.Ground, Vector3.zero);
            Renderer sourceRenderer = unit.gameObject.AddComponent<MeshRenderer>();
            unit.gameObject.AddComponent<DeathCorpsePresenter>().Configure(unit, CreateMaterial(Color.white), GroundLayer);

            unit.TakePhysicalDamage(unit.MaxHealth);

            Assert.That(sourceRenderer.enabled, Is.False);
            FindAndTrackNewCorpse("RenderedUnit_Corpse");

            yield return null;

            Assert.That(unit, Is.Null);
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
                if (candidate.name == name && !initialCorpseIds.Contains(candidate.GetInstanceID()))
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

        private static HashSet<int> GetCorpseIds()
        {
            HashSet<int> corpseIds = new HashSet<int>();
            foreach (GameObject corpse in FindCorpses())
            {
                corpseIds.Add(corpse.GetInstanceID());
            }

            return corpseIds;
        }
    }
}
