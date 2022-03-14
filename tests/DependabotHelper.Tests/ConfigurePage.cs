// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Playwright;

namespace MartinCostello.DependabotHelper;

public class ConfigurePage : AppPage
{
    public ConfigurePage(IPage page)
        : base(page)
    {
    }

    public async Task WaitForOwnerListAsync()
        => await Page.WaitForSelectorAsync(Selectors.OwnerList);

    private sealed class Selectors
    {
        internal const string OwnerList = "id=repository-owner-list";
    }
}
