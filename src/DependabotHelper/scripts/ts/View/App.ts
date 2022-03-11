// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { GitHubClient } from '../Client/GitHubClient';
import { OwnerElement } from './OwnerElement';
import { RateLimitsElement } from './RateLimitsElement';
import { RepositoryElement } from './RepositoryElement';
import { Selectors } from './Selectors';

export class App {

    private readonly client: GitHubClient;
    private rateLimits?: RateLimitsElement;

    constructor() {
        this.client = new GitHubClient();
    }

    async initialize(): Promise<void> {
        if (await this.client.isAuthenticated()) {

            this.rateLimits = new RateLimitsElement();

            // TODO Allow the user to select their own account plus any
            // organizations they this to look at repositories within,
            // and then select them and store in local storage so that
            // that it's user configurable, rather than server configurable.
            // Also shard in loacl storage in some way so that the list
            // is related to the GitHub server being pointed at.
            const owners = await this.client.getRepos();

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

                const repository = await this.client.getPullRequests(element.owner, element.name);

                element.update(repository);

                await this.updateRateLimits();
            }
        }
    }

    private async updateRateLimits(): Promise<void> {
        if (this.rateLimits) {
            const limits = await this.client.getRateLimits();
            this.rateLimits.update(limits);
        }
    }
}
