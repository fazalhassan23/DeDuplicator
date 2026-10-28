# Implementation diagrams

These Mermaid sources describe implemented paths and explicit refusals. CI renders every fence; reviewers still check semantics. The [persistent entity diagram](generated/entities.md) is generated from the SQLite initializer and labels logical relationships without implying foreign keys.

## Application architecture

```mermaid
flowchart LR
    User[Windows user] --> App[WPF views and MVVM commands]
    App --> Core[Profiles snapshots and keeper planning]
    App --> Infra[Scanner cleanup persistence and exports]
    Infra --> Core
    Infra --> Native[Win32 identity and locked read handles]
    Infra --> DB[(Local SQLite)]
    Native --> FS[Local or consented network filesystem]
    App --> DI[Explicit Microsoft DI and debug logging]
```

## Scan pipeline

```mermaid
flowchart TD
    Roots[Normalize and deduplicate roots] --> Enumerate[Incremental depth-first traversal]
    Enumerate --> Eligible{Settings and non-reparse path allow entry?}
    Eligible -- No --> Issue[Skip or record issue]
    Issue --> Enumerate
    Eligible -- Yes --> Identity[Handle snapshot and physical identity]
    Identity --> DB[(Persist metadata)]
    DB --> Size{Two physical files of equal length?}
    Size -- No --> Singleton[No singleton content reads]
    Size -- Yes --> Sampling{Samples enabled and at least 1 MiB?}
    Sampling -- Yes --> Sample[Hash start middle and end regions]
    Sample --> Match{Sample matches a peer?}
    Match -- No --> Reject[Eliminate candidate]
    Match -- Yes --> Full[Full SHA-256]
    Sampling -- No --> Full
    Full --> HashMatch{Matching full hash and length?}
    HashMatch -- No --> Reject
    HashMatch -- Yes --> Bytes[Complete representative byte comparison]
    Bytes --> Equal{All bytes equal and stable?}
    Equal -- Yes --> Verified[Verified at comparison time]
    Equal -- No --> Partition[Separate collision or invalidate candidate]
    Verified --> Results[Persist groups and hide provisional singletons]
    Enumerate -. Cancellation .-> Cancel[Persist cancelled state and require rescan]
    Full -. Cancellation .-> Cancel
    Bytes -. Cancellation .-> Cancel
```

## Stability and failures

```mermaid
flowchart TD
    Candidate[Candidate snapshot] --> Ancestors{No reparse ancestors?}
    Ancestors -- No --> Skip[Record issue and exclude]
    Ancestors -- Yes --> Open[Open handle denying write and delete sharing]
    Open --> Metadata{Identity and metadata match snapshot?}
    Metadata -- No --> Skip
    Metadata -- Yes --> Read[Read sample hash or complete bytes]
    Read --> After{Stable after reading?}
    After -- No --> Skip
    After -- Yes --> Accept[Accept this comparison result]
    Open -. Access lock or device failure .-> Skip
    Read -. Read failure .-> Skip
    Read -. Cancellation .-> Cancel[Persist cancelled historical state]
    Skip --> Continue[Continue other files without retry]
```

## Keeper selection and action planning

```mermaid
flowchart TD
    Page[Displayed verified member subsets] --> Fresh{Fresh completed scan?}
    Fresh -- No --> Rescan[Rescan saved profile]
    Fresh -- Yes --> Rule[Protect review-required members then apply preference]
    Rule --> Tie[Case-insensitive path tie-break]
    Tie --> Proposed[Propose eligible redundant copies]
    Proposed --> Override[User overrides and keeper choice]
    Override --> Keeper{One keeper retained per displayed subset?}
    Keeper -- No --> Refuse[Refuse plan and explain]
    Keeper -- Yes --> Protected{Selected members eligible and unprotected?}
    Protected -- No --> Refuse
    Protected -- Yes --> Plan[Immutable sources keepers action destinations and reasons]
    Plan --> Review[Review and confirm execution]
```

## Cleanup validation and execution

