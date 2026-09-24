namespace Mirepoix.AccessControl.Providers.Schema;

/// <summary>
/// Names the shared <c>ac_*</c> table and column contract for durable provider packages.
/// Table/column names and the singleton policy-set id are the wire between adapters and scripts.
/// </summary>
public static class AccessControlSchema
{
    /// <summary>Names the logical schema version this contract describes.</summary>
    public const int SchemaVersion = 3;

    /// <summary>
    /// Names the singleton row id for <see cref="PolicySetTable"/>. Providers store one active policy set at this id.
    /// </summary>
    public const int PolicySetSingletonId = 1;

    /// <summary>Names the policy set table (<c>id</c>, <c>version</c>, <c>payload_json</c>, <c>updated_utc</c>).</summary>
    public const string PolicySetTable = "ac_policy_set";

    /// <summary>Names the subject identity table.</summary>
    public const string SubjectTable = "ac_subject";

    /// <summary>Names the subject role membership table.</summary>
    public const string SubjectRoleTable = "ac_subject_role";

    /// <summary>Names the subject attribute table (<c>value_json</c>).</summary>
    public const string SubjectAttributeTable = "ac_subject_attribute";

    /// <summary>Names the role catalog table.</summary>
    public const string RoleTable = "ac_role";

    /// <summary>Names the separation-of-duties constraint header table.</summary>
    public const string SodConstraintTable = "ac_sod_constraint";

    /// <summary>Names the role-membership table for separation-of-duties constraints.</summary>
    public const string SodConstraintRoleTable = "ac_sod_constraint_role";

    /// <summary>Names the primary / row id column.</summary>
    public const string ColId = "id";

    /// <summary>Names the policy set version column.</summary>
    public const string ColVersion = "version";

    /// <summary>Names the policy set JSON payload column.</summary>
    public const string ColPayloadJson = "payload_json";

    /// <summary>Names the UTC timestamp column on the policy set row.</summary>
    public const string ColUpdatedUtc = "updated_utc";

    /// <summary>
    /// Names the subject id column. It is an FK on <see cref="SubjectAttributeTable"/> but not on
    /// <see cref="SubjectRoleTable"/>.
    /// </summary>
    public const string ColSubjectId = "subject_id";

    /// <summary>Names the role name column on <see cref="SubjectRoleTable"/>.</summary>
    public const string ColRole = "role";

    /// <summary>Names the role identifier column on role catalog and constraint-role rows.</summary>
    public const string ColRoleId = "role_id";

    /// <summary>Names the separation-of-duties constraint identifier column.</summary>
    public const string ColConstraintId = "constraint_id";

    /// <summary>Names the optional role description column.</summary>
    public const string ColDescription = "description";

    /// <summary>Names the attribute name column.</summary>
    public const string ColName = "name";

    /// <summary>Names the attribute value JSON column.</summary>
    public const string ColValueJson = "value_json";

}

