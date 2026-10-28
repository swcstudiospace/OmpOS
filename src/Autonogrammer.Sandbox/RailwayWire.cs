using System.Text.Json;

namespace Autonogrammer.Sandbox;

/// <summary>
/// The Railway GraphQL request variables and response data shapes, as small records for source-generated JSON.
/// Property names serialise camelCase, which matches the documented operation inputs.
/// </summary>
internal static class RailwayWire
{
    /// <summary>A GraphQL connection: <c>edges { node }</c>.</summary>
    /// <typeparam name="T">The node type.</typeparam>
    /// <param name="Edges">The edges, or null.</param>
    public sealed record Connection<T>(IReadOnlyList<Edge<T>>? Edges);

    /// <summary>A GraphQL connection edge.</summary>
    /// <typeparam name="T">The node type.</typeparam>
    /// <param name="Node">The node, or null.</param>
    public sealed record Edge<T>(T? Node);

    /// <summary>A service or volume node: <c>id name</c>.</summary>
    /// <param name="Id">The identifier.</param>
    /// <param name="Name">The name.</param>
    public sealed record IdNameNode(string? Id, string? Name);

    /// <summary>A deployment node.</summary>
    /// <param name="Id">The deployment identifier.</param>
    /// <param name="Status">The deployment status string.</param>
    /// <param name="CreatedAt">The creation time as the API reports it.</param>
    public sealed record DeploymentNode(string? Id, string? Status, string? CreatedAt);

    /// <summary>Extensions of a GraphQL error.</summary>
    /// <param name="Code">The error code, for example <c>GRAPHQL_VALIDATION_FAILED</c>.</param>
    /// <param name="TraceId">The trace identifier when the API puts it in the extensions (UNVERIFIED location).</param>
    public sealed record ErrorExtensions(string? Code, string? TraceId);

    /// <summary>Variables carrying one id.</summary>
    /// <param name="Id">The identifier.</param>
    public sealed record IdVariables(string Id);

    /// <summary>Variables of the volume deletion.</summary>
    /// <param name="VolumeId">The volume identifier.</param>
    public sealed record VolumeIdVariables(string VolumeId);

    /// <summary>Variables of a service-instance operation without input.</summary>
    /// <param name="ServiceId">The service identifier.</param>
    /// <param name="EnvironmentId">The environment identifier.</param>
    public sealed record ServiceInstanceVariables(string ServiceId, string EnvironmentId);

    /// <summary>Variables of <c>serviceInstanceUpdate</c>.</summary>
    /// <param name="ServiceId">The service identifier.</param>
    /// <param name="EnvironmentId">The environment identifier.</param>
    /// <param name="Input">The settings.</param>
    public sealed record ServiceInstanceUpdateVariables(string ServiceId, string EnvironmentId, ServiceInstanceUpdateInput Input);

    /// <summary>Settings of <c>ServiceInstanceUpdateInput</c> that the client sets.</summary>
    /// <param name="SleepApplication">Serverless sleep.</param>
    /// <param name="HealthcheckPath">The health check path.</param>
    /// <param name="RestartPolicyType">The restart policy, for example <c>ON_FAILURE</c>.</param>
    /// <param name="NumReplicas">The replica count.</param>
    public sealed record ServiceInstanceUpdateInput(bool? SleepApplication, string? HealthcheckPath, string? RestartPolicyType, int? NumReplicas);

    /// <summary>Variables of <c>serviceCreate</c>.</summary>
    /// <param name="Input">The input.</param>
    public sealed record ServiceCreateVariables(ServiceCreateInput Input);

    /// <summary>The <c>ServiceCreateInput</c> fields the client sets.</summary>
    /// <param name="ProjectId">The project identifier.</param>
    /// <param name="Name">The service name.</param>
    /// <param name="Source">The image source.</param>
    public sealed record ServiceCreateInput(string ProjectId, string Name, ServiceSource Source);

    /// <summary>The image source of a service.</summary>
    /// <param name="Image">The container image reference.</param>
    public sealed record ServiceSource(string Image);

    /// <summary>Variables of <c>variableCollectionUpsert</c>.</summary>
    /// <param name="Input">The input.</param>
    public sealed record VariableCollectionUpsertVariables(VariableCollectionUpsertInput Input);

    /// <summary>The <c>VariableCollectionUpsertInput</c>.</summary>
    /// <param name="ProjectId">The project identifier.</param>
    /// <param name="EnvironmentId">The environment identifier.</param>
    /// <param name="ServiceId">The service identifier.</param>
    /// <param name="Variables">The variables to set.</param>
    /// <param name="Replace">Replace all existing variables.</param>
    /// <param name="SkipDeploys">Do not redeploy after the change.</param>
    public sealed record VariableCollectionUpsertInput(
        string ProjectId,
        string EnvironmentId,
        string ServiceId,
        IReadOnlyDictionary<string, string> Variables,
        bool? Replace,
        bool? SkipDeploys);

