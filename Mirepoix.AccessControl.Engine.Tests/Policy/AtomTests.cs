using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Policy;

public class AtomTests
{
    [Fact]
    public void RoleMembershipAtom_requires_any_listed_role()
    {
        var atom = new RoleMembershipAtom(new[] { "EDITOR", "ADMIN" });
        var bundle = new AuthorizationBundle(
            new Subject("u1", new HashSet<string> { "EDITOR" }, new Dictionary<string, object?>()),
            new Resource("doc", ResourceKey.Single("1"), new Dictionary<string, object?>()),
            Operation.Parse("doc:edit"),
            new AccessContext(null, new Dictionary<string, object?>(), new Dictionary<string, object?>()));
        Assert.True(atom.IsSatisfied(bundle));
    }

    [Fact]
    public void RoleMembershipAtom_unsatisfied_when_no_listed_role()
    {
        var atom = new RoleMembershipAtom(new[] { "ADMIN" });
        Assert.False(atom.IsSatisfied(Bundle(roles: new[] { "USER" })));
    }

    [Fact]
    public void RoleMembershipAtom_name_is_role_membership()
    {
        Assert.Equal("role-membership", new RoleMembershipAtom(new[] { "EDITOR" }).Name);
    }

    [Fact]
    public void OperationMatchAtom_satisfied_when_operation_matches_pattern()
    {
        var atom = new OperationMatchAtom(Operation.Parse("doc:*"));
        Assert.True(atom.IsSatisfied(Bundle(operation: "doc:edit")));
    }

    [Fact]
    public void OperationMatchAtom_unsatisfied_when_operation_does_not_match()
    {
        var atom = new OperationMatchAtom(Operation.Parse("invoice:edit"));
        Assert.False(atom.IsSatisfied(Bundle(operation: "doc:edit")));
    }

    [Fact]
    public void OperationMatchAtom_name_is_operation_match()
    {
        Assert.Equal("operation-match", new OperationMatchAtom(Operation.Parse("doc:edit")).Name);
    }

    [Fact]
    public void AttributeValueAtom_equals_when_subject_attribute_matches()
    {
        var atom = new AttributeValueAtom(AttributeTarget.Subject, "subject", "dept", ComparisonOperator.Equals, "finance");
        Assert.True(atom.IsSatisfied(Bundle(subjectAttrs: new Dictionary<string, object?> { ["dept"] = "finance" })));
    }

    [Fact]
    public void AttributeValueAtom_equals_unsatisfied_when_value_differs()
    {
        var atom = new AttributeValueAtom(AttributeTarget.Subject, "subject", "dept", ComparisonOperator.Equals, "finance");
        Assert.False(atom.IsSatisfied(Bundle(subjectAttrs: new Dictionary<string, object?> { ["dept"] = "legal" })));
    }

    [Fact]
    public void AttributeValueAtom_not_equals_when_value_differs()
    {
        var atom = new AttributeValueAtom(AttributeTarget.Resource, "doc", "status", ComparisonOperator.NotEquals, "published");
        Assert.True(atom.IsSatisfied(Bundle(resourceAttrs: new Dictionary<string, object?> { ["status"] = "draft" })));
    }

    [Theory]
    [InlineData(ComparisonOperator.GreaterThan, 10, 5, true)]
    [InlineData(ComparisonOperator.GreaterThan, 5, 10, false)]
    [InlineData(ComparisonOperator.GreaterThanOrEqual, 5, 5, true)]
    [InlineData(ComparisonOperator.GreaterThanOrEqual, 4, 5, false)]
    [InlineData(ComparisonOperator.LessThan, 4, 5, true)]
    [InlineData(ComparisonOperator.LessThan, 5, 4, false)]
    [InlineData(ComparisonOperator.LessThanOrEqual, 5, 5, true)]
    [InlineData(ComparisonOperator.LessThanOrEqual, 6, 5, false)]
    public void AttributeValueAtom_ordering_uses_IComparable(ComparisonOperator op, int actual, int expected, bool satisfied)
    {
        var atom = new AttributeValueAtom(AttributeTarget.Subject, "subject", "level", op, expected);
        Assert.Equal(satisfied, atom.IsSatisfied(Bundle(subjectAttrs: new Dictionary<string, object?> { ["level"] = actual })));
    }

