namespace Medinilla.Infrastructure;

public static class StringExtensions
{
    public static string TrimNewLinesAndWhiteSpaces(this string text)
        => text.Trim(Environment.NewLine.ToCharArray()).Trim();

    public static string ExtractValueInQuotationMarks(this string value)
    {
        var startQuotationIndex = value.IndexOf('"');
        var endQuotationIndex = value.LastIndexOf('"');

        if (startQuotationIndex == -1 && endQuotationIndex == -1)
        {
            return string.Empty;
        }

        var len = endQuotationIndex - startQuotationIndex;
        return value.Substring(startQuotationIndex, len);
    }
}
