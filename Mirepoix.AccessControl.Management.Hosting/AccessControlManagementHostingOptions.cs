namespace Mirepoix.AccessControl.Management.Hosting;

/// <summary>
/// Selects management HTTP slices and their shared route settings.
/// </summary>
public sealed class AccessControlManagementHostingOptions
{
    /// <summary>Gets or sets the shared route prefix.</summary>
    public string RoutePrefix { get; set; } = "/access-control";

    /// <summary>Gets or sets the optional authorization policy applied to every management endpoint.</summary>
    public string? AuthorizationPolicy { get; set; }

    internal bool SubjectsEnabled { get; private set; }
    internal bool ResourcesEnabled { get; private set; }
    internal bool RolesEnabled { get; private set; }
    internal bool SodEnabled { get; private set; }
    internal bool PolicySetEnabled { get; private set; }

    /// <summary>Enables subject management endpoints.</summary>
    public AccessControlManagementHostingOptions AddSubjects()
    {
        SubjectsEnabled = true;
        return this;
    }

    /// <summary>Enables resource management endpoints.</summary>
    public AccessControlManagementHostingOptions AddResources()
    {
        ResourcesEnabled = true;
        return this;
    }

    /// <summary>Enables role catalog endpoints.</summary>
    public AccessControlManagementHostingOptions AddRoles()
    {
        RolesEnabled = true;
        return this;
    }

    /// <summary>Enables separation-of-duty constraint endpoints.</summary>
    public AccessControlManagementHostingOptions AddSod()
    {
        SodEnabled = true;
        return this;
    }

    /// <summary>Enables policy-set replacement.</summary>
    public AccessControlManagementHostingOptions AddPolicySet()
    {
        PolicySetEnabled = true;
        return this;
    }
}
