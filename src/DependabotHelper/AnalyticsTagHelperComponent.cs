// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Options;

namespace MartinCostello.DependabotHelper;

public sealed class AnalyticsTagHelperComponent : TagHelperComponent
{
    public AnalyticsTagHelperComponent(
        IHttpContextAccessor accessor,
        IOptionsSnapshot<SiteOptions> options)
    {
        Accessor = accessor;
        Options = options;
    }

    public override int Order => int.MaxValue;

    private IHttpContextAccessor Accessor { get; }

    private IOptionsSnapshot<SiteOptions> Options { get; }

    public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (string.Equals(context.TagName, "head", StringComparison.OrdinalIgnoreCase))
        {
            string analyticsId = Options.Value.AnalyticsId;
            string? nonce = Accessor.HttpContext?.GetCspNonce();

            const string Indent = "    ";

            var builder = new StringBuilder()
                .Append(Indent).Append("<script src=\"https://www.googletagmanager.com/gtag/js?id=").Append(analyticsId).AppendLine("\" async></script>")
                .Append(Indent).Append("<script type=\"text/javascript\" nonce=\"").Append(nonce).AppendLine("\">")
                .Append(Indent).Append(Indent).AppendLine("window.dataLayer = window.dataLayer || [];")
                .Append(Indent).Append(Indent).AppendLine("function gtag(){dataLayer.push(arguments);}")
                .Append(Indent).Append(Indent).AppendLine("gtag('js', new Date());")
                .Append(Indent).Append(Indent).Append("gtag('config', '").Append(analyticsId).AppendLine("');")
                .Append(Indent).AppendLine("</script>");

            output.PostContent.AppendHtml(builder.ToString());
        }

        return Task.CompletedTask;
    }
}
