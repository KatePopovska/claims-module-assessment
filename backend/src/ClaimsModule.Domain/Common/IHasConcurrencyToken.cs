namespace ClaimsModule.Domain.Common;

public interface IHasConcurrencyToken
{
    byte[] RowVer { get; set; }
}
