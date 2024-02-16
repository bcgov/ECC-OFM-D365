namespace OFM.Infrastructure.WebAPI.Extensions;

public class DateExtensions
{
    static bool IsFirstFridayOfOctober(DateTime date) =>
    date is { Month: 10, Day: <= 7, DayOfWeek: DayOfWeek.Friday };
}
