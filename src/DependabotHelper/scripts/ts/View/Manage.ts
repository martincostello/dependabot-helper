// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { GitHubClient } from '../Client/GitHubClient';
import { StorageClient } from '../Client/StorageClient';
import { RepositoryPullRequests } from '../Models/RepositoryPullRequests';
import { Elements } from './Elements';
import { ErrorsElement } from './ErrorsElement';
import { OwnerElement } from './OwnerElement';
import { Page } from './Page';
import { PullRequestsElement } from './PullRequestsElement';
import { RateLimitsElement } from './RateLimitsElement';
import { RepositoryElement } from './RepositoryElement';

export class Manage extends Page {

    private readonly ownerTemplateClass = 'owner-template';

    private modal: PullRequestsElement;

    constructor(gitHub: GitHubClient, storage: StorageClient, rateLimits: RateLimitsElement, errors: ErrorsElement) {
        super(gitHub, storage, rateLimits, errors);
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

        if (repoElements.length < 1) {
            Elements.show(Page.findId('not-configured'));
        } else {

            const modal = Page.findId('pr-modal');
            this.modal = new PullRequestsElement(modal);

            this.modal.onApprove(async (owner, name, number) => {

                try {
                    await this.gitHub.approvePullRequest(owner, name, number);
                } catch (error: any) {
                    this.showError(error);
                }

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

            await this.configureRepositories(repoElements, refreshInterval);
        }
    }

    private async configureRepositories(
        repositories: RepositoryElement[],
        refreshInterval: number): Promise<void> {

        const promises: Promise<void>[] = [];

        for (const repository of repositories) {

            promises.push(this.configureRepository(repository, refreshInterval));
        }

        await Promise.all(promises);
    }

    private async configureRepository(
        repository: RepositoryElement,
        refreshInterval: number): Promise<void> {

        await this.updateRepository(repository);

        repository.onMerge(async (owner, name) => {

            try {
                await this.gitHub.mergePullRequests(owner, name);
            } catch (error: any) {
                this.showError(error);
            }

            this.updateRateLimits();
            await this.updateRepository(repository);
        });

        repository.onPullRequests((pullRequests) => {
            this.modal.update(pullRequests);
        });

        repository.onRefresh(async (_owner, _name) => {
            await this.updateRepository(repository);
        });

        // Automatically refresh the UI with a random
        // jitter factor of 0-30 seconds, if configured.
        if (refreshInterval) {
            const jitter = Math.floor(Math.random() * 30) * 1000;
            const interval = refreshInterval + jitter;

            setInterval(async () => {
                await this.updateRepository(repository);
            }, interval);
        }
    }

    private async updateRepository(element: RepositoryElement): Promise<void> {

        let repository: RepositoryPullRequests = null;

        try {
            repository = await this.gitHub.getPullRequests(element.owner, element.name);
        } catch (error: any) {
            this.showError(error);
        }

        if (repository) {
            element.update(repository);
        }

        this.updateRateLimits();
    }
}
