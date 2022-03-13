// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Playwright;

namespace MartinCostello.DependabotHelper;

public class AppPage
{
    public AppPage(IPage page)
    {
        Page = page;
    }

    private IPage Page { get; }

    public async Task SignInAsync()
        => await Page.ClickAsync(Selectors.SignIn);

    public async Task SignOutAsync()
        => await Page.ClickAsync(Selectors.SignOut);

    public async Task<string> UserNameAsync()
        => await Page.InnerTextAsync(Selectors.UserName);

    public async Task WaitForNoRepositoriesAsync()
        => await Page.WaitForSelectorAsync(Selectors.NoRepositories);

    public async Task WaitForSignedInAsync()
        => await Page.WaitForSelectorAsync(Selectors.UserName);

    public async Task WaitForSignedOutAsync()
        => await Page.WaitForSelectorAsync(Selectors.SignIn);

    private sealed class Selectors
    {
        internal const string NoRepositories = "id=not-configured";
        internal const string SignIn = "id=sign-in";
        internal const string SignOut = "id=sign-out";
        internal const string UserName = "id=user-name";
    }
}
