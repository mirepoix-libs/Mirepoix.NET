using Mirepoix.AccessControl;

public class OperationTests
{
    [Theory]
    [InlineData("doc:edit", "doc:edit", true)]
    [InlineData("doc:edit", "doc:*", true)]
    [InlineData("doc:edit", "*:*", true)]
    [InlineData("doc:edit", "invoice:edit", false)]
    public void Matches_hierarchical_patterns(string value, string pattern, bool expected)
    {
        var op = Operation.Parse(value);
        var pat = Operation.Parse(pattern);
        Assert.Equal(expected, op.Matches(pat));
    }

    [Fact]
    public void Parse_rejects_empty()
    {
        Assert.Throws<ArgumentException>(() => Operation.Parse(""));
    }
}