    /// <summary>Variables of <c>deployments</c>.</summary>
    /// <param name="Input">The filter.</param>
    /// <param name="First">The page size.</param>
    public sealed record DeploymentsVariables(DeploymentListInput Input, int First);

    /// <summary>The <c>DeploymentListInput</c> fields the client sets.</summary>
    /// <param name="ProjectId">The project identifier.</param>
    /// <param name="ServiceId">The service identifier.</param>
    /// <param name="EnvironmentId">The environment identifier.</param>
    public sealed record DeploymentListInput(string ProjectId, string ServiceId, string EnvironmentId);

    /// <summary>Variables of <c>volumeCreate</c>.</summary>
    /// <param name="Input">The input.</param>
    public sealed record VolumeCreateVariables(VolumeCreateInput Input);

    /// <summary>The <c>VolumeCreateInput</c>.</summary>
    /// <param name="ProjectId">The project identifier.</param>
    /// <param name="ServiceId">The service identifier.</param>
    /// <param name="MountPath">The mount path.</param>
    /// <param name="EnvironmentId">The environment identifier.</param>
    public sealed record VolumeCreateInput(string ProjectId, string ServiceId, string MountPath, string? EnvironmentId);

    /// <summary>Variables of <c>volumeUpdate</c>.</summary>
    /// <param name="VolumeId">The volume identifier.</param>
    /// <param name="Input">The input.</param>
    public sealed record VolumeUpdateVariables(string VolumeId, VolumeUpdateInput Input);

    /// <summary>The <c>VolumeUpdateInput</c>.</summary>
    /// <param name="Name">The new volume name.</param>
    public sealed record VolumeUpdateInput(string Name);

    /// <summary>Variables of <c>volumeInstanceUpdate</c>.</summary>
    /// <param name="VolumeId">The volume identifier.</param>
    /// <param name="Input">The input.</param>
    public sealed record VolumeInstanceUpdateVariables(string VolumeId, VolumeInstanceUpdateInput Input);

    /// <summary>The <c>VolumeInstanceUpdateInput</c>.</summary>
    /// <param name="MountPath">The new mount path.</param>
    public sealed record VolumeInstanceUpdateInput(string MountPath);

    /// <summary>Data of <c>project { services }</c>.</summary>
    /// <param name="Project">The project.</param>
    public sealed record ProjectServicesData(ProjectServices? Project);

    /// <summary>The services of a project.</summary>
    /// <param name="Services">The service connection.</param>
    public sealed record ProjectServices(Connection<IdNameNode>? Services);

    /// <summary>Data of <c>project { volumes }</c>.</summary>
    /// <param name="Project">The project.</param>
    public sealed record ProjectVolumesData(ProjectVolumes? Project);

    /// <summary>The volumes of a project.</summary>
    /// <param name="Volumes">The volume connection.</param>
    public sealed record ProjectVolumes(Connection<IdNameNode>? Volumes);

    /// <summary>Data of <c>deployments</c>.</summary>
    /// <param name="Deployments">The deployment connection.</param>
    public sealed record DeploymentsData(Connection<DeploymentNode>? Deployments);

    /// <summary>Data of <c>serviceCreate</c>.</summary>
    /// <param name="ServiceCreate">The created service.</param>
    public sealed record ServiceCreateData(IdNameNode? ServiceCreate);

    /// <summary>Data of <c>volumeCreate</c>.</summary>
    /// <param name="VolumeCreate">The created volume.</param>
    public sealed record VolumeCreateData(IdNameNode? VolumeCreate);

    /// <summary>Data of <c>volumeUpdate</c>.</summary>
    /// <param name="VolumeUpdate">The updated volume.</param>
    public sealed record VolumeUpdateData(IdNameNode? VolumeUpdate);

    /// <summary>Data of the mutations that return a scalar; only the one that ran is present.</summary>
    /// <param name="VariableCollectionUpsert">The upsert result.</param>
    /// <param name="ServiceInstanceUpdate">The update result.</param>
    /// <param name="ServiceInstanceRedeploy">The redeploy result.</param>
    /// <param name="ServiceInstanceDeployV2">The deploy result.</param>
    /// <param name="ServiceDelete">The deletion result.</param>
    /// <param name="VolumeInstanceUpdate">The mount path update result.</param>
    /// <param name="VolumeDelete">The volume deletion result.</param>
    public sealed record ScalarMutationData(
        JsonElement? VariableCollectionUpsert,
        JsonElement? ServiceInstanceUpdate,
        JsonElement? ServiceInstanceRedeploy,
        JsonElement? ServiceInstanceDeployV2,
        JsonElement? ServiceDelete,
        JsonElement? VolumeInstanceUpdate,
        JsonElement? VolumeDelete);
}
