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

    public async Task<IReadOnlyList<OwnerItem>> GetOwnersAsync()
    {
        var elements = await Page.QuerySelectorAllAsync(Selectors.OwnerItem);

        var owners = new List<OwnerItem>(elements.Count);

        foreach (var element in elements)
        {
            if (await element.IsVisibleAsync())
            {
                owners.Add(new(element, Page));
            }
        }

        owners.TrimExcess();

        return owners;
    }

    public async Task WaitForNoOwners()
        => await Page.WaitForSelectorAsync(Selectors.NoOwners);

    public async Task WaitForOwnerListAsync()
        => await Page.WaitForSelectorAsync(Selectors.OwnerList);

    public sealed class OwnerItem : Item
    {
        internal OwnerItem(IElementHandle handle, IPage page)
            : base(handle, page)
        {
        }

        public async Task<IReadOnlyList<RepositoryItem>> GetRepositoriesAsync()
        {
            var elements = await Page.QuerySelectorAllAsync(Selectors.RepositoryItem);

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

        public async Task<string> NameAsync()
        {
            var element = await Handle.QuerySelectorAsync(Selectors.OwnerName);
            element.ShouldNotBeNull();
            return await element.InnerTextAsync();
        }

        public async Task WaitForRepositoryCountAsync(int count)
        {
            await Assertions.Expect(Page.Locator(Selectors.RepositoryItem))
                            .ToHaveCountAsync(count);
        }
    }

    public sealed class RepositoryItem : Item
    {
        internal RepositoryItem(IElementHandle handle, IPage page)
            : base(handle, page)
        {
        }

        public async Task<int> ApprovedCountAsync()
            => await CountAsync(Selectors.RepositoryCountApproved);

        public async Task<int> ErrorCountAsync()
            => await CountAsync(Selectors.RepositoryCountError);

        public async Task<bool> IsDependabotEnabledAsync()
            => !await IsDisabledAsync(Selectors.DependabotEnabled);

        public async Task<string> NameAsync()
        {
            var element = await Handle.QuerySelectorAsync(Selectors.RepositoryName);
            element.ShouldNotBeNull();
            return await element.InnerTextAsync();
        }

        public async Task<int> PendingCountAsync()
            => await CountAsync(Selectors.RepositoryCountPending);

        public async Task<int> SuccessCountAsync()
            => await CountAsync(Selectors.RepositoryCountSuccess);

        private async Task<int> CountAsync(string selector)
        {
            var element = await Handle.QuerySelectorAsync(selector);
            element.ShouldNotBeNull();

            string countString = await element.InnerTextAsync();

            int.TryParse(countString, out int count).ShouldBeTrue($"Failed to parse '{countString}' as an integer.");

            return count;
        }

        private async Task<bool> IsDisabledAsync(string selector)
        {
            IElementHandle? element = await Handle.QuerySelectorAsync(selector);

            if (element is null)
            {
                return false;
            }

            try
            {
                string? @class = await element.GetAttributeAsync("class");

                if (string.IsNullOrEmpty(@class))
                {
                    return false;
                }

                string[] classes = @class.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return classes.Contains("disabled");
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }
    }

    private sealed class Selectors
    {
        internal const string DependabotEnabled = "[class*='repo-configure']";
        internal const string NoOwners = "id=not-configured";
        internal const string OwnerItem = "[class*='owner-item']";
        internal const string OwnerList = "id=owner-list";
        internal const string OwnerName = "[class*='owner-name']";
        internal const string RepositoryCountApproved = "[class*='repo-count-approved']";
        internal const string RepositoryCountError = "[class*='repo-count-error']";
        internal const string RepositoryCountPending = "[class*='repo-count-pending']";
        internal const string RepositoryCountSuccess = "[class*='repo-count-success']";
        internal const string RepositoryItem = "[class*='repo-item']:not([class*='item-template'])";
        internal const string RepositoryList = "[class*='repo-list']";
        internal const string RepositoryName = "[class*='repo-name']";
    }
}
