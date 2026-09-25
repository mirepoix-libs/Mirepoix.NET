using Mirepoix.AccessControl;

public class OperationTests
{
    [Theory]
    [InlineData("doc:edit", "doc:edit", true)]
    [InlineData("doc:edit", "doc:*", true)]
    [InlineData("doc:edit", "*:*", true)]
    [InlineData("doc:edit", "invoice:edit", false)]
    [InlineData("doc:edit", "*", true)]
    [InlineData("doc:edit:extra", "*", true)]
    [InlineData("read", "*:*", true)]
    [InlineData("doc:edit", "doc", false)]
    [InlineData("doc:edit", "doc:*:extra", false)]
    public void Matches_hierarchical_patterns(string value, string pattern, bool expected)
    {
        var op = Operation.Parse(value);
        var pat = Operation.Parse(pattern);
        Assert.Equal(expected, op.Matches(pat));
    }

    [Fact]
    public void Matches_partial_wildcard_requires_equal_length()
    {
        var op = Operation.Parse("invoice:post");
        Assert.False(op.Matches(Operation.Parse("invoice:*:archive")));
    }

    [Fact]
    public void Parse_rejects_empty()
    {
        Assert.Throws<ArgumentException>(() => Operation.Parse(""));
    }
}