    [Fact]
    public void AttributeValueAtom_in_when_value_is_in_expected_enumeration()
    {
        var atom = new AttributeValueAtom(AttributeTarget.Context, null, "region", ComparisonOperator.In, new[] { "us", "eu" });
        Assert.True(atom.IsSatisfied(Bundle(values: new Dictionary<string, object?> { ["region"] = "eu" })));
    }

    [Fact]
    public void AttributeValueAtom_in_unsatisfied_when_value_not_in_enumeration()
    {
        var atom = new AttributeValueAtom(AttributeTarget.Context, null, "region", ComparisonOperator.In, new[] { "us", "eu" });
        Assert.False(atom.IsSatisfied(Bundle(values: new Dictionary<string, object?> { ["region"] = "apac" })));
    }

    [Fact]
    public void AttributeValueAtom_reads_context_claims_when_not_in_values()
    {
        var atom = new AttributeValueAtom(AttributeTarget.Context, null, "tenant", ComparisonOperator.Equals, "acme");
        Assert.True(atom.IsSatisfied(Bundle(claims: new Dictionary<string, object?> { ["tenant"] = "acme" })));
    }

    [Fact]
    public void AttributeValueAtom_context_values_take_precedence_over_claims()
    {
        var bundle = Bundle(
            claims: new Dictionary<string, object?> { ["tenant"] = "from-claims" },
            values: new Dictionary<string, object?> { ["tenant"] = "from-values" });

        Assert.True(new AttributeValueAtom(AttributeTarget.Context, null, "tenant", ComparisonOperator.Equals, "from-values").IsSatisfied(bundle));
        Assert.False(new AttributeValueAtom(AttributeTarget.Context, null, "tenant", ComparisonOperator.Equals, "from-claims").IsSatisfied(bundle));
    }

    [Fact]
    public void AttributeValueAtom_reads_context_time_when_key_is_time()
    {
        var now = new DateTimeOffset(2026, 9, 10, 2, 0, 0, TimeSpan.Zero);
        var atom = new AttributeValueAtom(AttributeTarget.Context, null, "time", ComparisonOperator.Equals, now);
        Assert.True(atom.IsSatisfied(Bundle(time: now)));
    }

    [Fact]
    public void AttributeValueAtom_context_time_key_is_ordinal_ignore_case()
    {
        var now = new DateTimeOffset(2026, 9, 10, 2, 0, 0, TimeSpan.Zero);
        var atom = new AttributeValueAtom(AttributeTarget.Context, null, "TIME", ComparisonOperator.Equals, now);
        Assert.True(atom.IsSatisfied(Bundle(time: now)));
    }

    [Fact]
    public void AttributeValueAtom_context_time_property_takes_precedence_over_values()
    {
        var fromTime = new DateTimeOffset(2026, 9, 10, 2, 0, 0, TimeSpan.Zero);
        var fromValues = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var atom = new AttributeValueAtom(AttributeTarget.Context, null, "time", ComparisonOperator.Equals, fromTime);
        Assert.True(atom.IsSatisfied(Bundle(
            time: fromTime,
            values: new Dictionary<string, object?> { ["time"] = fromValues })));
    }

    [Fact]
    public void AttributeValueAtom_missing_attribute_is_not_satisfied()
    {
        var atom = new AttributeValueAtom(AttributeTarget.Subject, "subject", "dept", ComparisonOperator.Equals, "finance");
        Assert.False(atom.IsSatisfied(Bundle()));
    }

