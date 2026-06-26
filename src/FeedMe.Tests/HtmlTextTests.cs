using FeedMe.Core.Services;

namespace FeedMe.Tests;

public class HtmlTextTests
{
    [Fact]
    public void ToPlainText_StripsTags_DecodesEntities_CollapsesWhitespace()
    {
        var result = HtmlText.ToPlainText("<p>Hello   &amp;   <b>world</b></p>");

        Assert.Equal("Hello & world", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToPlainText_ReturnsNull_ForEmptyInput(string? input)
    {
        Assert.Null(HtmlText.ToPlainText(input));
    }

    [Fact]
    public void ToReadableText_TurnsBlockElementsIntoParagraphs()
    {
        var result = HtmlText.ToReadableText("<p>First para</p><p>Second para</p>");

        Assert.Equal("First para\n\nSecond para", result);
    }

    [Fact]
    public void ToReadableText_DropsTagsButKeepsText()
    {
        var result = HtmlText.ToReadableText("<div>Hi <a href=\"x\">link</a> there</div>");

        Assert.Equal("Hi link there", result);
    }
}
