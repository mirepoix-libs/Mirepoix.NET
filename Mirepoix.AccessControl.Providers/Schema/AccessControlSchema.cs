namespace Mirepoix.AccessControl.Providers.Schema;

/// <summary>
/// Shared <c>ac_*</c> table contract for durable provider packages.
/// </summary>
public static class AccessControlSchema
{
    public const int SchemaVersion = 1;
    public const int PolicySetSingletonId = 1;

    public const string PolicySetTable = "ac_policy_set";
    public const string SubjectTable = "ac_subject";
    public const string SubjectRoleTable = "ac_subject_role";
    public const string SubjectAttributeTable = "ac_subject_attribute";
    public const string ResourceAttributeTable = "ac_resource_attribute";

    public const string ColId = "id";
    public const string ColVersion = "version";
    public const string ColPayloadJson = "payload_json";
    public const string ColUpdatedUtc = "updated_utc";
    public const string ColSubjectId = "subject_id";
    public const string ColRole = "role";
    public const string ColName = "name";
    public const string ColValueJson = "value_json";
    public const string ColResourceType = "resource_type";
    public const string ColResourceId = "resource_id";
}
