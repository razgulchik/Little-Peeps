using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LittlePeeps.Tests
{
    // The window's contract is ConfirmDialog's: the caller hears back exactly once, only after the window
    // has let go of both callbacks, and a greyed Pay cannot be given at all. On top of that, the quota's
    // layout — rows centred one by one, whole pixels — because a grid that left a short row hanging on the
    // left is the obvious "simplification" to undo it.
    //
    // The answers are given the way the player gives them: Pay's onClick, and a hold on Face the Wrath with
    // its hold time at zero (release confirms; Edit Mode never ticks the timer). Native Unity objects
    // throughout, so the offline harness skips these; they run in the Test Runner.
    public class OfferingWindowTests
    {
        private GameObject go;
        private OfferingWindow window;
        private Button pay;
        private HoldButton wrath;
        private CanvasGroup payGroup;
        private int offered;
        private int wrathed;

        private static readonly List<ResourceCost> Quota = new()
        {
            new ResourceCost { resourceType = ResourceType.Wood, amount = 40 },
        };

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("OfferingWindow", typeof(RectTransform), typeof(CanvasGroup));
            window = go.AddComponent<OfferingWindow>();

            var payGo = new GameObject("PayButton", typeof(RectTransform), typeof(CanvasGroup));
            payGo.transform.SetParent(go.transform);
            pay = payGo.AddComponent<Button>();
            payGroup = payGo.GetComponent<CanvasGroup>();

            var wrathGo = new GameObject("WrathButton", typeof(RectTransform));
            wrathGo.transform.SetParent(go.transform);
            wrath = wrathGo.AddComponent<HoldButton>();

            var so = new SerializedObject(window);
            so.FindProperty("payButton").objectReferenceValue = pay;
            so.FindProperty("payGroup").objectReferenceValue = payGroup;
            so.FindProperty("wrathButton").objectReferenceValue = wrath;
            so.ApplyModifiedPropertiesWithoutUndo();

            var hold = new SerializedObject(wrath);
            hold.FindProperty("holdDuration").floatValue = 0f;
            hold.ApplyModifiedPropertiesWithoutUndo();

            offered = 0;
            wrathed = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        private bool Ask(bool canOffer = true) => window.Show(Quota, canOffer, () => offered++, () => wrathed++);

        private static PointerEventData Pointer() => new PointerEventData(EventSystem.current);

        private void HoldWrath()
        {
            wrath.OnPointerDown(Pointer());
            wrath.OnPointerUp(Pointer());
        }

        [Test]
        public void Show_OpensTheWindow()
        {
            Assert.That(Ask(), Is.True);
            Assert.That(window.IsOpen, Is.True);
        }

        [Test]
        public void Pay_CallsOfferOnce_AndClosesTheWindow()
        {
            Ask();

            pay.onClick.Invoke();

            Assert.That(offered, Is.EqualTo(1));
            Assert.That(wrathed, Is.Zero);
            Assert.That(window.IsOpen, Is.False);
        }

        [Test]
        public void HoldingWrath_CallsWrathOnce()
        {
            Ask();

            HoldWrath();

            Assert.That(wrathed, Is.EqualTo(1));
            Assert.That(offered, Is.Zero);
            Assert.That(window.IsOpen, Is.False);
        }

        // The window still shows the offering when the stock cannot pay it — Pay is there, greyed, and dead.
        [Test]
        public void WhenTheStockCannotPay_PayIsGreyedAndDoesNothing()
        {
            Ask(canOffer: false);

            Assert.That(pay.interactable, Is.False);
            Assert.That(payGroup.alpha, Is.LessThan(1f));

            pay.onClick.Invoke();

            Assert.That(offered, Is.Zero);
            Assert.That(window.IsOpen, Is.True, "a dead Pay must not close the window either");
        }

        // A greyed Pay must not leak into the next cycle's window.
        [Test]
        public void EveryShowSetsPayAfresh()
        {
            Ask(canOffer: false);
            window.Hide();

            Ask(canOffer: true);

            Assert.That(pay.interactable, Is.True);
            Assert.That(payGroup.alpha, Is.EqualTo(1f));
        }

        // A double click, or both answers in the same frame: the first answer is the answer.
        [Test]
        public void OnlyTheFirstAnswerCounts()
        {
            Ask();

            pay.onClick.Invoke();
            HoldWrath();
            pay.onClick.Invoke();

            Assert.That(offered, Is.EqualTo(1));
            Assert.That(wrathed, Is.Zero);
        }

        // OfferingEndState hides the window from its Exit, whichever way it leaves: a click after that must
        // not call into a state that is no longer current.
        [Test]
        public void AfterHide_NoAnswerReachesTheCaller()
        {
            Ask();
            window.Hide();

            pay.onClick.Invoke();
            HoldWrath();

            Assert.That(offered, Is.Zero);
            Assert.That(wrathed, Is.Zero);
        }

        // Two asks must not wire the buttons twice — one click, one answer.
        [Test]
        public void AskingTwice_StillAnswersOnce()
        {
            Ask();
            LogAssert.Expect(LogType.Warning, new Regex("asked again while still open"));
            Ask();

            pay.onClick.Invoke();

            Assert.That(offered, Is.EqualTo(1));
        }

        // Inactive, the CanvasGroup cannot show anything — the caller must hear "no" and not freeze the game.
        [Test]
        public void AnInactiveWindowRefusesToAsk()
        {
            go.SetActive(false);
            LogAssert.Expect(LogType.Error, new Regex("cannot ask"));

            Assert.That(Ask(), Is.False);
            Assert.That(window.IsOpen, Is.False);
        }
    }

    // The quota's layout: rows of three, each centred on its own, the block centred in the area, every unit on
    // a whole pixel. Area and cell are the mockup's (114×26, 34×13, gaps 6×0).
    public class OfferingWindowLayoutTests
    {
        private GameObject go;
        private GameObject template;
        private OfferingWindow window;
        private RectTransform area;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("OfferingWindow", typeof(RectTransform), typeof(CanvasGroup));
            window = go.AddComponent<OfferingWindow>();

            area = new GameObject("Costs", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(go.transform);
            area.anchorMin = area.anchorMax = area.pivot = new Vector2(0f, 1f);
            area.sizeDelta = new Vector2(114f, 26f);

            // A unit with no icon and no label: only its root rect matters here.
            template = new GameObject("Unit", typeof(RectTransform));
            ((RectTransform)template.transform).sizeDelta = new Vector2(34f, 13f);
            var unit = template.AddComponent<ResourceUnit>();

            var pay = new GameObject("Pay", typeof(RectTransform)).AddComponent<Button>();
            pay.transform.SetParent(go.transform);
            var wrath = new GameObject("Wrath", typeof(RectTransform)).AddComponent<HoldButton>();
            wrath.transform.SetParent(go.transform);

            var so = new SerializedObject(window);
            so.FindProperty("payButton").objectReferenceValue = pay;
            so.FindProperty("wrathButton").objectReferenceValue = wrath;
            so.FindProperty("costArea").objectReferenceValue = area;
            so.FindProperty("costPrefab").objectReferenceValue = unit;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
            if (template != null) Object.DestroyImmediate(template);
        }

        private Vector2[] PositionsFor(int count)
        {
            var quota = new List<ResourceCost>();
            for (int i = 0; i < count; i++)
                quota.Add(new ResourceCost { resourceType = ResourceType.Wood, amount = 1 });

            window.Show(quota, true, null, null);

            var positions = new List<Vector2>();
            foreach (RectTransform child in area)
                if (child.gameObject.activeSelf) positions.Add(child.anchoredPosition);
            return positions.ToArray();
        }

        [Test]
        public void SixUnits_FillTwoFullRows_FromTheTopLeft()
        {
            Assert.That(PositionsFor(6), Is.EqualTo(new[]
            {
                new Vector2(0, 0), new Vector2(40, 0), new Vector2(80, 0),
                new Vector2(0, -13), new Vector2(40, -13), new Vector2(80, -13),
            }));
        }

        // One row, centred both ways: (114 − 34) / 2 = 40 across, (26 − 13) / 2 = 6.5 → 6 down.
        [Test]
        public void OneUnit_SitsInTheMiddle_OnWholePixels()
        {
            Assert.That(PositionsFor(1), Is.EqualTo(new[] { new Vector2(40, -6) }));
        }

        // The short second row is centred on its own instead of hanging on the left.
        [Test]
        public void FourUnits_CentreTheShortRowOnItsOwn()
        {
            Assert.That(PositionsFor(4), Is.EqualTo(new[]
            {
                new Vector2(0, 0), new Vector2(40, 0), new Vector2(80, 0),
                new Vector2(40, -13),
            }));
        }

        // Fewer units next cycle: the extras are switched off, not left standing with last cycle's amounts.
        [Test]
        public void AShorterQuota_SwitchesTheExtrasOff()
        {
            PositionsFor(5);

            Assert.That(PositionsFor(2), Is.EqualTo(new[] { new Vector2(20, -6), new Vector2(60, -6) }));
        }
    }
}
