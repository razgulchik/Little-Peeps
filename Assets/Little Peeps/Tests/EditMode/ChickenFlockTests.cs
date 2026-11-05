using NUnit.Framework;

namespace LittlePeeps.Tests
{
    // The flock's books: every bird is ready, out or resting, the flock's size never changes by being
    // worked, and a bird that is out can only be replaced by itself coming back and resting.
    public class ChickenFlockTests
    {
        private static ChickenFlock Flock(int size)
        {
            var flock = new ChickenFlock();
            flock.GrowTo(size);
            return flock;
        }

        [Test]
        public void NewBirds_AreReady()
        {
            var flock = Flock(5);
            Assert.AreEqual(5, flock.Ready);
            Assert.AreEqual(5, flock.Size);
        }

        [Test]
        public void GrowTo_ASmallerSize_RemovesNobody()
        {
            var flock = Flock(5);
            flock.GrowTo(3);
            Assert.AreEqual(5, flock.Size);
        }

        [Test]
        public void GrowTo_CountsBirdsThatAreOut()
        {
            var flock = Flock(5);
            flock.TakeOut();
            flock.GrowTo(6);
            Assert.AreEqual(5, flock.Ready);
            Assert.AreEqual(1, flock.Out);
            Assert.AreEqual(6, flock.Size);
        }

        [Test]
        public void TakeOut_WithNobodyReady_Refuses()
        {
            var flock = Flock(1);
            Assert.IsTrue(flock.TakeOut());
            Assert.IsFalse(flock.TakeOut());
            Assert.AreEqual(1, flock.Out);
        }

        [Test]
        public void Return_WithRest_IsNotReadyUntilItIsOver()
        {
            var flock = Flock(1);
            flock.TakeOut();
            flock.Return(5f);
            Assert.AreEqual(0, flock.Ready);
            Assert.AreEqual(1, flock.Resting);

            Assert.IsFalse(flock.Tick(4f));
            Assert.AreEqual(0, flock.Ready);

            Assert.IsTrue(flock.Tick(1f));
            Assert.AreEqual(1, flock.Ready);
            Assert.AreEqual(0, flock.Resting);
        }

        [Test]
        public void Return_WithNoRest_IsReadyAtOnce()
        {
            var flock = Flock(1);
            flock.TakeOut();
            flock.Return(0f);
            Assert.AreEqual(1, flock.Ready);
            Assert.AreEqual(0, flock.Resting);
        }

        [Test]
        public void Return_WithNobodyOut_ChangesNothing()
        {
            var flock = Flock(2);
            flock.Return(5f);
            Assert.AreEqual(2, flock.Ready);
            Assert.AreEqual(0, flock.Resting);
            Assert.AreEqual(2, flock.Size);
        }

        [Test]
        public void Rests_RunSeparately()
        {
            var flock = Flock(2);
            flock.TakeOut();
            flock.TakeOut();
            flock.Return(1f);
            flock.Return(3f);

            flock.Tick(1f);
            Assert.AreEqual(1, flock.Ready);
            Assert.AreEqual(1, flock.Resting);

            flock.Tick(2f);
            Assert.AreEqual(2, flock.Ready);
            Assert.AreEqual(0, flock.Resting);
        }

        [Test]
        public void FullCycle_KeepsTheFlockSize()
        {
            var flock = Flock(3);
            while (flock.TakeOut()) { }
            Assert.AreEqual(3, flock.Out);

            flock.Return(1f);
            flock.Return(0f);
            flock.Tick(1f);

            Assert.AreEqual(3, flock.Size);
            Assert.AreEqual(2, flock.Ready);
            Assert.AreEqual(1, flock.Out);
        }

        [Test]
        public void ForgetOut_DropsOnlyTheBirdsOut()
        {
            var flock = Flock(3);
            flock.TakeOut();
            flock.ForgetOut();
            Assert.AreEqual(0, flock.Out);
            Assert.AreEqual(2, flock.Ready);
        }
    }
}
