using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mirepoix.AccessControl.Internal;

namespace Mirepoix.AccessControl.Policy;

public static class PolicySerializers
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static string ToJson(PolicySet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        return JsonSerializer.Serialize(ToDto(set), JsonOptions);
    }

    public static PolicySet FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var dto = JsonSerializer.Deserialize<PolicySetDto>(json, JsonOptions)
            ?? throw new JsonException("PolicySet JSON deserialized to null.");
        return FromDto(dto);
    }

    // v1 binary is a length-prefixed UTF8 JSON envelope, not a distinct schema.
    public static byte[] ToBinary(PolicySet set)
    {
        var utf8 = Encoding.UTF8.GetBytes(ToJson(set));
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(utf8.Length);
            writer.Write(utf8);
        }

        return stream.ToArray();
    }

    public static PolicySet FromBinary(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        const int headerSize = sizeof(int);
        if (data.Length < headerSize)
            throw new FormatException("Binary PolicySet is truncated.");

        var length = BinaryPrimitives.ReadInt32LittleEndian(data);
        if (length < 0 || data.Length - headerSize < length)
            throw new FormatException("Binary PolicySet payload is truncated.");

        return FromJson(Encoding.UTF8.GetString(data, headerSize, length));
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static PolicySetDto ToDto(PolicySet set) =>
        new()
        {
            Version = set.Version,
            Policies = set.Policies.Select(ToDto).ToList(),
        };

    private static PolicyDto ToDto(Policy policy) =>
        new()
        {
            Id = policy.Id,
            Effect = policy.Effect,
            Description = policy.Description,
            Atoms = policy.Atoms.Select(ToDto).ToList(),
        };

    private static AtomDto ToDto(IAtom atom) =>
        atom switch
        {
            RoleMembershipAtom role => new RoleMembershipAtomDto { Roles = role.Roles.ToList() },
            AttributeValueAtom attribute => new AttributeValueAtomDto
            {
                Target = attribute.Target,
                Key = attribute.Key,
                Op = attribute.Op,
                Expected = ToJsonElement(attribute.Expected),
            },
            OperationMatchAtom operation => new OperationMatchAtomDto { Pattern = operation.Pattern.Value },
            SubjectIdEqualsAttributeAtom subjectId => new SubjectIdEqualsAttributeAtomDto
            {
                Target = subjectId.Target,
                Key = subjectId.Key,
            },
            AttributeEqualsAttributeAtom attributeEquals => new AttributeEqualsAttributeAtomDto
            {
                LeftTarget = attributeEquals.LeftTarget,
                LeftKey = attributeEquals.LeftKey,
                RightTarget = attributeEquals.RightTarget,
                RightKey = attributeEquals.RightKey,
                Op = attributeEquals.Op,
                Strict = attributeEquals.Strict,
            },
            _ => throw new NotSupportedException($"Unknown atom type '{atom.Name}'."),
        };

    private static PolicySet FromDto(PolicySetDto dto) =>
        new(dto.Version, dto.Policies.Select(FromDto).ToList());

    private static Policy FromDto(PolicyDto dto) =>
        new(dto.Id, dto.Effect, dto.Description, dto.Atoms.Select(FromDto).ToList());

    private static IAtom FromDto(AtomDto dto) =>
        dto switch
        {
            RoleMembershipAtomDto role => new RoleMembershipAtom(role.Roles),
            AttributeValueAtomDto attribute => new AttributeValueAtom(
                attribute.Target,
                attribute.Key,
                attribute.Op,
                FromJsonElement(attribute.Expected)),
            OperationMatchAtomDto operation => new OperationMatchAtom(Operation.Parse(operation.Pattern)),
            SubjectIdEqualsAttributeAtomDto subjectId => new SubjectIdEqualsAttributeAtom(subjectId.Target, subjectId.Key),
            AttributeEqualsAttributeAtomDto attributeEquals => new AttributeEqualsAttributeAtom(
                attributeEquals.LeftTarget,
                attributeEquals.LeftKey,
                attributeEquals.RightTarget,
                attributeEquals.RightKey,
                attributeEquals.Op,
                attributeEquals.Strict),
            _ => throw new JsonException($"Unknown atom payload '{dto.GetType().Name}'."),
        };

    private static JsonElement? ToJsonElement(object? value) =>
        value is null ? null : JsonSerializer.SerializeToElement(value, JsonOptions);

    private static object? FromJsonElement(JsonElement? element)
    {
        if (element is null)
            return null;

        return FromJsonElement(element.Value);
    }

    private static object? FromJsonElement(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => element.TryGetInt32(out var i) ? i
                : element.TryGetInt64(out var l) ? l
                : element.GetDouble(),
            JsonValueKind.Array => element.EnumerateArray().Select(FromJsonElement).ToList(),
            _ => element,
        };
}
