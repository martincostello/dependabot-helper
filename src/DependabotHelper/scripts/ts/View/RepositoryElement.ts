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

        this.repoName.textContent = name;
        this.container.classList.remove(Classes.itemTemplate);
        this.container.classList.remove(Classes.hidden);
    }

    update(repository: Repository): void {

        this.repoName.setAttribute('href', repository.htmlUrl);

        this.loader.classList.add(Classes.hidden);

        this.approvedCount.textContent = repository.approved.length.toLocaleString();
        this.errorCount.textContent = repository.error.length.toLocaleString();
        this.pendingCount.textContent = repository.pending.length.toLocaleString();
        this.successCount.textContent = repository.success.length.toLocaleString();

        const disabled = 'disabled';

        if (repository.all.length > 0) {
            this.mergeButton.removeAttribute(disabled);
        } else {
            this.mergeButton.setAttribute(disabled, '');
        }

        this.refreshButton.removeAttribute(disabled);
    }
}
