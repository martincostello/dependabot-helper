# Frequently Asked Questions

This document provides some _Frequently Asked Questions_ (FAQs) about usage of
Dependabot Helper that may help resolve common problems and queries.

## _My pull requests are not being merged_

There are multiple reasons why one or more pull requests may not be successfully
merged when you attempt to merge all of the pull requests for a repository.
Dependabot Helper defers to the GitHub API wherever possible to enforce
restrictions, such as protected branches, merge conflicts, etc.

Pull requests that cannot be merged are retried where possible, in the case of
transient errors, but are otherwise skipped when attempting to merge all the
eligible pull requests found by Dependabot Helper for a given repository.

Pull requests are merged in the order they were opened, from the oldest to the
most recent.

Reasons for a pull request failing to merge include:

1. The pull request contains merge conflicts that need to be manually resolved.
1. One or more branch protection rules are not satisfied. This can include:
  * Required approvals, for example Code Owners, are missing;
  * One or more required status check is missing/failed;
  * One ore more code conversations are unresolved;
  * Git pre-commit hooks fail.
1. The HTTP timeout to process a single request to merge open pull requests
elapses (for example if there are many open pull requests);
1. GitHub API rate limits for your account being exceeded.

If retrying the merge operation fails and/or the number of open pull requests
does not decrease when attempting to merge, review the relevant pull request(s)
using the GitHub website view. The website is likely to give a more specific
indication of why a specific pull request is failing to merge.
