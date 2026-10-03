namespace ProtocolChecks;

/// <summary>The six levels a member's declared accessibility resolves to, ordered least to most visible so that
/// <see cref="MemberAccessibility"/> can take the most visible of a property or event's own accessors with <c>Max()</c>.</summary>
internal enum AccessibilityLevel
{
    /// <summary>Visible to the declaring type only.</summary>
    Private,

    /// <summary>Visible to the declaring assembly and to a derived type, but only where both hold at once.</summary>
    PrivateProtected,

    /// <summary>Visible to the declaring type and its derived types.</summary>
    Protected,

    /// <summary>Visible to the declaring assembly and its recognised friends.</summary>
    Internal,

    /// <summary>Visible to the declaring assembly, its friends, or any derived type.</summary>
    ProtectedInternal,

    /// <summary>Visible everywhere the declaring type itself is visible.</summary>
    Public,
}
