using Sigil.Infrastructure.Services;

namespace Sigil.Infrastructure.Tests.Services;

public class SlackAlertSenderTests
{
    [Fact]
    public void EscapeSlack_Ampersand_Escaped()
    {
        SlackAlertSender.EscapeSlack("A & B").Should().Be("A &amp; B");
    }

    [Fact]
    public void EscapeSlack_LessThan_Escaped()
    {
        SlackAlertSender.EscapeSlack("a < b").Should().Be("a &lt; b");
    }

    [Fact]
    public void EscapeSlack_GreaterThan_Escaped()
    {
        SlackAlertSender.EscapeSlack("a > b").Should().Be("a &gt; b");
    }

    [Fact]
    public void EscapeSlack_Pipe_Replaced()
    {
        SlackAlertSender.EscapeSlack("a|b").Should().Be("a\u2502b");
    }

    [Fact]
    public void EscapeSlack_Backtick_Replaced()
    {
        SlackAlertSender.EscapeSlack("`code`").Should().Be("\u2018code\u2018");
    }

    [Fact]
    public void EscapeSlack_Asterisk_Replaced()
    {
        SlackAlertSender.EscapeSlack("*bold*").Should().Be("\u2217bold\u2217");
    }

    [Fact]
    public void EscapeSlack_MultipleSpecialChars_AllEscaped()
    {
        SlackAlertSender.EscapeSlack("<b> & *x* | `y`").Should().Be("&lt;b&gt; &amp; \u2217x\u2217 \u2502 \u2018y\u2018");
    }

    [Fact]
    public void EscapeSlack_NoSpecialChars_Unchanged()
    {
        SlackAlertSender.EscapeSlack("Hello World 123").Should().Be("Hello World 123");
    }

    [Fact]
    public void EscapeSlack_EmptyString_ReturnsEmpty()
    {
        SlackAlertSender.EscapeSlack("").Should().BeEmpty();
    }
}
