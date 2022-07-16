// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { OwnerRepositories } from '../Models/OwnerRepositories';
import { UserProfile } from '../Models/UserProfile';

export class StorageClient {
    private key = 'github-profiles';

    getOwners(): Map<string, string[]> {
        const userId = this.getUserId();
        return this.getOwnersForUser(userId);
    }

    setOwners(owner: string, names: string[]): void {
        const userId = this.getUserId();

        const profiles = this.getProfiles();
        let profile = profiles.find((item) => item.userId === userId);

        if (!profile) {
            profile = {
                userId: userId,
                owners: [],
            };
            profiles.push(profile);
        }

        let ownerItem = profile.owners.find((item) => item.owner === owner);

        if (!ownerItem) {
            ownerItem = {
                owner,
                names: [],
            };
            profile.owners.push(ownerItem);
        }

        ownerItem.names = names;

        localStorage.setItem(this.key, JSON.stringify(profiles));
    }

    private getOwnersForUser(userId: string): Map<string, string[]> {
        let owners = new Map<string, string[]>();

        const profiles = this.getProfiles();
        const profile = profiles.find((profile) => profile.userId === userId);

        if (profile) {
            const compareStrings = (first: string, second: string): any => {
                return first.toLowerCase().localeCompare(second.toLowerCase());
            };

            const compareOwners = (first: OwnerRepositories, second: OwnerRepositories): any => {
                return compareStrings(first.owner, second.owner);
            };

            for (const owner of profile.owners.sort(compareOwners)) {
                owners.set(owner.owner, owner.names.sort(compareStrings));
            }
        }

        return owners;
    }

    private getProfiles(): UserProfile[] {
        let profiles: UserProfile[] = [];

        try {
            profiles = JSON.parse(localStorage.getItem(this.key)) || [];
        } catch {
            // Ignore any errors accessing local storage
        }

        return profiles;
    }

    private getUserId(): string {
        const element = document.querySelector('meta[name="x-user-id"]');
        return element.getAttribute('content');
    }
}
