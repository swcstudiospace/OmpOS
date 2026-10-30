namespace Autonogrammer.Sandbox;

/// <summary>
/// The Railway GraphQL documents <see cref="RailwayCloudEnvironmentClient"/> sends, each tagged VERIFIED (shown by a documented
/// example) or UNVERIFIED (needs schema introspection). Each document starts with its operation name, which tests and logs use.
/// </summary>
public static class RailwayOperations
{
    /// <summary>The Railway documentation page set the VERIFIED tags cite.</summary>
    private const string DocsBase = "https://docs.railway.com/integrations/api/";

    /// <summary>
    /// Lists the services of a project. VERIFIED: https://docs.railway.com/integrations/api/manage-projects.md
    /// (project(id) services edges node id name).
    /// </summary>
    internal const string ProjectServices =
        "query projectServices($id: String!) { project(id: $id) { services { edges { node { id name } } } } }";

    /// <summary>
    /// Lists the volumes of a project. VERIFIED: https://docs.railway.com/integrations/api/manage-volumes.md
    /// (project(id) volumes edges node id name). Per-environment volume-instance lookup is UNVERIFIED: introspection needed.
    /// </summary>
    internal const string ProjectVolumes =
        "query projectVolumes($id: String!) { project(id: $id) { volumes { edges { node { id name } } } } }";

    /// <summary>
    /// Lists deployments of a service in an environment. VERIFIED: https://docs.railway.com/integrations/api/manage-deployments.md
    /// (deployments(input, first) edges node id status createdAt). That <c>first: 1</c> returns the newest deployment is
    /// UNVERIFIED: introspection needed (the documented latestDeployment example implies it).
    /// </summary>
    internal const string Deployments =
        "query deployments($input: DeploymentListInput!, $first: Int) { deployments(input: $input, first: $first) { edges { node { id status createdAt } } } }";

    /// <summary>
    /// Creates a service from a container image. VERIFIED: https://docs.railway.com/integrations/api/manage-services.md
    /// (input projectId, name, source.image). <c>input.environmentId</c> is UNVERIFIED: introspection needed and is not sent.
    /// </summary>
    internal const string ServiceCreate =
        "mutation serviceCreate($input: ServiceCreateInput!) { serviceCreate(input: $input) { id name } }";

    /// <summary>
    /// Upserts several variables. VERIFIED: https://docs.railway.com/integrations/api/manage-variables.md
    /// (input projectId, environmentId, serviceId, variables, replace, skipDeploys).
    /// </summary>
    internal const string VariableCollectionUpsert =
        "mutation variableCollectionUpsert($input: VariableCollectionUpsertInput!) { variableCollectionUpsert(input: $input) }";

    /// <summary>
    /// Updates service instance settings. VERIFIED: https://docs.railway.com/integrations/api/manage-services.md
    /// (sleepApplication, healthcheckPath, numReplicas, restartPolicyType). The scalar return type is UNVERIFIED: introspection needed.
    /// </summary>
    internal const string ServiceInstanceUpdate =
        "mutation serviceInstanceUpdate($serviceId: String!, $environmentId: String!, $input: ServiceInstanceUpdateInput!) { serviceInstanceUpdate(serviceId: $serviceId, environmentId: $environmentId, input: $input) }";

    /// <summary>
    /// Redeploys the latest deployment of a service instance. VERIFIED: https://docs.railway.com/integrations/api/manage-services.md.
    /// </summary>
    internal const string ServiceInstanceRedeploy =
        "mutation serviceInstanceRedeploy($serviceId: String!, $environmentId: String!) { serviceInstanceRedeploy(serviceId: $serviceId, environmentId: $environmentId) }";

    /// <summary>
    /// Deploys a service instance. VERIFIED: https://docs.railway.com/integrations/api/manage-services.md.
    /// The scalar return type is UNVERIFIED: introspection needed.
    /// </summary>
    internal const string ServiceInstanceDeployV2 =
        "mutation serviceInstanceDeployV2($serviceId: String!, $environmentId: String!) { serviceInstanceDeployV2(serviceId: $serviceId, environmentId: $environmentId) }";

    /// <summary>
    /// Deletes a service. VERIFIED: https://docs.railway.com/integrations/api/manage-services.md (also used by RailwaySwarmProvider).
    /// </summary>
    internal const string ServiceDelete =
        "mutation serviceDelete($id: String!) { serviceDelete(id: $id) }";

    /// <summary>
    /// Creates a volume attached to a service. VERIFIED: https://docs.railway.com/integrations/api/manage-volumes.md
    /// (input projectId, serviceId, mountPath, environmentId, region).
    /// </summary>
    internal const string VolumeCreate =
        "mutation volumeCreate($input: VolumeCreateInput!) { volumeCreate(input: $input) { id name } }";

    /// <summary>
    /// Renames a volume. VERIFIED: https://docs.railway.com/integrations/api/manage-volumes.md (input.name).
    /// </summary>
    internal const string VolumeUpdate =
        "mutation volumeUpdate($volumeId: String!, $input: VolumeUpdateInput!) { volumeUpdate(volumeId: $volumeId, input: $input) { id name } }";

    /// <summary>
    /// Changes a volume instance mount path. VERIFIED: https://docs.railway.com/integrations/api/manage-volumes.md (input.mountPath).
    /// </summary>
    internal const string VolumeInstanceUpdate =
        "mutation volumeInstanceUpdate($volumeId: String!, $input: VolumeInstanceUpdateInput!) { volumeInstanceUpdate(volumeId: $volumeId, input: $input) }";

    /// <summary>
    /// Deletes a volume and all its data. VERIFIED: https://docs.railway.com/integrations/api/manage-volumes.md.
    /// </summary>
    internal const string VolumeDelete =
        "mutation volumeDelete($volumeId: String!) { volumeDelete(volumeId: $volumeId) }";

    /// <summary>
    /// Gets the verification state of every operation the client sends, for reports and the ADR.
    /// </summary>
    public static IReadOnlyList<RailwayOperationStatus> Statuses { get; } =
    [
        new("projectServices", true, DocsBase + "manage-projects.md"),
        new("projectVolumes", true, DocsBase + "manage-volumes.md"),
        new("deployments", true, DocsBase + "manage-deployments.md; UNVERIFIED: introspection needed for newest-first ordering and the status enum values INITIALIZING, REMOVING, NEEDS_APPROVAL"),
        new("serviceCreate", true, DocsBase + "manage-services.md; UNVERIFIED: introspection needed for input.environmentId"),
        new("variableCollectionUpsert", true, DocsBase + "manage-variables.md"),
        new("serviceInstanceUpdate", true, DocsBase + "manage-services.md; UNVERIFIED: introspection needed for the return type"),
        new("serviceInstanceRedeploy", true, DocsBase + "manage-services.md"),
        new("serviceInstanceDeployV2", true, DocsBase + "manage-services.md; UNVERIFIED: introspection needed for the return type"),
        new("serviceDelete", true, DocsBase + "manage-services.md"),
        new("volumeCreate", true, DocsBase + "manage-volumes.md"),
        new("volumeUpdate", true, DocsBase + "manage-volumes.md"),
        new("volumeInstanceUpdate", true, DocsBase + "manage-volumes.md"),
        new("volumeDelete", true, DocsBase + "manage-volumes.md"),
        new("GraphQL error traceId location", false, "UNVERIFIED: introspection needed (top-level error field or extensions.traceId)"),
    ];
}
