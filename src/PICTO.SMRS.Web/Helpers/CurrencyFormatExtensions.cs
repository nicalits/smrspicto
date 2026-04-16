namespace PICTO.SMRS.Web.Helpers;

public static class CurrencyFormatExtensions
{
    public static string ToPeso(this decimal value) => $"₱{value:N2}";
}
