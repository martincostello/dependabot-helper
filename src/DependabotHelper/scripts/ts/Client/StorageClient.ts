// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { UserProfile } from '../Models/UserProfile';

export class StorageClient {

    private key = 'github-profiles';

    getOwners(): Map<string, string[]> {

        const userId = this.getUserId();

        let owners = new Map<string, string[]>();
        let profiles: UserProfile[] = [];

        try {
            profiles = JSON.parse(localStorage.getItem(this.key)) || [];
            const profile = profiles.find((profile) => profile.userId === userId);
            if (profile) {
                for (const owner of profile.owners) {
                    owners.set(owner.owner, owner.names);
                }
            }
        } catch {
            // Ignore any errors accessing local storage
        }

        return owners;
    }

    private getUserId(): string {
        return document.querySelector('meta[name="x-user-id"]').getAttribute('content');
    }
}
