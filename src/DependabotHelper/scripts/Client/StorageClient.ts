// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { OwnerRepositories, UserProfile } from '../Models/index';

export class StorageClient {
    private readonly key = 'github-profiles';

    constructor(private readonly userId: string) {}

    getOwners(): Map<string, string[]> {
        return this.getOwnersForUser(this.userId);
    }

    setOwners(owner: string, names: string[]): void {
        const profiles = this.getProfiles();
        let profile = profiles.find((item) => item.userId === this.userId);

        if (!profile) {
            profile = {
                userId: this.userId,
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
        const owners = new Map<string, string[]>();

        const profiles = this.getProfiles();
        const profile = profiles.find((profile) => profile.userId === userId);

        if (profile) {
            const compareStrings = (first: string, second: string): any => {
                return first.toLowerCase().localeCompare(second.toLowerCase());
            };

            const compareOwners = (first: OwnerRepositories, second: OwnerRepositories): any => {
                return compareStrings(first.owner, second.owner);
            };

            profile.owners.sort(compareOwners);

            for (const owner of profile.owners) {
                owner.names.sort(compareStrings);
                owners.set(owner.owner, owner.names);
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
}
