namespace PhieuFlow.Ui;

/// <summary>A selectable option for <see cref="RadioGroup"/>, <see cref="CheckboxGroup"/>, and
/// <see cref="SelectInput"/>. Model-agnostic, so either app's option type can map to it.</summary>
public sealed record Choice(Guid Id, string Label);
