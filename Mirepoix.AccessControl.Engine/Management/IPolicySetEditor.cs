using Mirepoix.AccessControl.Policy;

namespace Mirepoix.AccessControl.Management;

public interface IPolicySetEditor
{
    void Replace(PolicySet set);
}
