using ArknightsFrontline.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class GameInputActionsTests
    {
        [Test]
        public void DefaultsMatchTheApprovedKeyboardAndMouseLayout()
        {
            using (var input = new GameInputActions())
            {
                Assert.That(input.MoveClick.bindings[0].effectivePath, Is.EqualTo("<Mouse>/rightButton"));
                Assert.That(input.AttackMove.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/q"));
                Assert.That(input.Skill1.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/w"));
                Assert.That(input.Skill2.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/e"));
                Assert.That(input.Skill3.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/r"));
                Assert.That(input.Retreat.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/b"));
            }
        }

        [Test]
        public void BindingOverridesRoundTripThroughStore()
        {
            PlayerPrefs.DeleteKey("af.input.bindings.v1");
            using (var first = new GameInputActions())
            {
                first.AttackMove.ApplyBindingOverride(0, "<Keyboard>/a");
                InputBindingStore.Save(first.Asset);
            }

            using (var second = new GameInputActions())
            {
                InputBindingStore.Load(second.Asset);

                Assert.That(second.AttackMove.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/a"));
            }

            PlayerPrefs.DeleteKey("af.input.bindings.v1");
        }
    }
}
