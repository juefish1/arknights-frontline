using System.Collections.Generic;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class HealthBarPresenterTests
    {
        private readonly List<GameObject> gameObjects = new List<GameObject>();

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

            gameObjects.Clear();
        }

        [Test]
        public void ConfiguredHealthBarIsVisibleAndFullAtMaximumHealth()
        {
            CombatUnit unit = CreateUnit(100f);
            HealthBarPresenter presenter = unit.gameObject.AddComponent<HealthBarPresenter>();

            presenter.Configure(unit);
            presenter.Tick();

            Assert.That(presenter.IsVisible, Is.True);
            Assert.That(presenter.FillAmount, Is.EqualTo(1f));
            Assert.That(unit.transform.Find("HealthBar"), Is.Not.Null);
        }

        [Test]
        public void TickReflectsCurrentHealthAsFillAmount()
        {
            CombatUnit unit = CreateUnit(100f);
            HealthBarPresenter presenter = unit.gameObject.AddComponent<HealthBarPresenter>();
            presenter.Configure(unit);

            unit.TakePhysicalDamage(35f);
            presenter.Tick();

            Assert.That(presenter.FillAmount, Is.EqualTo(0.65f).Within(0.0001f));
        }

        [Test]
        public void TickConfiguresFillSpriteAndChangesItsVisualFillAfterDamage()
        {
            CombatUnit unit = CreateUnit(100f);
            HealthBarPresenter presenter = unit.gameObject.AddComponent<HealthBarPresenter>();
            presenter.Configure(unit);

            presenter.Tick();
            Image fill = unit.transform.Find("HealthBar/Fill").GetComponent<Image>();

            Assert.That(fill.sprite, Is.Not.Null);
            Assert.That(fill.fillAmount, Is.EqualTo(1f));

            unit.TakePhysicalDamage(35f);
            presenter.Tick();

            Assert.That(fill.fillAmount, Is.EqualTo(0.65f).Within(0.0001f));
        }

        [Test]
        public void TickPlacesHealthBarAboveTheUnitRenderer()
        {
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            gameObjects.Add(player);
            player.transform.position = new Vector3(0f, 1f, 0f);
            CombatUnit unit = player.AddComponent<CombatUnit>();
            unit.Configure(TeamId.Blue, Altitude.Ground, 100f, 0f, 0f, 0f, 0f, false, false);
            HealthBarPresenter presenter = unit.gameObject.AddComponent<HealthBarPresenter>();
            presenter.Configure(unit);

            presenter.Tick();

            Assert.That(
                unit.transform.Find("HealthBar").position.y,
                Is.GreaterThan(player.GetComponent<Renderer>().bounds.max.y));
        }

        [Test]
        public void TickPlacesParentedUnitHealthBarAboveTheOwningRenderer()
        {
            GameObject arenaBootstrap = new GameObject("ArenaBootstrap");
            gameObjects.Add(arenaBootstrap);

            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            gameObjects.Add(player);
            player.transform.SetParent(arenaBootstrap.transform);
            player.transform.localScale = new Vector3(1f, 3f, 1f);

            CombatUnit unit = player.AddComponent<CombatUnit>();
            unit.Configure(TeamId.Blue, Altitude.Ground, 100f, 0f, 0f, 0f, 0f, false, false);
            HealthBarPresenter presenter = unit.gameObject.AddComponent<HealthBarPresenter>();
            presenter.Configure(unit);

            presenter.Tick();

            Assert.That(
                unit.transform.Find("HealthBar").position.y,
                Is.GreaterThan(player.GetComponent<Renderer>().bounds.max.y));
        }

        [Test]
        public void TickPlacesHealthBarAboveChildRendererWhenRootHasNoRenderer()
        {
            GameObject tower = new GameObject("Tower");
            gameObjects.Add(tower);
            CombatUnit unit = tower.AddComponent<CombatUnit>();
            unit.Configure(TeamId.Blue, Altitude.Ground, 500f, 0f, 0f, 0f, 0f, false, false);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "TowerVisual";
            visual.transform.SetParent(tower.transform, false);
            visual.transform.localPosition = new Vector3(0f, 3f, 0f);
            visual.transform.localScale = new Vector3(3f, 6f, 3f);

            HealthBarPresenter presenter = tower.AddComponent<HealthBarPresenter>();
            presenter.Configure(unit);
            presenter.Tick();

            Assert.That(
                tower.transform.Find("HealthBar").position.y,
                Is.GreaterThan(visual.GetComponent<Renderer>().bounds.max.y));
        }

        private CombatUnit CreateUnit(float health)
        {
            GameObject gameObject = new GameObject("HealthBarTarget");
            gameObjects.Add(gameObject);
            CombatUnit unit = gameObject.AddComponent<CombatUnit>();
            unit.Configure(TeamId.Blue, Altitude.Ground, health, 0f, 0f, 0f, 0f, false, false);
            return unit;
        }
    }
}
