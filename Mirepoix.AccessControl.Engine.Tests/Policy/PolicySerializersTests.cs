using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Policy;

public class PolicySerializersTests
{
    [Fact]
    public void Json_round_trip_is_lossless()
    {
        var set = SampleSet();

        var json = PolicySerializers.ToJson(set);
        var roundTripped = PolicySerializers.FromJson(json);

        Assert.Contains("\"type\":\"role-membership\"", json);
        Assert.Contains("\"type\":\"attribute-value\"", json);
        Assert.Contains("\"type\":\"operation-match\"", json);
        Assert.Contains("\"type\":\"subject-id-equals-attribute\"", json);
        Assert.Contains("\"type\":\"attribute-equals-attribute\"", json);
        AssertRoundTrip(set, roundTripped);
    }

    [Fact]
    public void Binary_round_trip_is_lossless()
    {
        var set = SampleSet();

        var bytes = PolicySerializers.ToBinary(set);
        var roundTripped = PolicySerializers.FromBinary(bytes);

        AssertRoundTrip(set, roundTripped);
    }

    [Fact]
    public void FromBinary_throws_FormatException_when_buffer_shorter_than_length_prefix()
    {
        Assert.Throws<FormatException>(() => PolicySerializers.FromBinary(new byte[] { 1, 2 }));
    }

    [Fact]
    public void FromBinary_throws_FormatException_when_payload_truncated()
    {
        var data = BitConverter.GetBytes(50);
        Assert.Throws<FormatException>(() => PolicySerializers.FromBinary(data));
    }

    [Fact]
    public void FromBinary_throws_FormatException_when_length_negative()
    {
        var data = BitConverter.GetBytes(-1);
        Assert.Throws<FormatException>(() => PolicySerializers.FromBinary(data));
    }

    [Fact]
    public void Serializers_round_trip_entityType()
    {
        var set = new PolicySet("v1", [
            new Policy("p1", AuthorizationResult.Allow, null, [
                new AttributeValueAtom(AttributeTarget.Subject, "subject", "dept", ComparisonOperator.Equals, "finance")
            ])
        ]);
        var json = PolicySerializers.ToJson(set);
        Assert.Contains("\"entityType\":\"subject\"", json);
        Assert.Contains("\"type\":\"attribute-value\"", json);
        var round = PolicySerializers.FromJson(json);
        var atom = Assert.IsType<AttributeValueAtom>(round.Policies[0].Atoms[0]);
        Assert.Equal("subject", atom.Type);
    }

    [Fact]
    public void FromJson_subject_atom_without_entityType_throws()
    {
        var json = """
            {"version":"v1","policies":[{"id":"p1","effect":"Allow","atoms":[{"type":"attribute-value","target":"Subject","key":"dept","op":"Equals","expected":"x"}]}]}
            """;
        Assert.ThrowsAny<Exception>(() => PolicySerializers.FromJson(json));
    }

    [Fact]
    public void Serializers_round_trip_left_and_right_entityType()
    {
        var set = new PolicySet("v1", [
            new Policy("p1", AuthorizationResult.Allow, null, [
                new AttributeEqualsAttributeAtom(
                    AttributeTarget.Subject, "subject", "region",
                    AttributeTarget.Resource, "doc", "region",
                    ComparisonOperator.Equals),
                new SubjectIdEqualsAttributeAtom(AttributeTarget.Resource, "doc", "ownerId"),
            ])
        ]);
        var json = PolicySerializers.ToJson(set);
        Assert.Contains("\"leftEntityType\":\"subject\"", json);
        Assert.Contains("\"rightEntityType\":\"doc\"", json);
        Assert.Contains("\"type\":\"subject-id-equals-attribute\"", json);
        var round = PolicySerializers.FromJson(json);
        var equals = Assert.IsType<AttributeEqualsAttributeAtom>(round.Policies[0].Atoms[0]);
        Assert.Equal("subject", equals.LeftType);
        Assert.Equal("doc", equals.RightType);
        var owner = Assert.IsType<SubjectIdEqualsAttributeAtom>(round.Policies[0].Atoms[1]);
        Assert.Equal("doc", owner.Type);
    }

    [Fact]
    public void Serializers_round_trip_context_subject_id_null_entityType()
    {
        var set = new PolicySet("v1", [
            new Policy("p1", AuthorizationResult.Allow, null, [
                new SubjectIdEqualsAttributeAtom(AttributeTarget.Context, null, "actorId")
            ])
        ]);
        var json = PolicySerializers.ToJson(set);
        Assert.Contains("\"entityType\":null", json);
        Assert.Contains("\"type\":\"subject-id-equals-attribute\"", json);
        var round = PolicySerializers.FromJson(json);
        var atom = Assert.IsType<SubjectIdEqualsAttributeAtom>(round.Policies[0].Atoms[0]);
        Assert.Equal(AttributeTarget.Context, atom.Target);
        Assert.Null(atom.Type);
        Assert.Equal("actorId", atom.Key);
    }

