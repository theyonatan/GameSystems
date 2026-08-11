using UnityEngine;

public sealed class IslandSocket : MonoBehaviour
{
    [SerializeField]
    private SocketUsage usage = SocketUsage.Both;

    [SerializeField]
    private SocketRouteUsage routeUsage = SocketRouteUsage.Both;

    [SerializeField]
    private IslandConnectionMask allowedConnections = IslandConnectionMask.All;

    public SocketUsage Usage => usage;
    public SocketRouteUsage RouteUsage => routeUsage;
    public IslandConnectionMask AllowedConnections => allowedConnections;

    public bool CanBeUsedAs(SocketUsage requiredUsage)
    {
        return (usage & requiredUsage) == requiredUsage;
    }

    public bool SupportsRoute(SocketRouteUsage requiredRoute)
    {
        return (routeUsage & requiredRoute) != 0;
    }

    public bool Allows(IslandConnectionType connectionType)
    {
        return (allowedConnections & connectionType.ToMask()) != 0;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Color color;
        if (usage == SocketUsage.Entry)
            color = new Color(0.2f, 0.7f, 1f);
        else if (usage == SocketUsage.Exit)
            color = new Color(1f, 0.55f, 0.15f);
        else
            color = new Color(0.7f, 0.25f, 1f);

        Gizmos.color = color;
        Gizmos.DrawSphere(transform.position, 0.35f);
        Gizmos.DrawRay(transform.position, transform.forward * 2f);
    }
#endif
}
