using System.Collections;
using System.Globalization;
using System.Reflection;

namespace OFM.Infrastructure.WebAPI.Extensions;
public static class JsonSizeCalculator
{
    public static long Estimate(object obj, bool includeNulls)
    {
        if (obj is null) return 4;
        if (obj is Byte || obj is SByte) return 1;
        if (obj is Char) return 3;
        if (obj is Boolean b) return b ? 4 : 5;
        if (obj is Guid) return 38;
        if (obj is DateTime || obj is DateTimeOffset) return 35;
        if (obj is Int16 i16) return i16.ToString(CultureInfo.InvariantCulture).Length;
        if (obj is Int32 i32) return i32.ToString(CultureInfo.InvariantCulture).Length;
        if (obj is Int64 i64) return i64.ToString(CultureInfo.InvariantCulture).Length;
        if (obj is UInt16 u16) return u16.ToString(CultureInfo.InvariantCulture).Length;
        if (obj is UInt32 u32) return u32.ToString(CultureInfo.InvariantCulture).Length;
        if (obj is UInt64 u64) return u64.ToString(CultureInfo.InvariantCulture).Length;
        if (obj is String s) return s.Length + 2;
        if (obj is Decimal dec)
        {
            var left = (long)Math.Floor(dec % 10);
            var right = BitConverter.GetBytes(decimal.GetBits(dec)[3])[2];
            return right == 0 ? left : left + right + 1;
        }
        if (obj is Double dou) return dou.ToString(CultureInfo.InvariantCulture).Length;
        if (obj is Single sin) return sin.ToString(CultureInfo.InvariantCulture).Length;
        if (obj is IDictionary dict) return EstimateDictionary(dict, includeNulls);
        if (obj is IEnumerable enumerable) return EstimateEnumerable(enumerable, includeNulls);

        return EstimateObject(obj, includeNulls);
    }

    static long EstimateEnumerable(IEnumerable enumerable, bool includeNulls)
    {
        long size = 0;
        foreach (var item in enumerable)
            size += Estimate(item, includeNulls) + 1; // ,
        return size > 0 ? size + 1 : 2;
    }

    static readonly BindingFlags publicInstance = BindingFlags.Instance | BindingFlags.Public;

    static long EstimateDictionary(IDictionary dictionary, bool includeNulls)
    {
        long size = 2; // { }
        bool wasFirst = true;
        foreach (var key in dictionary.Keys)
        {
            var value = dictionary[key];
            if (includeNulls || value != null)
            {
                if (!wasFirst)
                    size++;
                else
                    wasFirst = false;
                size += Estimate(key, includeNulls) + 1 + Estimate(value, includeNulls); // :,
            }
        }
        return size;
    }

    static long EstimateObject(object obj, bool includeNulls)
    {
        long size = 2;
        bool wasFirst = true;
        var type = obj.GetType();
        var properties = type.GetProperties(publicInstance);
        foreach (var property in properties)
        {
            if (property.CanRead && property.CanWrite)
            {
                var value = property.GetValue(obj);
                if (includeNulls || value != null)
                {
                    if (!wasFirst)
                        size++;
                    else
                        wasFirst = false;
                    size += property.Name.Length + 3 + Estimate(value, includeNulls);
                }
            }
        }

        var fields = type.GetFields(publicInstance);
        foreach (var field in fields)
        {
            var value = field.GetValue(obj);
            if (includeNulls || value != null)
            {
                if (!wasFirst)
                    size++;
                else
                    wasFirst = false;
                size += field.Name.Length + 3 + Estimate(value, includeNulls);
            }
        }
        return size;
    }
}