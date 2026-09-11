namespace FrenchExDev.Net.Vos.VosFile;

public sealed class VosFileValidator : IVosFileValidator
{
    public Res.Result<IReadOnlyList<string>> Validate(VosConfig config)
    {
        var errors = VosConfigValidator.Validate(config);
        return errors.Count == 0
            ? Res.Result<IReadOnlyList<string>>.Success(errors)
            : Res.Result<IReadOnlyList<string>>.Failure(
                new System.ComponentModel.DataAnnotations.ValidationResult(
                    $"Config has {errors.Count} error(s)"));
    }
}
