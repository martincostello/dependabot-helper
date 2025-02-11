// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Playwright;

namespace MartinCostello.DependabotHelper.Pages;

public class ConfigurePage(IPage page) : AppPage(page)
{
    public async Task<IReadOnlyList<OwnerItem>> GetOwnersAsync()
    {
        var elements = await Page.QuerySelectorAllAsync(Selectors.OwnerItem);
        return [.. elements.Select((p) => new OwnerItem(p, Page))];
    }

    public async Task WaitForOwnerCountAsync(int count)
    {
        await Assertions.Expect(Page.Locator(Selectors.OwnerItem))
                        .ToHaveCountAsync(count);
    }

    public sealed class OwnerItem : Item
    {
        internal OwnerItem(IElementHandle handle, IPage page)
            : base(handle, page)
        {
        }

        public async Task<ConfigureRepositoriesModal> ConfigureAsync()
        {
            var button = await Handle.QuerySelectorAsync(Selectors.ConfigureButton);
            button.ShouldNotBeNull();

            await button.ClickAsync();

            await Assertions.Expect(Page.Locator(Selectors.RepositoriesModal))
                            .ToBeVisibleAsync();

            var modal = await Page.QuerySelectorAsync(Selectors.RepositoriesModal);
            modal.ShouldNotBeNull();

            return new ConfigureRepositoriesModal(modal, Page);
        }

        public async Task<string> NameAsync()
        {
            var element = await Handle.QuerySelectorAsync(Selectors.OwnerName);
            element.ShouldNotBeNull();
            return await element.InnerTextAsync();
        }
    }

    public sealed class ConfigureRepositoriesModal : Item
    {
        internal ConfigureRepositoriesModal(IElementHandle handle, IPage page)
            : base(handle, page)
        {
        }

        public async Task<ConfigurePage> CloseAsync()
        {
            var element = await Handle.QuerySelectorAsync(Selectors.CancelChanges);
            element.ShouldNotBeNull();

            await element.ClickAsync();

            await Assertions.Expect(Page.Locator(Selectors.RepositoriesModal))
                            .Not
                            .ToBeVisibleAsync();

            return new(Page);
        }

        public async Task<IReadOnlyList<RepositoryItem>> GetRepositoriesAsync()
        {
            var elements = await Handle.QuerySelectorAllAsync(Selectors.RepositoryItem);

            var repositories = new List<RepositoryItem>(elements.Count);

            foreach (var element in elements)
            {
                if (await element.IsVisibleAsync())
                {
                    repositories.Add(new(element, Page));
                }
            }

            repositories.TrimExcess();

            return repositories;
        }

        public async Task<ConfigurePage> SaveChangesAsync()
        {
            var element = await Handle.QuerySelectorAsync(Selectors.SaveChanges);
            element.ShouldNotBeNull();

            await element.ScrollIntoViewIfNeededAsync();
            await element.ClickAsync();

            return new(Page);
        }

        public async Task WaitForRepositoryCountAsync(int count)
        {
            await Assertions.Expect(Page.Locator(Selectors.RepositoryItem))
                            .ToHaveCountAsync(count);

            await Assertions.Expect(Page.Locator(Selectors.RepositoriesLoader)).ToBeHiddenAsync();
        }
    }

    public sealed class RepositoryItem : Item
    {
        internal RepositoryItem(IElementHandle handle, IPage page)
            : base(handle, page)
        {
        }

        public async Task<bool> IsForkAsync()
        {
            var element = await Handle.QuerySelectorAsync(Selectors.ForkIcon);
            element.ShouldNotBeNull();
            return await element.IsVisibleAsync();
        }

        public async Task<bool> IsPrivateAsync()
        {
            var element = await Handle.QuerySelectorAsync(Selectors.PrivateIcon);
            element.ShouldNotBeNull();
            return await element.IsVisibleAsync();
        }

        public async Task<bool> IsSelectedAsync()
        {
            var element = await Handle.QuerySelectorAsync(Selectors.RepositorySelect);
            element.ShouldNotBeNull();
            return await element.IsCheckedAsync();
        }

        public async Task<string> NameAsync()
        {
            var element = await Handle.QuerySelectorAsync(Selectors.RepositoryName);
            element.ShouldNotBeNull();
            return await element.InnerTextAsync();
        }

        public async Task<RepositoryItem> ToggleAsync()
        {
            var element = await Handle.QuerySelectorAsync(Selectors.RepositorySelect);
            element.ShouldNotBeNull();

            await element.ClickAsync();

            return this;
        }
    }

    private sealed class Selectors
    {
        internal const string CancelChanges = "id=repo-cancel";
        internal const string ConfigureButton = "[class*='repo-search']";
        internal const string ForkIcon = "[class*='repo-is-fork']";
        internal const string OwnerItem = "[class*='owner-item']";
        internal const string OwnerName = "[class*='owner-name']";
        internal const string OwnerList = "id=repository-owner-list";
        internal const string PrivateIcon = "[class*='repo-is-private']";
        internal const string RepositoryItem = "[class*='repository-item']:not([class*='check-template'])";
        internal const string RepositoryList = "id=repository-list";
        internal const string RepositoryName = "[class*='repo-name']";
        internal const string RepositorySelect = "[class*='repo-enable']";
        internal const string RepositoriesLoader = "[class*='table-loader']";
        internal const string RepositoriesModal = "id=repo-search-modal";
        internal const string SaveChanges = "id=repo-save";
    }
}
