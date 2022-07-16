// Copyright (c) Martin Costello, 2022. All rights reserved.

export class Elements {
    static hidden = 'd-none';

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
