using Ajure.Specification;

namespace Ajure.Worker;

public static class SimulatedSpecFactory
{
    public static ProjectSpec Create(
        string projectName,
        string summary,
        string constraints = "",
        string exclusions = "",
        string existingDocs = "",
        IReadOnlyList<string>? approvedDecisions = null)
    {
        var name = Clean(projectName, "Unnamed project");
        var concept = Clean(summary, $"Define the primary outcome for {name}.");
        var shortConcept = OneLine(concept);
        var constraintItems = Items(constraints);
        var constraintValues = constraintItems.Length > 0
            ? constraintItems
            : ["No additional technical or deployment constraints were supplied."];
        var exclusionItems = Items(exclusions);
        var nonGoals = exclusionItems.Length > 0
            ? exclusionItems.Select(static item => $"User exclusion: {item}").ToArray()
            : ["User exclusion: Features outside the submitted idea"];
        var existingValue = Clean(existingDocs, "No existing documents were provided.");
        var decisions = approvedDecisions?
            .Where(static decision => !string.IsNullOrWhiteSpace(decision))
            .Select(OneLine)
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];
        var decisionSummary = decisions.Length == 0
            ? "No additional decisions were approved."
            : string.Join("; ", decisions);

        return new ProjectSpec
        {
            ProjectName = name,
            Vision = concept,
            Problem = $"The primary user needs a dependable way to turn this intent into an outcome: {shortConcept}",
            Personas =
            [
                new Persona
                {
                    Id = "P-001",
                    Name = "Primary user",
                    Situation = $"Needs to achieve: {shortConcept}",
                    Motivation = "Complete the outcome described in the submitted idea.",
                    ExpectedOutcome = $"Receives a result aligned with: {shortConcept}",
                    Constraints = string.Join("; ", constraintValues),
                    IsPrimary = true
                }
            ],
            Goals =
            [
                new Goal
                {
                    Id = "GOAL-001",
                    Statement = $"The primary user can achieve the requested outcome: {shortConcept}",
                    SuccessMetric = "At least 80 percent of representative happy paths reach a confirmed outcome."
                },
                new Goal
                {
                    Id = "GOAL-002",
                    Statement = "The delivered result remains traceable to the submitted context and approvals.",
                    SuccessMetric = "100 percent of stated constraints, exclusions, and approved decisions have verification evidence."
                }
            ],
            NonGoals = nonGoals,
            Journeys =
            [
                new Journey
                {
                    Id = "J-001",
                    Title = "Complete the requested outcome",
                    Entry = $"The primary user submits this intent: {shortConcept}",
                    Steps =
                    [
                        "Review the submitted idea and context",
                        "Execute the smallest flow that satisfies the approved requirements",
                        "Confirm the result and any failure handling"
                    ],
                    SuccessExit = "The primary user receives a result aligned with the submitted intent.",
                    FailurePaths =
                    [
                        "Input violates a stated constraint",
                        "A required dependency is unavailable",
                        "The request is outside the approved scope"
                    ],
                    RequirementIds = ["FR-001", "FR-002", "FR-003"]
                }
            ],
            Requirements =
            [
                new Requirement
                {
                    Id = "FR-001",
                    Title = "Capture the submitted intent",
                    Statement = $"The product must accept the information needed to achieve {shortConcept}.",
                    Priority = Priority.Must,
                    Rationale = "The outcome cannot be verified if the original intent and context are lost.",
                    AcceptanceCriteriaIds = ["AC-001"],
                    TechnicalDecisionIds = ["TD-001"],
                    JourneyIds = ["J-001"]
                },
                new Requirement
                {
                    Id = "FR-002",
                    Title = "Deliver the primary outcome",
                    Statement = $"The product must deliver the primary outcome described by {shortConcept}.",
                    Priority = Priority.Must,
                    Rationale = "The main user journey must produce the result requested in the idea.",
                    AcceptanceCriteriaIds = ["AC-002"],
                    TechnicalDecisionIds = ["TD-001"],
                    JourneyIds = ["J-001"]
                },
                new Requirement
                {
                    Id = "FR-003",
                    Title = "Explain processing failures",
                    Statement = "The product should expose an actionable status when processing succeeds or fails.",
                    Priority = Priority.Should,
                    Rationale = "Users need to know whether to correct input, retry, or make a decision.",
                    AcceptanceCriteriaIds = ["AC-003"],
                    TechnicalDecisionIds = ["TD-003"],
                    JourneyIds = ["J-001"]
                }
            ],
            NonFunctionalRequirements =
            [
                new Requirement
                {
                    Id = "NFR-001",
                    Title = "Constraint compliance",
                    Statement = "The release must pass AC-004 before it is accepted.",
                    Priority = Priority.Must,
                    Rationale = $"The submitted constraints must be verified: {string.Join("; ", constraintValues)}",
                    Measurement = "Each submitted constraint has a recorded verification result.",
                    AcceptanceCriteriaIds = ["AC-004"],
                    TechnicalDecisionIds = ["TD-002"]
                },
                new Requirement
                {
                    Id = "NFR-002",
                    Title = "Boundary compliance",
                    Statement = "The release must pass AC-005 before it is accepted.",
                    Priority = Priority.Must,
                    Rationale = $"The recorded exclusions are: {string.Join("; ", nonGoals)}",
                    Measurement = "Each recorded exclusion is checked against the delivered scope.",
                    AcceptanceCriteriaIds = ["AC-005"],
                    TechnicalDecisionIds = ["TD-002"]
                }
            ],
            AcceptanceCriteria =
            [
                new AcceptanceCriterion
                {
                    Id = "AC-001",
                    Given = "A primary user has submitted the project idea and its context",
                    When = "the intake flow is completed",
                    Then = "the submitted intent is retained as the source for the outcome flow",
                    VerificationType = VerificationType.Automated,
                    RequirementIds = ["FR-001"]
                },
                new AcceptanceCriterion
                {
                    Id = "AC-002",
                    Given = $"valid input for {shortConcept}",
                    When = "the primary outcome flow completes",
                    Then = "the result is aligned with the submitted intent and can be confirmed by the user",
                    VerificationType = VerificationType.Ui,
                    RequirementIds = ["FR-002"]
                },
                new AcceptanceCriterion
                {
                    Id = "AC-003",
                    Given = "a processing request that succeeds or fails",
                    When = "the request reaches a terminal state",
                    Then = "the user sees the state, a stable explanation, and the next permitted action",
                    VerificationType = VerificationType.Ui,
                    RequirementIds = ["FR-003"]
                },
                new AcceptanceCriterion
                {
                    Id = "AC-004",
                    Given = "the submitted constraints are listed",
                    When = "the release verification runs",
                    Then = "every listed constraint has evidence that it is satisfied",
                    VerificationType = VerificationType.Automated,
                    RequirementIds = ["NFR-001"]
                },
                new AcceptanceCriterion
                {
                    Id = "AC-005",
                    Given = "the submitted exclusions are listed",
                    When = "the release verification runs",
                    Then = "no recorded exclusion is delivered as part of the approved result",
                    VerificationType = VerificationType.Automated,
                    RequirementIds = ["NFR-002"]
                }
            ],
            TechnicalDecisions =
            [
                new TechnicalDecision
                {
                    Id = "TD-001",
                    Title = "Preserve the semantic input",
                    Decision = "The submitted idea, constraints, exclusions, existing documents, and approved decisions remain available as the semantic input.",
                    Rationale = $"Approved decisions: {decisionSummary}",
                    Alternatives = ["Generate documents from independent summaries", "Infer missing scope without recording it"],
                    RequirementIds = ["FR-001", "FR-002"],
                    IsLocked = true
                },
                new TechnicalDecision
                {
                    Id = "TD-002",
                    Title = "Verify scope boundaries",
                    Decision = "Each supplied constraint is checked before release, and each recorded exclusion is checked against the delivered result.",
                    Rationale = "Explicit user boundaries must take precedence over inferred behavior.",
                    Alternatives = ["Treat constraints as informal notes", "Validate only after release"],
                    RequirementIds = ["NFR-001", "NFR-002"],
                    IsLocked = true
                },
                new TechnicalDecision
                {
                    Id = "TD-003",
                    Title = "Expose terminal states",
                    Decision = "Success and failure states include stable evidence and an allowed next action.",
                    Rationale = "A user cannot recover from an opaque processing result.",
                    Alternatives = ["Show only a generic error", "Hide transient failures"],
                    RequirementIds = ["FR-003"]
                }
            ],
            UxDecisions =
            [
                new UxDecision
                {
                    Id = "UX-001",
                    Title = "Visible outcome states",
                    Decision = "The primary flow shows input, processing, success, failure, and disabled states without optimistic completion.",
                    Rationale = "The user must be able to distinguish a confirmed outcome from an incomplete request.",
                    RequirementIds = ["FR-002", "FR-003"]
                }
            ],
            Risks =
            [
                new Risk
                {
                    Id = "RISK-001",
                    Statement = $"A detail needed for {shortConcept} may be absent from the submitted idea.",
                    Likelihood = RiskLevel.Medium,
                    Impact = RiskLevel.High,
                    Mitigation = "Record the assumption, ask only an implementation-changing decision, and block release when it is critical."
                },
                new Risk
                {
                    Id = "RISK-002",
                    Statement = "A supplied constraint or external dependency may conflict with the requested outcome.",
                    Likelihood = RiskLevel.Medium,
                    Impact = RiskLevel.Medium,
                    Mitigation = "Surface the conflict with affected IDs instead of silently changing the submitted scope."
                }
            ],
            Glossary =
            [
                new GlossaryEntry
                {
                    Term = "Submitted intent",
                    Definition = "The project idea, context, constraints, exclusions, existing documents, and approved decisions supplied by the user."
                },
                new GlossaryEntry
                {
                    Term = "Primary outcome",
                    Definition = "The user-visible result described by the submitted intent."
                }
            ],
            Evidence =
            [
                new EvidenceItem
                {
                    Statement = concept,
                    VerificationMethod = "Confirm the intended outcome with the project owner."
                },
                new EvidenceItem
                {
                    Statement = $"Stated constraints: {string.Join("; ", constraintValues)}",
                    VerificationMethod = "Check each constraint against the technical and release verification."
                },
                new EvidenceItem
                {
                    Statement = $"Recorded exclusions: {string.Join("; ", nonGoals)}",
                    VerificationMethod = "Check the delivered scope against AC-005."
                },
                new EvidenceItem
                {
                    Statement = $"Existing documents supplied as context: {existingValue}",
                    VerificationMethod = "Compare the generated intent and decisions with the supplied documents."
                }
            ],
            OptionsConsidered =
            [
                new ConsideredOption
                {
                    Title = "Implement only explicit intent",
                    Summary = $"Keep the result focused on {shortConcept} and the supplied boundaries.",
                    IsChosen = true
                },
                new ConsideredOption
                {
                    Title = "Infer unspecified features",
                    Summary = "Fill gaps with unrecorded product behavior.",
                    RejectionReason = "Unrecorded assumptions can change the requested outcome and violate exclusions."
                }
            ],
            ValuePropositions =
            [
                $"The product stays focused on {shortConcept}.",
                "Constraints, exclusions, existing context, and approvals remain visible to implementers."
            ],
            SuccessMetrics =
            [
                new SuccessMetric
                {
                    Name = "Primary outcome completion",
                    Target = "At least 80 percent of representative happy paths reach a confirmed outcome."
                },
                new SuccessMetric
                {
                    Name = "Scope fidelity",
                    Target = "100 percent of stated constraints and exclusions have verification evidence.",
                    Kind = MetricKind.Product
                }
            ],
            LockedDecisions =
            [
                "The submitted idea is the source of product intent.",
                .. constraintItems.Select(static item => $"Constraint: {item}"),
                .. exclusionItems.Select(static item => $"Excluded: {item}"),
                .. decisions.Select(static decision => $"Approved: {decision}")
            ],
            StateMatrix =
            [
                new StateMatrixEntry
                {
                    Screen = "Primary outcome flow",
                    Loading = "Show the current processing stage.",
                    Empty = "Prompt for the required project intent.",
                    Failure = "Show a stable error, affected input, and retry or decision action.",
                    Success = "Show the confirmed result and its verification evidence.",
                    Disabled = "Disable conflicting actions while processing.",
                    Permission = "Reject access outside the approved ownership boundary."
                }
            ],
            BusinessRules =
            [
                new BusinessRule
                {
                    Statement = "User-provided constraints take precedence over inferred defaults.",
                    Precedence = 1
                },
                new BusinessRule
                {
                    Statement = "Explicit exclusions take precedence over optional feature ideas.",
                    Precedence = 2
                }
            ],
            AnalyticsEvents =
            [
                new AnalyticsEvent
                {
                    Name = "outcome_started",
                    Properties = ["requestId", "inputHash"],
                    Purpose = "Measure how often users start the primary outcome flow."
                },
                new AnalyticsEvent
                {
                    Name = "outcome_completed",
                    Properties = ["requestId", "status", "durationMs"],
                    Purpose = "Measure confirmed outcomes and recoverable failures."
                }
            ],
            Release = new ReleaseScope
            {
                Mvp = ["FR-001", "FR-002", "FR-003", "NFR-001", "NFR-002"],
                Later = ["Features not described in the submitted idea"],
                BlockingConditions =
                [
                    "Every submitted constraint has verification evidence",
                    "No recorded exclusion is delivered",
                    "Every Must requirement passes its acceptance criterion"
                ]
            },
            Technical = new TechnicalProfile
            {
                Constraints = constraintValues,
                MustTechnologies =
                [
                    "Use the technologies already present in the target project when supplied.",
                    "Add a dependency only when required by an approved requirement."
                ],
                ForbiddenChoices = [.. nonGoals.Select(static item => $"Excluded scope: {item}")],
                Architecture = "The product accepts the submitted intent, verifies its boundaries, executes the core outcome, and exposes durable status and failure evidence.",
                TrustBoundaries =
                [
                    "User input to the application boundary",
                    "Application boundary to external services and persistence",
                    "Untrusted external or model output to the validation boundary"
                ],
                Components =
                [
                    new ComponentSpec
                    {
                        Name = "Input boundary",
                        Responsibility = "Validate and retain the submitted intent and context.",
                        Dependencies = ["Application storage"],
                        RequirementIds = ["FR-001"]
                    },
                    new ComponentSpec
                    {
                        Name = "Outcome processor",
                        Responsibility = "Execute the approved primary flow and expose terminal states.",
                        Dependencies = ["Input boundary", "Required external services"],
                        RequirementIds = ["FR-002", "FR-003"]
                    },
                    new ComponentSpec
                    {
                        Name = "Scope verifier",
                        Responsibility = "Check constraints and exclusions before the release is accepted.",
                        Dependencies = ["Outcome processor"],
                        RequirementIds = ["NFR-001", "NFR-002"]
                    }
                ],
                RepositoryStructure =
                [
                    new RepositoryArea { Path = "src/", Ownership = "Product implementation" },
                    new RepositoryArea { Path = "tests/", Ownership = "Automated verification" },
                    new RepositoryArea { Path = "docs/", Ownership = "Product and technical documentation" }
                ],
                DataEntities =
                [
                    new DataEntity
                    {
                        Name = "ProjectInput",
                        Fields = ["id", "submittedIdea", "constraints", "exclusions", "existingDocs"],
                        Relationships = ["A project input creates one outcome flow"],
                        Retention = "Retain according to the approved product policy."
                    },
                    new DataEntity
                    {
                        Name = "Outcome",
                        Fields = ["id", "projectInputId", "status", "result", "createdAt"],
                        Relationships = ["An outcome belongs to one project input"],
                        Retention = "Delete or retain only as stated in the approved constraints."
                    }
                ],
                ApiContracts =
                [
                    new ApiContract
                    {
                        Operation = "POST /api/requests",
                        Purpose = "Accept the submitted intent and start the primary outcome flow.",
                        Auth = "Use the access policy approved for the project.",
                        Request = "submitted idea, constraints, exclusions, and existing documents",
                        SuccessResponse = "202 with a request ID",
                        ErrorResponses = ["400 invalid input", "401 unauthenticated", "409 conflicting scope"],
                        Idempotency = "A client request ID prevents duplicate outcome flows.",
                        TimeoutAndRetry = "10 second enqueue timeout; retry only transient storage failures.",
                        RequirementIds = ["FR-001"]
                    },
                    new ApiContract
                    {
                        Operation = "GET /api/requests/{requestId}",
                        Purpose = "Return the current outcome status and evidence.",
                        Auth = "The approved owner can read the request.",
                        Request = "request ID path segment",
                        SuccessResponse = "200 with status and result metadata",
                        ErrorResponses = ["401 unauthenticated", "403 not owner", "404 unknown request"],
                        Idempotency = "Read only.",
                        TimeoutAndRetry = "5 second timeout; retry safe reads on transient failure.",
                        RequirementIds = ["FR-002", "FR-003"]
                    },
                    new ApiContract
                    {
                        Operation = "POST /api/requests/{requestId}/retry",
                        Purpose = "Retry a recoverable failed outcome flow.",
                        Auth = "The approved owner can retry the request.",
                        Request = "request ID and retry decision",
                        SuccessResponse = "202 with the existing request ID",
                        ErrorResponses = ["403 not owner", "404 unknown request", "409 failure is not retryable"],
                        Idempotency = "Only one retry can run for a request at a time.",
                        TimeoutAndRetry = "10 second enqueue timeout; retry only transient storage failures.",
                        RequirementIds = ["FR-003"]
                    }
                ],
                States =
                [
                    new WorkflowState
                    {
                        Name = "Draft",
                        AllowedTransitions = ["Processing"],
                        FailureHandling = "Input errors remain visible and do not start processing."
                    },
                    new WorkflowState
                    {
                        Name = "Processing",
                        AllowedTransitions = ["Succeeded", "Failed"],
                        FailureHandling = "Record a stable error and whether retry is allowed."
                    },
                    new WorkflowState
                    {
                        Name = "Succeeded",
                        AllowedTransitions = ["Draft"],
                        FailureHandling = "A new input creates a new outcome attempt."
                    },
                    new WorkflowState
                    {
                        Name = "Failed",
                        AllowedTransitions = ["Processing", "Draft"],
                        FailureHandling = "Retry only when the recorded failure is recoverable."
                    }
                ],
                Security =
                [
                    "Validate and authorize submitted input at the application boundary.",
                    "Do not log secrets or full user documents.",
                    "Treat external and model output as untrusted before persistence or rendering."
                ],
                Reliability =
                [
                    "Retry only transient dependency failures.",
                    "Keep failed input and a stable error code.",
                    "Never silently drop submitted scope."
                ],
                Observability =
                [
                    "Structured events include request IDs, stages, status, and correlation IDs.",
                    "Outcome evidence records verification results without capturing full sensitive content.",
                    "Processing duration and recoverable failure counts are measurable."
                ],
                Deployment =
                [
                    "Use the deployment target named in the approved constraints.",
                    "Keep credentials and environment configuration outside generated source.",
                    "Provide health and readiness checks."
                ],
                TestingStrategy =
                [
                    "Test every Must requirement and its linked acceptance criterion.",
                    "Test each submitted constraint and exclusion as a release check.",
                    "Exercise success, empty, failure, permission, and retry states."
                ],
                ImplementationOrder =
                [
                    "Normalize the submitted intent and preserve its context.",
                    "Implement FR-001 and the input boundary.",
                    "Implement FR-002 and the primary outcome flow.",
                    "Implement FR-003 and recoverable failure handling.",
                    "Run constraint, exclusion, acceptance, and release verification."
                ]
            }
        };
    }

    private static string Clean(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string OneLine(string value) =>
        string.Join(
            " ",
            value.Split(
                [' ', '\r', '\n', '\t'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string[] Items(string value) =>
        value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static item => item.Trim().TrimStart('-', '*').Trim())
            .Where(static item => item.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}
