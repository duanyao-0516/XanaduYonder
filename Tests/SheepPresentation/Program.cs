using System;
using XanaduYonder.Core.Sheep;
using XanaduYonder.Runtime.Sheep;

internal static class Program
{
    private static int checks;

    private static int Main()
    {
        try
        {
            foreach (var size in new[] { new[] { 656f, 764f }, new[] { 656f, 1000f }, new[] { 656f, 1300f }, new[] { 420f, 400f } })
            for (var level = 1; level <= 2; level++)
            for (var layout = 0; layout < (level == 1 ? 3 : 15); layout++)
                ValidateLayout(level, layout, size[0], size[1]);
            foreach (var size in new[] { new[] { 656f, 738f }, new[] { 656f, 1000f }, new[] { 656f, 1300f },
                new[] { 420f, 400f }, new[] { 960f, 738f }, new[] { 656f, 400f } })
                ValidateTutorialCardSize(size[0], size[1]);
            Console.WriteLine("PASS " + checks + " presentation checks: bounds, aspect, fan extents, overlap, gap limits, height use and consistent tutorial card size.");
            return 0;
        }
        catch (Exception error) { Console.WriteLine("FAIL " + error.Message); return 1; }
    }

    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }

    private static void ValidateLayout(int level, int layout, float width, float height)
    {
        var positions = SheepBoardLayout.GetPositions(level, layout);
        var geometry = new SheepBoardGeometry(positions, width, height);
        var context = level + "/" + layout + " @ " + width + "x" + height;
        foreach (var position in positions)
        {
            var face = geometry.TileBounds(position, 0);
            Check(Math.Abs(face.Width / face.Height - 72f / 81f) < .00001f, "Distorted card " + context);
            for (var depth = 0; depth <= (position.BlindStack > 0 ? 11 : 0); depth++)
                Inside(geometry.TileBounds(position, depth), width, height, context);
            if (position.BlindStack > 0) Inside(geometry.StackLabelBounds(position), width, height, context);
            foreach (var other in positions)
            {
                var before = Math.Abs(position.X - other.X) * 36 < 72 && Math.Abs(position.Y - other.Y) * 36 < 81;
                var b = geometry.TileBounds(other, 0);
                var after = Intersects(face, b);
                Check(before == after, "Changed visible overlap " + position.Id + "/" + other.Id + " " + context);
                if (SheepBoardLayout.Overlaps(position, other))
                    Check(Math.Abs((face.Y - b.Y) - (position.Y - other.Y) * 36 * geometry.Scale) < .001f,
                        "Changed cover offset " + context);
            }
        }
        Check(geometry.AddedGap >= 0 && geometry.AddedGap <= 72, "Unbounded empty gap " + context);
        Check(geometry.ContentHeight <= height - 16 + .001f, "Height overflow " + context);
        if (level == 2 && layout >= 3 && height >= 764)
            Check(geometry.ContentHeight > 630, "Challenge field failed to use reclaimed height " + context);
        Check(Math.Abs(geometry.TopPadding - (height - geometry.ContentHeight) / 2) < .001f, "Field not centered " + context);
    }

    private static void ValidateTutorialCardSize(float width, float height)
    {
        for (var tutorialLayout = 0; tutorialLayout < 3; tutorialLayout++)
        {
            var tutorialPositions = SheepBoardLayout.GetPositions(1, tutorialLayout);
            var tutorial = new SheepBoardGeometry(tutorialPositions, width, height);
            var tutorialCard = tutorial.TileBounds(tutorialPositions[0], 0);
            for (var challengeLayout = 0; challengeLayout < SheepBoardLayout.ChallengeLayoutCount; challengeLayout++)
            {
                var challengePositions = SheepBoardLayout.GetPositions(2, challengeLayout);
                var challenge = new SheepBoardGeometry(challengePositions, width, height);
                var challengeCard = challenge.TileBounds(challengePositions[0], 0);
                Check(tutorialCard.Width <= challengeCard.Width + .001f,
                    "Tutorial card larger than challenge card " + tutorialLayout + "/" + challengeLayout +
                    " @ " + width + "x" + height + ": " + tutorialCard.Width + " > " + challengeCard.Width);
            }
        }
    }

    private static bool Intersects(SheepTileBounds a, SheepTileBounds b)
    {
        const float epsilon = .001f;
        return a.X < b.X + b.Width - epsilon && b.X < a.X + a.Width - epsilon &&
            a.Y < b.Y + b.Height - epsilon && b.Y < a.Y + a.Height - epsilon;
    }

    private static void Inside(SheepTileBounds bounds, float width, float height, string context)
    {
        Check(bounds.X >= 7.99f && bounds.Y >= 7.99f && bounds.X + bounds.Width <= width - 7.99f &&
            bounds.Y + bounds.Height <= height - 7.99f, "Card, fan or label outside stage " + context);
    }
}