    [Fact]
    public void AttributeValueAtom_missing_attribute_satisfies_not_equals_only()
    {
        var notEquals = new AttributeValueAtom(AttributeTarget.Subject, "subject", "dept", ComparisonOperator.NotEquals, "finance");
        var greater = new AttributeValueAtom(AttributeTarget.Subject, "subject", "level", ComparisonOperator.GreaterThan, 0);
        var inn = new AttributeValueAtom(AttributeTarget.Subject, "subject", "dept", ComparisonOperator.In, new[] { "finance" });

        var bundle = Bundle();
        Assert.True(notEquals.IsSatisfied(bundle));
        Assert.False(greater.IsSatisfied(bundle));
        Assert.False(inn.IsSatisfied(bundle));
    }

    [Fact]
    public void AttributeValueAtom_name_is_attribute_value()
    {
        Assert.Equal(
            "attribute-value",
            new AttributeValueAtom(AttributeTarget.Subject, "subject", "k", ComparisonOperator.Equals, "v").Name);
    }

    [Fact]
    public void AttributeValueAtom_equals_int_expected_and_long_attribute()
    {
        var atom = new AttributeValueAtom(AttributeTarget.Subject, "subject", "level", ComparisonOperator.Equals, 5);
        Assert.True(atom.IsSatisfied(Bundle(subjectAttrs: new Dictionary<string, object?> { ["level"] = 5L })));
    }

    [Fact]
    public void AttributeValueAtom_greater_than_long_attribute_and_int_expected()
    {
        var atom = new AttributeValueAtom(AttributeTarget.Subject, "subject", "level", ComparisonOperator.GreaterThan, 5);
        Assert.True(atom.IsSatisfied(Bundle(subjectAttrs: new Dictionary<string, object?> { ["level"] = 6L })));
        Assert.False(atom.IsSatisfied(Bundle(subjectAttrs: new Dictionary<string, object?> { ["level"] = 5L })));
    }

    [Fact]
    public void AttributeValueAtom_orders_numeric_strings_as_numbers()
    {
        Assert.False(AttributeValueAtom.Compare("9", ComparisonOperator.GreaterThanOrEqual, 10));
        Assert.False(AttributeValueAtom.Compare("9", ComparisonOperator.GreaterThanOrEqual, "10"));
        Assert.True(AttributeValueAtom.Compare("10", ComparisonOperator.GreaterThan, "9"));
        Assert.False(AttributeValueAtom.Compare("100", ComparisonOperator.LessThanOrEqual, "50"));
    }

    [Fact]
    public void AttributeValueAtom_orders_non_numeric_strings_ordinally()
    {
        Assert.True(AttributeValueAtom.Compare("b", ComparisonOperator.GreaterThan, "a"));
        Assert.False(AttributeValueAtom.Compare("a", ComparisonOperator.GreaterThan, "9"));
    }

    [Fact]
    public void AttributeValueAtom_does_not_treat_distinct_date_strings_as_equal()
    {
        Assert.False(AttributeValueAtom.Compare(
            "2026-09-10T12:00:00+00:00",
            ComparisonOperator.Equals,
            "2026-09-10T20:00:00+08:00"));
    }

