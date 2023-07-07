// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { PullRequest } from '../Models/index';
import { Elements } from './Elements';

export class PullRequestsElement {
    private readonly templateClass = 'pr-template';

    private readonly template: Element;

    private onApproveHandler: (owner: string, name: string, number: number) => Promise<void>;

    constructor(private readonly modal: Element) {
        this.template = modal.querySelector('.pr-template');
        this.modal.addEventListener('hide.bs.modal', () => {
            this.unloadPullRequests();
        });
    }

    onApprove(handler: (owner: string, name: string, number: number) => Promise<void>) {
        this.onApproveHandler = handler;
    }

    update(pullRequests: PullRequest[]) {
        const body = this.template.parentElement;

        while (body.childElementCount > 1) {
            body.removeChild(body.lastChild);
        }

        for (const pullRequest of pullRequests) {
            this.createRow(body, pullRequest);
        }

        const names = document.querySelectorAll<HTMLElement>('.pr-repo');
        for (const name of names) {
            name.innerText = `${pullRequests[0].repositoryOwner}/${pullRequests[0].repositoryName}`;
        }
    }

    private createRow(body: Element, pullRequest: PullRequest) {
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

        const conflicts = element.querySelector('.pr-status-conflict');
        conflicts.setAttribute('data-count', '1');
        Elements.show(conflicts);

        const isApproved = element.querySelector('.pr-is-approved');

        if (pullRequest.isApproved) {
            Elements.show(isApproved);
        } else if (pullRequest.canApprove) {
            const approveButton = element.querySelector('.pr-approve');

            approveButton.addEventListener('click', async () => {
                if (this.onApproveHandler) {
                    const loader = approveButton.querySelector('.loader');

                    Elements.disable(approveButton);
                    Elements.show(loader);

                    let success = false;

                    try {
                        await this.onApproveHandler(pullRequest.repositoryOwner, pullRequest.repositoryName, pullRequest.number);
                        success = true;
                    } finally {
                        if (success) {
                            Elements.hide(approveButton);
                            Elements.show(isApproved);
                        } else {
                            Elements.enable(approveButton);
                            Elements.hide(loader);
                        }
                    }
                }
            });

            Elements.show(approveButton);
        } else {
            Elements.show(element.querySelector('.pr-approval-pending'));
        }

        Elements.show(element);
    }

    private unloadPullRequests() {
        const body = this.template.parentElement;
        while (body.childElementCount > 1) {
            body.removeChild(body.lastChild);
        }
    }
}
