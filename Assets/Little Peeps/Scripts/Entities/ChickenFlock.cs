using System.Collections.Generic;

namespace LittlePeeps
{
    // The books of one coop's flock: how many birds wait in the pen ready to go, how many rest there and
    // how many are out on the island. Every bird is in exactly one of the three, so the flock never grows
    // or shrinks by being worked; only GrowTo changes its size. Plain C# with no Unity behind it, so the
    // rule is tested without a scene. ChickenCoop drives it and owns the bodies of the birds that are out.
    public sealed class ChickenFlock
    {
        private readonly List<float> rests = new();   // seconds left, one entry per resting bird

        public int Ready { get; private set; }
        public int Out { get; private set; }
        public int Resting => rests.Count;
        public int Size => Ready + Out + Resting;

        // New birds arrive ready. A size below the current one removes nobody.
        public void GrowTo(int size)
        {
            if (size > Size) Ready += size - Size;
        }

        // One ready bird leaves the pen. False when none is ready.
        public bool TakeOut()
        {
            if (Ready == 0) return false;
            Ready--;
            Out++;
            return true;
        }

        // A bird that was out is back in the pen. With no rest to serve it is ready at once.
        public void Return(float rest)
        {
            if (Out == 0) return;
            Out--;
            if (rest > 0f) rests.Add(rest);
            else Ready++;
        }

        // Run the rests down. True when a bird has just become ready, so a view knows to look again.
        public bool Tick(float dt)
        {
            bool changed = false;
            for (int i = rests.Count - 1; i >= 0; i--)
            {
                rests[i] -= dt;
                if (rests[i] > 0f) continue;
                rests.RemoveAt(i);
                Ready++;
                changed = true;
            }
            return changed;
        }

        // The coop is gone: nobody is out any more.
        public void ForgetOut() => Out = 0;
    }
}
