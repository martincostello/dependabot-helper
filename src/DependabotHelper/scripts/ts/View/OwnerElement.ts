// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { Elements } from './Elements';
import { RepositoryElement } from './RepositoryElement';

export class OwnerElement {
    private readonly itemTemplateClass = 'item-template';

    private readonly element: Element;
    private readonly itemTemplate: Element;
    private readonly owner: string;
    private readonly repostoryList: Element;

    constructor(owner: string, element: Element) {
        this.owner = owner;
        this.element = element;

        const ownerElement = this.element.querySelector('.owner-name');
        ownerElement.textContent = owner;

        const captionElement = this.element.querySelector('.owner-caption');
        captionElement.textContent = `The GitHub repositories of ${owner} to manage Dependabot updates for.`;

        Elements.show(this.element);

        this.itemTemplate = this.element.querySelector('.' + this.itemTemplateClass);
        this.repostoryList = this.element.querySelector('.repo-list');
    }

    addRepository(name: string): RepositoryElement {
        const node = this.itemTemplate.cloneNode(true);
        this.repostoryList.appendChild(node);

        const element = this.repostoryList.lastElementChild;
        element.classList.remove(this.itemTemplateClass);
        element.classList.add('d-flex');

        return new RepositoryElement(this.owner, name, element);
    }
}
