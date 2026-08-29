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
        private const int TargetableLayer = 7;

        private readonly List<GameObject> gameObjects = new List<GameObject>();
        private readonly List<Material> materials = new List<Material>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject corpse in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (corpse.name.EndsWith("_Corpse"))
                {
                    Object.DestroyImmediate(corpse);
                }
            }

            foreach (GameObject gameObject in gameObjects)
            {
                Object.DestroyImmediate(gameObject);
            }

            foreach (Material material in materials)
            {
                Object.DestroyImmediate(material);
            }

            gameObjects.Clear();
            materials.Clear();
        }

        [Test]
        public void GroundUnitDeathCreatesSameMaterialCorpseAtGroundOffset()
        {
            CreateGround();
            CombatUnit unit = CreateUnit("BlueGround", Altitude.Ground, new Vector3(3f, 1f, -2f));
            Material material = CreateMaterial(Color.blue);
            unit.gameObject.AddComponent<DeathCorpsePresenter>().Configure(unit, material, GroundLayer);

            unit.TakePhysicalDamage(unit.MaxHealth);

            GameObject corpse = FindCorpse("BlueGround_Corpse");
            Assert.That(corpse.GetComponent<Renderer>().sharedMaterial, Is.EqualTo(material));
            Assert.That(corpse.transform.position, Is.EqualTo(new Vector3(3f, 0.01f, -2f)));
            Assert.That(corpse.GetComponent<CombatUnit>(), Is.Null);
            Assert.That(corpse.layer, Is.Not.EqualTo(TargetableLayer));
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
            CorpseFallController fall = FindCorpse("RedAir_Corpse").GetComponent<CorpseFallController>();
            fall.Tick(0.15f);
            Assert.That(fall.transform.position.y, Is.GreaterThan(0.01f));
            fall.Tick(0.15f);
            Assert.That(fall.HasLanded, Is.True);
            Assert.That(fall.transform.position.y, Is.EqualTo(0.01f).Within(0.0001f));
        }

        [Test]
        public void DeathWithoutConfiguredPresenterDoesNotCreateCorpse()
        {
            CreateGround();
            CombatUnit unit = CreateUnit("TrainingTarget", Altitude.Ground, Vector3.zero);

            unit.TakePhysicalDamage(unit.MaxHealth);

            Assert.That(GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None),
                Has.None.Matches<GameObject>(gameObject => gameObject.name.EndsWith("_Corpse")));
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

        private static GameObject FindCorpse(string name)
        {
            GameObject corpse = GameObject.Find(name);
            Assert.That(corpse, Is.Not.Null);
            return corpse;
        }
    }
}
