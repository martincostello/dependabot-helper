// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Playwright;

namespace MartinCostello.DependabotHelper.Pages;

public class ManagePage : AppPage
{
    public ManagePage(IPage page)
        : base(page)
    {
    }

    public async Task WaitForNoRepositoriesAsync()
        => await Page.WaitForSelectorAsync(Selectors.NoRepositories);

    private sealed class Selectors
    {
        internal const string NoRepositories = "id=not-configured";
    }
}
