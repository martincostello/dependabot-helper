// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { PullRequest, RepositoryPullRequests } from '../Models/index';
import { Elements } from './Elements';

export class RepositoryElement {
    private readonly inactiveClass = 'table-inactive';
    private readonly loaderSelector = '.loader';

    private readonly loader: Element;
    private readonly repoName: Element;

    private readonly approvedCount: Element;
    private readonly errorCount: Element;
    private readonly pendingCount: Element;
    private readonly successCount: Element;

    private readonly configureLink: Element;
    private readonly mergeButton: Element;
    private readonly pullRequestsButton: Element;
    private readonly refreshButton: Element;

    private pullRequests: PullRequest[];

    private onMergeHandler: (owner: string, name: string) => Promise<void>;
    private onPullRequestsHandler: (pullRequests: PullRequest[]) => void;
    private onRefreshHandler: (owner: string, name: string) => Promise<void>;

    constructor(public readonly owner: string, public readonly name: string, private readonly container: Element) {
        this.pullRequests = [];

        this.loader = this.container.querySelector(this.loaderSelector);
        this.repoName = this.container.querySelector('.repo-name');

        this.approvedCount = this.container.querySelector('.repo-count-approved');
        this.errorCount = this.container.querySelector('.repo-count-error');
        this.pendingCount = this.container.querySelector('.repo-count-pending');
        this.successCount = this.container.querySelector('.repo-count-success');

        this.configureLink = this.container.querySelector('.repo-configure');
        this.mergeButton = this.container.querySelector('.repo-merge');
        this.pullRequestsButton = this.container.querySelector('.repo-pull-requests');
        this.refreshButton = this.container.querySelector('.repo-refresh');

        this.mergeButton.addEventListener('click', async () => {
            if (this.onMergeHandler) {
                try {
                    Elements.disable(this.mergeButton);
                    this.showLoader(this.mergeButton);
                    await this.onMergeHandler(this.owner, this.name);
                } catch (err) {
                    throw err;
                } finally {
                    this.hideLoader(this.mergeButton);
                }
            }
        });

        this.pullRequestsButton.addEventListener('click', () => {
            if (this.onPullRequests) {
                this.onPullRequestsHandler(this.pullRequests);
            }
        });

        this.refreshButton.addEventListener('click', async () => {
            if (this.onRefreshHandler) {
                try {
                    Elements.disable(this.refreshButton);
                    this.showLoader(this.refreshButton);
                    await this.onRefreshHandler(this.owner, this.name);
                } catch (err) {
                    throw err;
                } finally {
                    this.hideLoader(this.refreshButton);
                    Elements.enable(this.refreshButton);
                }
            }
        });

        this.repoName.textContent = name;
        Elements.show(this.container);
    }

    onMerge(handler: (owner: string, name: string) => Promise<void>) {
        this.onMergeHandler = handler;
    }

    onPullRequests(handler: (pullRequests: PullRequest[]) => void) {
        this.onPullRequestsHandler = handler;
    }

    onRefresh(handler: (owner: string, name: string) => Promise<void>) {
        this.onRefreshHandler = handler;
    }

    update(repository: RepositoryPullRequests): void {
        this.pullRequests = repository.all;

        this.repoName.setAttribute('href', repository.htmlUrl);

        if (repository.dependabotHtmlUrl) {
            this.configureLink.setAttribute('href', repository.dependabotHtmlUrl);
            this.configureLink.classList.remove('disabled');
        }

        const statuses = new Map<Element, number>();
        statuses.set(this.approvedCount, repository.approved.length);
        statuses.set(this.errorCount, repository.error.length);
        statuses.set(this.pendingCount, repository.pending.length);
        statuses.set(this.successCount, repository.success.length);

        for (const [element, count] of statuses) {
            element.textContent = count.toLocaleString();
            element.setAttribute('data-count', count.toString(10));
        }

        if (this.pullRequests.length > 0) {
            this.container.classList.remove(this.inactiveClass);
            Elements.enable(this.mergeButton);
            Elements.enable(this.pullRequestsButton);
        } else {
            this.container.classList.add(this.inactiveClass);
            Elements.disable(this.mergeButton);
            Elements.disable(this.pullRequestsButton);
        }

        Elements.enable(this.refreshButton);
        Elements.hide(this.loader);
    }

    private hideLoader(element: Element): void {
        const loader = element.querySelector(this.loaderSelector);
        Elements.show(loader.previousElementSibling);
        Elements.hide(loader);
    }

    private showLoader(element: Element): void {
        const loader = element.querySelector(this.loaderSelector);
        Elements.hide(loader.previousElementSibling);
        Elements.show(loader);
    }
}
