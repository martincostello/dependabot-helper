// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { Classes } from './Classes';
import { RepositoryElement } from './RepositoryElement';
import { Selectors } from './Selectors';

export class OwnerElement {

    private readonly element: Element;
    private readonly itemTemplate: Element;
    private readonly owner: string;
    private readonly repostoryList: Element;

    constructor(owner: string, element: Element) {

        this.owner = owner;
        this.element = element;

        this.element.querySelector(Selectors.ownerName).textContent = owner;

        this.element.classList.remove(Classes.ownerTemplate);
        this.element.classList.remove(Classes.hidden);

        this.itemTemplate = this.element.querySelector(Selectors.itemTemplate);
        this.repostoryList = this.element.querySelector(Selectors.repositoryList);
    }

    addRepository(name: string): RepositoryElement {

        const node = this.itemTemplate.cloneNode(true);
        this.repostoryList.appendChild(node);

        return new RepositoryElement(this.owner, name, this.repostoryList.lastElementChild);
    }
}
