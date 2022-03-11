// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { GitHubClient } from '../Client/GitHubClient';
import { StorageClient } from '../Client/StorageClient';
import { Classes } from './Classes';
import { OwnerElement } from './OwnerElement';
import { RateLimitsElement } from './RateLimitsElement';
import { RepositoryElement } from './RepositoryElement';
import { Selectors } from './Selectors';

export class App {

    private readonly gitHub: GitHubClient;
    private readonly storage: StorageClient;
    private rateLimits?: RateLimitsElement;

    constructor() {
        this.gitHub = new GitHubClient();
        this.storage = new StorageClient();
    }

    async initialize(): Promise<void> {
        if (await this.gitHub.isAuthenticated()) {

            this.rateLimits = new RateLimitsElement();
            const owners = this.storage.getOwners();

            // Add the owner tables and each of their repositories
            const ownerList = document.getElementById('owner-list');
            const elements: RepositoryElement[] = [];

            for (const [owner, repositories] of owners) {

                if (repositories.length < 1) {
                    continue;
                }

                // Create a new owner table and append to the list
                const template = document.querySelector(Selectors.ownerTemplate);
                const node = template.cloneNode(true);
                ownerList.appendChild(node);

                const element = new OwnerElement(owner, ownerList.lastElementChild);

                for (const name of repositories) {
                    elements.push(element.addRepository(name));
                }
            }

            // Sequentially load the Pull Requests for each repository listed
            for (const element of elements) {
                this.updateRepository(element);

                element.onMerge(async (owner, name) => {
                    await this.gitHub.mergePullRequests(owner, name);
                    await this.updateRateLimits();
                    await this.updateRepository(element);
                });

                element.onRefresh(async (_owner, _name) => {
                    await this.updateRepository(element);
                });

                // TODO Add a 5-10 minute auto-refresh setTimeout()
            }

            if (elements.length < 1) {
                document.getElementById('not-configured').classList.remove(Classes.hidden);
            }
        }
    }

    private async updateRepository(element: RepositoryElement): Promise<void> {
        const repository = await this.gitHub.getPullRequests(element.owner, element.name);
        element.update(repository);
        await this.updateRateLimits();
    }

    private async updateRateLimits(): Promise<void> {
        if (this.rateLimits) {
            const limits = await this.gitHub.getRateLimits();
            this.rateLimits.update(limits);
        }
    }
}
