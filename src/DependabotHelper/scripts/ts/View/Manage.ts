// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { GitHubClient } from '../Client/GitHubClient';
import { StorageClient } from '../Client/StorageClient';
import { Elements } from './Elements';
import { OwnerElement } from './OwnerElement';
import { Page } from './Page';
import { PullRequestsElement } from './PullRequestsElement';
import { RateLimitsElement } from './RateLimitsElement';
import { RepositoryElement } from './RepositoryElement';

export class Manage extends Page {

    private readonly ownerTemplateClass = 'owner-template';

    private modal: PullRequestsElement;

    constructor(gitHub: GitHubClient, storage: StorageClient, rateLimits: RateLimitsElement) {
        super(gitHub, storage, rateLimits);
    }

    async initialize(): Promise<void> {

        const repoElements: RepositoryElement[] = [];
        const owners = this.storage.getOwners();

        const ownerList = Page.ownerList();

        for (const [owner, repositories] of owners) {

            if (repositories.length < 1) {
                continue;
            }

            // Create a new owner table and append to the list
            const template = ownerList.querySelector('.' + this.ownerTemplateClass);
            const node = template.cloneNode(true);
            ownerList.appendChild(node);

            const element = ownerList.lastElementChild;
            element.classList.remove(this.ownerTemplateClass);

            const ownerElement = new OwnerElement(owner, element);

            for (const name of repositories) {
                repoElements.push(ownerElement.addRepository(name));
            }
        }

        const modal = Page.findId('pr-modal');
        this.modal = new PullRequestsElement(modal);

        this.modal.onApprove(async (owner, name, number) => {

            await this.gitHub.approvePullRequest(owner, name, number);

            const element = repoElements.find((element) => element.owner === owner && element.name === name);

            if (element) {
                await this.updateRepository(element);
            }
        });

        let refreshInterval: number = null;

        const refreshIntervalString = ownerList.getAttribute('data-refresh-period');

        if (refreshIntervalString) {
            const interval = parseInt(refreshIntervalString, 10);
            if (interval > 1000 * 30) {
                refreshInterval = interval;
            }
        }

        // Sequentially load the Pull Requests for each repository listed
        for (const element of repoElements) {

            await this.updateRepository(element);

            element.onMerge(async (owner, name) => {
                await this.gitHub.mergePullRequests(owner, name);
                this.updateRateLimits();
                await this.updateRepository(element);
            });

            element.onPullRequests((pullRequests) => {
                this.modal.update(pullRequests);
            });

            element.onRefresh(async (_owner, _name) => {
                await this.updateRepository(element);
            });

            // Automatically refresh the UI with a random
            // jitter factor of 0-30 seconds, if configured.
            if (refreshInterval) {
                const jitter = Math.floor(Math.random() * 30) * 1000;
                const interval = refreshInterval + jitter;

                setInterval(async () => {
                    await this.updateRepository(element);
                }, interval);
            }
        }

        if (repoElements.length < 1) {
            Elements.show(Page.findId('not-configured'));
        }
    }

    private async updateRepository(element: RepositoryElement): Promise<void> {
        const repository = await this.gitHub.getPullRequests(element.owner, element.name);
        element.update(repository);
        this.updateRateLimits();
    }
}
