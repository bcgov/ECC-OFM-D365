using ECC.Core.DataContext;
using OFM.Infrastructure.WebAPI.Models;
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

public static class TimeExtensions
{

    public static string GetIanaTimeZoneId(TimeZoneInfo tzi)
    {
        if (tzi.HasIanaId)
            return tzi.Id;  // no conversion necessary

        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(tzi.Id, out string ianaId))
            return ianaId;  // use the converted ID

        throw new TimeZoneNotFoundException($"No IANA time zone found for {tzi.Id}.");
    }

    public static DateTime GetCurrentPSTDateTime()
    {
        _ = TimeZoneInfo.Local;
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        TimeZoneInfo timeZone;
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
    /// Convert a UTC datetime to local PST date & time
    /// </summary>
    /// <param name="utcDate"></param>
    /// <returns></returns>
    public static DateTime ToLocalPST(this DateTime utcDate)
    {
        _ = TimeZoneInfo.Local;
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        TimeZoneInfo timeZone;
        if (isWindows)
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        }
        else
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Vancouver");
        }

        DateTime pacificTime = TimeZoneInfo.ConvertTimeFromUtc(utcDate, timeZone);

        return pacificTime;
    }

    /// <summary>
    /// A pre-determined Invoice Date when OFM system sends the payment request over to CFS.
    /// </summary>
    public static DateTime GetCFSInvoiceDate(this DateTime invoiceReceivedDate, List<DateTime> holidays,int businessDaysToSubtract = 5)
    {
        int businessDaysCount = 0;
        DateTime invoiceDate = invoiceReceivedDate;

        while (businessDaysCount < businessDaysToSubtract)
        {
            invoiceDate = invoiceDate.AddDays(-1);

            if (invoiceDate.DayOfWeek != DayOfWeek.Saturday && invoiceDate.DayOfWeek != DayOfWeek.Sunday && !holidays.Exists(holiday => holiday.Date == invoiceDate.Date))
            {
                businessDaysCount++;
            }
        }

        return invoiceDate;
    }

    /// <summary>
    /// A pre-determined CFS Effective Date. The recommended default is 2 days after the Invoice Date.
    /// </summary>
    public static DateTime GetCFSEffectiveDate(this DateTime invoiceDate, List<DateTime> holidays, int defaultDaysAfter = 2, int trailingTotalDays = 3)
    {
        var potentialDates = Enumerable.Range(defaultDaysAfter, defaultDaysAfter + trailingTotalDays).Select(day => IsBusinessDay(day, invoiceDate, holidays));
        potentialDates = potentialDates.Where(d => d != DateTime.MinValue).ToList();
        return potentialDates
                .Distinct()
                .OrderBy(d => d.Date)
                .First();
    }

    /// <summary>
    ///  A pre-determined CFS Invoice Received Date. Last business day of previous month so for following month it is paid in advance.
    ///  For first month payment, it is always same as start date of funding.
    /// </summary>
    public static DateTime GetLastBusinessDayOfThePreviousMonth(this DateTime targetDate, List<DateTime> holidays)
    {
        DateTime firstDayOfMonth = new(targetDate.Year, targetDate.Month, 1);
        DateTime lastDayOfPreviousMonth = firstDayOfMonth.AddDays(-1);

        // Iterate backward to find the last business day
        while (lastDayOfPreviousMonth.DayOfWeek == DayOfWeek.Saturday ||
                lastDayOfPreviousMonth.DayOfWeek == DayOfWeek.Sunday ||
                holidays.Exists(excludedDate => excludedDate.Date.Equals(lastDayOfPreviousMonth.Date)))
        {
            lastDayOfPreviousMonth = lastDayOfPreviousMonth.AddDays(-1);
        }

        return lastDayOfPreviousMonth;
    }
    public static DateTime GetPreviousBusinessDay(this DateTime targetDate, List<DateTime> holidays)
    {
        
        DateTime previousBusinessDay = targetDate.AddDays(-1);

        // find previous business day
        while (previousBusinessDay.DayOfWeek == DayOfWeek.Saturday ||
                previousBusinessDay.DayOfWeek == DayOfWeek.Sunday ||
                holidays.Exists(excludedDate => excludedDate.Date.Equals(previousBusinessDay.Date)))
        {
            previousBusinessDay = previousBusinessDay.AddDays(-1);
        }

        return previousBusinessDay;
    }

    private static DateTime IsBusinessDay(int days, DateTime targetDate, List<DateTime> holidays)
    {
        var dateToCheck = targetDate.AddDays(days);
        var isNonBusinessDay =
            dateToCheck.DayOfWeek == DayOfWeek.Saturday ||
            dateToCheck.DayOfWeek == DayOfWeek.Sunday ||
            holidays.Exists(excludedDate => excludedDate.Date.Equals(dateToCheck.Date));

        return !isNonBusinessDay ? dateToCheck : DateTime.MinValue;
    }

    /// <summary>
    /// Adding x business days from the target date.
    /// </summary>
    /// <param name="targetDate"></param>
    /// <param name="businessDaysToAdd"></param>
    /// <param name="holidays"></param>
    /// <returns></returns>
    public static DateTime AddBusinessDays(this DateTime targetDate, int businessDaysToAdd, List<DateTime> holidays)
    {
        DateTime futureDate = targetDate;
        int addedDays = 0;

        while (addedDays < businessDaysToAdd)
        {
            futureDate = futureDate.AddDays(1);
            if (futureDate.DayOfWeek != DayOfWeek.Saturday &&
                futureDate.DayOfWeek != DayOfWeek.Sunday &&
                !holidays.Exists(excludedDate => excludedDate.Date.Equals(futureDate.Date)))
            {
                addedDays++;
            }
        }

        return futureDate;
    }

    public static Guid MatchFiscalYear(this DateTime paymentDate, List<ofm_fiscal_year> fiscalYears)
    {
        ofm_fiscal_year? matchingFiscalYear = fiscalYears.FirstOrDefault(fiscalYear => paymentDate >= fiscalYear.ofm_start_date && paymentDate <= fiscalYear.ofm_end_date);

        if (matchingFiscalYear == null)
        {
            return Guid.Empty;
        }

        return matchingFiscalYear!.ofm_fiscal_yearid!.Value;
    }
    public static Guid GetCurrentFiscalYear(this DateTime currentDate, List<D365FiscalYear> fiscalYears)
    {
        ofm_fiscal_year? matchingFiscalYear = fiscalYears.FirstOrDefault(fiscalYear => currentDate >= ((DateTime)fiscalYear.ofm_start_date).ToLocalPST() && currentDate <= ((DateTime)fiscalYear.ofm_end_date).ToLocalPST());

        if (matchingFiscalYear == null)
        {

            return Guid.Empty;
        }

        return matchingFiscalYear!.ofm_fiscal_yearid!.Value;
    }
}