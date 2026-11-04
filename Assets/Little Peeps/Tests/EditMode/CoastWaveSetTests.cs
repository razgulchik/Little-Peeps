using System.Collections.Generic;
using NUnit.Framework;

namespace LittlePeeps.Tests
{
    // The waves' clock and which art each outline piece takes. Pure — no asset, no sprite — so it runs offline;
    // the tiles and tilemaps themselves are IslandDrawingTests'.
    public class CoastWaveSetTests
    {
        // The artist's wave_01.gif: frame 1 for half a second, then 2, 3, 4, 3, 2 for a fifth each.
        private static readonly List<CoastWaveSet.Step> GifLoop = new()
        {
            new(0, 0.5f), new(1, 0.2f), new(2, 0.2f), new(3, 0.2f), new(2, 0.2f), new(1, 0.2f),
        };

        [TestCase(0.0, 0)]
        [TestCase(0.25, 0)]
        [TestCase(0.6, 1)]
        [TestCase(0.8, 2)]
        [TestCase(1.0, 3)]
        [TestCase(1.2, 2)]
        [TestCase(1.4, 1)]
        public void FrameAt_PlaysTheStepsInTurn(double seconds, int frame)
        {
            Assert.That(CoastWaveSet.FrameAt(GifLoop, seconds), Is.EqualTo(frame));
        }

        [TestCase(1.75, 0)]
        [TestCase(2.1, 1)]
        [TestCase(-0.1, 1)]
        public void FrameAt_GoesRoundAndRound_EitherSideOfZero(double seconds, int frame)
        {
            // A loop is 1.5 s: 1.75 is 0.25 into the second, 2.1 is 0.6 in, -0.1 is 1.4 into the one before.
            Assert.That(CoastWaveSet.FrameAt(GifLoop, seconds), Is.EqualTo(frame));
        }

        [Test]
        public void AStepOfNoLength_NeverShows()
        {
            var steps = new List<CoastWaveSet.Step> { new(0, 0.5f), new(1, 0f), new(2, 0.5f) };
            Assert.That(CoastWaveSet.FrameAt(steps, 0.6), Is.EqualTo(2));
        }

        [Test]
        public void WithoutAStepThatLasts_TheFirstFrameStandsStill()
        {
            Assert.That(CoastWaveSet.FrameAt(new List<CoastWaveSet.Step>(), 3.0), Is.EqualTo(0));
            Assert.That(CoastWaveSet.FrameAt(null, 3.0), Is.EqualTo(0));
            Assert.That(CoastWaveSet.FrameAt(new List<CoastWaveSet.Step> { new(3, 0f), new(1, 0f) }, 3.0), Is.EqualTo(3));
        }

        [Test]
        public void ACellWithABandAndAColumn_IsACorner_MirroredBesideAWallToTheWest()
        {
            var expected = new Dictionary<(IslandTrimPiece, IslandTrimPiece), (CoastWaveCorner, bool)>
            {
                [(IslandTrimPiece.BottomEdgeMid, IslandTrimPiece.LeftEdgeMid)] = (CoastWaveCorner.Bay, false),
                [(IslandTrimPiece.BottomEdgeMid, IslandTrimPiece.RightEdgeMid)] = (CoastWaveCorner.Bay, true),
                [(IslandTrimPiece.BottomEdgeLeft, IslandTrimPiece.LeftEdgeMid)] = (CoastWaveCorner.Step, false),
                [(IslandTrimPiece.BottomEdgeRight, IslandTrimPiece.RightEdgeMid)] = (CoastWaveCorner.Step, true),
                [(IslandTrimPiece.BottomEdgeRight, IslandTrimPiece.LeftEdgeTop)] = (CoastWaveCorner.Notch, false),
                [(IslandTrimPiece.BottomEdgeLeft, IslandTrimPiece.RightEdgeTop)] = (CoastWaveCorner.Notch, true),
            };
            foreach (IslandTrimPiece horizontal in System.Enum.GetValues(typeof(IslandTrimPiece)))
                foreach (IslandTrimPiece vertical in System.Enum.GetValues(typeof(IslandTrimPiece)))
                {
                    var corner = CoastWaveSet.CornerOf(horizontal, vertical, out bool mirrored);
                    var want = expected.TryGetValue((horizontal, vertical), out var e) ? e : (CoastWaveCorner.None, false);
                    Assert.That((corner, mirrored), Is.EqualTo(want), $"{horizontal} + {vertical}");
                }
        }

        [Test]
        public void EveryCornerTheOutlineDraws_HasItsArt()
        {
            // Every pair the painter puts on one water cell, over grown islands, must be a corner with art. The one
            // pair without (a band rounded at its left end over a column's top) needs a strip one cell wide, which
            // the island shapes never have — this fails if they ever do.
            var seen = new HashSet<(IslandTrimPiece, IslandTrimPiece)>();
            for (int seed = 1; seed <= 20; seed++)
            {
                var generator = new IslandGenerator(new IslandRules(), seed);
                generator.Commit(generator.GenerateStart());
                for (int age = 0; age < 3; age++)
                {
                    var grown = generator.Propose(1);
                    if (grown.Count == 0) break;
                    generator.Commit(grown[0]);
                }
                var grid = new IslandGrid(1f);
                foreach (var cell in generator.Land) grid.SetCell(cell, TerrainType.Grass);
                foreach (var paint in IslandTilePainter.Plan(new IslandTilePainter.Land(grid)).Values)
                    if (!paint.land && paint.horizontal != IslandTrimPiece.None && paint.vertical != IslandTrimPiece.None)
                        seen.Add((paint.horizontal, paint.vertical));
            }

            Assert.That(seen, Is.Not.Empty, "no island had a corner — nothing was tested");
            foreach (var (horizontal, vertical) in seen)
                Assert.That(CoastWaveSet.CornerOf(horizontal, vertical, out _), Is.Not.EqualTo(CoastWaveCorner.None),
                            $"{horizontal} + {vertical} comes up on an island but has no corner art");
        }

        [Test]
        public void TheRightEdgesPieces_AreTheLeftsMirrored_AndNothingElseIs()
        {
            var mirroredPieces = new[]
            {
                IslandTrimPiece.RightEdgeTop, IslandTrimPiece.RightEdgeMid, IslandTrimPiece.RightEdgeBottom,
                IslandTrimPiece.BottomEdgeRight,
            };
            var frame = new CoastWaveSet.Frame();
            foreach (IslandTrimPiece piece in System.Enum.GetValues(typeof(IslandTrimPiece)))
            {
                frame.Of(piece, out bool mirrored);
                Assert.That(mirrored, Is.EqualTo(System.Array.IndexOf(mirroredPieces, piece) >= 0), piece.ToString());
            }
        }
    }
}
