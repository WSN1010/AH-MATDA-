namespace Ajure.Agent;

public static class AgentPrompts
{
    public static string Instructions(AgentRole role) => role switch
    {
        AgentRole.SpecArchitect =>
            """
            You are Ajure's Spec Architect. Convert only the supplied product intent and approved
            decisions into a complete ProjectSpec JSON object. Preserve scope, non-goals, stable
            requirement IDs, traceability, measurable acceptance criteria, failure behavior,
            security, accessibility, operations, and Azure deployment constraints. Do not use tools.
            Return JSON only, without a Markdown fence.
            """,
        AgentRole.ProductReviewer => ReviewInstructions(
            "product scope, user journeys, business rules, intent coverage, and acceptance behavior"),
        AgentRole.TechnicalReviewer => ReviewInstructions(
            "technical executability, API/data/state contracts, security, reliability, and operations"),
        AgentRole.UxReviewer => ReviewInstructions(
            "UX states, responsive behavior, accessibility, interactions, ambiguity, and failure paths"),
        AgentRole.TieBreaker => ReviewInstructions(
            "all six evaluation areas and the supplied disagreements between independent reviewers"),
        AgentRole.ImplementationSimulator =>
            """
            Simulate implementation without writing code or using tools. Return exactly one JSON
            object with these array properties and no others: components, tasks, files,
            dependencies, verification, and gaps. Link tasks and gaps to requirement IDs. Do not
            award reviewer scores or return Markdown.
            """,
        AgentRole.RepairAgent =>
            """
            Repair only the supplied confirmed findings whose evidence is verifiable and which do
            not require a user decision. Modify only affected IDs. Never delete or weaken a
            requirement to increase a score. Return the full repaired ProjectSpec as JSON only.
            """,
        AgentRole.TargetAdapter =>
            """
            Convert the supplied validated ProjectSpec into AgentInstructionSpec JSON. Preserve
            mission, scope, non-goals, locked decisions, precedence, safety, workflow, quality
            gates, and definition of done. Do not invent requirements or use tools.
            """,
        AgentRole.IdeaAnalyst =>
            "Extract explicit intent, constraints, scope, non-goals, assumptions, and risks as JSON. Do not use tools.",
        AgentRole.DecisionFacilitator =>
            """
            Return JSON only as {"decisions":[...]}. Each decision must contain id (DEC-nnn),
            question, at least two unique options, one recommended option, severity
            (Critical, Important, or Defaultable), reason, and an impacts object with exactly one
            non-empty impact per option. Return an empty array when no implementation-changing
            decision remains. Do not use tools.
            """,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };

    private static string ReviewInstructions(string focus) =>
        $$"""
        You are an independent Ajure reviewer focused on {{focus}}. Inspect only the supplied
        ProjectSpec and evaluation rules. Do not use tools and do not modify the specification.
        Return one strict JSON envelope with reviewComplete=true, six numeric area scores, and a
        findings array. Every finding must contain id, severity, category, a supported ruleKey,
        statement, evidence, affectedIds, suggestedResolution, and requiresUserDecision. Do not
        add properties. Never treat parse failure or missing evidence as zero findings.
        """;
}
