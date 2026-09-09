namespace PhieuFlow.Hub.Endpoints;

// Keyset-pagination `take` bound, shared by the list endpoints.
internal static class TakeParameter
{
    internal const int Max = 100;

    internal static bool IsValid(int take) => take is >= 1 and <= Max;

    internal static IResult OutOfRange() =>
        Results.BadRequest($"'take' must be between 1 and {Max}.");
}
