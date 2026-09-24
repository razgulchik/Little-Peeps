using NUnit.Framework;

namespace LittlePeeps.Tests
{
    // The island rise's notes: each tile breaking the surface plays one step higher up a pentatonic scale,
    // the first tile on the base pitch and the last on the top of the climb, however many tiles there are.
    // NotePitch is static on the profile precisely so it can be pinned here without an asset.
    public class IslandRiseNoteTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void TheFirstTile_PlaysTheBasePitch()
        {
            Assert.That(IslandRiseProfile.NotePitch(0, 40, 10, 0.5f), Is.EqualTo(0.5f).Within(Eps));
        }

        [Test]
        public void FiveSteps_ClimbExactlyOneOctave()
        {
            Assert.That(IslandRiseProfile.NotePitch(39, 40, 5, 1f), Is.EqualTo(2f).Within(Eps));
            Assert.That(IslandRiseProfile.NotePitch(39, 40, 10, 1f), Is.EqualTo(4f).Within(Eps));
        }

        [Test]
        public void TheNotesNeverGoDown_AndSpanTheWholeClimb_ForAnyZoneSize()
        {
            foreach (int tiles in new[] { 2, 7, 40, 120 })
            {
                float previous = 0f;
                for (int note = 0; note < tiles; note++)
                {
                    float pitch = IslandRiseProfile.NotePitch(note, tiles, 10, 1f);
                    Assert.That(pitch, Is.GreaterThanOrEqualTo(previous), $"{tiles} tiles, note {note}");
                    previous = pitch;
                }
                Assert.That(previous, Is.EqualTo(4f).Within(Eps), $"{tiles} tiles end two octaves up");
            }
        }

        [Test]
        public void OneTile_OrNoSteps_StayOnTheBasePitch()
        {
            Assert.That(IslandRiseProfile.NotePitch(0, 1, 10, 0.7f), Is.EqualTo(0.7f).Within(Eps));
            Assert.That(IslandRiseProfile.NotePitch(30, 40, 0, 0.7f), Is.EqualTo(0.7f).Within(Eps));
        }
    }
}