    [Fact]
    public void AttributeValueAtom_unspecified_datetime_uses_clock_time_as_utc()
    {
        var clock = new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Unspecified);
        var utc = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
        Assert.True(AttributeValueAtom.Compare(clock, ComparisonOperator.Equals, utc));
    }

    [Fact]
    public void AttributeValueAtom_distinguishes_large_integers()
    {
        const long smaller = 9007199254740992L;
        const long larger = 9007199254740993L;
        Assert.False(AttributeValueAtom.Compare(smaller, ComparisonOperator.Equals, larger));
        Assert.True(AttributeValueAtom.Compare(larger, ComparisonOperator.GreaterThan, smaller));
    }

    [Fact]
    public void AttributeValueAtom_less_than_datetime_vs_iso_string()
    {
        var cutoff = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
        var atom = new AttributeValueAtom(
            AttributeTarget.Context,
            null,
            "time",
            ComparisonOperator.LessThan,
            cutoff.ToString("o"));
        Assert.True(atom.IsSatisfied(Bundle(time: cutoff.AddHours(-1))));
        Assert.False(atom.IsSatisfied(Bundle(time: cutoff.AddHours(1))));
    }

    [Fact]
    public void SubjectIdEqualsAttributeAtom_satisfied_when_resource_owner_matches_subject_id()
    {
        var atom = new SubjectIdEqualsAttributeAtom(AttributeTarget.Resource, "doc", "ownerId");
        Assert.True(atom.IsSatisfied(Bundle(resourceAttrs: new Dictionary<string, object?> { ["ownerId"] = "u1" })));
    }

    [Fact]
    public void SubjectIdEqualsAttributeAtom_unsatisfied_when_owner_is_different_subject()
    {
        var atom = new SubjectIdEqualsAttributeAtom(AttributeTarget.Resource, "doc", "ownerId");
        Assert.False(atom.IsSatisfied(Bundle(
            subjectId: "u2",
            resourceAttrs: new Dictionary<string, object?> { ["ownerId"] = "u1" })));
    }

    [Fact]
    public void SubjectIdEqualsAttributeAtom_unsatisfied_when_attribute_missing()
    {
        var atom = new SubjectIdEqualsAttributeAtom(AttributeTarget.Resource, "doc", "ownerId");
        Assert.False(atom.IsSatisfied(Bundle()));
    }

    [Fact]
    public void SubjectIdEqualsAttributeAtom_name_is_subject_id_equals_attribute()
    {
        Assert.Equal(
            "subject-id-equals-attribute",
            new SubjectIdEqualsAttributeAtom(AttributeTarget.Resource, "doc", "ownerId").Name);
    }

    [Fact]
    public void AttributeEqualsAttributeAtom_equals_when_subject_and_resource_attributes_match()
    {
        var atom = new AttributeEqualsAttributeAtom(
            AttributeTarget.Subject, "subject", "region",
            AttributeTarget.Resource, "doc", "region",
            ComparisonOperator.Equals);

        Assert.True(atom.IsSatisfied(Bundle(
            subjectAttrs: new Dictionary<string, object?> { ["region"] = "us-east" },
            resourceAttrs: new Dictionary<string, object?> { ["region"] = "us-east" })));
    }

    [Fact]
    public void AttributeEqualsAttributeAtom_equals_unsatisfied_when_attributes_differ()
    {
        var atom = new AttributeEqualsAttributeAtom(
            AttributeTarget.Subject, "subject", "region",
            AttributeTarget.Resource, "doc", "region",
            ComparisonOperator.Equals);

        Assert.False(atom.IsSatisfied(Bundle(
            subjectAttrs: new Dictionary<string, object?> { ["region"] = "us-east" },
            resourceAttrs: new Dictionary<string, object?> { ["region"] = "eu-west" })));
    }

    [Fact]
    public void AttributeEqualsAttributeAtom_supports_cross_target_comparisons()
    {
        var atom = new AttributeEqualsAttributeAtom(
            AttributeTarget.Subject, "subject", "level",
            AttributeTarget.Context, null, "minLevel",
            ComparisonOperator.GreaterThanOrEqual);

        Assert.True(atom.IsSatisfied(Bundle(
            subjectAttrs: new Dictionary<string, object?> { ["level"] = 5 },
            values: new Dictionary<string, object?> { ["minLevel"] = 3 })));
        Assert.False(atom.IsSatisfied(Bundle(
            subjectAttrs: new Dictionary<string, object?> { ["level"] = 2 },
            values: new Dictionary<string, object?> { ["minLevel"] = 3 })));
    }

    [Fact]
    public void AttributeEqualsAttributeAtom_in_when_left_value_is_in_right_enumeration()
    {
        var atom = new AttributeEqualsAttributeAtom(
            AttributeTarget.Subject, "subject", "region",
            AttributeTarget.Context, null, "allowedRegions",
            ComparisonOperator.In);

        Assert.True(atom.IsSatisfied(Bundle(
            subjectAttrs: new Dictionary<string, object?> { ["region"] = "eu" },
            values: new Dictionary<string, object?> { ["allowedRegions"] = new[] { "us", "eu" } })));
    }

    [Fact]
    public void AttributeEqualsAttributeAtom_strict_missing_attribute_is_not_satisfied()
    {
        var atom = new AttributeEqualsAttributeAtom(
            AttributeTarget.Subject, "subject", "region",
            AttributeTarget.Resource, "doc", "region",
            ComparisonOperator.Equals,
            strict: true);

        Assert.False(atom.IsSatisfied(Bundle(
            subjectAttrs: new Dictionary<string, object?> { ["region"] = "us-east" })));
        Assert.False(atom.IsSatisfied(Bundle(
            resourceAttrs: new Dictionary<string, object?> { ["region"] = "us-east" })));
    }

    [Fact]
    public void AttributeEqualsAttributeAtom_non_strict_missing_attribute_satisfies_not_equals_only()
    {
        var notEquals = new AttributeEqualsAttributeAtom(
            AttributeTarget.Subject, "subject", "region",
            AttributeTarget.Resource, "doc", "region",
            ComparisonOperator.NotEquals,
            strict: false);
        var equals = new AttributeEqualsAttributeAtom(
            AttributeTarget.Subject, "subject", "region",
            AttributeTarget.Resource, "doc", "region",
            ComparisonOperator.Equals,
            strict: false);

        var bundle = Bundle();
        Assert.True(notEquals.IsSatisfied(bundle));
        Assert.False(equals.IsSatisfied(bundle));
    }

    [Fact]
    public void AttributeEqualsAttributeAtom_defaults_to_strict()
    {
        var atom = new AttributeEqualsAttributeAtom(
            AttributeTarget.Subject, "subject", "region",
            AttributeTarget.Resource, "doc", "region",
            ComparisonOperator.NotEquals);

        Assert.False(atom.IsSatisfied(Bundle()));
    }

    [Fact]
    public void AttributeEqualsAttributeAtom_name_is_attribute_equals_attribute()
    {
        Assert.Equal(
            "attribute-equals-attribute",
            new AttributeEqualsAttributeAtom(
                AttributeTarget.Subject, "subject", "a",
                AttributeTarget.Resource, "doc", "b",
                ComparisonOperator.Equals).Name);
    }

    [Fact]
    public void AttributeValue_requires_type_for_subject()
    {
        Assert.Throws<ArgumentException>(() =>
            new AttributeValueAtom(AttributeTarget.Subject, null, "dept", ComparisonOperator.Equals, "x"));
    }

    [Fact]
    public void AttributeValue_rejects_blank_type_for_resource()
    {
        Assert.Throws<ArgumentException>(() =>
            new AttributeValueAtom(AttributeTarget.Resource, "  ", "status", ComparisonOperator.Equals, "open"));
    }

    [Fact]
    public void AttributeValue_rejects_type_for_context()
    {
        Assert.Throws<ArgumentException>(() =>
            new AttributeValueAtom(AttributeTarget.Context, "ctx", "time", ComparisonOperator.Equals, "x"));
    }

    [Fact]
    public void AttributeValue_type_mismatch_is_not_satisfied()
    {
        var atom = new AttributeValueAtom(AttributeTarget.Resource, "invoice", "status", ComparisonOperator.Equals, "open");
        var bundle = Bundle(resourceAttrs: new Dictionary<string, object?> { ["status"] = "open" });
        Assert.False(atom.IsSatisfied(bundle));
    }

    [Fact]
    public void AttributeValue_type_mismatch_fails_before_value_compare()
    {
        var atom = new AttributeValueAtom(AttributeTarget.Resource, "invoice", "status", ComparisonOperator.NotEquals, "closed");
        Assert.False(atom.IsSatisfied(Bundle(resourceAttrs: new Dictionary<string, object?> { ["status"] = "open" })));
    }

    [Fact]
    public void AttributeValue_type_compare_is_ordinal()
    {
        var atom = new AttributeValueAtom(AttributeTarget.Resource, "Doc", "status", ComparisonOperator.Equals, "open");
        Assert.False(atom.IsSatisfied(Bundle(resourceAttrs: new Dictionary<string, object?> { ["status"] = "open" })));
    }

    [Fact]
    public void AttributeValue_subject_type_comes_from_subjectType_attribute()
    {
        var atom = new AttributeValueAtom(AttributeTarget.Subject, "employee", "dept", ComparisonOperator.Equals, "finance");
        var match = Bundle(subjectAttrs: new Dictionary<string, object?>
        {
            ["subjectType"] = "employee",
            ["dept"] = "finance",
        });
        var fallback = Bundle(subjectAttrs: new Dictionary<string, object?> { ["dept"] = "finance" });

        Assert.True(atom.IsSatisfied(match));
        Assert.False(atom.IsSatisfied(fallback));
    }

    [Fact]
    public void AttributeValue_non_string_subjectType_falls_back_to_subject()
    {
        var atom = new AttributeValueAtom(AttributeTarget.Subject, "subject", "dept", ComparisonOperator.Equals, "finance");
        Assert.True(atom.IsSatisfied(Bundle(subjectAttrs: new Dictionary<string, object?>
        {
            ["subjectType"] = 1,
            ["dept"] = "finance",
        })));
    }

    [Fact]
    public void SubjectIdEquals_requires_type()
    {
        Assert.Throws<ArgumentException>(() =>
            new SubjectIdEqualsAttributeAtom(AttributeTarget.Resource, " ", "ownerId"));
    }

    [Fact]
    public void SubjectIdEquals_type_mismatch_is_not_satisfied()
    {
        var atom = new SubjectIdEqualsAttributeAtom(AttributeTarget.Resource, "invoice", "ownerId");
        Assert.False(atom.IsSatisfied(Bundle(resourceAttrs: new Dictionary<string, object?> { ["ownerId"] = "u1" })));
    }

    [Fact]
    public void AttributeEquals_type_mismatch_is_not_satisfied()
    {
        var atom = new AttributeEqualsAttributeAtom(
            AttributeTarget.Subject, "subject", "region",
            AttributeTarget.Resource, "invoice", "region",
            ComparisonOperator.Equals);

        Assert.False(atom.IsSatisfied(Bundle(
            subjectAttrs: new Dictionary<string, object?> { ["region"] = "us" },
            resourceAttrs: new Dictionary<string, object?> { ["region"] = "us" })));
    }

    [Fact]
    public void AttributeEquals_rejects_type_on_context_side()
    {
        Assert.Throws<ArgumentException>(() =>
            new AttributeEqualsAttributeAtom(
                AttributeTarget.Subject, "subject", "level",
                AttributeTarget.Context, "ctx", "minLevel",
                ComparisonOperator.Equals));
    }

    private static AuthorizationBundle Bundle(
        IEnumerable<string>? roles = null,
        Dictionary<string, object?>? subjectAttrs = null,
        Dictionary<string, object?>? resourceAttrs = null,
        Dictionary<string, object?>? claims = null,
        Dictionary<string, object?>? values = null,
        DateTimeOffset? time = null,
        string operation = "doc:edit",
        string subjectId = "u1")
    {
        return new AuthorizationBundle(
            new Subject(subjectId, new HashSet<string>(roles ?? Array.Empty<string>()), subjectAttrs ?? new Dictionary<string, object?>()),
            new Resource("doc", ResourceKey.Single("1"), resourceAttrs ?? new Dictionary<string, object?>()),
            Operation.Parse(operation),
            new AccessContext(time, claims ?? new Dictionary<string, object?>(), values ?? new Dictionary<string, object?>()));
    }
}
