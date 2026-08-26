namespace PhieuFlow.FormBuilder.Components.Shared.QuestionEditors;

public static class MinMaxValidation
{
    public static bool ExceedsMax<T>(T? min, T? max) where T : struct, IComparable<T>
    {
        return min is not null && max is not null && min.Value.CompareTo(max.Value) > 0;
    }

    public static string InputClasses(bool invalid) => "h-[30px] w-full rounded-md border bg-page-bg px-[10px] text-[13px] text-text outline-none focus:border-accent "
        + (invalid ? "border-danger-border" : "border-border-control");

    public static int? ParseNullableInt(object? value)
    {
        return int.TryParse(value?.ToString(), out var parsed) ? parsed : null;
    }

    public static decimal? ParseNullableDecimal(object? value)
    {
        return decimal.TryParse(value?.ToString(), out var parsed) ? parsed : null;
    }

    public static DateOnly? ParseNullableDate(object? value)
    {
        return DateOnly.TryParse(value?.ToString(), out var parsed) ? parsed : null;
    }
}
