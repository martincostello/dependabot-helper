// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { GitHubClient } from '../Client/GitHubClient';
import { StorageClient } from '../Client/StorageClient';
import { PullRequest } from '../Models/PullRequest';
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

        for (const [owner, repositories] of owners) {

            if (repositories.length < 1) {
                continue;
            }

            // Create a new owner table and append to the list
            const ownerList = Page.ownerList();
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

        // Sequentially load the Pull Requests for each repository listed
        for (const element of repoElements) {

            await this.updateRepository(element);

            element.onMerge(async (owner, name) => {
                await this.gitHub.mergePullRequests(owner, name);
                await this.updateRateLimits();
                await this.updateRepository(element);
            });

            element.onPullRequests((pullRequests) => {
                this.modal.update(pullRequests);
            });

            element.onRefresh(async (_owner, _name) => {
                await this.updateRepository(element);
            });

            // TODO Add a 5-10 minute auto-refresh setTimeout()
        }

        if (repoElements.length < 1) {
            Elements.show(Page.findId('not-configured'));
        }

        const modal = Page.findId('pr-modal');
        this.modal = new PullRequestsElement(modal);
    }

    private async updateRepository(element: RepositoryElement): Promise<void> {
        const repository = await this.gitHub.getPullRequests(element.owner, element.name);
        element.update(repository);
        await this.updateRateLimits();
    }
}
