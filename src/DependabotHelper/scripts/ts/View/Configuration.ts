// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { GitHubClient } from '../Client/GitHubClient';
import { StorageClient } from '../Client/StorageClient';
import { Repository } from '../Models/Repository';
import { Elements } from './Elements';
import { ErrorsElement } from './ErrorsElement';
import { Page } from './Page';
import { RateLimitsElement } from './RateLimitsElement';

export class Configuration extends Page {

    private readonly checkTemplateClass = 'check-template';

    private readonly ownerAttribute = 'data-owner';
    private readonly valueAttribute = 'value';

    private readonly checkboxSelector = '.repo-enable';

    private modal: Element;
    private save: Element;
    private template: Element;

    constructor(gitHub: GitHubClient, storage: StorageClient, rateLimits: RateLimitsElement, errors: ErrorsElement) {
        super(gitHub, storage, rateLimits, errors);
    }

    initialize() {

        this.modal = Page.searchModal();
        this.save = Page.findId('repo-save');
        this.template = this.modal.querySelector('.' + this.checkTemplateClass);

        this.save.addEventListener('click', () => {
            this.saveChanges();
        });

        const modalEvents = $(this.modal);

        modalEvents.on('show.bs.modal', async (event: any) => {
            const owner: string = event.relatedTarget.getAttribute(this.ownerAttribute);
            await this.loadRepositories(owner);
        });

        modalEvents.on('hide.bs.modal', () => {
            this.unloadRepositories();
        });
    }

    private async loadRepositories(owner: string): Promise<void> {

        const owners = this.storage.getOwners();
        const checkedNames = owners.get(owner) || [];

        this.modal.setAttribute(this.ownerAttribute, owner);

        const loader = this.modal.querySelector('.table-loader');
        Elements.show(loader);

        let repositories: Repository[] = [];

        try {
            repositories = await this.gitHub.getRepositories(owner);
        } catch (error: any) {
            this.showError(error);
        }

        this.updateRateLimits();

        const body = this.template.parentElement;

        while (body.childElementCount > 1) {
            body.removeChild(body.lastChild);
        }

        for (const repository of repositories) {

            const node = this.template.cloneNode(true);
            body.appendChild(node);

            const element = body.lastElementChild;
            element.classList.remove(this.checkTemplateClass);

            const name = element.querySelector('.repo-name');
            name.textContent = repository.name;
            name.setAttribute('href', repository.htmlUrl);

            const checkbox = <HTMLInputElement>element.querySelector(this.checkboxSelector);
            checkbox.setAttribute('aria-label', `Toggle whether to manage dependabot updates for the ${repository.name} repository.`);
            checkbox.setAttribute(this.valueAttribute, repository.name);

            if (checkedNames.indexOf(repository.name) > -1) {
                checkbox.checked = true;
            }

            if (repository.isFork) {
                Elements.show(element.querySelector('.repo-is-fork'));
            }

            if (repository.isPrivate) {
                Elements.show(element.querySelector('.repo-is-private'));
            }

            Elements.show(element);
        }

        Elements.hide(loader);
    }

    private saveChanges() {

        const spinner = this.save.querySelector('.loader');
        Elements.show(spinner);

        try {
            const owner = this.modal.getAttribute(this.ownerAttribute);
            const names = this.getSelectedRepositories();

            this.storage.setOwners(owner, names);
        } finally {
            Elements.hide(spinner);
        }
    }

    private getSelectedRepositories(): string[] {

        const names: string[] = [];

        const body = this.template.parentElement;
        let element = body.firstElementChild.nextElementSibling;

        while (element) {

            const check = <HTMLInputElement>element.querySelector(this.checkboxSelector);

            if (check.checked) {
                const name = check.getAttribute(this.valueAttribute);
                names.push(name);
            }

            element = element.nextElementSibling;
        }

        return names;
    }

    private unloadRepositories() {
        const body = this.template.parentElement;
        while (body.childElementCount > 1) {
            body.removeChild(body.lastChild);
        }
    }
}
