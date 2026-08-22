namespace Ajure.Specification.Tests;

/// <summary>A complete, deterministic specification used across the tests.</summary>
internal static class SampleSpec
{
    public static DateTimeOffset GeneratedAt { get; } = new(2026, 8, 22, 5, 0, 0, TimeSpan.Zero);

    public static DocumentContext Context { get; } = new()
    {
        ProjectName = "Trip Planner",
        SpecVersion = "v1",
        Status = SpecStatus.Validating,
        TargetIds = [TargetCatalog.ClaudeCode, TargetCatalog.Cursor, TargetCatalog.GitHubCopilot, TargetCatalog.OpenAiCodex],
        GeneratedAt = GeneratedAt
    };

    public static ProjectSpec Create() => new()
    {
        ProjectName = "Trip Planner",
        Vision = "A planning tool that turns a rough travel idea into a shareable day by day itinerary.",
        Problem = "Travellers keep plans in chat threads and spreadsheets, so the group loses track of what was decided.",
        Personas =
        [
            new Persona
            {
                Id = "P-001",
                Name = "Group organiser",
                Situation = "Plans a four day trip for five people.",
                Motivation = "Wants one place that holds every decision.",
                ExpectedOutcome = "A shared itinerary that everyone can read.",
                Constraints = "Uses a phone during the trip.",
                IsPrimary = true
            },
            new Persona
            {
                Id = "P-002",
                Name = "Participant",
                Situation = "Joins a trip planned by someone else.",
                Motivation = "Wants to know the plan for each day.",
                ExpectedOutcome = "Reads the itinerary without an account.",
                Constraints = "Opens a shared link only."
            }
        ],
        Goals =
        [
            new Goal
            {
                Id = "GOAL-001",
                Statement = "An organiser produces a complete itinerary in one session.",
                SuccessMetric = "80 percent of started trips reach a shared itinerary."
            },
            new Goal
            {
                Id = "GOAL-002",
                Statement = "Participants read the plan without signing up.",
                SuccessMetric = "Median link open to first read below 5 seconds."
            }
        ],
        NonGoals =
        [
            "Booking or payment handling",
            "Native mobile application",
            "Real time chat between participants"
        ],
        Journeys =
        [
            new Journey
            {
                Id = "J-001",
                Title = "Create and share an itinerary",
                Entry = "The organiser opens the planner with a destination in mind.",
                Steps =
                [
                    "Enter destination and travel dates",
                    "Add activities to each day",
                    "Share the read only link"
                ],
                SuccessExit = "A shared link renders the full itinerary.",
                FailurePaths = ["Saving fails and the draft is kept locally", "The share link expires and can be reissued"],
                RequirementIds = ["FR-001", "FR-002", "FR-003"]
            }
        ],
        Requirements =
        [
            new Requirement
            {
                Id = "FR-001",
                Title = "Create a trip",
                Statement = "The organiser must be able to create a trip with a destination and a date range of 1 to 30 days.",
                Priority = Priority.Must,
                Rationale = "Every itinerary needs a container with a fixed date range.",
                AcceptanceCriteriaIds = ["AC-001"],
                TechnicalDecisionIds = ["TD-001"],
                JourneyIds = ["J-001"]
            },
            new Requirement
            {
                Id = "FR-002",
                Title = "Plan each day",
                Statement = "The organiser must be able to add, reorder and remove up to 12 activities per day.",
                Priority = Priority.Must,
                Rationale = "The itinerary is only useful when each day is filled in.",
                AcceptanceCriteriaIds = ["AC-002"],
                TechnicalDecisionIds = ["TD-001"],
                JourneyIds = ["J-001"]
            },
            new Requirement
            {
                Id = "FR-003",
                Title = "Share a read only link",
                Statement = "The organiser must be able to issue a read only link that renders the itinerary without an account.",
                Priority = Priority.Must,
                Rationale = "Participants should not need to sign up.",
                AcceptanceCriteriaIds = ["AC-003"],
                TechnicalDecisionIds = ["TD-002"],
                JourneyIds = ["J-001"]
            },
            new Requirement
            {
                Id = "FR-004",
                Title = "Export the itinerary as a file",
                Statement = "The organiser should be able to download the itinerary as a single printable file.",
                Priority = Priority.Should,
                Rationale = "Some participants keep an offline copy.",
                AcceptanceCriteriaIds = ["AC-004"],
                TechnicalDecisionIds = ["TD-002"],
                JourneyIds = ["J-001"]
            }
        ],
        NonFunctionalRequirements =
        [
            new Requirement
            {
                Id = "NFR-001",
                Title = "Itinerary render time",
                Statement = "A shared itinerary of 30 days must render within 2 seconds on a 4G connection.",
                Priority = Priority.Must,
                Rationale = "Participants open the link on mobile networks.",
                Measurement = "p95 render time below 2000 ms measured in the browser performance trace.",
                AcceptanceCriteriaIds = ["AC-005"],
                TechnicalDecisionIds = ["TD-002"]
            },
            new Requirement
            {
                Id = "NFR-002",
                Title = "Keyboard accessibility",
                Statement = "Every planning control must be reachable and operable with the keyboard only.",
                Priority = Priority.Must,
                Rationale = "The planner is used in accessibility regulated environments.",
                Measurement = "Zero critical issues in an automated axe scan of the planner screen.",
                AcceptanceCriteriaIds = ["AC-006"],
                NoTechnicalImpact = true
            }
        ],
        AcceptanceCriteria =
        [
            new AcceptanceCriterion
            {
                Id = "AC-001",
                Given = "an organiser on the create trip screen",
                When = "a destination and a 5 day range are submitted",
                Then = "the trip is stored and the day list shows 5 days",
                VerificationType = VerificationType.Automated,
                RequirementIds = ["FR-001"]
            },
            new AcceptanceCriterion
            {
                Id = "AC-002",
                Given = "a trip with 3 days",
                When = "an activity is added to day 2 and moved to day 1",
                Then = "day 1 shows the activity in the new order after a reload",
                VerificationType = VerificationType.Automated,
                RequirementIds = ["FR-002"]
            },
            new AcceptanceCriterion
            {
                Id = "AC-003",
                Given = "a trip with at least one activity",
                When = "the organiser issues a read only link and opens it while signed out",
                Then = "the itinerary renders and no editing control is present",
                VerificationType = VerificationType.Ui,
                RequirementIds = ["FR-003"]
            },
            new AcceptanceCriterion
            {
                Id = "AC-004",
                Given = "a trip with 3 planned days",
                When = "the organiser downloads the itinerary",
                Then = "a single file containing all 3 days is produced",
                VerificationType = VerificationType.Api,
                RequirementIds = ["FR-004"]
            },
            new AcceptanceCriterion
            {
                Id = "AC-005",
                Given = "a shared itinerary with 30 days",
                When = "the link is opened on a throttled 4G profile",
                Then = "the p95 render time stays below 2000 ms",
                VerificationType = VerificationType.Automated,
                RequirementIds = ["NFR-001"]
            },
            new AcceptanceCriterion
            {
                Id = "AC-006",
                Given = "the planner screen",
                When = "an automated accessibility scan runs",
                Then = "no critical keyboard or label issue is reported",
                VerificationType = VerificationType.Automated,
                RequirementIds = ["NFR-002"]
            }
        ],
        TechnicalDecisions =
        [
            new TechnicalDecision
            {
                Id = "TD-001",
                Title = "Relational storage for trips",
                Decision = "Trips, days and activities are stored in a single relational database with row level ownership.",
                Rationale = "Ordering and ownership checks are simpler with relational constraints.",
                Alternatives = ["Document store", "In-memory cache with periodic flush"],
                RequirementIds = ["FR-001", "FR-002"],
                IsLocked = true
            },
            new TechnicalDecision
            {
                Id = "TD-002",
                Title = "Signed share tokens",
                Decision = "Read only links carry a signed token with a 90 day expiry and no personal data.",
                Rationale = "Participants must read without an account while links stay revocable.",
                Alternatives = ["Public unguessable url", "Account based sharing"],
                RequirementIds = ["FR-003", "FR-004", "NFR-001"]
            }
        ],
        UxDecisions =
        [
            new UxDecision
            {
                Id = "UX-001",
                Title = "Day column layout",
                Decision = "Days are shown as columns on desktop and as a vertical list below 768 px.",
                Rationale = "Organisers plan on desktop and read on mobile.",
                RequirementIds = ["FR-002"]
            }
        ],
        Risks =
        [
            new Risk
            {
                Id = "RISK-001",
                Statement = "A leaked share link exposes trip details.",
                Likelihood = RiskLevel.Medium,
                Impact = RiskLevel.High,
                Mitigation = "Tokens expire after 90 days and can be revoked from the trip settings."
            },
            new Risk
            {
                Id = "RISK-002",
                Statement = "Large itineraries slow the shared view.",
                Likelihood = RiskLevel.Low,
                Impact = RiskLevel.Medium,
                Mitigation = "The shared view is rendered server side and cached per token."
            }
        ],
        Glossary =
        [
            new GlossaryEntry { Term = "Trip", Definition = "A destination with a continuous date range." },
            new GlossaryEntry { Term = "Activity", Definition = "A single planned item inside one day of a trip." }
        ],
        Evidence =
        [
            new EvidenceItem
            {
                Statement = "Organisers currently keep plans in group chats.",
                IsVerified = true,
                VerificationMethod = "12 user interviews recorded in the research log."
            },
            new EvidenceItem
            {
                Statement = "Participants will open a link without creating an account.",
                VerificationMethod = "Measure link open to read completion in the first release."
            }
        ],
        OptionsConsidered =
        [
            new ConsideredOption
            {
                Title = "Shared document template",
                Summary = "Provide a document template instead of an application.",
                RejectionReason = "No ordering guarantees and no read only sharing."
            },
            new ConsideredOption
            {
                Title = "Web planner with signed share links",
                Summary = "A web application that stores the itinerary and issues read only links.",
                IsChosen = true
            }
        ],
        ValuePropositions =
        [
            "Organisers keep every decision in one itinerary instead of a chat thread.",
            "Participants read the plan without an account."
        ],
        SuccessMetrics =
        [
            new SuccessMetric { Name = "Completed itineraries", Target = "80 percent of started trips" },
            new SuccessMetric { Name = "Shared link opens", Target = "3 opens per trip", Kind = MetricKind.Product }
        ],
        LockedDecisions =
        [
            "Participants never need an account to read a shared itinerary.",
            "The product does not handle bookings or payments."
        ],
        StateMatrix =
        [
            new StateMatrixEntry
            {
                Screen = "Planner",
                Loading = "Skeleton day columns",
                Empty = "Prompt to add the first activity",
                Failure = "Inline error with retry",
                Success = "Day columns with activities",
                Disabled = "Controls disabled while saving",
                Permission = "Read only banner for participants"
            },
            new StateMatrixEntry
            {
                Screen = "Shared itinerary",
                Loading = "Progressive render of days",
                Empty = "Message that no activity was planned",
                Failure = "Expired link message with a request access action",
                Success = "Full itinerary",
                Disabled = "Editing controls are never rendered",
                Permission = "Token validation failure message"
            }
        ],
        BusinessRules =
        [
            new BusinessRule { Statement = "A trip may span at most 30 days.", Precedence = 1 },
            new BusinessRule { Statement = "Only the organiser can issue or revoke a share token.", Precedence = 2 }
        ],
        AnalyticsEvents =
        [
            new AnalyticsEvent
            {
                Name = "trip_created",
                Properties = ["dayCount", "hasActivities"],
                Purpose = "Measure how many started trips reach a plan."
            },
            new AnalyticsEvent
            {
                Name = "share_link_opened",
                Properties = ["tripId", "isExpired"],
                Purpose = "Measure participant reach and link expiry problems."
            }
        ],
        Release = new ReleaseScope
        {
            Mvp = ["FR-001", "FR-002", "FR-003", "NFR-001", "NFR-002"],
            Later = ["FR-004"],
            BlockingConditions = ["Share token revocation is verified", "Accessibility scan reports no critical issue"]
        },
        Technical = new TechnicalProfile
        {
            Constraints = ["Runs as a single web application", "One relational database", "No background worker in the first release"],
            MustTechnologies = ["ASP.NET Core", "PostgreSQL", "React"],
            ForbiddenChoices = ["Client side only persistence", "Third party booking integrations"],
            Architecture = "A React client calls an ASP.NET Core API which owns the relational store and renders shared itineraries server side.",
            TrustBoundaries = ["Browser to API over HTTPS", "API to database inside the private network"],
            Components =
            [
                new ComponentSpec
                {
                    Name = "Planner API",
                    Responsibility = "Trip, day and activity write operations with ownership checks.",
                    Dependencies = ["PostgreSQL"],
                    RequirementIds = ["FR-001", "FR-002"]
                },
                new ComponentSpec
                {
                    Name = "Share renderer",
                    Responsibility = "Validates share tokens and renders the read only itinerary.",
                    Dependencies = ["Planner API"],
                    RequirementIds = ["FR-003", "FR-004", "NFR-001"]
                },
                new ComponentSpec
                {
                    Name = "Accessibility test suite",
                    Responsibility = "Runs the automated accessibility scan in the pipeline.",
                    Dependencies = ["Planner API"],
                    RequirementIds = ["NFR-002"]
                }
            ],
            RepositoryStructure =
            [
                new RepositoryArea { Path = "src/api", Ownership = "Backend" },
                new RepositoryArea { Path = "src/web", Ownership = "Frontend" },
                new RepositoryArea { Path = "tests", Ownership = "Shared" }
            ],
            DataEntities =
            [
                new DataEntity
                {
                    Name = "Trip",
                    Fields = ["id", "ownerId", "destination", "startDate", "endDate"],
                    Relationships = ["Trip has many Day"],
                    Retention = "Deleted 30 days after the owner deletes the trip."
                },
                new DataEntity
                {
                    Name = "Activity",
                    Fields = ["id", "dayId", "title", "position"],
                    Relationships = ["Activity belongs to Day"],
                    Retention = "Removed with the parent trip."
                }
            ],
            ApiContracts =
            [
                new ApiContract
                {
                    Operation = "POST /api/trips",
                    Purpose = "Create a trip.",
                    Auth = "Session cookie, organiser role",
                    Request = "destination, startDate, endDate",
                    SuccessResponse = "201 with the trip id",
                    ErrorResponses = ["400 invalid range", "401 unauthenticated", "409 duplicate trip"],
                    Idempotency = "Client supplied request id",
                    TimeoutAndRetry = "5 second timeout, one retry on 503",
                    RequirementIds = ["FR-001"]
                },
                new ApiContract
                {
                    Operation = "GET /api/share/{token}",
                    Purpose = "Render a read only itinerary.",
                    Auth = "Signed share token",
                    Request = "token path segment",
                    SuccessResponse = "200 with the itinerary payload",
                    ErrorResponses = ["404 unknown token", "410 expired token"],
                    Idempotency = "Read only",
                    TimeoutAndRetry = "2 second timeout, no retry",
                    RequirementIds = ["FR-003", "NFR-001"]
                }
            ],
            States =
            [
                new WorkflowState
                {
                    Name = "Draft",
                    AllowedTransitions = ["Planned"],
                    FailureHandling = "Local draft is kept when saving fails."
                },
                new WorkflowState
                {
                    Name = "Planned",
                    AllowedTransitions = ["Shared", "Draft"],
                    FailureHandling = "Validation errors keep the trip in Planned."
                },
                new WorkflowState
                {
                    Name = "Shared",
                    AllowedTransitions = ["Planned"],
                    FailureHandling = "Token revocation returns the trip to Planned."
                }
            ],
            Security =
            [
                "Session cookie authentication with row level ownership checks",
                "Share tokens are signed, expire after 90 days and carry no personal data",
                "Secrets are read from the platform secret store, never from the repository"
            ],
            Reliability =
            [
                "Write failures return a retryable error code",
                "Share rendering degrades to a cached copy when the database is unavailable"
            ],
            Observability =
            [
                "Structured logs with a correlation id per request",
                "Metrics for itinerary render time and share token failures",
                "Share tokens and personal data are never logged"
            ],
            Deployment =
            [
                "Container image deployed to the managed application platform",
                "Database migrations run before the new revision receives traffic",
                "Health endpoint gates the rollout and enables rollback"
            ],
            TestingStrategy =
            [
                "Unit tests for ordering and token validation",
                "Contract tests for both documented endpoints",
                "End to end test for create, plan and share"
            ],
            ImplementationOrder =
            [
                "Trip and day storage",
                "Activity ordering",
                "Share token issue and render",
                "Accessibility and performance verification"
            ]
        }
    };
}