    [Fact]
    public void Json_round_trip_preserves_time_and_numeric_evaluation()
    {
        var cutoff = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
        var set = new PolicySet("v1", new[]
        {
            new Policy("time-gate", AuthorizationResult.Allow, null, new IAtom[]
            {
                new AttributeValueAtom(AttributeTarget.Context, null, "time", ComparisonOperator.LessThanOrEqual, cutoff)
            }),
            new Policy("level", AuthorizationResult.Allow, null, new IAtom[]
            {
                new AttributeValueAtom(AttributeTarget.Subject, "subject", "level", ComparisonOperator.Equals, 5)
            }),
        });

        var roundTripped = PolicySerializers.FromJson(PolicySerializers.ToJson(set));
        var timeAtom = Assert.IsType<AttributeValueAtom>(roundTripped.Policies[0].Atoms[0]);
        var levelAtom = Assert.IsType<AttributeValueAtom>(roundTripped.Policies[1].Atoms[0]);

        Assert.True(timeAtom.IsSatisfied(TimeBundle(cutoff)));
        Assert.True(timeAtom.IsSatisfied(TimeBundle(cutoff.AddMinutes(-1))));
        Assert.False(timeAtom.IsSatisfied(TimeBundle(cutoff.AddMinutes(1))));

        Assert.True(levelAtom.IsSatisfied(LevelBundle(5)));
        Assert.True(levelAtom.IsSatisfied(LevelBundle(5L)));
        Assert.False(levelAtom.IsSatisfied(LevelBundle(6L)));
    }

    private static PolicySet SampleSet() =>
        new("v1", new[]
        {
            new Policy("p-allow", AuthorizationResult.Allow, "editors", new IAtom[]
            {
                new RoleMembershipAtom(new[] { "EDITOR" }),
                new AttributeValueAtom(AttributeTarget.Subject, "subject", "dept", ComparisonOperator.Equals, "finance"),
                new OperationMatchAtom(Operation.Parse("doc:edit")),
                new SubjectIdEqualsAttributeAtom(AttributeTarget.Resource, "doc", "ownerId"),
                new AttributeEqualsAttributeAtom(
                    AttributeTarget.Subject, "subject", "region",
                    AttributeTarget.Resource, "doc", "region",
                    ComparisonOperator.Equals),
            }),
            new Policy("p-deny", AuthorizationResult.Deny, null, new IAtom[]
            {
                new RoleMembershipAtom(new[] { "GUEST" }),
            }),
        });

    private static void AssertRoundTrip(PolicySet original, PolicySet roundTripped)
    {
        Assert.Equal(original.Version, roundTripped.Version);
        Assert.Equal(original.Policies.Count, roundTripped.Policies.Count);

        Assert.Equal("p-allow", roundTripped.Policies[0].Id);
        Assert.Equal(AuthorizationResult.Allow, roundTripped.Policies[0].Effect);
        Assert.Equal("editors", roundTripped.Policies[0].Description);
        Assert.Equal(
            new[] { "role-membership", "attribute-value", "operation-match", "subject-id-equals-attribute", "attribute-equals-attribute" },
            roundTripped.Policies[0].Atoms.Select(a => a.Name));

        var allowRole = Assert.IsType<RoleMembershipAtom>(roundTripped.Policies[0].Atoms[0]);
        Assert.Equal(new[] { "EDITOR" }, allowRole.Roles);

        var attribute = Assert.IsType<AttributeValueAtom>(roundTripped.Policies[0].Atoms[1]);
        Assert.Equal(AttributeTarget.Subject, attribute.Target);
        Assert.Equal("subject", attribute.Type);
        Assert.Equal("dept", attribute.Key);
        Assert.Equal(ComparisonOperator.Equals, attribute.Op);
        Assert.Equal("finance", attribute.Expected);

        var operation = Assert.IsType<OperationMatchAtom>(roundTripped.Policies[0].Atoms[2]);
        Assert.Equal("doc:edit", operation.Pattern.Value);

        var owner = Assert.IsType<SubjectIdEqualsAttributeAtom>(roundTripped.Policies[0].Atoms[3]);
        Assert.Equal(AttributeTarget.Resource, owner.Target);
        Assert.Equal("doc", owner.Type);
        Assert.Equal("ownerId", owner.Key);

        var regionMatch = Assert.IsType<AttributeEqualsAttributeAtom>(roundTripped.Policies[0].Atoms[4]);
        Assert.Equal(AttributeTarget.Subject, regionMatch.LeftTarget);
        Assert.Equal("subject", regionMatch.LeftType);
        Assert.Equal("region", regionMatch.LeftKey);
        Assert.Equal(AttributeTarget.Resource, regionMatch.RightTarget);
        Assert.Equal("doc", regionMatch.RightType);
        Assert.Equal("region", regionMatch.RightKey);
        Assert.Equal(ComparisonOperator.Equals, regionMatch.Op);
        Assert.True(regionMatch.Strict);

        Assert.Equal("p-deny", roundTripped.Policies[1].Id);
        Assert.Equal(AuthorizationResult.Deny, roundTripped.Policies[1].Effect);
        Assert.Null(roundTripped.Policies[1].Description);
        Assert.Equal(
            new[] { "role-membership" },
            roundTripped.Policies[1].Atoms.Select(a => a.Name));

        var denyRole = Assert.IsType<RoleMembershipAtom>(roundTripped.Policies[1].Atoms[0]);
        Assert.Equal(new[] { "GUEST" }, denyRole.Roles);
    }

    private static AuthorizationBundle TimeBundle(DateTimeOffset time) =>
        new(
            new Subject("u1", new HashSet<string>(), new Dictionary<string, object?>()),
            new Resource("doc", ResourceKey.Single("1"), new Dictionary<string, object?>()),
            Operation.Parse("doc:edit"),
            new AccessContext(time, new Dictionary<string, object?>(), new Dictionary<string, object?>()));

    private static AuthorizationBundle LevelBundle(object level) =>
        new(
            new Subject("u1", new HashSet<string>(), new Dictionary<string, object?> { ["level"] = level }),
            new Resource("doc", ResourceKey.Single("1"), new Dictionary<string, object?>()),
            Operation.Parse("doc:edit"),
            new AccessContext(null, new Dictionary<string, object?>(), new Dictionary<string, object?>()));
}
