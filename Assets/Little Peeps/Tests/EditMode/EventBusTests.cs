using System;
using NUnit.Framework;

namespace LittlePeeps.Tests
{
    // EventBus<T> publishes over a CACHED ARRAY of subscribers, rebuilt only when the set changes, and
    // captures that array into a local before iterating. Two consequences of that design are what these
    // tests exist for, because both are invisible until a handler misbehaves at runtime:
    //
    //  - a handler that subscribes or unsubscribes MID-DISPATCH must not disturb the dispatch already
    //    in flight; its change lands on the next Publish (snapshot semantics, by design);
    //  - a handler that re-entrantly Publishes may cause the cache to be rebuilt underneath the outer
    //    loop, which must keep walking its captured copy and still reach every remaining subscriber.
    //
    // The bus is static, so every test starts and ends by clearing it — otherwise a handler from one
    // test would keep firing in the next for the rest of the editor session.
    public class EventBusTests
    {
        private struct Ping
        {
            public int value;
        }

        [SetUp]
        public void ClearBusBefore() => EventBus<Ping>.Clear();

        [TearDown]
        public void ClearBusAfter() => EventBus<Ping>.Clear();

        [Test]
        public void Publish_DeliversThePayloadToEverySubscriber()
        {
            int a = 0, b = 0;
            EventBus<Ping>.Subscribe(p => a += p.value);
            EventBus<Ping>.Subscribe(p => b += p.value);

            EventBus<Ping>.Publish(new Ping { value = 3 });

            Assert.AreEqual(3, a);
            Assert.AreEqual(3, b);
        }

        [Test]
        public void Publish_WithNoSubscribers_DoesNothing()
        {
            Assert.DoesNotThrow(() => EventBus<Ping>.Publish(new Ping { value = 1 }));
        }

        [Test]
        public void Unsubscribe_StopsDelivery()
        {
            int calls = 0;
            Action<Ping> handler = _ => calls++;

            EventBus<Ping>.Subscribe(handler);
            EventBus<Ping>.Publish(default);
            EventBus<Ping>.Unsubscribe(handler);
            EventBus<Ping>.Publish(default);

            Assert.AreEqual(1, calls);
        }

        [Test]
        public void Unsubscribe_OfAHandlerThatWasNeverSubscribed_DoesNothing()
        {
            int calls = 0;
            Action<Ping> subscribed = _ => calls++;
            EventBus<Ping>.Subscribe(subscribed);

            Assert.DoesNotThrow(() => EventBus<Ping>.Unsubscribe(_ => { }));

            EventBus<Ping>.Publish(default);
            Assert.AreEqual(1, calls, "the real subscriber must survive an unrelated Unsubscribe");
        }

        [Test]
        public void Subscribe_Twice_DeliversOnce()
        {
            int calls = 0;
            Action<Ping> handler = _ => calls++;

            // OnEnable running twice (re-enabled object, additive scene load) must not double the
            // handler — and one Unsubscribe in OnDisable must then be enough to stop it entirely.
            EventBus<Ping>.Subscribe(handler);
            EventBus<Ping>.Subscribe(handler);
            EventBus<Ping>.Publish(default);

            Assert.AreEqual(1, calls);

            EventBus<Ping>.Unsubscribe(handler);
            EventBus<Ping>.Publish(default);
            Assert.AreEqual(1, calls, "a single Unsubscribe must fully remove the handler");
        }

        [Test]
        public void Clear_RemovesEverySubscriber()
        {
            int calls = 0;
            EventBus<Ping>.Subscribe(_ => calls++);
            EventBus<Ping>.Subscribe(_ => calls++);

            EventBus<Ping>.Clear();
            EventBus<Ping>.Publish(default);

            Assert.AreEqual(0, calls);
        }

        // --- changing the subscriber set DURING a dispatch ------------------------------------------

