﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Playwright;

namespace MartinCostello.DependabotHelper.Pages;

public abstract class AppPage(IPage page)
{
    protected IPage Page { get; } = page;

    public async Task<ConfigurePage> ConfigureAsync()
    {
        await Page.ClickAsync(Selectors.ConfigureLink);
        return new(Page);
    }

    public async Task<ManagePage> ManageAsync()
    {
        await Page.ClickAsync(Selectors.ManageLink);
        return new(Page);
    }

    public async Task SignInAsync()
        => await Page.ClickAsync(Selectors.SignIn);

    public async Task SignOutAsync()
        => await Page.ClickAsync(Selectors.SignOut);

    public async Task<string> UserNameAsync()
        => await Page.InnerTextAsync(Selectors.UserName);

    public async Task WaitForSignedInAsync()
        => await Page.WaitForSelectorAsync(Selectors.UserName);

    public async Task WaitForSignedOutAsync()
        => await Assertions.Expect(Page.Locator(Selectors.SignIn))
                           .ToBeVisibleAsync();

    public abstract class Item(IElementHandle handle, IPage page)
    {
        protected IElementHandle Handle { get; } = handle;

        protected IPage Page { get; } = page;
    }

    private sealed class Selectors
    {
        internal const string ConfigureLink = "id=configure-link";
        internal const string ManageLink = "id=manage-link";
        internal const string SignIn = "id=sign-in";
        internal const string SignOut = "id=sign-out";
        internal const string UserName = "id=user-name";
    }
}
