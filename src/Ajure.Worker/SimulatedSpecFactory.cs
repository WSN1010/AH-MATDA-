using Ajure.Specification;

namespace Ajure.Worker;

public static class SimulatedSpecFactory
{
    public static ProjectSpec Create(string projectName, string idea) => new()
    {
        ProjectName = projectName,
        Vision = idea,
        Problem = "Product teams need one traceable specification that can drive implementation without hidden assumptions.",
        Personas =
        [
            new Persona
            {
                Id = "P-001",
                Name = "Product builder",
                Situation = "Starts with a product idea and implementation constraints.",
                Motivation = "Wants an implementation-ready plan before writing product code.",
                ExpectedOutcome = "Receives consistent product, technical, and agent instruction documents.",
                Constraints = "Must inspect and approve implementation-changing decisions.",
                IsPrimary = true
            }
        ],
        Goals =
        [
            new Goal
            {
                Id = "GOAL-001",
                Statement = "Turn an approved product idea into one traceable specification package.",
                SuccessMetric = "Every Must requirement links to a verifiable acceptance criterion."
            }
        ],
        NonGoals =
        [
            "Generating product implementation source code",
            "Deploying the generated product",
            "Replacing explicit user decisions with model guesses"
        ],
        Journeys =
        [
            new Journey
            {
                Id = "J-001",
                Title = "Generate and export a validated specification",
                Entry = "A builder submits a product idea and approves critical decisions.",
                Steps =
                [
                    "Generate a normalized ProjectSpec",
                    "Validate it with deterministic and independent model checks",
                    "Export the Ready artifacts"
                ],
                SuccessExit = "A deterministic ZIP contains the Ready specification and target instructions.",
                FailurePaths =
                [
                    "Invalid model output fails closed",
                    "Unresolved critical findings return the version for a user decision"
                ],
                RequirementIds = ["FR-001", "FR-002"]
            }
        ],
        Requirements =
        [
            new Requirement
            {
                Id = "FR-001",
                Title = "Generate one semantic specification",
                Statement = "The system must create one ProjectSpec and render every document from that same immutable version.",
                Priority = Priority.Must,
                Rationale = "A single semantic source prevents document drift.",
                AcceptanceCriteriaIds = ["AC-001"],
                TechnicalDecisionIds = ["TD-001"],
                JourneyIds = ["J-001"]
            },
            new Requirement
            {
                Id = "FR-002",
                Title = "Validate with independent models",
                Statement = "The system must use at least 2 distinct model IDs and deterministic consensus before marking a version Ready.",
                Priority = Priority.Must,
                Rationale = "Independent review reduces single-model blind spots.",
                AcceptanceCriteriaIds = ["AC-002"],
                TechnicalDecisionIds = ["TD-002"],
                JourneyIds = ["J-001"]
            }
        ],
        NonFunctionalRequirements =
        [
            new Requirement
            {
                Id = "NFR-001",
                Title = "Deterministic export",
                Statement = "The same Ready inputs must produce a byte-identical ZIP archive.",
                Priority = Priority.Must,
                Rationale = "Stable output supports audit and reproducible delivery.",
                Measurement = "Repeated exports have the same SHA-256 digest.",
                AcceptanceCriteriaIds = ["AC-003"],
                TechnicalDecisionIds = ["TD-001"]
            }
        ],
        AcceptanceCriteria =
        [
            new AcceptanceCriterion
            {
                Id = "AC-001",
                Given = "approved project intent and target IDs",
                When = "the generation job completes",
                Then = "all rendered artifacts reference the same specification version and hash",
                VerificationType = VerificationType.Automated,
                RequirementIds = ["FR-001"]
            },
            new AcceptanceCriterion
            {
                Id = "AC-002",
                Given = "at least 2 available configured models",
                When = "the validation stage runs",
                Then = "independent reviewer sessions are aggregated and unresolved Critical findings block Ready",
                VerificationType = VerificationType.Automated,
                RequirementIds = ["FR-002"]
            },
            new AcceptanceCriterion
            {
                Id = "AC-003",
                Given = "an unchanged Ready specification and artifact set",
                When = "the export job runs twice",
                Then = "both ZIP archives have the same SHA-256 digest",
                VerificationType = VerificationType.Automated,
                RequirementIds = ["NFR-001"]
            }
        ],
        TechnicalDecisions =
        [
            new TechnicalDecision
            {
                Id = "TD-001",
                Title = "One ProjectSpec and deterministic renderers",
                Decision = "ProjectSpec is the only semantic source; native renderers and the exporter own paths, timestamps, and hashes.",
                Rationale = "Models should not control deterministic artifact structure.",
                Alternatives = ["Generate each Markdown file independently"],
                RequirementIds = ["FR-001", "NFR-001"],
                IsLocked = true
            },
            new TechnicalDecision
            {
                Id = "TD-002",
                Title = "Independent multi-model validation",
                Decision = "Reviewers use isolated sessions and at least 2 distinct model IDs, followed by deterministic aggregation.",
                Rationale = "Role and model diversity are auditable and fail closed.",
                Alternatives = ["One model self-review", "Non-deterministic reviewer voting"],
                RequirementIds = ["FR-002"],
                IsLocked = true
            }
        ],
        UxDecisions =
        [
            new UxDecision
            {
                Id = "UX-001",
                Title = "Visible validation state",
                Decision = "The client presents queued, validating, needs-decision, failed, and Ready states without optimistic success.",
                Rationale = "Long-running model work and fail-closed outcomes must remain understandable.",
                RequirementIds = ["FR-002"]
            }
        ],
        Risks =
        [
            new Risk
            {
                Id = "RISK-001",
                Statement = "A model can return syntactically plausible but incomplete structured output.",
                Likelihood = RiskLevel.Medium,
                Impact = RiskLevel.High,
                Mitigation = "Strict parsing, deterministic validation, one parse retry, and fail-closed hard gates."
            }
        ],
        Glossary =
        [
            new GlossaryEntry
            {
                Term = "ProjectSpec",
                Definition = "The canonical structured product and technical specification."
            },
            new GlossaryEntry
            {
                Term = "Ready",
                Definition = "A version with at least 90 points and every hard gate passed."
            }
        ],
        Evidence =
        [
            new EvidenceItem
            {
                Statement = idea,
                VerificationMethod = "Confirm the submitted intent with the project owner."
            }
        ],
        OptionsConsidered =
        [
            new ConsideredOption
            {
                Title = "Independent document generation",
                Summary = "Ask a model to write each document separately.",
                RejectionReason = "Separate generations cannot guarantee semantic consistency."
            },
            new ConsideredOption
            {
                Title = "Canonical ProjectSpec",
                Summary = "Generate one structured specification and render every artifact from it.",
                IsChosen = true
            }
        ],
        ValuePropositions =
        [
            "Builders receive one traceable implementation contract.",
            "Deterministic checks and independent reviewers expose gaps before implementation."
        ],
        SuccessMetrics =
        [
            new SuccessMetric
            {
                Name = "Ready package traceability",
                Target = "100 percent of Must requirements linked to acceptance criteria"
            },
            new SuccessMetric
            {
                Name = "Deterministic export",
                Target = "100 percent matching SHA-256 across unchanged repeated exports",
                Kind = MetricKind.Product
            }
        ],
        LockedDecisions =
        [
            "ProjectSpec is the single semantic source of truth.",
            "A Ready decision requires at least two distinct model IDs.",
            "The application is self-hosted with SQLite and user-supplied model API keys."
        ],
        StateMatrix =
        [
            new StateMatrixEntry
            {
                Screen = "Specification workspace",
                Loading = "Show the current job stage and latest event.",
                Empty = "Prompt for a product idea.",
                Failure = "Show a stable error code and whether retry is allowed.",
                Success = "Show the Ready score, artifacts, and export action.",
                Disabled = "Disable export until the version is Ready.",
                Permission = "Reject access to projects owned by another principal."
            }
        ],
        BusinessRules =
        [
            new BusinessRule
            {
                Statement = "User-approved decisions override model recommendations.",
                Precedence = 1
            },
            new BusinessRule
            {
                Statement = "A hard-gate failure overrides the numeric score.",
                Precedence = 2
            }
        ],
        AnalyticsEvents =
        [
            new AnalyticsEvent
            {
                Name = "validation_completed",
                Properties = ["versionId", "status", "score", "repairIteration"],
                Purpose = "Measure first-pass readiness and repeated repair failures."
            }
        ],
        Release = new ReleaseScope
        {
            Mvp = ["FR-001", "FR-002", "NFR-001"],
            Later = ["Provider-specific deployment automation"],
            BlockingConditions =
            [
                "No unresolved Critical finding",
                "At least 2 distinct model IDs completed review",
                "All artifact hashes and version labels match"
            ]
        },
        Technical = new TechnicalProfile
        {
            Constraints =
            [
                "Backend services use .NET 10",
                "Persistent state and jobs use one SQLite file",
                "Model calls use direct OpenAI, Anthropic, and Gemini HTTPS APIs"
            ],
            MustTechnologies = ["SQLite", "Microsoft Agent Framework", "IChatClient"],
            ForbiddenChoices = ["Azure services", "GitHub Copilot SDK", "Model-generated product source code", "Logging prompt or model response bodies"],
            Architecture = "An ASP.NET Core API queues jobs in SQLite; a Worker runs isolated agents through direct model APIs, validates ProjectSpec, and writes immutable artifacts.",
            TrustBoundaries =
            [
                "Client input to the API",
                "Queue messages to the Worker",
                "Untrusted model JSON to strict parsers",
                "Worker output to SQLite"
            ],
            Components =
            [
                new ComponentSpec
                {
                    Name = "Ajure API",
                    Responsibility = "Validate requests, own HTTP and SSE contracts, and enqueue identifier-only jobs.",
                    Dependencies = ["SQLite"],
                    RequirementIds = ["FR-001"]
                },
                new ComponentSpec
                {
                    Name = "Ajure Worker",
                    Responsibility = "Run generation, independent validation, repair, rendering, and deterministic export.",
                    Dependencies = ["OpenAI/Anthropic/Gemini APIs", "SQLite"],
                    RequirementIds = ["FR-001", "FR-002", "NFR-001"]
                }
            ],
            RepositoryStructure =
            [
                new RepositoryArea { Path = "src/Ajure.Api", Ownership = "Backend HTTP and SSE" },
                new RepositoryArea { Path = "src/Ajure.Worker", Ownership = "Backend orchestration" },
                new RepositoryArea { Path = "src/Ajure.Specification", Ownership = "Canonical model and renderers" },
                new RepositoryArea { Path = "src/Ajure.Validation", Ownership = "Deterministic evaluation rules" }
            ],
            DataEntities =
            [
                new DataEntity
                {
                    Name = "SpecVersion",
                    Fields = ["id", "projectId", "number", "status", "specHash"],
                    Relationships = ["A project has immutable specification versions"],
                    Retention = "Retained until the owning project is deleted."
                },
                new DataEntity
                {
                    Name = "ValidationRun",
                    Fields = ["id", "versionId", "score", "models", "sessions", "hardGates", "repairIteration"],
                    Relationships = ["A specification version has validation runs"],
                    Retention = "Retained with the specification version."
                }
            ],
            ApiContracts =
            [
                new ApiContract
                {
                    Operation = "POST /api/spec-versions/{versionId}/generate",
                    Purpose = "Queue generation and validation.",
                    Auth = "Authenticated project owner",
                    Request = "version ID",
                    SuccessResponse = "202 with job ID",
                    ErrorResponses = ["404 unknown version", "409 invalid version state"],
                    Idempotency = "A version can have one active generation job.",
                    TimeoutAndRetry = "HTTP enqueue timeout is 10 seconds; retry only transient storage failures.",
                    RequirementIds = ["FR-001", "FR-002"]
                },
                new ApiContract
                {
                    Operation = "POST /api/spec-versions/{versionId}/export",
                    Purpose = "Create a deterministic ZIP for a Ready version.",
                    Auth = "Authenticated project owner",
                    Request = "Ready version ID",
                    SuccessResponse = "202 with job ID",
                    ErrorResponses = ["404 unknown version", "409 version is not Ready"],
                    Idempotency = "Unchanged Ready inputs produce the same archive digest.",
                    TimeoutAndRetry = "HTTP enqueue timeout is 10 seconds; retry only transient storage failures.",
                    RequirementIds = ["NFR-001"]
                }
            ],
            States =
            [
                new WorkflowState
                {
                    Name = "Draft",
                    AllowedTransitions = ["Validating", "NeedsDecision"],
                    FailureHandling = "Validation errors keep the version non-exportable."
                },
                new WorkflowState
                {
                    Name = "Validating",
                    AllowedTransitions = ["Ready", "NeedsDecision", "Failed"],
                    FailureHandling = "The job records a stable error or hard-gate result."
                },
                new WorkflowState
                {
                    Name = "Ready",
                    AllowedTransitions = ["Superseded"],
                    FailureHandling = "Export fails closed if any stored artifact is stale."
                }
            ],
            Security =
            [
                "Authorization is enforced at the project ownership boundary.",
                "Model requests contain no tool definitions.",
                "Secrets, prompt bodies, and model response bodies are never logged."
            ],
            Reliability =
            [
                "Queue delivery is idempotent and terminal job redelivery is a no-op.",
                "Only timeout, throttling, and transient storage or service errors are retried.",
                "Schema, policy, and model-diversity failures fail closed."
            ],
            Observability =
            [
                "Structured events use correlation IDs, job IDs, stages, and stable error codes.",
                "Validation audit data records roles, model IDs, session IDs, parse attempts, scores, clusters, and gates.",
                "Generative AI message content capture is disabled."
            ],
            Deployment =
            [
                "API and Worker share an operator-configured SQLite path.",
                "The application runs on a standard .NET host without a required cloud account.",
                "Health checks gate service readiness."
            ],
            TestingStrategy =
            [
                "Unit tests cover parsing, normalization, consensus, repair scope, rendering, and ZIP determinism.",
                "Integration tests cover SQLite storage, queue retry, SSE replay, and API problem details.",
                "End-to-end tests cover simulated and real-model generation through Ready export."
            ],
            ImplementationOrder =
            [
                "Create and validate ProjectSpec",
                "Run independent reviewers and implementation simulation",
                "Repair only confirmed affected IDs",
                "Render and persist immutable artifacts",
                "Export only Ready versions"
            ]
        }
    };
}
