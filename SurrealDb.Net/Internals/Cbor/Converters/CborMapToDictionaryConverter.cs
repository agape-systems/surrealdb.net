using System.Runtime.CompilerServices;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters;

namespace SurrealDb.Net.Internals.Cbor.Converters;

/// <summary>
/// Reads an arbitrary CBOR map into Dictionary[string, object?], or null if the value is null.
/// Nested maps become Dictionary, arrays become List[object?], primitives stay as-is.
/// Used for RPC error details so any server shape is accepted without throwing.
/// </summary>
public sealed class CborMapToDictionaryConverter : CborConverterBase<Dictionary<string, object?>?>
{
    public override Dictionary<string, object?>? Read(ref CborReader reader)
    {
        return ReadNullableMap(ref reader);
    }

    public override void Write(ref CborWriter writer, Dictionary<string, object?>? value)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteBeginMap(value.Count);
        foreach (var kvp in value)
        {
            writer.WriteString(kvp.Key);
            WriteCborValue(ref writer, kvp.Value);
        }
    }

    private static void WriteCborValue(ref CborWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNull();
                break;
            case bool b:
                writer.WriteBoolean(b);
                break;
            case string s:
                writer.WriteString(s);
                break;
            case int i:
                writer.WriteInt32(i);
                break;
            case long l:
                writer.WriteInt64(l);
                break;
            case float f:
                writer.WriteSingle(f);
                break;
            case double d:
                writer.WriteDouble(d);
                break;
            case Dictionary<string, object?> map:
                writer.WriteBeginMap(map.Count);
                foreach (var kvp in map)
                {
                    writer.WriteString(kvp.Key);
                    WriteCborValue(ref writer, kvp.Value);
                }
                break;
            case List<object?> list:
                writer.WriteBeginArray(list.Count);
                foreach (var item in list)
                {
                    WriteCborValue(ref writer, item);
                }
                break;
            default:
                writer.WriteString(value.ToString());
                break;
        }
    }

    internal static Dictionary<string, object?>? ReadNullableMap(ref CborReader reader)
    {
        if (reader.GetCurrentDataItemType() == CborDataItemType.Null)
        {
            reader.ReadNull();
            return null;
        }

        if (reader.GetCurrentDataItemType() != CborDataItemType.Map)
        {
            reader.SkipDataItem();
            return null;
        }

        reader.ReadBeginMap();

        int remainingItemCount = reader.ReadSize();
        var dict = new Dictionary<string, object?>(remainingItemCount);

        while (reader.MoveNextMapItem(ref remainingItemCount))
        {
            string? key = reader.ReadString();
            if (key is not null)
            {
                object? value = ReadCborValueIntoObject(ref reader);
                dict[key] = value;
            }
            else
            {
                reader.SkipDataItem();
            }
        }

        return dict;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static object? ReadCborValueIntoObject(ref CborReader reader)
    {
        var itemType = reader.GetCurrentDataItemType();

        return itemType switch
        {
            CborDataItemType.Null => ReadNullValue(ref reader),
            CborDataItemType.Boolean => reader.ReadBoolean(),
            CborDataItemType.String => reader.ReadString(),
            CborDataItemType.Signed => reader.ReadInt64(),
            CborDataItemType.Unsigned => reader.ReadUInt64(),
            CborDataItemType.Single => reader.ReadSingle(),
            CborDataItemType.Double => reader.ReadDouble(),
            CborDataItemType.Map => ReadMapIntoDictionary(ref reader),
            CborDataItemType.Array => ReadArrayIntoList(ref reader),
            _ => SkipAndReturnNull(ref reader),
        };
    }

    private static Dictionary<string, object?> ReadMapIntoDictionary(ref CborReader reader)
    {
        reader.ReadBeginMap();

        int remainingItemCount = reader.ReadSize();
        var dict = new Dictionary<string, object?>(remainingItemCount);

        while (reader.MoveNextMapItem(ref remainingItemCount))
        {
            string? key = reader.ReadString();
            if (key is not null)
            {
                object? value = ReadCborValueIntoObject(ref reader);
                dict[key] = value;
            }
            else
            {
                reader.SkipDataItem();
            }
        }

        return dict;
    }

    private static List<object?> ReadArrayIntoList(ref CborReader reader)
    {
        reader.ReadBeginArray();

        int size = reader.ReadSize();
        var list = new List<object?>(size);

        for (int i = 0; i < size; i++)
        {
            list.Add(ReadCborValueIntoObject(ref reader));
        }

        return list;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static object? ReadNullValue(ref CborReader reader)
    {
        reader.ReadNull();
        return null;
    }

    private static object? SkipAndReturnNull(ref CborReader reader)
    {
        reader.SkipDataItem();
        return null;
    }
}
