﻿using System.Runtime.InteropServices;

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
    public static DateTime GetCFSInvoiceDate(this DateTime invoiceReceivedDate, List<DateTime> holidays)
    {
        int businessDaysToSubtract = 5;
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
    /// <param name="invoiceDate"></param>
    /// <param name="holidays"></param>
    /// <param name="defaultDaysAfter"></param>
    /// <param name="trailingTotalDays"></param>
    /// <returns></returns>
    public static DateTime GetCFSEffectiveDate(this DateTime invoiceDate, List<DateTime> holidays, int defaultDaysAfter = 2, int trailingTotalDays = 3)
    {
        var potentialDates = Enumerable.Range(defaultDaysAfter, defaultDaysAfter + trailingTotalDays).Select(day => IsBusinessDay(day, invoiceDate, holidays));
        potentialDates = potentialDates.Where(d => d != DateTime.MinValue).ToList();
        return potentialDates
                .Distinct()
                .OrderBy(d => d.Date)
                .First();
    }

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

    private static DateTime IsBusinessDay(int days, DateTime checkingDate, List<DateTime> holidays)
    {
        var dateToCheck = checkingDate.AddDays(days);
        var isNonBusinessDay =
            dateToCheck.DayOfWeek == DayOfWeek.Saturday ||
            dateToCheck.DayOfWeek == DayOfWeek.Sunday ||
            holidays.Exists(excludedDate => excludedDate.Date.Equals(dateToCheck.Date));

        return !isNonBusinessDay ? dateToCheck : DateTime.MinValue;
    }

    /// <summary>
    /// Adding 3 days from current date as revised invoice date.
    /// </summary>
    /// <param name="currentDate"></param>
    /// <param name="daysToAdd"></param>
    /// <param name="holidays"></param>
    /// <returns></returns>
    public static DateTime GetRevisedInvoiceDate(this DateTime currentDate, int daysToAdd, List<DateTime> holidays)
    {
        DateTime futureDate = currentDate.AddDays(daysToAdd);
        while (futureDate.DayOfWeek == DayOfWeek.Saturday || futureDate.DayOfWeek == DayOfWeek.Sunday || holidays.Exists(excludedDate => excludedDate.Date.Equals(futureDate.Date)))
        {
            futureDate = futureDate.AddDays(1);
        }
        return futureDate;
    }

}