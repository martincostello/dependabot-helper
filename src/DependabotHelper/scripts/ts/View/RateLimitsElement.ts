// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import * as moment from '../../../node_modules/moment/moment';
import { RateLimits } from '../Models/RateLimits';
import { Elements } from './Elements';
import { Page } from './Page';

export class RateLimitsElement {

    private readonly titleAttribute = 'title';

    private remaining: HTMLElement;
    private resets: HTMLElement;
    private total: HTMLElement;

    constructor() {
        this.remaining = Page.findId('rate-limit-remaining');
        this.resets = Page.findId('rate-limit-resets');
        this.total = Page.findId('rate-limit-total');

        setInterval(() => {
            const timestamp = this.resets.getAttribute(this.titleAttribute);
            if (timestamp) {
                this.updateRelativeTime(moment(timestamp));
            }
        }, 60 * 1000);
    }

    update(limits: RateLimits): void {

        if (!limits.limit) {
            return;
        }

        this.remaining.innerText = limits.remaining.toLocaleString();
        this.total.innerText = limits.limit.toLocaleString();

        const resetsAt = moment(limits.resets * 1000);

        this.resets.setAttribute(this.titleAttribute, resetsAt.format());
        this.updateRelativeTime(resetsAt);

        Elements.show(this.remaining.parentElement);
    }

    private updateRelativeTime(timestamp: moment.Moment) {
        this.resets.innerText = timestamp.fromNow();
    }
}
