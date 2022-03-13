// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { PullRequest } from '../Models/PullRequest';
import { Elements } from './Elements';

export class PullRequestsElement {

    private readonly templateClass = 'pr-template';

    private readonly modal: Element;
    private readonly template: Element;

    constructor(modal: Element) {
        this.modal = modal;
        this.template = modal.querySelector('.pr-template');

        $(this.modal).on('hide.bs.modal', () => {
            this.unloadPullRequests();
        });
    }

    update(pullRequests: PullRequest[]) {

        const body = this.template.parentElement;

        while (body.childElementCount > 1) {
            body.removeChild(body.lastChild);
        }

        for (const pullRequest of pullRequests) {

            const node = this.template.cloneNode(true);
            body.appendChild(node);

            const element = body.lastElementChild;
            element.classList.remove(this.templateClass);

            const title = element.querySelector('.pr-title');
            title.textContent = pullRequest.title;
            title.setAttribute('data-number', pullRequest.number.toString(10));
            title.setAttribute('href', pullRequest.htmlUrl);

            const status = element.querySelector(`.pr-status-${pullRequest.status.toLowerCase()}`);
            status.setAttribute('data-count', '1');
            Elements.show(status);

            if (pullRequest.isApproved) {
                Elements.show(element.querySelector('.pr-is-approved'));
            }

            Elements.show(element);
        }
    }

    private unloadPullRequests() {
        const body = this.template.parentElement;
        while (body.childElementCount > 1) {
            body.removeChild(body.lastChild);
        }
    }
}
