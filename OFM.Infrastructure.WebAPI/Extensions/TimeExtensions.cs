using System.Runtime.InteropServices;

namespace OFM.Infrastructure.WebAPI.Extensions;

public interface IRange<T> where T : IComparable<T>
{
    T Start { get; }
    T End { get; }
    bool WithinRange(T value);
    bool WithinRange(IRange<T> range);

    //public static T Max<T>(T a, T b) where T : IComparable<T>
    //{
    //    return (a.CompareTo(b) > 0) ? a : b;
    //}
}

public class Range<T> : IRange<T> where T : IComparable<T>
{
    public T Start { get; }
    public T End { get; }

    public Range(T start, T end)
    {
        Start = start;
        End = end;
    }

    /// <summary>
    /// The Start object is earlier than the value, it returns a negative value
    /// The End object is later than the second, it returns a positive value
    /// The two objects are equal, it returns zero
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public bool WithinRange(T value)
    {
        var possitive = value.CompareTo(Start);
        var negative = value.CompareTo(End);

        return (possitive >= 0 && negative <= 0);
    }

    public bool WithinRange(IRange<T> range)
    {
        throw new NotImplementedException();
    }
}

public static class TimeExtensions {

    public static string GetIanaTimeZoneId(TimeZoneInfo tzi)
    {
        if (tzi.HasIanaId)
            return tzi.Id;  // no conversion necessary

        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(tzi.Id, out string ianaId))
            return ianaId;  // use the converted ID

        throw new TimeZoneNotFoundException($"No IANA time zone found for {tzi.Id}.");
    }

    public static DateTime GetCurrentPSTdateTime() {

    TimeZoneInfo timeZone = TimeZoneInfo.Local;
        bool isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        if (isWindows)
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        }
        else
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Vancouver");
        }

        DateTime pacificTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);

        return pacificTime;
    }

    /// <summary>
    /// A pre-determined Invoice Date when OFM system sends the payment request over to CFS.
    /// </summary>
    /// <param name="currentDate"></param>
    /// <param name="holidays"></param>
    /// <param name="trailingDays"></param>
    /// <returns></returns>
    public static DateTime GetCFSInvoiceDate(this DateTime currentDate, List<DateTime> holidays, int trailingDays = 3)
    {
        Func<int, DateTime> isBusinessDay = days =>
        {
            var dateToCheck = currentDate.AddDays(days);
            var isNonWorkingDay =
                dateToCheck.DayOfWeek == DayOfWeek.Saturday ||
                dateToCheck.DayOfWeek == DayOfWeek.Sunday ||
                holidays.Exists(excludedDate => excludedDate.Date.Equals(dateToCheck.Date));

            return !isNonWorkingDay ? dateToCheck : DateTime.MinValue;
        };

        var potentialDates = Enumerable.Range(0, trailingDays).Select(isBusinessDay);

        return potentialDates
                .Where(d => !d.Date.Equals(DateTime.MinValue.Date))
                .OrderBy(d => d.Date)
                .First();
    }

    /// <summary>
    /// A pre-determined CFS Effective Date. The recommended default is 2 days after the Invoice Date.
    /// </summary>
    /// <param name="invoiceDate"></param>
    /// <param name="holidays"></param>
    /// <param name="defaultDaysAfter"></param>
    /// <param name="trailingDays"></param>
    /// <returns></returns>
    public static DateTime GetCFSEffectiveDate(this DateTime invoiceDate, List<DateTime> holidays, int defaultDaysAfter = 2, int trailingDays = 3)
    {
        Func<int, DateTime> isBusinessDay = days =>
        {
            var dateToCheck = invoiceDate.AddDays(days);
            var isNonWorkingDay =
                dateToCheck.DayOfWeek == DayOfWeek.Saturday ||
                dateToCheck.DayOfWeek == DayOfWeek.Sunday ||
                holidays.Exists(excludedDate => excludedDate.Date.Equals(dateToCheck.Date));

            return !isNonWorkingDay ? dateToCheck : DateTime.MinValue;
        };

        var potentialDates = Enumerable.Range(defaultDaysAfter, defaultDaysAfter + trailingDays).Select(isBusinessDay);

        return potentialDates
                .Where(d => !d.Date.Equals(DateTime.MinValue.Date))
                .OrderBy(d => d.Date)
                .First();
    }

    /// <summary>
    ///  A pre-determined CFS Invoice Received Date. The recommended default is 4 days before the Invoice Date.
    /// </summary>
    /// <param name="invoiceDate"></param>
    /// <param name="holidays"></param>
    /// <param name="defaultDaysBefore"></param>
    /// <param name="trailingDays"></param>
    /// <returns></returns>
    public static DateTime GetCFSInvoiceReceivedDate(this DateTime invoiceDate, List<DateTime> holidays, int defaultDaysBefore = -4, int trailingDays = -3)
    {
        Func<int, DateTime> isBusinessDay = days =>
        {
            var dateToCheck = invoiceDate.AddDays(days);
            var isNonWorkingDay =
                dateToCheck.DayOfWeek == DayOfWeek.Saturday ||
                dateToCheck.DayOfWeek == DayOfWeek.Sunday ||
                holidays.Exists(excludedDate => excludedDate.Date.Equals(dateToCheck.Date));

            return !isNonWorkingDay ? dateToCheck : DateTime.MinValue;
        };

        var potentialDates = Enumerable.Range(defaultDaysBefore, defaultDaysBefore + trailingDays).Select(isBusinessDay);

        return potentialDates
                .Where(d => !d.Date.Equals(DateTime.MinValue.Date))
                .OrderBy(d => d.Date)
                .First();
    }
}