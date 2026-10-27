using System.Text.Json.Serialization;

namespace OmpOS.Sandbox;

/// <summary>
/// Source-generated JSON metadata for the Railway GraphQL request and response envelopes.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RailwayGraphQlRequest<RailwayWire.IdVariables>), TypeInfoPropertyName = "IdRequest")]
[JsonSerializable(typeof(RailwayGraphQlRequest<RailwayWire.VolumeIdVariables>), TypeInfoPropertyName = "VolumeIdRequest")]
[JsonSerializable(typeof(RailwayGraphQlRequest<RailwayWire.ServiceInstanceVariables>), TypeInfoPropertyName = "ServiceInstanceRequest")]
[JsonSerializable(typeof(RailwayGraphQlRequest<RailwayWire.ServiceInstanceUpdateVariables>), TypeInfoPropertyName = "ServiceInstanceUpdateRequest")]
[JsonSerializable(typeof(RailwayGraphQlRequest<RailwayWire.ServiceCreateVariables>), TypeInfoPropertyName = "ServiceCreateRequest")]
[JsonSerializable(typeof(RailwayGraphQlRequest<RailwayWire.VariableCollectionUpsertVariables>), TypeInfoPropertyName = "VariableCollectionUpsertRequest")]
[JsonSerializable(typeof(RailwayGraphQlRequest<RailwayWire.DeploymentsVariables>), TypeInfoPropertyName = "DeploymentsRequest")]
[JsonSerializable(typeof(RailwayGraphQlRequest<RailwayWire.VolumeCreateVariables>), TypeInfoPropertyName = "VolumeCreateRequest")]
[JsonSerializable(typeof(RailwayGraphQlRequest<RailwayWire.VolumeUpdateVariables>), TypeInfoPropertyName = "VolumeUpdateRequest")]
[JsonSerializable(typeof(RailwayGraphQlRequest<RailwayWire.VolumeInstanceUpdateVariables>), TypeInfoPropertyName = "VolumeInstanceUpdateRequest")]
[JsonSerializable(typeof(RailwayGraphQlResponse<RailwayWire.ProjectServicesData>), TypeInfoPropertyName = "ProjectServicesResponse")]
[JsonSerializable(typeof(RailwayGraphQlResponse<RailwayWire.ProjectVolumesData>), TypeInfoPropertyName = "ProjectVolumesResponse")]
[JsonSerializable(typeof(RailwayGraphQlResponse<RailwayWire.DeploymentsData>), TypeInfoPropertyName = "DeploymentsResponse")]
[JsonSerializable(typeof(RailwayGraphQlResponse<RailwayWire.ServiceCreateData>), TypeInfoPropertyName = "ServiceCreateResponse")]
[JsonSerializable(typeof(RailwayGraphQlResponse<RailwayWire.VolumeCreateData>), TypeInfoPropertyName = "VolumeCreateResponse")]
[JsonSerializable(typeof(RailwayGraphQlResponse<RailwayWire.VolumeUpdateData>), TypeInfoPropertyName = "VolumeUpdateResponse")]
[JsonSerializable(typeof(RailwayGraphQlResponse<RailwayWire.ScalarMutationData>), TypeInfoPropertyName = "ScalarMutationResponse")]
internal sealed partial class RailwayJsonContext : JsonSerializerContext
{
}
