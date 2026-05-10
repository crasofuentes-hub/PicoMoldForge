namespace PicoMoldForge.Core.Analysis;

public sealed record OpeningDirection3(float X, float Y, float Z)
{
    public static OpeningDirection3 PositiveZ => new(0.0f, 0.0f, 1.0f);

    public bool IsZero => X == 0.0f && Y == 0.0f && Z == 0.0f;
}