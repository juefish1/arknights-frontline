using System.Collections;
using ArknightsFrontline.Arena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ArknightsFrontline.Tests.PlayMode
{
    public sealed class ArenaSceneSmokeTests
    {
        [UnityTest]
        public IEnumerator PrototypeArenaContainsRequiredRoots()
        {
            SceneManager.LoadScene("PrototypeArena");
            yield return null;
            ArenaBootstrap arena = Object.FindFirstObjectByType<ArenaBootstrap>();
            Assert.That(arena, Is.Not.Null);
            Assert.That(arena.BlueTower, Is.Not.Null);
            Assert.That(arena.RedTower, Is.Not.Null);
            Assert.That(UnityEngine.Camera.main, Is.Not.Null);
        }
    }
}