```mermaid
flowchart TD
    Plan[Confirmed plan] --> Valid{Unique sources and no keeper scheduled for removal?}
    Valid -- No --> Reject[Reject before acting]
    Valid -- Yes --> Journal[Durable Started entry]
    Journal --> Open[Reopen source and keeper against snapshots]
    Open --> Safety{Protected paths streams permissions attributes and identities safe?}
    Safety -- No --> Failed[Journal Failed and retain source]
    Safety -- Yes --> Compare[Compare complete bytes and record source hash]
    Compare --> Equal{Equal and stable?}
    Equal -- No --> Failed
    Equal -- Yes --> Action{Reviewed action}
    Action -- Quarantine --> Move[Handle rename or verified cross-volume transfer]
    Action -- Recycle --> Refuse[Refuse and require new explicit choice]
    Refuse --> Failed
    Action -- PermanentDelete --> Enabled{Advanced option enabled?}
    Enabled -- No --> Failed
    Enabled -- Yes --> Delete[Delete through validated handle]
    Move --> Done[Journal Complete]
    Delete --> Done
    Failed --> Next[Continue remaining operations]
    Done --> Next
    Compare -. Cancellation .-> Stop[Journal Interrupted and stop batch]
    Move -. Cancellation or crash .-> Inspect[Inspect journal and both paths]
```

## Quarantine and recovery

```mermaid
flowchart TD
    Begin[Started entry and destination recorded] --> Rename{No-overwrite rename succeeds?}
    Rename -- Yes --> Complete[Record Complete]
    Rename -- Cross-device only --> Copy[Create exclusive partial and copy]
    Copy --> Flush[Flush and record Copied]
    Flush --> Verify{All destination bytes match source?}
    Verify -- No --> Inspect[Retain source and inspect partial copy]
    Verify -- Yes --> Promote[Promote without overwrite]
    Promote --> Remove[Delete source through validated handle]
    Remove --> Complete
    Rename -- Other error --> Fail[Record Failed and inspect]
    Copy -. Interruption .-> Inspect
    Promote -. Interruption .-> Inspect
    Remove -. Crash before journal update .-> Inspect
    Complete --> Restore[User requests restore]
    Restore --> Vacant{Original path vacant and parents non-reparse?}
    Vacant -- No --> Conflict[Refuse overwrite and require explicit resolution]
    Vacant -- Yes --> Hash{Quarantine hash length and review state valid?}
    Hash -- No --> Conflict
    Hash -- Yes --> Restoring[Record Restoring and transfer without overwrite]
    Restoring --> Restored[Record Restored]
    Restoring -. Failure .-> RestoreFailed[Record Restore failed and inspect paths]
    Startup[Application restart] --> Scans[Mark Running scans interrupted and require rescan]
    Startup --> InFlight[Mark Started Copied and Restoring entries interrupted]
    InFlight --> Inspect
```

## Git and release workflow

```mermaid
flowchart TD
    Staging[Latest origin staging] --> Branch[Cohesive feature fix or docs branch]
    Branch --> Change[Implementation docs diagrams and fragment or exemption]
    Change --> Local[Generate references and run relevant checks]
    Local --> PR[Push branch and open PR to staging]
    PR --> Checks{Complete scope checks and reviews satisfied?}
    Checks -- No --> Remain[Keep draft or resolve feedback]
    Remain --> Change
    Checks -- Yes --> Merge[Integrate into staging]
    Merge --> Prepare[Dedicated release-preparation PR into staging]
    Prepare --> Version[Set version assemble fragments review compatibility]
    Version --> Gate[Validate package and release checklist]
    Gate --> Auth{Specific human authorization for main?}
    Auth -- No --> Wait[Keep main unchanged]
    Auth -- Yes --> ReleasePR[Staging to main PR with required checks and reviews]
    ReleasePR --> Main[Integrate only authorized scope]
    Main --> Publish{Separate tag and publication authorization?}
    Publish -- No --> NoTag[No tag or publication]
    Publish -- Yes --> Release[Immutable authorized tag and release]
```
