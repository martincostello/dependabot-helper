// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

export class Elements {
    static readonly hidden = 'd-none';

    static disable(element: Element): void {
        element.setAttribute('disabled', '');
    }

    static enable(element: Element): void {
        element.removeAttribute('disabled');
    }

    static hide(element: Element) {
        element.classList.add(Elements.hidden);
    }

    static show(element: Element) {
        element.classList.remove(Elements.hidden);
    }
}
