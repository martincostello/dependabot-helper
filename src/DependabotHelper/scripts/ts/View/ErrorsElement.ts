// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { Elements } from './Elements';
import { Page } from './Page';

export class ErrorsElement {

    private alert: HTMLElement;

    constructor() {
        this.alert = Page.findId('error-alert');
    }

    show(error: any): void {

        const messageElement = this.alert.querySelector('.error-message');
        const stackTraceElement = this.alert.querySelector('.error-stack-trace');

        let message: string;
        let stack: string;

        if (error instanceof Error) {
            message = error.message;
            stack = error.stack;
        } else {
            message = error.toString();
            stack = '';
        }

        messageElement.textContent = message;
        stackTraceElement.textContent = stack;

        Elements.show(this.alert);
    }
}
