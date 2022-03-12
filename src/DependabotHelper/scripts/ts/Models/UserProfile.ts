// Copyright (c) Martin Costello, 2022. All rights reserved.

import { OwnerRepositories } from './OwnerRepositories';

export interface UserProfile {
    userId: string;
    owners: OwnerRepositories[];
}
