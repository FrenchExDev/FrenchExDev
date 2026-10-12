namespace FrenchExDev.Net.Vos.VosFile;

public interface IVosFileValidator
{
    Res.Result<IReadOnlyList<string>> Validate(VosConfig config);
}