        [Test]
        public void UnsubscribeDuringDispatch_DoesNotDisturbTheDispatchInFlight()
        {
            int firstCalls = 0, secondCalls = 0;
            Action<Ping> first = null;
            Action<Ping> second = _ => secondCalls++;
            first = _ =>
            {
                firstCalls++;
                EventBus<Ping>.Unsubscribe(first);
                EventBus<Ping>.Unsubscribe(second);
            };

            EventBus<Ping>.Subscribe(first);
            EventBus<Ping>.Subscribe(second);
            EventBus<Ping>.Publish(default);

            // Snapshot semantics, deliberate: the running dispatch walks the array it started with, so
            // BOTH handlers still see this event even though both were removed halfway through it. The
            // guarantee under test is that the dispatch completes intact, not that removal is instant.
            Assert.AreEqual(1, firstCalls);
            Assert.AreEqual(1, secondCalls, "a handler removed mid-dispatch still receives the event in flight");
        }

        [Test]
        public void UnsubscribeDuringDispatch_TakesEffectOnTheNextPublish()
        {
            int firstCalls = 0, secondCalls = 0;
            Action<Ping> first = null;
            Action<Ping> second = _ => secondCalls++;
            first = _ =>
            {
                firstCalls++;
                EventBus<Ping>.Unsubscribe(first);
                EventBus<Ping>.Unsubscribe(second);
            };

            EventBus<Ping>.Subscribe(first);
            EventBus<Ping>.Subscribe(second);
            EventBus<Ping>.Publish(default);
            EventBus<Ping>.Publish(default);

            Assert.AreEqual(1, firstCalls, "removal landed before the second Publish");
            Assert.AreEqual(1, secondCalls, "removal landed before the second Publish");
        }

        [Test]
        public void SubscribeDuringDispatch_TakesEffectOnTheNextPublish()
        {
            int lateCalls = 0;
            Action<Ping> late = _ => lateCalls++;
            bool subscribed = false;

            EventBus<Ping>.Subscribe(_ =>
            {
                if (subscribed) return;
                subscribed = true;
                EventBus<Ping>.Subscribe(late);
            });

            EventBus<Ping>.Publish(default);
            Assert.AreEqual(0, lateCalls, "a handler added mid-dispatch must not join the event in flight");

            EventBus<Ping>.Publish(default);
            Assert.AreEqual(1, lateCalls);
        }

        // --- re-entrant Publish ---------------------------------------------------------------------

        [Test]
        public void ReentrantPublish_ReachesEverySubscriberOnBothLevels()
        {
            int reentrantCalls = 0, plainCalls = 0;
            bool recursed = false;

            Action<Ping> reentrant = _ =>
            {
                reentrantCalls++;
                if (recursed) return;
                recursed = true;
                EventBus<Ping>.Publish(new Ping { value = 2 });   // publish from inside a handler
            };
            Action<Ping> plain = _ => plainCalls++;

            EventBus<Ping>.Subscribe(reentrant);
            EventBus<Ping>.Subscribe(plain);
            EventBus<Ping>.Publish(new Ping { value = 1 });

            // Inner dispatch runs both handlers to completion, then the outer one resumes and still
            // reaches `plain` — nobody is skipped and nobody is called twice per dispatch.
            Assert.AreEqual(2, reentrantCalls);
            Assert.AreEqual(2, plainCalls);
        }

        [Test]
        public void ReentrantPublish_AfterSubscribingMidDispatch_StillFinishesTheOuterDispatch()
        {
            // The nastiest ordering: a handler subscribes (marking the cache dirty) and then publishes
            // re-entrantly, so the inner Publish REBUILDS the shared cache array while the outer loop
            // is still walking it. The outer loop captured its own reference, so `plain` must still be
            // reached after the re-entrant call returns.
            int plainCalls = 0, lateCalls = 0;
            bool recursed = false;
            Action<Ping> late = _ => lateCalls++;

            EventBus<Ping>.Subscribe(_ =>
            {
                if (recursed) return;
                recursed = true;
                EventBus<Ping>.Subscribe(late);           // dirties the cache
                EventBus<Ping>.Publish(default);          // rebuilds it mid-iteration
            });
            EventBus<Ping>.Subscribe(_ => plainCalls++);

            EventBus<Ping>.Publish(default);

            Assert.AreEqual(2, plainCalls, "outer dispatch must survive the cache being rebuilt under it");
            Assert.AreEqual(1, lateCalls, "the newly added handler joins from the inner Publish onward");
        }
    }
}
