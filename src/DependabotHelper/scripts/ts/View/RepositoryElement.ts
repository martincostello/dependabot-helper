// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { Repository } from '../Models/Repository';
import { Classes } from './Classes';
import { Selectors } from './Selectors';

export class RepositoryElement {

    readonly owner: string;
    readonly name: string;

    private readonly container: Element;
    private readonly loader: Element;
    private readonly repoName: Element;

    private readonly approvedCount: Element;
    private readonly errorCount: Element;
    private readonly pendingCount: Element;
    private readonly successCount: Element;

    private readonly mergeButton: Element;
    private readonly refreshButton: Element;

    private onMergeHandler: (owner: string, name: string) => Promise<void>;
    private onRefreshHandler: (owner: string, name: string) => Promise<void>;

    constructor(owner: string, name: string, element: Element) {

        this.owner = owner;
        this.name = name;
        this.container = element;

        this.loader = this.container.querySelector(Selectors.loader);
        this.repoName = this.container.querySelector(Selectors.repositoryName);

        this.approvedCount = this.container.querySelector(Selectors.countApproved);
        this.errorCount = this.container.querySelector(Selectors.countError);
        this.pendingCount = this.container.querySelector(Selectors.countPending);
        this.successCount = this.container.querySelector(Selectors.countSuccess);

        this.mergeButton = this.container.querySelector(Selectors.mergePullRequests);
        this.refreshButton = this.container.querySelector(Selectors.refreshPullRequests);

        this.mergeButton.addEventListener('click', async () => {
            if (this.onMergeHandler) {
                try {
                    this.disable(this.mergeButton);
                    this.showLoader(this.mergeButton);
                    await this.onMergeHandler(this.owner, this.name);
                } catch (err) {
                    throw err;
                } finally {
                    this.hideLoader(this.mergeButton);
                }
            }
        });

        this.refreshButton.addEventListener('click', async () => {
            if (this.onRefreshHandler) {
                try {
                    this.disable(this.refreshButton);
                    this.showLoader(this.refreshButton);
                    await this.onRefreshHandler(this.owner, this.name);
                } catch (err) {
                    throw err;
                } finally {
                    this.hideLoader(this.refreshButton);
                    this.enable(this.refreshButton);
                }
            }
        });

        this.repoName.textContent = name;
        this.container.classList.remove(Classes.itemTemplate);
        this.container.classList.remove(Classes.hidden);
    }

    onMerge(handler: (owner: string, name: string) => Promise<void>) {
        this.onMergeHandler = handler;
    }

    onRefresh(handler: (owner: string, name: string) => Promise<void>) {
        this.onRefreshHandler = handler;
    }

    update(repository: Repository): void {

        this.repoName.setAttribute('href', repository.htmlUrl);

        this.approvedCount.textContent = repository.approved.length.toLocaleString();
        this.errorCount.textContent = repository.error.length.toLocaleString();
        this.pendingCount.textContent = repository.pending.length.toLocaleString();
        this.successCount.textContent = repository.success.length.toLocaleString();

        if (repository.all.length > 0) {
            this.enable(this.mergeButton);
        } else {
            this.disable(this.mergeButton);
        }

        this.enable(this.refreshButton);

        this.loader.classList.add(Classes.hidden);
    }

    private disable(element: Element): void {
        element.setAttribute('disabled', '');
    }

    private enable(element: Element): void {
        element.removeAttribute('disabled');
    }

    private hideLoader(element: Element): void {
        element.querySelector(Selectors.loader).classList.add(Classes.hidden);
    }

    private showLoader(element: Element): void {
        element.querySelector(Selectors.loader).classList.remove(Classes.hidden);
    }
}
