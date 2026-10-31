using Lazpoly.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Lazpoly.Presentation.Tests
{
    public class UiBuilderTests
    {
        private GameObject _root;
        private RectTransform _container;

        [SetUp]
        public void CreateContainer()
        {
            _root = new GameObject("UI Test Root", typeof(RectTransform));
            _container = (RectTransform)_root.transform;
        }

        [TearDown]
        public void DestroyContainer() => Object.DestroyImmediate(_root);

        [Test]
        public void ButtonRunsItsActionThenAsksForARedraw()
        {
            int clicks = 0, redraws = 0;
            var ui = new UiBuilder(_container, () => redraws++);
            Button button = ui.Button("Zar At", () => clicks++);

            button.onClick.Invoke();

            Assert.AreEqual(1, clicks);
            Assert.AreEqual(1, redraws);
            Assert.AreEqual("Zar At", button.GetComponentInChildren<Text>().text);
        }

        [Test]
        public void DisabledButtonIsNotInteractable()
        {
            Button button = new UiBuilder(_container).Button("Satın Al", () => { }, enabled: false);
            Assert.IsFalse(button.interactable);
        }

        [Test]
        public void RowsAndCardsPassTheRedrawRequestOn()
        {
            int redraws = 0;
            var ui = new UiBuilder(_container, () => redraws++);
            ui.Card().Row().Button("+10", () => { }).onClick.Invoke();
            Assert.AreEqual(1, redraws);
        }

        [Test]
        public void ToggleReportsItsValue()
        {
            bool? reported = null;
            Toggle toggle = new UiBuilder(_container).Toggle("Tam ekran", false, on => reported = on);
            toggle.isOn = true;
            Assert.AreEqual(true, reported);
        }

        [Test]
        public void InputShowsItsValueWithoutReportingIt()
        {
            string reported = null;
            InputField input = new UiBuilder(_container).Input("Oyuncu 1", text => reported = text);
            Assert.AreEqual("Oyuncu 1", input.text);
            Assert.IsNull(reported);
            input.text = "Ayşe";
            Assert.AreEqual("Ayşe", reported);
        }

        [Test]
        public void ClearRemovesEverything()
        {
            var ui = new UiBuilder(_container);
            ui.Label("a");
            ui.Button("b", null);
            ui.Clear();
            Assert.AreEqual(0, _container.childCount);
        }

        [Test]
        public void ViewRebuildsOnlyWhenItsKeyChangesOrItIsInvalidated()
        {
            int renders = 0;
            var view = new UiView(new UiBuilder(_container), ui =>
            {
                renders++;
                ui.Button("x", () => { });
            });

            Assert.IsTrue(view.Refresh(1));
            Assert.IsFalse(view.Refresh(1));
            Assert.IsTrue(view.Refresh(2));

            _container.GetComponentInChildren<Button>().onClick.Invoke();
            Assert.IsTrue(view.Refresh(2), "A click must redraw the view.");
            Assert.AreEqual(3, renders);
            Assert.AreEqual(1, _container.childCount, "Old content must be removed.");
        }

        [Test]
        public void RoundedSpriteIsOpaqueInsideAndClearInTheCorner()
        {
            Assert.AreEqual(1f, UiSprites.Coverage(16, 16));
            Assert.AreEqual(0f, UiSprites.Coverage(0, 0));
            Assert.IsNotNull(UiSprites.Rounded);
            Assert.Greater(UiSprites.Rounded.border.x, 0f);
        }
    }
}
